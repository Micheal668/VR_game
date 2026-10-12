using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace LunarEscape
{
    // 按剧情阶段切换背景音乐，两路声源交叉淡化：
    //   第二天（场景 11）：醒来与抢修时紧张，开门撤离时急促（最后 30 秒加快），升空奏响一段凯歌，入轨与对接平静；
    //   第一天（场景 12）：舱内平静，出舱后空旷；入睡时淡出。
    // 有人说话时音乐自动压低，不盖过地面指挥和指挥官。音乐不受舱外闷音处理影响。
    public sealed class MusicDirector : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private AscentMission flight;
        [SerializeField] private MissionVoice voice;
        [SerializeField] private DayOneMission dayOne;
        [SerializeField, Range(0f, 1f)] private float volume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float duckedLevel = 0.45f;
        [SerializeField, Min(0.1f)] private float crossfadeSeconds = 2.5f;

        private readonly Dictionary<MusicTrack, AudioClip> clips = new();
        private readonly Dictionary<MusicTrack, Task<float[]>> rendering = new();
        private AudioSource a, b, sting;
        private MusicTrack playing = MusicTrack.None;
        private AscentPhase lastFlightPhase;
        private float duck = 1f;

        public MusicTrack Current => playing;
        public MusicTrack Wanted { get; private set; }

        public void Configure(StationMission task, LifeSupportMission support, AscentMission ascent, MissionVoice speaker, DayOneMission day)
        { mission = task; life = support; flight = ascent; voice = speaker; dayOne = day; }

        private void Awake()
        {
            a = Source("Music A"); b = Source("Music B"); sting = Source("Music Sting");
            sting.loop = false;
            // 后台线程预先合成本场景会用到的乐段。
            foreach (var track in dayOne != null ? new[] { MusicTrack.Calm, MusicTrack.Outside }
                         : new[] { MusicTrack.Tension, MusicTrack.Urgent, MusicTrack.Triumph, MusicTrack.Calm })
                Prepare(track);
            if (flight != null) lastFlightPhase = flight.Phase;
        }

        private AudioSource Source(string name)
        {
            var source = FeedbackSounds.CreateSource(transform, name, 0f, true);
            source.bypassListenerEffects = true;
            source.volume = 0f;
            return source;
        }

        private void Prepare(MusicTrack track)
        {
            if (track == MusicTrack.None || clips.ContainsKey(track) || rendering.ContainsKey(track)) return;
            rendering[track] = Task.Run(() => MusicSynth.Render(track));
        }

        private AudioClip Clip(MusicTrack track)
        {
            if (clips.TryGetValue(track, out var clip)) return clip;
            Prepare(track);
            var task = rendering[track];
            if (!task.IsCompleted) return null;
            rendering.Remove(track);
            if (task.IsFaulted) { Debug.LogException(task.Exception); return null; }
            clip = AudioClip.Create("Music " + track, task.Result.Length, 1, MusicSynth.Rate, false);
            clip.SetData(task.Result, 0);
            clips[track] = clip;
            return clip;
        }

        public MusicTrack Choose()
        {
            if (dayOne != null)
                return dayOne.Current == DayOneMission.Stage.Sleeping ? MusicTrack.None : dayOne.PlayerOutside ? MusicTrack.Outside : MusicTrack.Calm;
            if (mission == null) return MusicTrack.None;
            if (mission.Phase == StationMissionPhase.Failed || flight != null && flight.Phase == AscentPhase.Failed) return MusicTrack.None;
            if (flight != null && flight.IsLocked)
                return flight.Phase == AscentPhase.Startup || flight.Phase == AscentPhase.Ignition ? MusicTrack.Tension : MusicTrack.Calm;
            if (life != null && life.DoorOpen || mission.Phase == StationMissionPhase.Evacuation) return MusicTrack.Urgent;
            return MusicTrack.Tension;
        }

        private void Update()
        {
            Wanted = Choose();
            if (Wanted != playing)
            {
                var clip = Wanted == MusicTrack.None ? null : Clip(Wanted);
                if (Wanted == MusicTrack.None || clip != null)
                {
                    (a, b) = (b, a); // a 为新曲，b 淡出
                    a.clip = clip;
                    if (clip != null) { a.volume = 0f; a.Play(); }
                    playing = Wanted;
                }
            }
            // 升空与对接成功时插入一段凯歌。
            if (flight != null && flight.Phase != lastFlightPhase)
            {
                lastFlightPhase = flight.Phase;
                if (flight.Phase == AscentPhase.Ascent || flight.Phase == AscentPhase.Docked)
                {
                    var triumph = Clip(MusicTrack.Triumph);
                    if (triumph != null) sting.PlayOneShot(triumph, volume * 2.2f);
                }
            }
            float speaking = voice != null && voice.IsSpeaking ? duckedLevel : 1f;
            duck = Mathf.MoveTowards(duck, speaking, Time.deltaTime * 1.5f);
            float fade = Time.deltaTime / crossfadeSeconds;
            a.volume = Mathf.MoveTowards(a.volume, a.clip != null ? volume * duck : 0f, fade * volume);
            b.volume = Mathf.MoveTowards(b.volume, 0f, fade * volume);
            if (b.volume <= 0f && b.isPlaying) b.Stop();
            // 撤离最后 30 秒节奏加快。
            a.pitch = playing == MusicTrack.Urgent && mission != null && mission.Phase == StationMissionPhase.Evacuation && mission.RemainingSeconds < 30f ? 1.08f
                : playing == MusicTrack.Calm && flight != null && flight.IsLocked ? 0.92f : 1f;
        }

        private void OnDestroy()
        {
            foreach (var clip in clips.Values) if (clip != null) Destroy(clip);
        }
    }
}
