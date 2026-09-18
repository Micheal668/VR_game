using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 确认按钮只在身体进入舱内后有效；是否成功仍由同一任务时钟判定。
    public sealed class CargoBoardingController : MonoBehaviour
    {
        [SerializeField] private StationMissionSession session;
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private LocalizedText instructions;
        [SerializeField] private Button confirm;
        private int lastDisplay = -1;

        public void Configure(StationMissionSession flow, CargoInventory cargo, LocalizedText hint, Button button)
        {
            session = flow; inventory = cargo; instructions = hint; confirm = button;
            lastDisplay = -1;
            Refresh();
        }

        public void ConfirmBoarding()
        {
            if (session == null || session.Mission.Phase != StationMissionPhase.Evacuation
                || !session.Exit.ContainsPlayer || session.Mission.RemainingSeconds <= 0f) return;
            if (!inventory.LoadAllCarriedIntoShip()) return;
            // 只提交确认。由 Session 下一次统一计时后结算，避免点击先于本帧
            // 倒计时更新时，用零时间步抢先成功，绕过同帧归零的失败优先规则。
            session.ConfirmExit();
            Refresh();
        }

        private void Update() => Refresh();
        private void Refresh()
        {
            if (session == null) return;
            bool complete = session.Mission.Phase == StationMissionPhase.Completed;
            bool ready = session.Mission.Phase == StationMissionPhase.Evacuation && session.Exit.ContainsPlayer;
            int display = complete ? 2 : ready ? 1 : 0;
            confirm.interactable = ready;
            if (display == lastDisplay) return;
            instructions.SetKey(complete ? "cargo.done" : ready ? "cargo.ready" : "cargo.await");
            lastDisplay = display;
        }
    }
}
