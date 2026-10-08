using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 三个位置的乘员面板只显示同一状态：基地操作、梯旁等待与舱内医疗，不在界面决定生死。
    public sealed class CrewPanelPresenter : MonoBehaviour
    {
        [SerializeField] private CrewMission crew;
        [SerializeField] private LocalizationService localization;
        [SerializeField] private GameObject groundPanel, boardingPanel, cabinPanel;
        [SerializeField] private LocalizedText groundStatus, groundHelp, boardingStatus, cabinStatus;
        [SerializeField] private Button rescueButton, medicalButton, cabinMedicalButton;
        [SerializeField] private GroundCrewController controller;
        [SerializeField] private bool onlyDuringEvacuation;
        [SerializeField] private bool hideCabinPanel;
        private bool listening;
        public GameObject GroundPanel => groundPanel;
        public GameObject BoardingPanel => boardingPanel;
        public GameObject CabinPanel => cabinPanel;
        public Button RescueButton => rescueButton;
        public Button MedicalButton => medicalButton;
        public Button CabinMedicalButton => cabinMedicalButton;
        public void ConfigureGroundVisibility(bool alarmOnly) { onlyDuringEvacuation = alarmOnly; if (crew != null) Refresh(); }
        public void ConfigureCabinVisibility(bool visible) { hideCabinPanel = !visible; if (crew != null) Refresh(); }
        public void Configure(CrewMission task, LocalizationService language, GroundCrewController bridge,
            GameObject ground, GameObject boarding, GameObject cabin, LocalizedText status, LocalizedText help,
            LocalizedText boardingText, LocalizedText cabinText, Button rescue, Button medical, Button cabinMedical)
        {
            Unsubscribe();
            crew = task; localization = language; controller = bridge;
            groundPanel = ground; boardingPanel = boarding; cabinPanel = cabin;
            groundStatus = status; groundHelp = help; boardingStatus = boardingText; cabinStatus = cabinText;
            rescueButton = rescue; medicalButton = medical; cabinMedicalButton = cabinMedical;
            if (isActiveAndEnabled) Subscribe();
        }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (crew == null || listening) return;
            listening = true;
            crew.Changed += Refresh; crew.Station.Changed += Refresh; crew.Flight.Changed += Refresh;
            crew.Inventory.Changed += Refresh; localization.LanguageChanged += Refresh; Refresh();
        }
        private void OnDisable() => Unsubscribe();
        private void Unsubscribe()
        {
            if (crew == null || !listening) return;
            crew.Changed -= Refresh; crew.Station.Changed -= Refresh; crew.Flight.Changed -= Refresh;
            crew.Inventory.Changed -= Refresh; localization.LanguageChanged -= Refresh;
            listening = false;
        }
        private void Update()
        {
            // 移动会改变近身可操作范围；只刷新按钮，不另起任务时钟。
            if (crew != null && groundPanel.activeInHierarchy)
            {
                rescueButton.interactable = crew.CanRescue;
                bool canTreat = false;
                foreach (var item in crew.Inventory.Items)
                    if (item != null && crew.CanTreatCommanderWith(item) && Vector3.Distance(item.transform.position, controller.MedicalPort.position) <= .48f) { canTreat = true; break; }
                medicalButton.interactable = canTreat;
            }
        }
        private void Refresh()
        {
            if (groundPanel == null) return;
            bool onGround = crew.Station.Phase != StationMissionPhase.Completed && crew.Station.Phase != StationMissionPhase.Failed;
            if (onlyDuringEvacuation) onGround = crew.Station.Phase == StationMissionPhase.Evacuation;
            groundPanel.SetActive(onGround);
            boardingPanel.SetActive(crew.Station.Phase == StationMissionPhase.Evacuation);
            cabinPanel.SetActive(!hideCabinPanel && crew.CommanderBoarded && crew.Flight.IsLocked && !crew.IsOutcomeResolved);
            string state = localization.Format("crew.state." + crew.CommanderState);
            groundStatus.SetKey("crew.telemetry", state, Mathf.CeilToInt(crew.CommanderHealth), Mathf.CeilToInt(crew.RescueProgress01 * 100));
            groundHelp.SetKey(crew.CommanderState == CrewState.Trapped ? "crew.help.rescue" : crew.CommanderState == CrewState.Following ? "crew.help.follow" : "crew.help.ready");
            boardingStatus.SetKey(crew.CommanderState == CrewState.Following && controller.ReachedBoarding ? "crew.board.ready" : "crew.board.alone", state);
            cabinStatus.SetKey("crew.cabin.status", state, Mathf.CeilToInt(crew.CommanderHealth), crew.CommanderInjuryPerSecond.ToString("0.00"));
            cabinMedicalButton.interactable = crew.CanUseLoadedMedicalOnCommander;
            Update();
        }
    }
}
