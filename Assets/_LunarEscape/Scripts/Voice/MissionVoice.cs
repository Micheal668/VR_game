using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LunarEscape
{
    public enum VoiceSpeaker { MissionControl, Commander }
    // Quip=指挥官对玩家动作的即时吐槽：排在紧急警告之后、操作提示之前，同一时间只留最新的一句，等太久就放弃；
    // Hint=地面指挥的操作提示，按顺序排队；Urgent=危险警告，插队并打断非紧急台词。
    public enum VoicePriority { Quip, Hint, Urgent }

    [Serializable]
    public sealed class VoiceLine
    {
        public string id;
        public VoiceSpeaker speaker;
        public AudioClip clip;
    }

    // 语音播放器：同一时间只说一句，按优先级排队。地面指挥（ЦУП）走无线电：
    // 开讲/收讲提示音 + 带通滤波 + 轻微失真，在头显内正中听到；指挥官的声音从他本人的位置传来。
    // 只负责“怎么播”，“什么时候说什么”由 MissionVoiceDirector 决定。
    public sealed class MissionVoice : MonoBehaviour
    {
        [SerializeField] private VoiceLine[] lines = Array.Empty<VoiceLine>();
        [Tooltip("无线电声源挂在玩家头部（摄像机）下。")]
        [SerializeField] private Transform head;
        [Tooltip("指挥官（随他移动）；为空时指挥官也从头显正中发声。")]
        [SerializeField] private Transform commanderBody;
        [Tooltip("指挥官嘴部离脚底的高度（米）。")]
        [SerializeField, Min(0f)] private float mouthHeight = 1.55f;
        [SerializeField, Range(0f, 1f)] private float radioVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float commanderVolume = 1f;
        [SerializeField, Min(0f)] private float gapSeconds = 0.35f;
        [Tooltip("俏皮话在前面的话说完后还要再等超过这个时间，就不再说：时机已过，笑话不好笑了。")]
        [SerializeField, Min(0f)] private float quipStaleSeconds = 6f;
        [SerializeField, Min(0f)] private float hintStaleSeconds = 20f;

        private sealed class Request
        {
            public VoiceLine line;
            public VoicePriority priority;
            public Func<bool> stillValid;
            // 过期时刻：排在前面的话说完所需时间 + 允许的额外等待。
            public float deadline;
        }

        private readonly Dictionary<string, VoiceLine> byId = new();
        private readonly List<Request> queue = new();
        private readonly List<string> history = new();
        private Request current;
        private Coroutine speaking;
        private bool running;
        private AudioSource radio, commander;
        private float idleSince, currentEndsAt;

        public IReadOnlyList<VoiceLine> Lines => lines;
        public IReadOnlyList<string> History => history;
        public string CurrentId => current?.line.id;
        public bool IsSpeaking => current != null;
        public int QueuedCount => queue.Count;
        // 从上一句结束算起的安静时长；正在说话时为 0。
        public float IdleSeconds => current != null || queue.Count > 0 ? 0f : Time.time - idleSince;
        public event Action<VoiceLine> LineStarted;

        public void Configure(VoiceLine[] voiceLines, Transform listenerHead, Transform commanderModel)
        {
            lines = voiceLines ?? Array.Empty<VoiceLine>();
            head = listenerHead;
            commanderBody = commanderModel;
            byId.Clear();
        }

        private void Awake()
        {
            radio = FeedbackSounds.CreateSource(head != null ? head : transform, "Mission Control Radio", 0f);
            radio.volume = radioVolume;
            // 无线电音色：只保留 450–3400 Hz 的话音频段，再加一点削波的毛刺感。
            radio.gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = 450f;
            radio.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3400f;
            radio.gameObject.AddComponent<AudioDistortionFilter>().distortionLevel = 0.35f;
            // 声源运行时才挂到指挥官身上，场景里的人物层级保持不变。
            if (commanderBody != null)
            {
                commander = FeedbackSounds.CreateSource(commanderBody, "Commander Voice", 0.85f);
                commander.transform.position = commanderBody.position + Vector3.up * mouthHeight;
            }
            else commander = FeedbackSounds.CreateSource(head != null ? head : transform, "Commander Voice", 0f);
            commander.volume = commanderVolume;
            commander.maxDistance = 18f;
            commander.minDistance = 1.2f;
            idleSince = Time.time;
        }

        public bool Has(string id) => Find(id) != null;

        public bool Say(string id, VoicePriority priority = VoicePriority.Hint, Func<bool> stillValid = null)
        {
            var line = Find(id);
            if (line == null || !isActiveAndEnabled) return false;
            if (current != null && current.line.id == id) return false;
            foreach (var queued in queue) if (queued.line.id == id) return false;
            // 吐槽要紧跟动作：替换掉还没说出口的旧吐槽，并排到操作提示前面。
            if (priority == VoicePriority.Quip) queue.RemoveAll(r => r.priority == VoicePriority.Quip);
            int index = queue.Count;
            if (priority != VoicePriority.Hint) { index = 0; while (index < queue.Count && queue[index].priority == VoicePriority.Urgent) index++; }
            bool interrupt = priority == VoicePriority.Urgent && current != null && current.priority != VoicePriority.Urgent;
            float ahead = interrupt ? 0f : Mathf.Max(0f, currentEndsAt - Time.time);
            for (int i = 0; i < index; i++) ahead += Duration(queue[i].line);
            float patience = priority == VoicePriority.Quip ? quipStaleSeconds : priority == VoicePriority.Hint ? hintStaleSeconds : float.PositiveInfinity;
            queue.Insert(index, new Request { line = line, priority = priority, stillValid = stillValid, deadline = Time.time + ahead + patience });
            if (interrupt) Interrupt();
            EnsureRunning();
            return true;
        }

        // 清空排队；keepCurrent 为 false 时连正在说的一句也停下（重新开始、任务失败）。
        public void Clear(bool keepCurrent = false)
        {
            queue.Clear();
            if (!keepCurrent) Interrupt();
        }

        private void OnDisable()
        {
            queue.Clear();
            Interrupt();
        }

        private VoiceLine Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId.Count == 0) foreach (var line in lines) if (line != null && !string.IsNullOrEmpty(line.id)) byId[line.id] = line;
            return byId.TryGetValue(id, out var found) ? found : null;
        }

        // 队列里的话可能全部过期而被同步跳过，协程在 StartCoroutine 返回前就已结束；用 running 标记而非协程句柄判断。
        private void EnsureRunning()
        {
            if (running || queue.Count == 0 || !isActiveAndEnabled) return;
            running = true;
            speaking = StartCoroutine(Run());
            if (!running) speaking = null;
        }

        private void Interrupt()
        {
            if (speaking != null) StopCoroutine(speaking);
            speaking = null;
            running = false;
            if (radio != null) radio.Stop();
            if (commander != null) commander.Stop();
            if (current != null) idleSince = Time.time;
            current = null;
            currentEndsAt = Time.time;
            EnsureRunning();
        }

        private IEnumerator Run()
        {
            while (queue.Count > 0)
            {
                var next = queue[0];
                queue.RemoveAt(0);
                if (Time.time > next.deadline) continue;
                if (next.stillValid != null && !next.stillValid()) continue;
                current = next;
                currentEndsAt = Time.time + Duration(next.line);
                history.Add(next.line.id);
                LineStarted?.Invoke(next.line);
                bool onRadio = next.line.speaker == VoiceSpeaker.MissionControl;
                var source = onRadio ? radio : commander;
                if (onRadio)
                {
                    FeedbackSounds.Play(radio, FeedbackSound.RadioOpen, 0.5f);
                    yield return Wait(0.3f);
                }
                // 时长按片段长度计时，而非 isPlaying：无声卡或批处理测试中播放状态不可靠。
                float length = next.line.clip != null ? next.line.clip.length : 0f;
                if (next.line.clip != null && source.isActiveAndEnabled) { source.clip = next.line.clip; source.Play(); }
                yield return Wait(length);
                if (onRadio)
                {
                    FeedbackSounds.Play(radio, FeedbackSound.RadioClose, 0.45f);
                    yield return Wait(0.3f);
                }
                current = null;
                idleSince = Time.time;
                yield return Wait(gapSeconds);
            }
            speaking = null;
            running = false;
            idleSince = Mathf.Min(idleSince, Time.time);
        }

        // 一句话从开讲到下一句可以开始的总时长（无线电含开讲/收讲提示音）。
        private float Duration(VoiceLine line)
            => (line.clip != null ? line.clip.length : 0f) + (line.speaker == VoiceSpeaker.MissionControl ? 0.6f : 0f) + gapSeconds;

        private static IEnumerator Wait(float seconds)
        {
            for (float end = Time.time + seconds; Time.time < end;) yield return null;
        }
    }
}
