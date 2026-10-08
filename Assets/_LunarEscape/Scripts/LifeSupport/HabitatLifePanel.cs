using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 墙面设备显示环境实况，穿戴点和气闸按钮的可用性还会按玩家真实距离复核。
    public sealed class HabitatLifePanel : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private LocalizationService language;
        [SerializeField] private ResourceReadout oxygen, power;
        [SerializeField] private LocalizedText temperature, condition, suitState, suitHint, hatchState;
        [SerializeField] private Button don, hatch;
        [SerializeField] private Image donProgress;
        public ResourceReadout Oxygen => oxygen;
        public ResourceReadout Power => power;
        public Button DonButton => don;
        public Button HatchButton => hatch;
        public void Configure(LifeSupportMission source, LocalizationService localization, ResourceReadout o2, ResourceReadout battery,
            LocalizedText thermal, LocalizedText state, LocalizedText suit, LocalizedText help, LocalizedText door,
            Button wear, Button open, Image progress)
        {
            Unsubscribe(); life = source; language = localization; oxygen = o2; power = battery; temperature = thermal;
            condition = state; suitState = suit; suitHint = help; hatchState = door; don = wear; hatch = open; donProgress = progress;
            if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() => Unsubscribe();
        private void Subscribe()
        { if (life != null) { life.Changed -= Refresh; life.Changed += Refresh; life.Station.Changed -= Refresh; life.Station.Changed += Refresh; } if (language != null) { language.LanguageChanged -= Refresh; language.LanguageChanged += Refresh; } }
        private void Unsubscribe()
        { if (life != null) { life.Changed -= Refresh; life.Station.Changed -= Refresh; } if (language != null) language.LanguageChanged -= Refresh; }
        private void Update()
        { if (life != null) { don.interactable = life.CanDon; hatch.interactable = life.CanOpenDoor; } }
        private void Refresh()
        {
            if (life == null || oxygen == null) return;
            oxygen.Set(life.BaseOxygen, life.BaseOxygenSeconds); power.Set(life.BasePower, life.BasePowerSeconds);
            temperature.SetKey("life.base.temperature", life.BaseTemperature.ToString("0.0"), language.Format(life.IsDaylight ? "life.day" : "life.night"), life.ExternalTemperature.ToString("0"));
            condition.SetKey(life.DoorOpen ? "life.base.vented" : life.BaseOxygen <= 0 ? "life.base.no_air" : life.BasePower <= 0 ? "life.base.no_power" : "life.base.sealed");
            suitState.SetKey(life.SuitWorn ? "life.suit.ready" : "life.suit.progress", Mathf.FloorToInt(life.DonProgress * 100));
            suitHint.SetKey(life.SuitWorn ? "life.suit.get_supplies" : "life.suit.instructions", life.Config.DonSeconds.ToString("0"));
            hatchState.SetKey(life.DoorOpen ? "life.hatch.open" : !life.DoorRepaired ? "life.hatch.repair_required" : life.SuitWorn ? "life.hatch.suited" : "life.hatch.warning",
                Mathf.FloorToInt((life.DoorRepairTask != null ? life.DoorRepairTask.Progress : 0) * 100));
            hatch.GetComponentInChildren<LocalizedText>().SetKey(life.DoorRepaired ? "life.hatch.release" : "life.hatch.locked");
            donProgress.fillAmount = life.DonProgress; Update();
        }
    }
}
