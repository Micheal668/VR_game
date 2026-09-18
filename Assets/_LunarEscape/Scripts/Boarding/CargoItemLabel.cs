using UnityEngine;

namespace LunarEscape
{
    public sealed class CargoItemLabel : MonoBehaviour
    {
        [SerializeField] private LocalizationService localization;
        [SerializeField] private LocalizedText label;
        [SerializeField] private CargoKind kind;
        [SerializeField] private CargoConfig config;
        public void Configure(LocalizationService service, LocalizedText text, CargoKind itemKind, CargoConfig settings)
        {
            if (localization != null) localization.LanguageChanged -= Refresh;
            localization = service; label = text; kind = itemKind; config = settings;
            if (isActiveAndEnabled) localization.LanguageChanged += Refresh;
            Refresh();
        }
        private void OnEnable() { if (localization != null) { localization.LanguageChanged += Refresh; Refresh(); } }
        private void OnDisable() { if (localization != null) localization.LanguageChanged -= Refresh; }
        private void Refresh()
        {
            if (label != null && config != null)
                label.SetKey("cargo.item", localization.Format("cargo.kind." + kind), config.GetWeightKg(kind), config.GetMaxQuantity(kind));
        }
    }
}
