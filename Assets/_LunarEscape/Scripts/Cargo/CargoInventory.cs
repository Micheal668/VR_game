using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace LunarEscape
{
    // 手持、腰包和船舱共享同一份额度。物资换位置不会腾出第二份容量。
    public sealed class CargoInventory : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private CargoConfig config;
        [SerializeField] private CargoItem[] items = Array.Empty<CargoItem>();
        [SerializeField] private Transform dropPoint;
        [SerializeField] private CargoPackZone packZone;
        [SerializeField] private EvacuationZone shipZone;
        [SerializeField] private AscentMission flightUse;
        private ReadOnlyCollection<CargoItem> readOnlyItems;
        private readonly Dictionary<CargoItem, float> releasedNearPack = new();
        private readonly List<CargoItem> pendingPackChecks = new();

        public StationMission Mission => mission;
        public CargoConfig Config => config;
        public IReadOnlyList<CargoItem> Items => readOnlyItems ??= Array.AsReadOnly(items);
        public CargoRejection LastRejection { get; private set; }
        public int TotalCount => CountState(null);
        public int HeldCount => CountState(CargoState.Held);
        public int PackedCount => CountState(CargoState.Packed);
        public int LoadedCount => CountState(CargoState.Loaded);
        public int TypeCount
        {
            get
            {
                int count = 0;
                foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
                    if (GetCount(kind) > 0) count++;
                return count;
            }
        }
        public float TotalWeightKg => (float)TotalWeight();
        public event Action Changed;

        private void Awake()
        {
            if (mission != null && config != null) BindItems();
        }

        private void OnEnable()
        {
            if (mission == null) return;
            mission.PhaseChanged += OnPhaseChanged;
            if (mission.Phase == StationMissionPhase.Briefing && config != null) ResetInventory();
        }

        private void OnDisable()
        {
            if (mission != null) mission.PhaseChanged -= OnPhaseChanged;
            // 卸载场景时不复位或重新启用任何 XR 物体。
        }

        public void Configure(StationMission task, CargoConfig settings, CargoItem[] candidates, Transform discardPoint)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (discardPoint == null) throw new ArgumentNullException(nameof(discardPoint));
            if (task.Phase != StationMissionPhase.Briefing)
                throw new InvalidOperationException("只能在任务开始前配置物资清单。");
            var unique = new HashSet<CargoItem>();
            foreach (CargoItem item in candidates)
                if (item == null || !unique.Add(item) || (item.Inventory != null && item.Inventory != this))
                    throw new ArgumentException("物资必须非空、不重复，且只属于一份清单。", nameof(candidates));

            if (isActiveAndEnabled && mission != null) mission.PhaseChanged -= OnPhaseChanged;
            mission = task;
            config = settings;
            items = (CargoItem[])candidates.Clone();
            readOnlyItems = Array.AsReadOnly(items);
            dropPoint = discardPoint;
            BindItems();
            if (isActiveAndEnabled) mission.PhaseChanged += OnPhaseChanged;
            ResetInventory();
        }

        public void ConfigureShipZone(EvacuationZone zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            shipZone = zone;
        }

        public void RegisterPackZone(CargoPackZone zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            packZone = zone;
        }

        public int GetCount(CargoKind kind)
        {
            int count = 0;
            foreach (CargoItem item in items)
                if (item != null && item.Kind == kind && item.IsCarried) count++;
            return count;
        }

        public void ConfigureFlightUse(AscentMission flight)
        {
            if (flight == null || flight.Inventory != this) throw new ArgumentException("飞行必须使用同一份物资。");
            flightUse = flight;
        }
        internal bool TryConsumeLoaded(CargoKind kind, AscentMission requester)
        {
            if (requester == null || requester != flightUse || !requester.CanUseSupplies
                || mission.Phase != StationMissionPhase.Completed) return false;
            foreach (var item in items)
            {
                if (item == null || item.Kind != kind || item.State != CargoState.Loaded) continue;
                item.SetState(CargoState.Consumed);
                item.HideForStorage();
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public bool CanAcquire(CargoItem item, out CargoRejection rejection)
        {
            rejection = CargoRejection.None;
            if (!CanChange()) rejection = CargoRejection.WrongPhase;
            else if (!Owns(item) || (item.State != CargoState.World && item.State != CargoState.Held))
                rejection = CargoRejection.InvalidItem;
            // 另一只手接住同一件物品，或 XRI 每帧复查已有抓取，都不能重复占额度。
            else if (item.State == CargoState.Held) return true;
            else if (GetCount(item.Kind) >= config.GetMaxQuantity(item.Kind)) rejection = CargoRejection.QuantityLimit;
            else if (GetCount(item.Kind) == 0 && TypeCount >= config.MaxTypes) rejection = CargoRejection.TypeLimit;
            else if (TotalWeight() + config.GetWeightKg(item.Kind) > config.MaxWeightKg)
                rejection = CargoRejection.WeightLimit;
            return rejection == CargoRejection.None;
        }

        public bool TryHold(CargoItem item)
        {
            if (!CanAcquire(item, out CargoRejection reason)) return Reject(reason);
            releasedNearPack.Remove(item);
            if (item.State == CargoState.Held) return Accept(false);
            item.SetState(CargoState.Held);
            return Accept();
        }

        public bool DropHeld(CargoItem item)
        {
            if (!CanChange()) return Reject(CargoRejection.WrongPhase);
            if (!Owns(item)) return Reject(CargoRejection.InvalidItem);
            if (item.State != CargoState.Held) return Reject(CargoRejection.NotHeld);
            item.SetState(CargoState.World);
            // 正常松手不取消官方投掷；直接调用此动作时才取消尚存的抓取。
            if (item.Grab.isSelected) item.CancelSelection();
            return Accept();
        }

        public bool TryPack(CargoItem item)
        {
            if (!CanChange()) return Reject(CargoRejection.WrongPhase);
            if (!Owns(item)) return Reject(CargoRejection.InvalidItem);
            if (item.State != CargoState.Held) return Reject(CargoRejection.NotHeld);
            if (packZone == null || !packZone.Contains(item)) return Reject(CargoRejection.InvalidItem);
            item.SetState(CargoState.Packed);
            item.HideForStorage();
            return Accept();
        }

        internal void Released(CargoItem item, bool canceled)
        {
            if (!CanChange() || !Owns(item) || item.State != CargoState.Held) return;
            // 因停用、拒绝或场景切换导致的取消不能被误判为主动收进腰包。
            if (!canceled && packZone != null && packZone.Contains(item)) TryPack(item);
            else
            {
                DropHeld(item);
                // 松手时还在框口上方的物品，允许自然落进收纳区。
                // 只跟踪刚主动释放且靠近框口的物品，不吸走远处或从未拿过的物资。
                if (!canceled && packZone != null && packZone.IsNearOpening(item))
                    releasedNearPack[item] = Time.time + 2f;
            }
        }

        private void FixedUpdate()
        {
            if (!CanChange()) { releasedNearPack.Clear(); return; }
            pendingPackChecks.Clear(); pendingPackChecks.AddRange(releasedNearPack.Keys);
            foreach (var item in pendingPackChecks)
            {
                if (item == null || item.State != CargoState.World || item.Grab.isSelected
                    || !item.isActiveAndEnabled || Time.time > releasedNearPack[item])
                { releasedNearPack.Remove(item); continue; }
                if (packZone == null || !packZone.Contains(item)) continue;
                releasedNearPack.Remove(item);
                // 下落期间玩家可能已拿起另一类物资，入包时仍重新验证同一份额度。
                if (!CanAcquire(item, out var reason)) { ReportRejection(reason); continue; }
                item.SetState(CargoState.Packed); item.HideForStorage(); Accept();
            }
        }

        public bool LoadPackedIntoShip() => LoadIntoShip(false);
        public bool LoadAllCarriedIntoShip() => LoadIntoShip(true);

        private bool LoadIntoShip(bool includeHeld)
        {
            if (!CanChange()) return Reject(CargoRejection.WrongPhase);
            if (shipZone == null || !shipZone.ContainsPlayer) return Reject(CargoRejection.NotAtShip);
            var loading = new List<CargoItem>();
            foreach (CargoItem item in items)
                if (item != null && (item.State == CargoState.Packed || (includeHeld && item.State == CargoState.Held)))
                    loading.Add(item);
            // 先更新全部状态，再取消抓取/隐藏物体，避免 XR 回调把已装船物资当成丢弃。
            foreach (CargoItem item in loading) item.SetState(CargoState.Loaded);
            foreach (CargoItem item in loading) item.HideForStorage();
            // 空手撤离合法，零件物资也能完成这一步。
            return Accept();
        }

        public bool Discard(CargoKind kind)
        {
            if (!CanChange()) return Reject(CargoRejection.WrongPhase);
            CargoItem discarded = null;
            foreach (CargoItem item in items)
                if (item != null && item.Kind == kind && (item.State == CargoState.Packed || item.State == CargoState.Loaded))
                {
                    discarded = item;
                    if (item.State == CargoState.Packed) break;
                }
            if (discarded == null) return Reject(CargoRejection.NotStored);
            if (dropPoint == null) throw new InvalidOperationException("丢弃物资需要玩家附近的实体落点。");
            discarded.SetState(CargoState.World);
            discarded.RestoreAt(dropPoint.position, dropPoint.rotation);
            return Accept();
        }

        public void ResetInventory()
        {
            if (mission == null || mission.Phase != StationMissionPhase.Briefing)
            {
                Reject(CargoRejection.WrongPhase);
                return;
            }
            releasedNearPack.Clear();
            foreach (CargoItem item in items)
                if (item != null) item.SetState(CargoState.World);
            foreach (CargoItem item in items)
                if (item != null) item.ResetToStart();
            Accept();
        }

        private void OnPhaseChanged(StationMissionPhase phase)
        {
            if (phase == StationMissionPhase.Briefing) ResetInventory();
            else Changed?.Invoke();
        }

        internal void ReportRejection(CargoRejection reason) => Reject(reason);

        private bool Reject(CargoRejection reason)
        {
            if (LastRejection != reason)
            {
                LastRejection = reason;
                Changed?.Invoke();
            }
            return false;
        }

        private bool Accept(bool changed = true)
        {
            bool hadRejection = LastRejection != CargoRejection.None;
            LastRejection = CargoRejection.None;
            if (changed || hadRejection) Changed?.Invoke();
            return true;
        }

        private bool CanChange() => mission != null && config != null && mission.Phase == StationMissionPhase.Evacuation;
        private bool Owns(CargoItem item) => item != null && item.Inventory == this && Array.IndexOf(items, item) >= 0;

        private int CountState(CargoState? state)
        {
            int count = 0;
            foreach (CargoItem item in items)
                if (item != null && (state.HasValue ? item.State == state.Value : item.IsCarried)) count++;
            return count;
        }

        private double TotalWeight()
        {
            if (config == null) return 0d;
            double total = 0d;
            foreach (CargoItem item in items)
                if (item != null && item.IsCarried) total += config.GetWeightKg(item.Kind);
            return total;
        }

        private void BindItems()
        {
            foreach (CargoItem item in items)
            {
                if (item == null) throw new InvalidOperationException("物资清单存在未连接的物品。");
                item.Configure(this, item.Kind);
            }
        }
    }
}
