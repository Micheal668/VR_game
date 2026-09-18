using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 只负责把任务状态转成文案和按钮显示，不推进计时、不决定成功或失败。
    public sealed class MissionPresenter : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private LocalizedText heading;
        [SerializeField] private LocalizedText instructions;
        [SerializeField] private LocalizedText clock;
        [SerializeField] private LocalizedText repairSummary;
        [SerializeField] private Button beginButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private GameObject repairDetails;
        [SerializeField] private string instructionPrefix = "mission.";

        public void ConfigureInstructionPrefix(string prefix)
        {
            instructionPrefix = prefix;
            hasDisplayed = false;
            Refresh();
        }

        private bool hasDisplayed;
        private StationMissionPhase lastPhase;
        private bool lastRepairRestored;
        private int lastSeconds;
        private int lastBonus;

        public void Configure(StationMission mission, LocalizedText heading, LocalizedText instructions,
            LocalizedText clock, LocalizedText repairSummary, Button beginButton, Button retryButton,
            GameObject repairDetails = null)
        {
            if (isActiveAndEnabled && this.mission != null) this.mission.Changed -= Refresh;
            this.mission = mission;
            this.heading = heading;
            this.instructions = instructions;
            this.clock = clock;
            this.repairSummary = repairSummary;
            this.beginButton = beginButton;
            this.retryButton = retryButton;
            this.repairDetails = repairDetails;
            if (isActiveAndEnabled && mission != null) mission.Changed += Refresh;
            hasDisplayed = false;
            Refresh();
        }

        private void OnEnable()
        {
            if (mission != null) mission.Changed += Refresh;
            hasDisplayed = false;
            Refresh();
        }

        private void OnDisable()
        {
            if (mission != null) mission.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (mission == null) return;
            var phase = mission.Phase;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(mission.RemainingSeconds));
            int bonus = mission.Config != null ? Mathf.CeilToInt(mission.Config.RepairBonusSeconds) : 0;
            bool phaseChanged = !hasDisplayed || phase != lastPhase
                || mission.RepairRestored != lastRepairRestored || bonus != lastBonus;

            if (phaseChanged)
            {
                RefreshStage(phase, bonus);
                // 按钮只切换可见性，点击后的业务操作由场景连接到任务组件。
                if (beginButton != null) beginButton.gameObject.SetActive(phase == StationMissionPhase.Briefing);
                if (retryButton != null) retryButton.gameObject.SetActive(phase != StationMissionPhase.Briefing);
            }

            // 倒计时只在整秒变化时更新；本地化文字组件会自己响应语言切换。
            if (phaseChanged || seconds != lastSeconds)
                RefreshClock(phase, seconds);

            lastPhase = phase;
            lastRepairRestored = mission.RepairRestored;
            lastSeconds = seconds;
            lastBonus = bonus;
            hasDisplayed = true;
        }

        private void RefreshStage(StationMissionPhase phase, int bonus)
        {
            // 警报之后只展示撤离任务，避免旧维修提示让玩家误以为还应留在设备旁。
            if (repairDetails != null)
                repairDetails.SetActive(phase == StationMissionPhase.Briefing
                    || phase == StationMissionPhase.Repair || phase == StationMissionPhase.Stabilized);

            string stage = phase switch
            {
                StationMissionPhase.Repair => "repair",
                StationMissionPhase.Stabilized => "stabilized",
                StationMissionPhase.Evacuation => "evacuation",
                StationMissionPhase.Completed => "completed",
                StationMissionPhase.Failed => "failed",
                _ => "briefing"
            };
            SetText(heading, "mission." + stage + ".title");
            string instructionKey = instructionPrefix + stage + ".instructions";
            if (phase == StationMissionPhase.Evacuation)
                instructionKey += mission.RepairRestored ? ".repaired" : ".unrepaired";
            SetText(instructions, instructionKey, bonus);

            bool repairEnded = phase == StationMissionPhase.Evacuation
                || phase == StationMissionPhase.Completed || phase == StationMissionPhase.Failed;
            string result = mission.RepairRestored ? "repaired" : repairEnded ? "unrepaired" : "pending";
            SetText(repairSummary, "mission.summary." + result, bonus);
        }

        private void RefreshClock(StationMissionPhase phase, int seconds)
        {
            // 向上取整：还剩 0.2 秒时显示 00:01，不提前显示超时。
            string time = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            if (phase == StationMissionPhase.Briefing)
                SetText(clock, "mission.clock.ready");
            else if (phase == StationMissionPhase.Completed)
                SetText(clock, "mission.clock.completed", time);
            else
            {
                SetText(clock, phase == StationMissionPhase.Stabilized
                    ? "mission.clock.stabilized" : "mission.clock.remaining", time);
            }
        }

        private static void SetText(LocalizedText label, string key, params object[] values)
        {
            // 小面板可只连接需要的文字；未连接的可选项无需额外空对象。
            if (label != null) label.SetKey(key, values);
        }
    }
}
