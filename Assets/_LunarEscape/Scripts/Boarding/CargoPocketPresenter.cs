using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 路上低头也能丢弃已收纳物资，不必折返资源架。
    public sealed class CargoPocketPresenter : MonoBehaviour
    {
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private LocalizationService localization;
        [SerializeField] private LocalizedText capacity;
        [SerializeField] private LocalizedText[] rows;
        [SerializeField] private Button[] buttons;
        private CargoKind[] kinds = System.Array.Empty<CargoKind>();
        public void Configure(CargoInventory cargo, LocalizationService service, LocalizedText header,
            LocalizedText[] labels, Button[] discard)
        {
            Unsubscribe(); inventory = cargo; localization = service; capacity = header; rows = labels; buttons = discard;
            if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        public void DiscardSlot(int index)
        {
            if (index >= 0 && index < kinds.Length) inventory.Discard(kinds[index]);
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() => Unsubscribe();
        private void Subscribe()
        {
            if (inventory != null) { inventory.Changed += Refresh; inventory.Mission.PhaseChanged += OnPhase; }
            if (localization != null) localization.LanguageChanged += Refresh;
        }
        private void Unsubscribe()
        {
            if (inventory != null) { inventory.Changed -= Refresh; inventory.Mission.PhaseChanged -= OnPhase; }
            if (localization != null) localization.LanguageChanged -= Refresh;
        }
        private void OnPhase(StationMissionPhase _) => Refresh();
        private void Refresh()
        {
            if (inventory == null || rows == null) return;
            kinds = inventory.Items.Where(i => i.IsCarried).Select(i => i.Kind).Distinct().OrderBy(k => k).ToArray();
            capacity.SetKey("cargo.capacity", inventory.TypeCount, inventory.Config.MaxTypes, inventory.TotalWeightKg, inventory.Config.MaxWeightKg);
            for (int i = 0; i < rows.Length; i++)
            {
                bool hasItem = i < kinds.Length;
                rows[i].gameObject.SetActive(hasItem);
                buttons[i].gameObject.SetActive(hasItem);
                if (!hasItem) continue;
                var kind = kinds[i];
                rows[i].SetKey("cargo.row", localization.Format("cargo.kind." + kind), inventory.GetCount(kind));
                buttons[i].interactable = inventory.Mission.Phase == StationMissionPhase.Evacuation
                    && inventory.Items.Any(item => item.Kind == kind && (item.State == CargoState.Packed || item.State == CargoState.Loaded));
            }
        }
    }
}
