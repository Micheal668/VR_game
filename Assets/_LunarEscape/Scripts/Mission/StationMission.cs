using System;
using UnityEngine;

namespace LunarEscape
{
    public enum StationMissionPhase { Briefing, Repair, Stabilized, Evacuation, Completed, Failed }
    public enum StationMissionFailure { None, EvacuationTimeout, Suffocation, Hypothermia, Hyperthermia }

    // 只管理任务阶段和时间。输入、维修接触与出口检测由场景组件传入。
    // 维修和任务共用本组件的 Tick，避免同一帧被两套计时器重复推进。
    public sealed class StationMission : MonoBehaviour
    {
        [SerializeField] private StationMissionConfig config;
        [SerializeField] private TimedRepairTask repairTask;
        [SerializeField] private LifeSupportMission lifeSupport;
        private uint attemptVersion;

        public StationMissionConfig Config => config;
        public TimedRepairTask RepairTask => repairTask;
        public LifeSupportMission LifeSupport => lifeSupport;
        public StationMissionPhase Phase { get; private set; } = StationMissionPhase.Briefing;
        public float RemainingSeconds { get; private set; }
        public bool RepairRestored { get; private set; }
        public float EvacuationBudgetSeconds { get; private set; }
        public StationMissionFailure FailureReason { get; private set; }
        public bool IsTerminal => Phase == StationMissionPhase.Completed || Phase == StationMissionPhase.Failed;

        public event Action Changed;
        public event Action<StationMissionPhase> PhaseChanged;
        // 仅撤离阶段实际消耗的时间；余额已扣除，但终态尚未发布。
        public event Action<float> TimeAdvanced;

        public void ConfigureLifeSupport(LifeSupportMission source)
        {
            if (source == null || source.Station != this) throw new ArgumentException("生命保障必须属于当前基地任务。");
            lifeSupport = source;
        }
        internal void FailLifeSupport(LifeSupportMission source, StationMissionFailure reason)
        {
            if (source != lifeSupport || source == null || IsTerminal || Phase == StationMissionPhase.Briefing
                || reason == StationMissionFailure.None || reason == StationMissionFailure.EvacuationTimeout) return;
            ++attemptVersion; FailureReason = reason; SetPhase(StationMissionPhase.Failed);
        }

        // 生命保障场景以实际开门作为撤离起点，不再要求先等完旧教学阶段。
        internal bool BeginLifeSupportEvacuation(LifeSupportMission source)
        {
            if (source == null || source != lifeSupport || !source.DoorOpen || !source.IsGroundActive) return false;
            if (Phase == StationMissionPhase.Evacuation) return true;
            ++attemptVersion; // 若在 Tick 的回调中开门，旧阶段不能继续写入本轮计时。
            EnterEvacuation();
            return Phase == StationMissionPhase.Evacuation;
        }

        private void Awake()
        {
            ResetMission();
        }

        public void Configure(StationMissionConfig settings, TimedRepairTask repair)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (repair == null) throw new ArgumentNullException(nameof(repair));
            config = settings;
            repairTask = repair;
            ResetMission();
        }

        public void Begin()
        {
            if (Phase != StationMissionPhase.Briefing) return;
            if (config == null || repairTask == null)
                throw new InvalidOperationException("开始任务前需要配置任务参数和维修任务。");

            ++attemptVersion;
            EvacuationBudgetSeconds = config.BaseEvacuationSeconds;
            RemainingSeconds = config.RepairWindowSeconds;
            SetPhase(StationMissionPhase.Repair);
        }

        public void ResetMission()
        {
            uint version = ++attemptVersion;
            StationMissionPhase previousPhase = Phase;
            Phase = StationMissionPhase.Briefing;
            RemainingSeconds = 0f;
            RepairRestored = false;
            EvacuationBudgetSeconds = config != null ? config.BaseEvacuationSeconds : 0f;
            FailureReason = StationMissionFailure.None;
            repairTask?.ResetTask();
            lifeSupport?.ResetForMission();

            // 回调可以重开任务；旧调用不能继续修改新一轮的状态。
            if (version != attemptVersion) return;
            if (previousPhase != Phase) PublishPhase(Phase, version);
            if (version == attemptVersion) Changed?.Invoke();
        }

        public void Tick(float deltaTime, bool canRepair, bool playerAtExit)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ||
                Phase == StationMissionPhase.Briefing || IsTerminal) return;

            uint version = attemptVersion;
            float frameSeconds = deltaTime;
            while (version == attemptVersion && !IsTerminal)
            {
                switch (Phase)
                {
                    case StationMissionPhase.Repair:
                    {
                        float step = Mathf.Min(frameSeconds, RemainingSeconds);
                        // 修好时就结束维修阶段，剩余帧时间留给后面的阶段。
                        if (canRepair) step = Mathf.Min(step, repairTask.RemainingSeconds);
                        if (lifeSupport != null) step = lifeSupport.LimitStep(step);
                        RepairState previousRepairState = repairTask.State;
                        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - step);
                        frameSeconds = Mathf.Max(0f, frameSeconds - step);
                        lifeSupport?.Advance(step);
                        if (version != attemptVersion || IsTerminal) return;
                        repairTask.Tick(canRepair, step);
                        if (version != attemptVersion) return;

                        // 在维修截止点恰好完成，仍然获得修复奖励。
                        if (repairTask.State == RepairState.Complete)
                        {
                            RepairRestored = true;
                            EvacuationBudgetSeconds = config.BaseEvacuationSeconds + config.RepairBonusSeconds;
                            RemainingSeconds = config.StabilizedSeconds;
                            SetPhase(StationMissionPhase.Stabilized);
                        }
                        else if (RemainingSeconds <= 0f)
                        {
                            EnterEvacuation();
                        }
                        else
                        {
                            if (step > 0f || previousRepairState != repairTask.State) Changed?.Invoke();
                            if (frameSeconds > 0f && step > 0f) break;
                            return;
                        }
                        break;
                    }
                    case StationMissionPhase.Stabilized:
                    {
                        float step = Mathf.Min(frameSeconds, RemainingSeconds);
                        if (lifeSupport != null) step = lifeSupport.LimitStep(step);
                        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - step);
                        frameSeconds = Mathf.Max(0f, frameSeconds - step);
                        lifeSupport?.Advance(step);
                        if (version != attemptVersion || IsTerminal) return;
                        if (RemainingSeconds <= 0f)
                        {
                            EnterEvacuation();
                        }
                        else
                        {
                            if (step > 0f) Changed?.Invoke();
                            if (frameSeconds > 0f && step > 0f) break;
                            return;
                        }
                        break;
                    }
                    case StationMissionPhase.Evacuation:
                    {
                        float step = Mathf.Min(frameSeconds, RemainingSeconds);
                        if (lifeSupport != null) step = lifeSupport.LimitStep(step);
                        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - step);
                        frameSeconds = Mathf.Max(0f, frameSeconds - step);
                        lifeSupport?.Advance(step);
                        if (version != attemptVersion || IsTerminal) return;
                        if (step > 0f) PublishTime(step, version);
                        // 乘员/跟随回调可以重试，旧 Tick 不能再结束新一轮任务。
                        if (version != attemptVersion) return;
                        // 同一帧到达出口且时间归零时，明确按超时处理。
                        if (RemainingSeconds <= 0f)
                        {
                            FailureReason = StationMissionFailure.EvacuationTimeout;
                            SetPhase(StationMissionPhase.Failed);
                        }
                        else if (playerAtExit)
                        {
                            // 完成时保留剩余时间，供完成界面及后续报告使用。
                            SetPhase(StationMissionPhase.Completed);
                        }
                        else if (step > 0f)
                        {
                            Changed?.Invoke();
                            if (frameSeconds > 0f) break;
                        }
                        return;
                    }
                    default:
                        return;
                }
            }
        }

        // 奖励时间：完成恢复照明、气闸抢修、救出队友等关键行动时延长当前倒计时。
        // 只在维修与撤离阶段有效，不能让已结束或尚未开始的任务复活。
        public bool AddBonusTime(float seconds)
        {
            if (seconds <= 0f || !float.IsFinite(seconds)) return false;
            if (Phase != StationMissionPhase.Repair && Phase != StationMissionPhase.Evacuation) return false;
            RemainingSeconds += seconds;
            Changed?.Invoke();
            return true;
        }

        private void EnterEvacuation()
        {
            RemainingSeconds = EvacuationBudgetSeconds;
            SetPhase(StationMissionPhase.Evacuation);
        }

        private void SetPhase(StationMissionPhase nextPhase)
        {
            if (Phase == nextPhase) return;
            Phase = nextPhase;
            uint version = attemptVersion;
            PublishPhase(nextPhase, version);
            if (version == attemptVersion) Changed?.Invoke();
        }

        private void PublishTime(float step, uint version)
        {
            if (TimeAdvanced == null) return;
            foreach (Action<float> listener in TimeAdvanced.GetInvocationList())
            {
                // 在前一个监听者重试后，不能把旧轮的时间继续交给后面的乘员监听者。
                if (version != attemptVersion) return;
                listener(step);
            }
        }

        private void PublishPhase(StationMissionPhase phase, uint version)
        {
            if (PhaseChanged == null) return;
            foreach (Action<StationMissionPhase> listener in PhaseChanged.GetInvocationList())
            {
                if (version != attemptVersion || Phase != phase) return;
                listener(phase);
            }
        }
    }
}
