using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 放入接口后才充装；取出即暂停。接口不占用逃生背包的携带额度。
    [RequireComponent(typeof(XRSocketInteractor))]
    public sealed class SupplyChargingDock : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [SerializeField] private StationPreparation preparation;
        [SerializeField] private CargoKind kind;
        [SerializeField] private string persistentId;
        [SerializeField] private float fillSeconds = 20;
        [SerializeField] private TMP_Text readout;
        [SerializeField] private CockpitLamp lamp;
        private XRSocketInteractor socket;
        private LocalizationService language;
        private LineRenderer outline;
        private MaterialPropertyBlock tint;
        private bool highlighted;
        public bool Highlighted => highlighted;
        public string Id => persistentId;
        public CargoKind Kind => kind;
        public XRSocketInteractor Socket => socket != null ? socket : socket = GetComponent<XRSocketInteractor>();
        public PreparedCargo Connected => Socket.firstInteractableSelected is Component c ? c.GetComponent<PreparedCargo>() : null;
        public bool canProcess => isActiveAndEnabled;
        public void Configure(StationPreparation owner, CargoKind type, string id, TMP_Text label, CockpitLamp indicator)
        { preparation = owner; kind = type; persistentId = id; readout = label; lamp = indicator; }
        private void Awake() => language = FindAnyObjectByType<LocalizationService>();
        private void OnEnable() { Socket.selectFilters.Add(this); Socket.hoverFilters.Add(this); }
        private void OnDisable() { Socket.selectFilters.Remove(this); Socket.hoverFilters.Remove(this); }
        internal void BuildOutline(Material material)
        {
            if (material == null || outline != null) return;
            var root = new GameObject("Port outline");
            root.transform.SetParent(transform, false);
            // Place the mark on the mounting plate, where the container's base actually lands.
            root.transform.position = new Vector3(transform.position.x, lamp.transform.position.y + .014f, transform.position.z);
            outline = root.AddComponent<LineRenderer>();
            outline.sharedMaterial = material;
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.numCornerVertices = 3;
            if (kind == CargoKind.Oxygen)
            {
                outline.positionCount = 32;
                for (int i = 0; i < outline.positionCount; i++)
                {
                    float a = 2 * Mathf.PI * i / outline.positionCount;
                    outline.SetPosition(i, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .14f);
                }
            }
            else
            {
                outline.positionCount = 4;
                outline.SetPositions(new[] { new Vector3(-.16f, 0, -.10f), new Vector3(.16f, 0, -.10f),
                    new Vector3(.16f, 0, .10f), new Vector3(-.16f, 0, .10f) });
            }
            tint = new MaterialPropertyBlock();
            UpdateOutline(false);
        }
        private void UpdateOutline(bool matchingSupply)
        {
            highlighted = matchingSupply;
            if (outline == null) return;
            var color = kind == CargoKind.Oxygen ? new Color(.18f, .86f, 1f) : new Color(1f, .70f, .15f);
            tint.SetColor("_BaseColor", color * (matchingSupply ? 1.8f : .65f));
            outline.SetPropertyBlock(tint);
            outline.widthMultiplier = matchingSupply ? .024f : .010f;
        }
        public bool Accepts(CargoItem item) => item != null && item.Kind == kind && item.TryGetComponent<PreparedCargo>(out _)
            && (item.State == CargoState.World || item.State == CargoState.Held);
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => interactable is Component c && Accepts(c.GetComponent<CargoItem>());
        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => interactable is Component c && Accepts(c.GetComponent<CargoItem>());
        private void Update() { Advance(Time.deltaTime); Refresh(); }
        public void Advance(float seconds)
        {
            var connected = Connected;
            if (seconds <= 0 || !float.IsFinite(seconds) || connected == null || !Accepts(connected.Item)
                || connected.Fraction >= 1 || preparation == null || !preparation.CanCharge(kind, connected)) return;
            float step = Mathf.Min(seconds, (1 - connected.Fraction) * fillSeconds);
            step = preparation.LimitPoweredSeconds(step, .08f);
            connected.Fill(step / fillSeconds, preparation.IsFirstDay);
            preparation.SpendPower(step * .08f);
        }
        private void Refresh()
        {
            var connected = Connected;
            bool matching = false;
            if (connected == null && preparation != null)
                foreach (var supply in preparation.Cargo)
                    if (supply.Item.State == CargoState.Held && supply.Item.Kind == kind) { matching = true; break; }
            if (matching != highlighted) UpdateOutline(matching);
            if (outline != null) outline.enabled = connected == null;
            string key = connected == null ? (kind == CargoKind.Oxygen ? "prep.dock.oxygen" : "prep.dock.battery") : connected.Fraction >= 1 ? "prep.dock.full"
                : !preparation.IsFirstDay && !connected.PreparedOnDayOne ? "prep.dock.not_prepared"
                : !preparation.HasPower ? "prep.dock.power"
                : kind == CargoKind.Oxygen && !preparation.OxygenMachineWorks ? "prep.dock.repair"
                : "prep.dock.filling";
            if (readout != null && language != null) readout.text = language.Format(key, connected == null ? 0 : Mathf.RoundToInt(connected.Fraction * 100));
            if (lamp != null) lamp.State = connected == null ? LampState.Next : connected.Fraction >= 1 ? LampState.Done : LampState.Busy;
        }
    }
}
