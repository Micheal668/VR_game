using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 两处清单和丢弃按钮读取同一个背包，显示位置不会改变携带额度。
    public sealed class CargoPresenter : MonoBehaviour
    {
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private StationMission mission;
        [SerializeField] private LocalizationService localization;
        [SerializeField] private LocalizedText capacity;
        [SerializeField] private LocalizedText status;
        [SerializeField] private LocalizedText[] rows;
        [SerializeField] private Button[] discardButtons;

        public void Configure(CargoInventory cargo, StationMission task, LocalizationService service,
            LocalizedText summary, LocalizedText feedback, LocalizedText[] entries, Button[] discard)
        {
            Unsubscribe();
            inventory = cargo; mission = task; localization = service;
            capacity = summary; status = feedback; rows = entries; discardButtons = discard;
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }

        public void DiscardIndex(int index)
        {
            if (index < 0 || index >= 6 || inventory == null) return;
            inventory.Discard((CargoKind)index);
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() => Unsubscribe();
        private void Subscribe()
        {
            if (inventory != null) inventory.Changed += Refresh;
            if (mission != null) mission.PhaseChanged += OnPhase;
            if (localization != null) localization.LanguageChanged += Refresh;
        }
        private void Unsubscribe()
        {
            if (inventory != null) inventory.Changed -= Refresh;
            if (mission != null) mission.PhaseChanged -= OnPhase;
            if (localization != null) localization.LanguageChanged -= Refresh;
        }
        private void OnPhase(StationMissionPhase _) => Refresh();
        private void Refresh()
        {
            if (inventory == null || inventory.Config == null || localization == null || rows == null) return;
            capacity.SetKey("cargo.capacity", inventory.TypeCount, inventory.Config.MaxTypes,
                inventory.TotalWeightKg, inventory.Config.MaxWeightKg);
            for (int i = 0; i < rows.Length; i++)
            {
                var kind = (CargoKind)i;
                rows[i].SetKey("cargo.row", localization.Format("cargo.kind." + kind), inventory.GetCount(kind));
                discardButtons[i].interactable = mission.Phase == StationMissionPhase.Evacuation
                    && inventory.Items.Any(item => item.Kind == kind
                        && (item.State == CargoState.Packed || item.State == CargoState.Loaded));
            }
            string key;
            if (mission.Phase == StationMissionPhase.Completed) key = "cargo.done";
            else if (mission.Phase != StationMissionPhase.Evacuation) key = "cargo.status.ready";
            else key = inventory.LastRejection switch
            {
                CargoRejection.TypeLimit => "cargo.reject.types",
                CargoRejection.WeightLimit => "cargo.reject.weight",
                CargoRejection.QuantityLimit => "cargo.reject.quantity",
                CargoRejection.WrongPhase => "cargo.reject.phase",
                _ => "cargo.status.ok"
            };
            status.SetKey(key);
        }
    }
}
