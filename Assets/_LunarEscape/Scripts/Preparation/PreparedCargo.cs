using TMPro;
using UnityEngine;

namespace LunarEscape
{
    // 同一件物资跨两天保留身份、充装量与研究来源；旧教学场景的物资不受影响。
    [RequireComponent(typeof(CargoItem))]
    public sealed class PreparedCargo : MonoBehaviour
    {
        [SerializeField] private string persistentId;
        [SerializeField] private TMP_Text readout;
        private CargoItem item;
        private LocalizationService language;
        public string Id => persistentId;
        public CargoItem Item => item != null ? item : item = GetComponent<CargoItem>();
        public float Fraction { get; private set; }
        public bool PreparedOnDayOne { get; private set; }
        public bool ResearchReady { get; private set; }
        public bool NeedsCharging => Item.Kind == CargoKind.Oxygen || Item.Kind == CargoKind.Battery;
        public bool IsResearch => Item.Kind == CargoKind.DataCore || Item.Kind == CargoKind.LunarSample;
        public bool CanTake => !IsResearch || ResearchReady;
        public float UsableFraction => NeedsCharging ? (PreparedOnDayOne ? Fraction : 0) : CanTake ? 1 : 0;

        public void Configure(string id, TMP_Text label) { persistentId = id; readout = label; }
        private void Awake() => language = FindAnyObjectByType<LocalizationService>();
        internal void Restore(float fraction, bool prepared, bool researched)
        { Fraction = Mathf.Clamp01(fraction); PreparedOnDayOne = prepared; ResearchReady = researched; Refresh(); }
        internal void Fill(float amount, bool firstDay)
        {
            if (!NeedsCharging || amount <= 0 || !float.IsFinite(amount) || (!firstDay && !PreparedOnDayOne)) return;
            Fraction = Mathf.Clamp01(Fraction + amount);
            if (firstDay && Fraction > 0) PreparedOnDayOne = true;
            Refresh();
        }
        internal void CompleteResearch() { ResearchReady = true; gameObject.SetActive(true); Refresh(); }
        private void LateUpdate() => Refresh();
        private void Refresh()
        {
            if (readout == null) return;
            string name = language != null ? language.Format("cargo.kind." + Item.Kind) : Item.Kind.ToString();
            string state = NeedsCharging ? Mathf.RoundToInt(Fraction * 100) + "%"
                : language != null ? language.Format(ResearchReady ? "prep.recovered" : "prep.stored") : "";
            readout.text = NeedsCharging ? (Item.Kind == CargoKind.Oxygen ? "O2  " : "") + state : name + "\n" + state;
            readout.color = NeedsCharging && Fraction <= 0 ? new Color(1, .65f, .22f) : new Color(.55f, 1, .82f);
        }
    }
}
