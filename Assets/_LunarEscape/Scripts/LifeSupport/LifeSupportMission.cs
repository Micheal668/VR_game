using System;
using UnityEngine;

namespace LunarEscape
{
    // 地面生命保障只有 StationMission 一条时钟。UI、穿戴和补给都不能自行扣时间。
    public sealed class LifeSupportMission : MonoBehaviour
    {
        [SerializeField] private LifeSupportConfig config;
        [SerializeField] private StationMissionSession session;
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private Transform suitRack, doorControl;
        [SerializeField] private BoxCollider habitatVolume;
        [SerializeField] private RepairContact doorRepairContact;
        private System.Random random;
        private uint version;
        private bool donHeld;
        private Func<bool> donContact;
        public LifeSupportConfig Config => config;
        public StationMission Station => session != null ? session.Mission : null;
        public CargoInventory Inventory => inventory;
        public CharacterController Player => session != null ? session.Exit.PlayerBody : null;
        public Transform SuitRack => suitRack;
        public Transform DoorControl => doorControl;
        public RepairContact DoorRepairContact => doorRepairContact;
        public TimedRepairTask DoorRepairTask => doorRepairContact != null ? doorRepairContact.GetComponent<TimedRepairTask>() : null;
        public bool DoorRepaired => DoorRepairTask != null && DoorRepairTask.State == RepairState.Complete;
        public bool CanRepairDoor => IsGroundActive && !DoorOpen && !DoorRepaired && doorRepairContact != null && Near(doorRepairContact.RepairPoint, 1.6f);
        private bool WorkingOnDoor => CanRepairDoor && doorRepairContact.HasValidContact();
        public bool IsConfigured => config != null && session != null && inventory != null && habitatVolume != null;
        public bool SuitWorn { get; private set; }
        public bool DoorOpen { get; private set; }
        public bool IsDaylight { get; private set; }
        public float BaseOxygen { get; private set; }
        public float BasePower { get; private set; }
        public float SuitOxygen { get; private set; }
        public float SuitPower { get; private set; }
        public float BaseTemperature { get; private set; } = 22;
        public float BodyTemperature { get; private set; } = 37;
        public float DonProgressSeconds { get; private set; }
        public float HypoxiaSeconds { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int SuppliesUsed { get; private set; }
        public int AttemptNumber { get; private set; }
        public string FeedbackKey { get; private set; } = "life.feedback.ready";
        public float DonProgress => config != null ? DonProgressSeconds / config.DonSeconds : 0;
        public bool IsGroundActive => IsConfigured && isActiveAndEnabled && Station.Phase != StationMissionPhase.Briefing && !Station.IsTerminal;
        public bool HudPowered => SuitWorn && SuitPower > 0 && Station != null && !Station.IsTerminal;
        public float ExternalTemperature => config == null ? 22 : IsDaylight ? config.DaylightTemperature : config.NightTemperature;
        public bool IsInsideHabitat => IsConfigured && Player != null && habitatVolume.bounds.Contains(Player.transform.TransformPoint(Player.center));
        public bool HasBaseAir => !DoorOpen && BaseOxygen > 0 && IsInsideHabitat;
        public bool CanBreathe => SuitWorn ? SuitOxygen > 0 : HasBaseAir;
        public bool ThermalProtection => SuitWorn && SuitPower > 0 || !DoorOpen && BasePower > 0 && IsInsideHabitat;
        public float SuitOxygenRate => config == null ? 0 : config.SuitOxygenRate * (SuitPower > 0 ? 1 : config.UnpoweredOxygenMultiplier);
        public float BaseOxygenRate => config == null || DoorOpen ? 0 : config.BaseOxygenRate * (BasePower > 0 ? 1 : 1.6f);
        public float SuitOxygenSeconds => SuitOxygenRate > 0 ? SuitOxygen / SuitOxygenRate : 0;
        public float SuitPowerSeconds => config != null ? SuitPower / config.SuitPowerRate : 0;
        public float BaseOxygenSeconds => BaseOxygenRate > 0 ? BaseOxygen / BaseOxygenRate : 0;
        public float BasePowerSeconds => config != null ? BasePower / config.BasePowerRate : 0;
        public float SuffocationRemaining => config != null ? Mathf.Max(0, config.SuffocationSeconds - HypoxiaSeconds) : 0;
        public float ThermalMarginPercent => Mathf.Clamp01((IsDaylight ? 42 - BodyTemperature : BodyTemperature - 32) / 5) * 100;
        public bool CanDon => IsGroundActive && !SuitWorn && Near(suitRack, 1.7f);
        public bool CanOpenDoor => IsGroundActive && DoorRepaired && !DoorOpen && Near(doorControl, 2.1f);
        public event Action Changed;

        public void Configure(LifeSupportConfig settings, StationMissionSession source, CargoInventory cargo,
            Transform rack, Transform hatchControl, BoxCollider habitat)
        {
            if (settings == null || source == null || cargo == null || rack == null || hatchControl == null || habitat == null)
                throw new ArgumentNullException("生命保障必须连接同一场景、穿戴点和基地边界。");
            if (cargo.Mission != source.Mission) throw new ArgumentException("生命保障和物资必须属于同一任务。");
            config = settings; session = source; inventory = cargo; suitRack = rack; doorControl = hatchControl; habitatVolume = habitat;
            source.Mission.ConfigureLifeSupport(this); cargo.ConfigureLifeSupportUse(this); ResetForMission();
        }

        // 测试和可复现实验只控制随机种子；下一次重试仍从同一随机序列抽取新一轮数值。
        public void ConfigureDoorRepair(RepairContact contact)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));
            doorRepairContact = contact;
            // 接触组件仅检测工具；进度统一由基地任务时钟推进。
            contact.enabled = false;
            DoorRepairTask.Configure(config.DoorRepairSeconds);
            Changed?.Invoke();
        }
        public void SetRandomSeed(int seed) => random = new System.Random(seed);
        internal void ResetForMission()
        {
            if (!IsConfigured) return;
            ++version; ++AttemptNumber; random ??= new System.Random(Guid.NewGuid().GetHashCode());
            float Pick(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
            BaseOxygen = Pick(config.BaseOxygenRange); BasePower = Pick(config.BasePowerRange);
            SuitOxygen = Pick(config.SuitOxygenRange); SuitPower = Pick(config.SuitPowerRange);
            IsDaylight = random.Next(2) == 0;
            SuitWorn = DoorOpen = donHeld = false;
            DonProgressSeconds = HypoxiaSeconds = ElapsedSeconds = 0; SuppliesUsed = 0;
            BaseTemperature = 22; BodyTemperature = 37; FeedbackKey = "life.feedback.ready";
            if (doorRepairContact != null) { doorRepairContact.enabled = false; DoorRepairTask.Configure(config.DoorRepairSeconds); }
            Changed?.Invoke();
        }
        public void ConfigureDonContact(Func<bool> probe) => donContact = probe;
        public void SetDonHeld(bool held)
        {
            bool next = held && CanDon;
            if (next == donHeld) return;
            donHeld = next; Changed?.Invoke();
        }
        public bool TryOpenDoor()
        {
            if (!CanOpenDoor) return false;
            uint attempt = version;
            DoorOpen = true; BaseOxygen = 0; FeedbackKey = SuitWorn ? "life.feedback.vented" : "life.feedback.no_suit";
            Station.BeginLifeSupportEvacuation(this);
            if (attempt != version || !DoorOpen) return false;
            Changed?.Invoke(); return attempt == version && DoorOpen;
        }
        public void OpenDoor() => TryOpenDoor();

        // 基地阶段的大时间步在穿戴、耗尽、窒息和体温边界处分段，结果与逐帧执行一致。
        internal float LimitStep(float proposed)
        {
            if (!IsGroundActive || proposed <= 0) return proposed;
            float result = proposed;
            void Limit(float seconds) { if (seconds > .000001f) result = Mathf.Min(result, seconds); }
            if (donHeld && CanDon && (donContact == null || donContact())) Limit(config.DonSeconds - DonProgressSeconds);
            if (WorkingOnDoor) Limit(DoorRepairTask.RemainingSeconds);
            if (!DoorOpen && BaseOxygen > 0) Limit(BaseOxygen / BaseOxygenRate);
            if (BasePower > 0) Limit(BasePower / config.BasePowerRate);
            if (SuitWorn)
            {
                if (SuitOxygen > 0) Limit(SuitOxygenSeconds);
                if (SuitPower > 0) Limit(SuitPowerSeconds);
            }
            if (!CanBreathe) Limit(SuffocationRemaining);
            if (!ThermalProtection) Limit((IsDaylight ? 42 - BodyTemperature : BodyTemperature - 32) / config.ExposedBodyDegreesPerSecond);
            return result;
        }
        internal void Advance(float seconds)
        {
            if (!IsGroundActive || seconds < 0 || !float.IsFinite(seconds)) return;
            uint attempt = version;
            bool breathing = CanBreathe, protectedTemperature = ThermalProtection;
            bool dressing = donHeld && CanDon && (donContact == null || donContact());
            bool repairingDoor = WorkingOnDoor;
            if (!dressing) donHeld = false;
            float oxygenRate = SuitOxygenRate;
            BaseOxygen = Mathf.Max(0, BaseOxygen - BaseOxygenRate * seconds);
            BasePower = Mathf.Max(0, BasePower - config.BasePowerRate * seconds);
            if (SuitWorn)
            {
                SuitOxygen = Mathf.Max(0, SuitOxygen - oxygenRate * seconds);
                SuitPower = Mathf.Max(0, SuitPower - config.SuitPowerRate * seconds);
            }
            if (BaseOxygen < .00001f) BaseOxygen = 0;
            if (BasePower < .00001f) BasePower = 0;
            if (SuitOxygen < .00001f) SuitOxygen = 0;
            if (SuitPower < .00001f) SuitPower = 0;
            float target = !DoorOpen && BasePower > 0 ? 22 : ExternalTemperature;
            BaseTemperature = Mathf.MoveTowards(BaseTemperature, target, config.BaseDegreesPerSecond * seconds);
            HypoxiaSeconds = breathing ? 0 : Mathf.Min(config.SuffocationSeconds, HypoxiaSeconds + seconds);
            BodyTemperature = protectedTemperature ? Mathf.MoveTowards(BodyTemperature, 37, .4f * seconds)
                : BodyTemperature + (IsDaylight ? 1 : -1) * config.ExposedBodyDegreesPerSecond * seconds;
            ElapsedSeconds += seconds;
            // 截止时死亡优先，不能在窒息已完成的同一步穿戴或登舱复活。
            StationMissionFailure failure = HypoxiaSeconds >= config.SuffocationSeconds - .00001f ? StationMissionFailure.Suffocation
                : BodyTemperature <= 32.00001f ? StationMissionFailure.Hypothermia
                : BodyTemperature >= 41.99999f ? StationMissionFailure.Hyperthermia : StationMissionFailure.None;
            if (failure != StationMissionFailure.None)
            {
                donHeld = false; Station.FailLifeSupport(this, failure);
                if (attempt == version) Changed?.Invoke(); return;
            }
            if (dressing)
            {
                DonProgressSeconds = Mathf.Min(config.DonSeconds, DonProgressSeconds + seconds);
                if (DonProgressSeconds >= config.DonSeconds - .00001f)
                { SuitWorn = true; donHeld = false; DonProgressSeconds = config.DonSeconds; HypoxiaSeconds = 0; FeedbackKey = "life.feedback.suited"; }
            }
            if (attempt == version && DoorRepairTask != null) DoorRepairTask.Tick(repairingDoor, seconds);
            if (attempt == version) Changed?.Invoke();
        }
        public bool CanUseSupply(CargoItem item)
        {
            if (!IsGroundActive || !SuitWorn || item == null || item.Inventory != inventory) return false;
            bool carried = item.State == CargoState.Packed || item.State == CargoState.Held && item.Grab.isSelected;
            return carried && (item.Kind == CargoKind.Oxygen && SuitOxygen < 99.99f || item.Kind == CargoKind.Battery && SuitPower < 99.99f);
        }
        public bool CanUseSupply(CargoKind kind)
        {
            if (inventory == null) return false;
            foreach (var item in inventory.Items) if (item != null && item.Kind == kind && CanUseSupply(item)) return true;
            return false;
        }
        public bool TryUseSupply(CargoItem item)
        {
            if (!CanUseSupply(item)) return false;
            uint attempt = version; CargoKind kind = item.Kind;
            if (!inventory.TryConsumeForSuit(item, this) || attempt != version || !IsGroundActive) return false;
            if (kind == CargoKind.Oxygen) { SuitOxygen = Mathf.Min(100, SuitOxygen + config.OxygenRefillPercent); HypoxiaSeconds = 0; }
            else SuitPower = Mathf.Min(100, SuitPower + config.BatteryRefillPercent);
            ++SuppliesUsed; FeedbackKey = kind == CargoKind.Oxygen ? "life.feedback.oxygen" : "life.feedback.battery";
            Changed?.Invoke(); return true;
        }
        public bool TryUseSupply(CargoKind kind)
        {
            if (inventory == null) return false;
            foreach (var item in inventory.Items) if (item != null && item.Kind == kind && TryUseSupply(item)) return true;
            return false;
        }
        public void UseOxygen() => TryUseSupply(CargoKind.Oxygen);
        public void UseBattery() => TryUseSupply(CargoKind.Battery);
        private bool Near(Transform target, float distance)
        {
            if (target == null || Player == null || !Player.enabled || !target.gameObject.activeInHierarchy) return false;
            var center = Player.transform.TransformPoint(Player.center);
            return Mathf.Abs(center.y - target.position.y) < 1.7f &&
                Vector2.Distance(new Vector2(center.x, center.z), new Vector2(target.position.x, target.position.z)) <= distance;
        }
    }
}
