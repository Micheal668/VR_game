using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 登舱是舱门上的主动操作：身体必须在梯子下的区域，舱体本身始终阻挡行走。
    public sealed class HatchBoardingController : MonoBehaviour
    {
        [SerializeField] private StationMissionSession session;
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private Transform hatch;
        [SerializeField] private Button button;
        [SerializeField] private XRSimpleInteractable doorInteraction;
        [SerializeField] private LocalizedText hint;
        [SerializeField] private float reachDistance = 5.5f;
        [SerializeField] private Button accessibleButton;
        [SerializeField] private LocalizedText accessibleHint;
        private bool requested;
        public Transform Hatch => hatch;
        public Button AccessibleButton => accessibleButton;
        public bool CanBoard => !requested && session != null && session.Mission.Phase == StationMissionPhase.Evacuation
            && session.Mission.RemainingSeconds > 0 && session.Exit.ContainsPlayer
            && Vector3.Distance(session.Player.Camera.transform.position, hatch.position) <= reachDistance;
        public void Configure(StationMissionSession flow, CargoInventory cargo, Transform door,
            Button control, XRSimpleInteractable interactable, LocalizedText instructions)
        {
            session=flow; inventory=cargo; hatch=door; button=control; doorInteraction=interactable; hint=instructions;
        }
        public void ConfigureAccessiblePrompt(Button control, LocalizedText instructions, float maximumReach)
        { accessibleButton=control; accessibleHint=instructions; reachDistance=maximumReach; }
        private void OnEnable() { if (session != null) { session.Mission.PhaseChanged += OnPhase; OnPhase(session.Mission.Phase); } }
        private void OnDisable() { if (session != null) session.Mission.PhaseChanged -= OnPhase; }
        private void OnPhase(StationMissionPhase phase) { if (phase == StationMissionPhase.Briefing) requested=false; }
        private void Update()
        {
            bool ready=CanBoard;
            button.interactable=ready;
            // 射线可见，但远离梯子时点击不能绕过身体距离判断。
            doorInteraction.enabled=session.Mission.Phase==StationMissionPhase.Evacuation && !requested;
            hint.SetKey(ready ? "lander.hatch.ready" : "lander.hatch.approach");
            if(accessibleButton!=null) accessibleButton.interactable=ready;
            if(accessibleHint!=null) accessibleHint.SetKey(ready ? "boarding.ready" : "boarding.approach");
        }
        public void OnSelected(SelectEnterEventArgs args) => RequestBoarding();
        public void RequestBoarding()
        {
            if (!CanBoard || !inventory.LoadAllCarriedIntoShip()) return;
            requested=session.ConfirmExit();
            // 不调用零时间 Tick；本帧倒计时归零仍然优先判失败。
            if (requested) {button.interactable=false;if(accessibleButton!=null)accessibleButton.interactable=false;}
        }
    }
}
