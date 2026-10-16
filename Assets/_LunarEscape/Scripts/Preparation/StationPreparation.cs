using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 场景 11/12 共用工作站和物资清单。睡觉只传递一次快照，第二天重试回到该快照。
    [DefaultExecutionOrder(100)]
    public sealed class StationPreparation : MonoBehaviour
    {
        [SerializeField] private bool firstDay;
        [SerializeField] private DayOneMission day;
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private HabitatBreaker breaker;
        [SerializeField] private StationRandomSupplies.Group[] groups = Array.Empty<StationRandomSupplies.Group>();
        [SerializeField] private PreparedCargo[] cargo = Array.Empty<PreparedCargo>();
        [SerializeField] private SupplyChargingDock[] docks = Array.Empty<SupplyChargingDock>();
        [SerializeField] private LunarResearchStation research;
        [SerializeField] private DustyPanel[] panels = Array.Empty<DustyPanel>();
        [SerializeField] private DrillSite[] sites = Array.Empty<DrillSite>();
        [SerializeField] private NitrogenBlower blower;
        [SerializeField] private CoreDrill drill;
        [SerializeField] private Transform[] looseTools = Array.Empty<Transform>();
        private Snapshot checkpoint;
        private bool initialized;
        private static Snapshot pending;
        private float boardTime;

        [Serializable] private sealed class CargoSave
        {
            public string id, dock;
            public Vector3 position;
            public Quaternion rotation;
            public bool active, prepared, researched;
            public float fraction;
        }
        [Serializable] private sealed class CoreSave
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool active;
            public int slot;
            public float progress;
        }
        [Serializable] private sealed class PropSave { public Vector3 position; public Quaternion rotation; }
        [Serializable] private sealed class Snapshot
        {
            public CargoSave[] cargo;
            public CoreSave[] cores;
            public PropSave[] tools;
            public bool[] cleanPanels;
            public float suitOxygen = 100, suitPower = 100;
        }

        public bool IsFirstDay => firstDay;
        public bool Initialized => initialized;
        public PreparedCargo[] Cargo => cargo;
        public SupplyChargingDock[] Docks => docks;
        public LunarResearchStation Research => research;
        public DustyPanel[] Panels => panels;
        public DrillSite[] Sites => sites;
        public NitrogenBlower Blower => blower;
        public CoreDrill Drill => drill;
        public bool CanWork => initialized && (firstDay ? day != null && (day.Current == DayOneMission.Stage.Working || day.Current == DayOneMission.Stage.Evening)
            : life != null && life.IsGroundActive);
        public bool HasPower => firstDay || life != null && breaker != null && breaker.IsOn && life.BasePower > 0;
        public bool OxygenMachineWorks => firstDay || life != null && life.Station.RepairRestored;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCarriedState() => pending = null;

        public void Configure(LifeSupportMission mission, HabitatBreaker power, StationRandomSupplies.Group[] supplyGroups,
            PreparedCargo[] items, SupplyChargingDock[] ports, LunarResearchStation laboratory, DustyPanel[] solar,
            DrillSite[] drillSites, NitrogenBlower gas, CoreDrill coreDrill, Transform[] tools)
        {
            life = mission; breaker = power; groups = supplyGroups; cargo = items; docks = ports; research = laboratory;
            panels = solar; sites = drillSites; blower = gas; drill = coreDrill; looseTools = tools; firstDay = false;
        }
        public void ConfigureDayOne(DayOneMission mission)
        {
            day = mission; firstDay = true; life = null; breaker = null;
            foreach (var entry in cargo) entry.Item.ConfigurePreparation();
        }
        private void OnEnable()
        {
            if (!firstDay && life != null) life.Station.PhaseChanged += OnPhaseChanged;
        }
        private void OnDisable()
        {
            if (!firstDay && life != null) life.Station.PhaseChanged -= OnPhaseChanged;
        }
        private void Start()
        {
            if (firstDay)
            {
                pending = null;
                InitializeFresh();
            }
            else
            {
                if (pending != null) { checkpoint = pending; pending = null; }
                else InitializeFresh();
                Restore(checkpoint);
                // 两天保留同一套工具与取样点，逃生当天不再生成新的样品。
                foreach (var site in sites) site.enabled = false;
                if (blower != null) blower.enabled = false;
                if (drill != null) drill.enabled = false;
            }
            initialized = true;
        }
        private void InitializeFresh()
        {
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            foreach (var item in cargo) item.Restore(0, false, false);
            foreach (var group in groups)
            {
                int count = random.Next(1, Mathf.Min(3, group.items.Length) + 1);
                for (int i = 0; i < group.items.Length; i++)
                {
                    var item = group.items[i];
                    bool science = item.Kind == CargoKind.DataCore || item.Kind == CargoKind.LunarSample;
                    item.gameObject.SetActive(!science && i < count);
                }
            }
            for (int i = 0; i < research.Cores.Length; i++)
            { research.Cores[i].gameObject.SetActive(false); research.RestoreProgress(i, 0); }
            checkpoint = Capture();
        }
        private void Update()
        {
            if (!firstDay || day == null) return;
            boardTime += Time.deltaTime;
            if (boardTime < .25f) return;
            boardTime = 0; day.Refresh();
        }
        public bool CanCharge(CargoKind kind, PreparedCargo item) => CanWork && HasPower
            && (kind != CargoKind.Oxygen || OxygenMachineWorks) && (firstDay || item.PreparedOnDayOne);
        public void SpendPower(float amount) { if (!firstDay && life != null) life.DrainBasePower(amount); }
        public float LimitPoweredSeconds(float seconds, float costPerSecond)
            => firstDay ? seconds : Mathf.Min(seconds, life != null ? life.BasePower / costPerSecond : 0);
        public int ChargedCount(CargoKind kind)
        {
            int count = 0;
            foreach (var item in cargo) if (item != null && item.Item.Kind == kind && item.gameObject.activeSelf && item.Fraction >= .999f) count++;
            return count;
        }
        public int AvailableCount(CargoKind kind)
        {
            int count = 0;
            foreach (var item in cargo) if (item != null && item.Item.Kind == kind && item.gameObject.activeSelf) count++;
            return count;
        }
        public void CarryToNextDay()
        {
            if (firstDay && initialized) pending = Capture();
        }
        private void OnPhaseChanged(StationMissionPhase phase)
        {
            if (phase == StationMissionPhase.Briefing && initialized && checkpoint != null) Restore(checkpoint);
        }
        private Snapshot Capture()
        {
            var state = new Snapshot { cargo = new CargoSave[cargo.Length], cores = new CoreSave[research.Cores.Length],
                tools = new PropSave[looseTools.Length], cleanPanels = new bool[panels.Length],
                suitOxygen = day != null ? day.Suit.Oxygen : 100, suitPower = day != null ? day.Suit.Power : 100 };
            for (int i = 0; i < cargo.Length; i++)
            {
                var item = cargo[i];
                string dock = null;
                foreach (var port in docks) if (port.Connected == item) dock = port.Id;
                state.cargo[i] = new CargoSave { id = item.Id, position = item.transform.position, rotation = item.transform.rotation,
                    active = item.gameObject.activeSelf, fraction = item.Fraction, prepared = item.PreparedOnDayOne, researched = item.ResearchReady, dock = dock };
            }
            for (int i = 0; i < research.Cores.Length; i++)
            {
                var core = research.Cores[i];
                state.cores[i] = new CoreSave { position = core.transform.position, rotation = core.transform.rotation,
                    active = core.gameObject.activeSelf, slot = research.SlotFor(i), progress = research.GetProgress(i) };
            }
            for (int i = 0; i < looseTools.Length; i++) state.tools[i] = new PropSave { position = looseTools[i].position, rotation = looseTools[i].rotation };
            for (int i = 0; i < panels.Length; i++) state.cleanPanels[i] = panels[i].IsClean;
            return state;
        }
        private void Restore(Snapshot state)
        {
            if (life != null) life.RestorePreparedSuit(state.suitOxygen, state.suitPower);
            var saved = new Dictionary<string, CargoSave>();
            foreach (var entry in state.cargo) saved.Add(entry.id, entry);
            foreach (var item in cargo)
            {
                var entry = saved[item.Id];
                item.Item.SetState(CargoState.World);
                item.Item.RestoreAt(entry.position, entry.rotation);
                item.Restore(entry.fraction, entry.prepared, entry.researched);
                item.gameObject.SetActive(entry.active);
            }
            for (int i = 0; i < research.Cores.Length; i++)
            {
                var core = research.Cores[i]; var entry = state.cores[i];
                if (core.Grab.isSelected) core.Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)core.Grab);
                core.transform.SetPositionAndRotation(entry.position, entry.rotation);
                core.Body.position = entry.position; core.Body.rotation = entry.rotation;
                core.Body.isKinematic = false; core.Body.linearVelocity = Vector3.zero; core.Body.angularVelocity = Vector3.zero;
                core.gameObject.SetActive(entry.active); research.RestoreProgress(i, entry.progress);
                if (entry.active) sites[i].RestoreCompleted();
            }
            for (int i = 0; i < looseTools.Length; i++)
            {
                var prop = looseTools[i]; var entry = state.tools[i];
                if (prop.TryGetComponent<XRGrabInteractable>(out var grab) && grab.isSelected) grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
                prop.SetPositionAndRotation(entry.position, entry.rotation);
                if (prop.TryGetComponent<Rigidbody>(out var body))
                { body.position = entry.position; body.rotation = entry.rotation; if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; } }
            }
            for (int i = 0; i < panels.Length; i++) if (state.cleanPanels[i]) panels[i].CleanInstantly();
            Physics.SyncTransforms();
            // 恢复插槽占用，而不只摆回相同位置；未完成的研究与充装才能接续。
            foreach (var item in cargo)
            {
                var entry = saved[item.Id];
                if (!entry.active || string.IsNullOrEmpty(entry.dock)) continue;
                foreach (var port in docks) if (port.Id == entry.dock) Select(port.Socket, item.Item.Grab);
            }
            for (int i = 0; i < research.Cores.Length; i++)
                if (state.cores[i].active && state.cores[i].slot >= 0) Select(research.Rack.Slots[state.cores[i].slot], research.Cores[i].Grab);
        }
        private static void Select(XRSocketInteractor socket, XRGrabInteractable grab)
        {
            var manager = socket.interactionManager != null ? socket.interactionManager : FindAnyObjectByType<XRInteractionManager>();
            manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)grab);
        }
    }
}
