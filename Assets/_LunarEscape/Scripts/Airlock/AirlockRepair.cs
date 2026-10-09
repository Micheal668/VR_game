using System;
using UnityEngine;

namespace LunarEscape
{
    // 气闸 A 抢修：汇总三处故障（断电、压差、锁销），全部修好后拉下手动开门拉杆并保持，
    // 气闸完成开锁循环即“放行”（解除门锁；生命保障场景中再由气闸压力屏开门）。放行事实交给 StationMissionSession（维修阶段内放行获得撤离奖励）
    // 和 MissionEnvironment（撤离阶段只有放行后舱门才真正打开）。不读取手柄输入，不改任务时间。
    public sealed class AirlockRepair : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private AirlockFault[] faults = new AirlockFault[0];
        [SerializeField] private ReleaseLever lever;
        [SerializeField] private CockpitLamp leverLamp;
        [SerializeField, Min(0.1f)] private float cycleSeconds = 1.5f;

        private readonly HapticRumble rumble = new();
        private AudioSource motor;
        private AudioSource oneShot;
        private bool deniedThisPull;

        public StationMission Mission => mission;

        public AirlockFault[] Faults => faults;
        public ReleaseLever Lever => lever;
        public bool IsReleased { get; private set; }
        public float CycleProgress { get; private set; }
        public int FixedCount { get { int n = 0; foreach (var f in faults) if (f != null && f.IsFixed) n++; return n; } }
        public bool AllFixed => faults.Length > 0 && FixedCount == faults.Length;

        // 任务开始后、成功放行前可以修理；撤离阶段仍未修好时可以继续修。
        public bool Active => !IsReleased && mission != null
            && (mission.Phase == StationMissionPhase.Repair || mission.Phase == StationMissionPhase.Evacuation);

        public event Action Changed;
        // 故障未排除就拉杆：界面据此闪烁未完成的项目。
        public event Action Denied;

        public void Configure(StationMission task, AirlockFault[] parts, ReleaseLever release, CockpitLamp lamp)
        {
            mission = task; faults = parts; lever = release; leverLamp = lamp;
            foreach (var fault in faults) fault.ConfigureOwner(this);
        }

        private void Awake()
        {
            var anchor = lever != null ? lever.transform : transform;
            motor = FeedbackSounds.CreateLoop(anchor, "Airlock Motor Audio", FeedbackSound.EngineLoop, 1f);
            oneShot = FeedbackSounds.CreateSource(anchor, "Airlock Audio", 1f);
        }

        private void OnEnable()
        {
            if (mission != null) mission.PhaseChanged += OnPhaseChanged;
            foreach (var fault in faults) fault.Changed += OnFaultChanged;
            if (!motor.isPlaying) motor.Play();
        }

        private void OnDisable()
        {
            if (mission != null) mission.PhaseChanged -= OnPhaseChanged;
            foreach (var fault in faults) fault.Changed -= OnFaultChanged;
            motor.volume = 0f;
        }

        private void OnFaultChanged() => Changed?.Invoke();

        private void OnPhaseChanged(StationMissionPhase phase)
        {
            if (phase == StationMissionPhase.Briefing) ResetAirlock();
            Changed?.Invoke();
        }

        private void Update()
        {
            bool cycling = false;
            if (lever.Pulled && Active)
            {
                if (AllFixed)
                {
                    cycling = true;
                    CycleProgress = Mathf.Min(1f, CycleProgress + Time.deltaTime / cycleSeconds);
                    rumble.Drive(lever.HoldingHand, 0.3f + 0.4f * CycleProgress);
                    if (CycleProgress >= 1f) Release();
                }
                else if (!deniedThisPull)
                {
                    // 一次拉杆只提示一次，避免持续嗡鸣。
                    deniedThisPull = true;
                    lever.Deny();
                    if (leverLamp != null) leverLamp.Flash();
                    Denied?.Invoke();
                }
            }
            else
            {
                if (!lever.isSelected) deniedThisPull = false;
                if (!IsReleased) CycleProgress = 0f;
            }

            // 开锁循环期间电机由低到高嗡鸣；松手即停，需要重新拉住。
            motor.volume = Mathf.MoveTowards(motor.volume, cycling ? 0.45f : 0f, Time.deltaTime * 3f);
            motor.pitch = 0.6f + 0.6f * CycleProgress;
            if (leverLamp != null)
                leverLamp.State = IsReleased ? LampState.Done : cycling ? LampState.Busy : AllFixed && Active ? LampState.Next : LampState.Off;
        }

        private void Release()
        {
            IsReleased = true;
            lever.LockedDown = true;
            FeedbackSounds.Play(oneShot, FeedbackSound.Clunk, 1f);
            FeedbackSounds.Play(oneShot, FeedbackSound.Hiss, 0.8f);
            HandHaptics.Pulse(lever.HoldingHand, 0.9f, 0.3f);
            Changed?.Invoke();
        }

        // 开发与测试捷径：跳过三处故障直接视为已放行（例如只想测试撤离或飞行段）。
        // 正式流程不会调用；真实手部操作由 AirlockRepairTests 单独验证。
        public void SkipRepair()
        {
            if (IsReleased) return;
            CycleProgress = 1f;
            IsReleased = true;
            if (lever != null) lever.LockedDown = true;
            Changed?.Invoke();
        }

        public void ResetAirlock()
        {
            IsReleased = false;
            CycleProgress = 0f;
            deniedThisPull = false;
            lever.ResetLever();
            foreach (var fault in faults) fault.ResetFault();
            Changed?.Invoke();
        }
    }
}
