using System;
using UnityEngine;

namespace LunarEscape
{
    // 基地照明总闸：开局整站断电、灯全灭，只有总闸旁一盏红色应急灯。
    // 玩家握住总闸拉杆拉到底即合闸：咔嗒一声，舱内灯带闪烁几下后亮起。
    // 只负责“照明是否恢复”及亮度过渡；灯的强度由 LifeSupportEnvironment 统一计算。
    public sealed class HabitatBreaker : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private ReleaseLever lever;
        [SerializeField] private CockpitLamp lamp;
        [SerializeField] private Light emergencyLight;
        [Tooltip("合闸后灯光从闪烁到稳定所需秒数。")]
        [SerializeField, Min(0.1f)] private float warmUpSeconds = 1.6f;
        private AudioSource source;
        private float onTime = -1f;
        private bool deniedThisPull;
        [SerializeField] private StationPatchPuzzle circuitPuzzle;
        public void ConfigureCircuit(StationPatchPuzzle puzzle) => circuitPuzzle = puzzle;

        public ReleaseLever Lever => lever;
        public bool IsOn { get; private set; }

        // 0=全黑，1=正常亮度；合闸后带几次闪烁逐渐稳定。
        public float Brightness
        {
            get
            {
                if (!IsOn) return 0f;
                float t = (Time.time - onTime) / warmUpSeconds;
                if (t >= 1f) return 1f;
                // 老式灯管启辉：前半段闪两三下，后半段平滑亮起。
                bool flicker = t < 0.55f && Mathf.Repeat(t * 9f, 1f) > 0.55f;
                return flicker ? 0.05f : Mathf.SmoothStep(0.2f, 1f, t);
            }
        }

        public event Action SwitchedOn;

        public void Configure(StationMission task, ReleaseLever handle, CockpitLamp indicator, Light emergency)
        { mission = task; lever = handle; lamp = indicator; emergencyLight = emergency; }

        private void Awake() => source = FeedbackSounds.CreateSource(lever != null ? lever.transform : transform, "Breaker Audio", 1f);

        private void OnEnable()
        {
            if (mission == null) return;
            mission.PhaseChanged += OnPhaseChanged;
            // The ground is inactive during flight and can miss the retry event.
            // Reconcile before the first lighting update when it returns.
            if (mission.Phase == StationMissionPhase.Briefing) ResetBreaker();
        }
        private void OnDisable() { if (mission != null) mission.PhaseChanged -= OnPhaseChanged; }

        private void OnPhaseChanged(StationMissionPhase phase) { if (phase == StationMissionPhase.Briefing) ResetBreaker(); }

        private bool CanSwitch => mission != null && (mission.Phase == StationMissionPhase.Repair || mission.Phase == StationMissionPhase.Evacuation)
            && (circuitPuzzle == null || circuitPuzzle.IsSolved && mission.LifeSupport != null && mission.LifeSupport.BasePower > 0);

        private void Update()
        {
            if (!IsOn && lever.Pulled)
            {
                if (CanSwitch) SwitchOn();
                else if (!deniedThisPull)
                {
                    // 任务开始前总闸被联锁：先在控制台开始任务。
                    deniedThisPull = true;
                    lever.Deny();
                    if (lamp != null) lamp.Flash();
                }
            }
            if (!lever.isSelected) deniedThisPull = false;
            if (lamp != null) lamp.State = IsOn ? LampState.Done : CanSwitch ? LampState.Next : LampState.Busy;
            if (emergencyLight != null) emergencyLight.enabled = (!IsOn || Brightness < 0.5f)
                && (mission.LifeSupport == null || mission.LifeSupport.BasePower > 0);
        }

        // 也供开发菜单与测试使用：直接合闸。
        public void SwitchOn()
        {
            if (IsOn || circuitPuzzle != null && !CanSwitch) return;
            IsOn = true;
            onTime = Time.time;
            lever.LockedDown = true;
            FeedbackSounds.Play(source, FeedbackSound.Clunk, 1f);
            HandHaptics.Pulse(lever.HoldingHand, 0.8f, 0.2f);
            SwitchedOn?.Invoke();
        }

        public void ResetBreaker()
        {
            IsOn = false;
            onTime = -1f;
            deniedThisPull = false;
            if (lever != null) lever.ResetLever();
        }
    }
}
