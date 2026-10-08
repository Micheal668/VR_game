using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 头盔 HUD 完全受服装电源控制。实体腕部补给键独立可用，断电后仍能更换已携带电池。
    public sealed class SuitHudPresenter : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private AscentMission flight;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject hud, wristControls;
        [SerializeField] private ResourceReadout oxygen, power;
        [SerializeField] private LocalizedText temperature, routeText, warning;
        [SerializeField] private RectTransform routeArrow;
        [SerializeField] private LineRenderer routeLine;
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private Button[] oxygenButtons, batteryButtons;
        private readonly List<Vector3> path = new();
        public GameObject Hud => hud;
        public GameObject WristControls => wristControls;
        public LineRenderer RouteLine => routeLine;
        public RouteChevronGuide RouteGuide => routeLine!=null ? routeLine.GetComponent<RouteChevronGuide>() : null;
        public ResourceReadout Oxygen => oxygen;
        public ResourceReadout Power => power;
        public void Configure(LifeSupportMission source, AscentMission ascent, Camera camera, GameObject display, GameObject wrist,
            ResourceReadout o2, ResourceReadout battery, LocalizedText thermal, LocalizedText route, LocalizedText alert,
            RectTransform arrow, LineRenderer line, Transform[] points, Button[] useOxygen, Button[] useBattery)
        {
            Unsubscribe(); life = source; flight = ascent; playerCamera = camera; hud = display; wristControls = wrist;
            oxygen = o2; power = battery; temperature = thermal; routeText = route; warning = alert; routeArrow = arrow;
            routeLine = line; waypoints = points; oxygenButtons = useOxygen; batteryButtons = useBattery;
            if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() { Unsubscribe(); if (hud != null) hud.SetActive(false); if (routeLine != null) routeLine.enabled = false; RouteGuide?.SetVisible(false); }
        private void Subscribe()
        {
            if (life != null) { life.Changed -= Refresh; life.Changed += Refresh; life.Station.Changed -= Refresh; life.Station.Changed += Refresh; life.Inventory.Changed -= Refresh; life.Inventory.Changed += Refresh; }
            if (flight != null) { flight.Changed -= Refresh; flight.Changed += Refresh; }
        }
        private void Unsubscribe()
        { if (life != null) { life.Changed -= Refresh; life.Station.Changed -= Refresh; life.Inventory.Changed -= Refresh; } if (flight != null) flight.Changed -= Refresh; }
        private void Refresh()
        {
            if (life == null || hud == null) return;
            bool ground = !flight.IsLocked && !life.Station.IsTerminal;
            hud.SetActive(life.HudPowered && ground); wristControls.SetActive(life.SuitWorn && ground);
            routeLine.enabled = false;
            RouteGuide?.SetVisible(hud.activeSelf && life.IsGroundActive);
            oxygen.Set(life.SuitOxygen, life.SuitOxygenSeconds); power.Set(life.SuitPower, life.SuitPowerSeconds);
            temperature.SetKey("life.suit.thermal", life.BodyTemperature.ToString("0.0"), Mathf.RoundToInt(life.ThermalMarginPercent));
            warning.SetKey(!life.CanBreathe ? "life.hud.hypoxia" : life.SuitPower < 15 ? "life.hud.battery" : life.SuitOxygen < 25 ? "life.hud.oxygen" : "life.hud.nominal", ResourceReadout.Clock(life.SuffocationRemaining));
            foreach (var button in oxygenButtons) button.interactable = life.CanUseSupply(CargoKind.Oxygen);
            foreach (var button in batteryButtons) button.interactable = life.CanUseSupply(CargoKind.Battery);
        }
        private void LateUpdate()
        {
            if (life == null || !hud.activeSelf || waypoints == null || waypoints.Length < 2) return;
            Vector3 position = life.Player.transform.TransformPoint(life.Player.center); position.y = .065f;
            int next = 1; float minimum = float.PositiveInfinity;
            for (int i = 1; i < waypoints.Length; i++)
            {
                var a = waypoints[i - 1].position; a.y = position.y; var b = waypoints[i].position; b.y = position.y;
                var segment = b - a; float t = Mathf.Clamp01(Vector3.Dot(position - a, segment) / Mathf.Max(.001f, segment.sqrMagnitude));
                float distance = (position - a - segment * t).sqrMagnitude;
                if (distance < minimum) { minimum = distance; next = i; }
            }
            Vector3 target = life.DoorOpen ? waypoints[next].position : life.DoorControl.position;
            target.y = position.y;
            if (Vector3.Distance(position, target) < 1.1f && next + 1 < waypoints.Length && life.DoorOpen) target = waypoints[++next].position;
            path.Clear(); path.Add(position);
            if (!life.DoorOpen) path.Add(new Vector3(target.x, .065f, target.z));
            else for (int i = next; i < waypoints.Length; i++) path.Add(new Vector3(waypoints[i].position.x, .065f, waypoints[i].position.z));
            routeLine.positionCount = path.Count; routeLine.SetPositions(path.ToArray());
            RouteGuide?.Draw(path);
            var forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up);
            float angle = Vector3.SignedAngle(forward, Vector3.ProjectOnPlane(target - position, Vector3.up), Vector3.up);
            routeArrow.localRotation = Quaternion.Euler(0, 0, -angle);
            float remaining = 0; for (int i = 1; i < path.Count; i++) remaining += Vector3.Distance(path[i - 1], path[i]);
            routeText.SetKey(life.DoorOpen ? "life.hud.route_ship" : "life.hud.route_hatch", Mathf.CeilToInt(remaining));
        }
    }
}
