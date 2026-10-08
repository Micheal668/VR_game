using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 三块实体屏共用原飞行/对接规则。温度用摄氏度，温控保障余量与供电剩余量相对应。
    public sealed class CockpitScreenPresenter : MonoBehaviour
    {
        [SerializeField] private AscentMission flight;
        [SerializeField] private DockingMission docking;
        [SerializeField] private CrewMission crew;
        [SerializeField] private LocalizationService language;
        [SerializeField] private ResourceReadout oxygen, power, thermal;
        [SerializeField] private LocalizedText temperature, phase, telemetry, alignment, guidance, feedback, crewState, assistance;
        [SerializeField] private GameObject startupRoot, dockingRoot;
        [SerializeField] private Button[] startup, supplyButtons, thrustButtons;
        [SerializeField] private Button circularize, assist, commanderMedical;
        [SerializeField] private CargoKind[] supplyKinds;
        public ResourceReadout Oxygen => oxygen;
        public ResourceReadout Power => power;
        public ResourceReadout Thermal => thermal;
        public GameObject StartupRoot => startupRoot;
        public GameObject DockingRoot => dockingRoot;
        public Button[] StartupButtons => startup;
        public Button CircularizeButton => circularize;
        public Button[] SupplyButtons => supplyButtons;
        public Button[] ThrustButtons => thrustButtons;
        public float CabinTemperature => 22 + (flight != null ? flight.LeakPerSecond * 1.8f : 0);
        public void Configure(AscentMission task, DockingMission controller, CrewMission members, LocalizationService localization,
            ResourceReadout o2, ResourceReadout battery, ResourceReadout thermalReserve, LocalizedText[] labels,
            GameObject start, GameObject rcs, Button[] starts, Button[] supplies, CargoKind[] kinds, Button[] thrust,
            Button orbit, Button assistanceButton, Button medical)
        {
            Unsubscribe(); flight = task; docking = controller; crew = members; language = localization;
            oxygen = o2; power = battery; thermal = thermalReserve;
            temperature = labels[0]; phase = labels[1]; telemetry = labels[2]; alignment = labels[3];
            guidance = labels[4]; feedback = labels[5]; crewState = labels[6]; assistance = labels[7];
            startupRoot = start; dockingRoot = rcs; startup = starts; supplyButtons = supplies; supplyKinds = kinds;
            thrustButtons = thrust; circularize = orbit; assist = assistanceButton; commanderMedical = medical;
            if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() { Unsubscribe(); docking?.ReleaseControls(); }
        private void Subscribe()
        {
            if (flight == null) return;
            flight.Changed -= Refresh; flight.Changed += Refresh; docking.Changed -= Refresh; docking.Changed += Refresh;
            crew.Changed -= Refresh; crew.Changed += Refresh; flight.Inventory.Changed -= Refresh; flight.Inventory.Changed += Refresh;
            language.LanguageChanged -= Refresh; language.LanguageChanged += Refresh;
        }
        private void Unsubscribe()
        {
            if (flight == null) return;
            flight.Changed -= Refresh; docking.Changed -= Refresh; crew.Changed -= Refresh;
            flight.Inventory.Changed -= Refresh; language.LanguageChanged -= Refresh;
        }
        private void Refresh()
        {
            if (flight == null || oxygen == null) return;
            oxygen.Set(flight.Oxygen / flight.Config.OxygenCapacity * 100, flight.OxygenSupportSeconds);
            float powerSeconds = flight.Power / .12f;
            power.Set(flight.Power, powerSeconds); thermal.Set(flight.Power, powerSeconds);
            temperature.SetKey("life.cabin.temperature", CabinTemperature.ToString("0.0"));
            phase.SetKey("flight.phase." + flight.Phase);
            bool approaching = flight.Phase == AscentPhase.Rendezvous || flight.Phase == AscentPhase.Docking || flight.Phase == AscentPhase.Docked;
            startupRoot.SetActive(!approaching); dockingRoot.SetActive(approaching);
            var attitude = docking.Attitude.eulerAngles;
            telemetry.SetKey("life.dock.telemetry", docking.Distance.ToString("0.0"), docking.ClosingSpeed.ToString("+0.00;-0.00;0.00"), docking.LateralError.ToString("0.00"), docking.AlignmentError.ToString("0.0"));
            alignment.SetKey("life.dock.axes", docking.Position.x.ToString("+0.00;-0.00;0.00"), docking.Position.y.ToString("+0.00;-0.00;0.00"),
                Mathf.DeltaAngle(0, attitude.x).ToString("+0.0;-0.0;0.0"), Mathf.DeltaAngle(0, attitude.y).ToString("+0.0;-0.0;0.0"), flight.MainFuel.ToString("0"), docking.RcsFuel.ToString("0"));
            guidance.SetKey(approaching ? docking.State == DockingState.Capturing ? "life.dock.capturing" : docking.CanAssist ? "life.dock.capture_ready" : "life.dock.manual"
                : flight.Phase == AscentPhase.OrbitalInsertion ? "life.flight.circularize" : "life.flight.checklist");
            feedback.SetKey(flight.Operation != StartupOperation.None ? "flight.busy" : flight.FeedbackKey, Mathf.CeilToInt(flight.OperationRemaining));
            crewState.SetKey("life.cabin.crew", language.Format("crew.state." + crew.CommanderState), Mathf.CeilToInt(crew.CommanderHealth), ResourceReadout.Clock(flight.BaseRemaining));
            startup[0].interactable = flight.CanPowerOn; startup[1].interactable = flight.CanNavigate;
            startup[2].interactable = flight.CanPrepareEngine; startup[3].interactable = flight.CanIgnite;
            circularize.interactable = flight.CanCircularize;
            for (int i = 0; i < supplyButtons.Length; i++) supplyButtons[i].interactable = flight.CanUse(supplyKinds[i]);
            foreach (var button in thrustButtons)
            {
                var command = button.GetComponent<DockingThrustButton>().Command;
                button.interactable = docking.Active && (command != DockCommand.MainBoost || docking.CanBoost);
            }
            assist.interactable = docking.Active; assistance.SetKey(docking.AssistanceEnabled ? "dock.assist.on" : "dock.assist.off");
            commanderMedical.interactable = crew.CanUseLoadedMedicalOnCommander;
        }
    }
}
