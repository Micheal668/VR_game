using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace LunarEscape
{
    // 维修手感：工具有效接触维修点期间，维修点发出电焊声，持握工具的手持续细碎震动，
    // 震动随进度略微增强；完成时播放提示音并重震一下。只读取任务状态，不推进维修。
    [RequireComponent(typeof(TimedRepairTask), typeof(RepairContact))]
    public sealed class RepairFeedback : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float loopVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float chimeVolume = 0.7f;
        [Tooltip("开始维修时的震动幅度；随进度增加到 working + progressBoost。")]
        [SerializeField, Range(0f, 1f)] private float workingAmplitude = 0.18f;
        [SerializeField, Range(0f, 1f)] private float progressBoost = 0.22f;
        [SerializeField, Range(0f, 1f)] private float completeAmplitude = 0.8f;
        private readonly HapticRumble rumble = new();
        private TimedRepairTask task;
        private RepairContact contact;
        private AudioSource loop;
        private AudioSource oneShot;
        private HapticImpulsePlayer lastHand;

        private void Awake()
        {
            task = GetComponent<TimedRepairTask>();
            contact = GetComponent<RepairContact>();
            Transform anchor = contact.RepairPoint != null ? contact.RepairPoint : transform;
            loop = FeedbackSounds.CreateLoop(anchor, "Repair Loop Audio", FeedbackSound.RepairLoop, 1f);
            oneShot = FeedbackSounds.CreateSource(anchor, "Repair Feedback Audio", 1f);
        }

        private void OnEnable()
        {
            task.Completed += OnCompleted;
            if (!loop.isPlaying) loop.Play();
        }

        private void OnDisable()
        {
            task.Completed -= OnCompleted;
            loop.volume = 0f;
            rumble.Clear();
        }

        private void Update()
        {
            // 同时要求任务处于维修中且工具仍在接触：阶段切换后任务状态可能停留在 Working。
            RepairTool tool = task.State == RepairState.Working ? contact.FindContactTool() : null;
            float target = tool != null ? loopVolume : 0f;
            loop.volume = Mathf.MoveTowards(loop.volume, target, Time.deltaTime * 4f);
            if (tool == null) return;
            var hand = HandHaptics.FromGrab(tool.Grab);
            if (hand != null) lastHand = hand;
            // 随机起伏模拟火花的颗粒感，避免长时间均匀震动让手麻木。
            float amplitude = workingAmplitude + progressBoost * task.Progress;
            rumble.Drive(hand, amplitude * Random.Range(0.6f, 1.2f));
        }

        private void OnCompleted()
        {
            FeedbackSounds.Play(oneShot, FeedbackSound.Chime, chimeVolume);
            // 完成的这一帧工具通常仍在手中；万一已松手，就用最近一次维修的手。
            var tool = contact.FindContactTool();
            var hand = tool != null ? HandHaptics.FromGrab(tool.Grab) : null;
            HandHaptics.Pulse(hand != null ? hand : lastHand, completeAmplitude, 0.25f);
        }
    }
}
