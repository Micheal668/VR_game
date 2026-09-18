using System;
using UnityEngine;

namespace LunarEscape
{
    public enum StationMissionPhase { Briefing, Repair, Stabilized, Evacuation, Completed, Failed }
    public enum StationMissionFailure { None, EvacuationTimeout }

    // 只管理任务阶段和时间。输入、维修接触与出口检测由场景组件传入。
    // 维修和任务共用本组件的 Tick，避免同一帧被两套计时器重复推进。
    public sealed class StationMission : MonoBehaviour
    {
        [SerializeField] private StationMissionConfig config;
        [SerializeField] private TimedRepairTask repairTask;
        private uint attemptVersion;

        public StationMissionConfig Config => config;
        public TimedRepairTask RepairTask => repairTask;
        public StationMissionPhase Phase { get; private set; } = StationMissionPhase.Briefing;
        public float RemainingSeconds { get; private set; }
        public bool RepairRestored { get; private set; }
        public float EvacuationBudgetSeconds { get; private set; }
        public StationMissionFailure FailureReason { get; private set; }
        public bool IsTerminal => Phase == StationMissionPhase.Completed || Phase == StationMissionPhase.Failed;

        public event Action Changed;
        public event Action<StationMissionPhase> PhaseChanged;

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

            // 回调可以重开任务；旧调用不能继续修改新一轮的状态。
            if (version != attemptVersion) return;
            if (previousPhase != Phase) PhaseChanged?.Invoke(Phase);
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
                        RepairState previousRepairState = repairTask.State;
                        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - step);
                        frameSeconds = Mathf.Max(0f, frameSeconds - step);
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
                            return;
                        }
                        break;
                    }
                    case StationMissionPhase.Stabilized:
                    {
                        float step = Mathf.Min(frameSeconds, RemainingSeconds);
                        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - step);
                        frameSeconds = Mathf.Max(0f, frameSeconds - step);
                        if (RemainingSeconds <= 0f)
                        {
                            EnterEvacuation();
                        }
                        else
                        {
                            if (step > 0f) Changed?.Invoke();
                            return;
                        }
                        break;
                    }
                    case StationMissionPhase.Evacuation:
                    {
                        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - frameSeconds);
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
                        else if (frameSeconds > 0f)
                        {
                            Changed?.Invoke();
                        }
                        return;
                    }
                    default:
                        return;
                }
            }
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
            PhaseChanged?.Invoke(nextPhase);
            if (version == attemptVersion) Changed?.Invoke();
        }
    }
}
