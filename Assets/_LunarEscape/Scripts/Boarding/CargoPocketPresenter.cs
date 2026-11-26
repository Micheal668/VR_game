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
        private CargoRetrievalSocket output;
        private void Start()
        {
            var outlet = new GameObject("Pocket pickup");
            outlet.transform.SetParent(transform.parent, false);
            outlet.transform.localPosition = new Vector3(.18f, .40f, -.22f);
            output = outlet.AddComponent<CargoRetrievalSocket>();
            output.attachTransform = outlet.transform;
            output.showInteractableHoverMeshes = false;
            output.keepSelectedTargetValid = true;
            output.selectExited.AddListener(_ => Refresh());
            foreach (var button in buttons) button.GetComponentInChildren<LocalizedText>(true).SetKey("prep.pocket.take");
            Refresh();
        }
        public void Configure(CargoInventory cargo, LocalizationService service, LocalizedText header,
            LocalizedText[] labels, Button[] discard)
        {
            Unsubscribe(); inventory = cargo; localization = service; capacity = header; rows = labels; buttons = discard;
            if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        public void DiscardSlot(int index)
        {
            if (index >= 0 && index < kinds.Length) inventory.RetrievePacked(kinds[index], output);
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
            kinds = inventory.Items.Where(i => i.State == CargoState.Packed).Select(i => i.Kind).Distinct().OrderBy(k => k).ToArray();
            capacity.SetKey("cargo.capacity", inventory.TypeCount, inventory.Config.MaxTypes, inventory.TotalWeightKg, inventory.Config.MaxWeightKg);
            for (int i = 0; i < rows.Length; i++)
            {
                bool hasItem = i < kinds.Length;
                rows[i].gameObject.SetActive(hasItem);
                buttons[i].gameObject.SetActive(hasItem);
                if (!hasItem) continue;
                var kind = kinds[i];
                rows[i].SetKey("cargo.row", localization.Format("cargo.kind." + kind), inventory.Items.Count(item => item.Kind == kind && item.State == CargoState.Packed));
                buttons[i].interactable = output != null && !output.hasSelection && (inventory.Mission.Phase == StationMissionPhase.Evacuation ||
                    inventory.Mission.LifeSupport != null && inventory.Mission.LifeSupport.IsGroundActive);
            }
        }
    }
}
