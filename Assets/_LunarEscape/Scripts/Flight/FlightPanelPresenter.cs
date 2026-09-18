using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    public sealed class FlightPanelPresenter : MonoBehaviour
    {
        [SerializeField] private AscentMission mission;
        [SerializeField] private LocalizationService localization;
        [SerializeField] private LocalizedText phase, vitals, danger, condition, feedback, result;
        [SerializeField] private Button[] startup, supplies;
        [SerializeField] private LocalizedText[] supplyLabels;
        [SerializeField] private Button retry;
        private static readonly CargoKind[] Kinds = { CargoKind.Oxygen, CargoKind.RepairKit, CargoKind.Battery, CargoKind.MedicalKit };
        public void Configure(AscentMission task, LocalizationService language, LocalizedText[] labels,
            Button[] startButtons, Button[] useButtons, LocalizedText[] useLabels, Button restart)
        {
            mission = task; localization = language;
            phase = labels[0]; vitals = labels[1]; danger = labels[2]; condition = labels[3]; feedback = labels[4]; result = labels[5];
            startup = startButtons; supplies = useButtons; supplyLabels = useLabels; retry = restart;
        }
        private void OnEnable()
        {
            if (mission == null) return;
            mission.Changed += Refresh; localization.LanguageChanged += Refresh; Refresh();
        }
        private void OnDisable()
        {
            if (mission == null) return;
            mission.Changed -= Refresh; localization.LanguageChanged -= Refresh;
        }
        public void Refresh()
        {
            if (mission == null) return;
            phase.SetKey("flight.phase." + mission.Phase);
            if(mission.Config.OrbitalFlight && mission.Phase==AscentPhase.Ascent)phase.SetKey("orbit.phase.ascent");
            vitals.SetKey("flight.vitals", Mathf.CeilToInt(mission.OxygenSupportSeconds), Mathf.CeilToInt(mission.Health), Mathf.CeilToInt(mission.Power));
            danger.SetKey(mission.BaseExploded ? "flight.afterblast" : "flight.countdown",
                Mathf.CeilToInt(mission.BaseExploded ? mission.RecoveryRemaining : mission.BaseRemaining));
            if(mission.Config.OrbitalFlight && mission.BaseExploded && mission.Phase!=AscentPhase.Recovery)
                danger.SetKey("orbit.baseclear");
            condition.SetKey("flight.condition", mission.LeakPerSecond.ToString("0.0"), mission.InjuryPerSecond.ToString("0.0"));
            feedback.SetKey(mission.Operation != StartupOperation.None ? "flight.busy" : mission.FeedbackKey,
                Mathf.CeilToInt(mission.OperationRemaining));
            startup[0].interactable = mission.CanPowerOn; startup[1].interactable = mission.CanNavigate;
            startup[2].interactable = mission.CanPrepareEngine; startup[3].interactable = mission.CanIgnite;
            for (int i = 0; i < Kinds.Length; i++)
            {
                supplies[i].interactable = mission.CanUse(Kinds[i]);
                supplyLabels[i].SetKey("flight.use." + Kinds[i], mission.Inventory.GetCount(Kinds[i]));
            }
            bool completed = mission.Phase == AscentPhase.Completed || mission.Phase==AscentPhase.Orbit || mission.Phase==AscentPhase.Docked;
            result.gameObject.SetActive(completed); retry.gameObject.SetActive(completed);
            if (completed) result.SetKey(mission.Phase==AscentPhase.Orbit ? "orbit.result" : "flight.result", mission.SuppliesUsed,
                mission.Inventory.GetCount(CargoKind.DataCore), mission.Inventory.GetCount(CargoKind.LunarSample));
        }
    }
}
