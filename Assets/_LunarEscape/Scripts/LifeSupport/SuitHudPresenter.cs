using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
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
        [SerializeField] private DayOneMission day;
        private StationExpansionMission expansion;
        private RouteChevronGuide chevrons;
        private readonly List<Vector3> path = new();
        private float nextRouteUpdate;
        private Vector3 routeOrigin;
        private string previousInstruction;
        private Transform previousTarget;
        private Vector3 nextPoint;
        private bool pointing;
        public GameObject Hud => hud;
        public GameObject WristControls => wristControls;
        public LineRenderer RouteLine => routeLine;
        public RouteChevronGuide RouteGuide => chevrons != null ? chevrons : chevrons = routeLine != null ? routeLine.GetComponent<RouteChevronGuide>() : null;
        public ResourceReadout Oxygen => oxygen;
        public ResourceReadout Power => power;

        public void ConfigureDayOne(DayOneMission mission, GameObject tasks)
        {
            Unsubscribe(); life = null; flight = null; day = mission; wristControls = tasks;
            foreach (var button in oxygenButtons) if (button != null) button.gameObject.SetActive(false);
            foreach (var button in batteryButtons) if (button != null) button.gameObject.SetActive(false);
            oxygenButtons = batteryButtons = System.Array.Empty<Button>();
        }

        public void Configure(LifeSupportMission source, AscentMission ascent, Camera camera, GameObject display, GameObject wrist,
            ResourceReadout o2, ResourceReadout battery, LocalizedText thermal, LocalizedText route, LocalizedText alert,
            RectTransform arrow, LineRenderer line, Transform[] points, Button[] useOxygen, Button[] useBattery)
        {
            Unsubscribe(); life = source; flight = ascent; playerCamera = camera; hud = display; wristControls = wrist;
            oxygen = o2; power = battery; temperature = thermal; routeText = route; warning = alert; routeArrow = arrow;
            routeLine = line; waypoints = points; oxygenButtons = useOxygen; batteryButtons = useBattery;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            expansion = GetComponent<StationExpansionMission>();
            if (hud == null) return;
            // The compass carries one instruction. Keep refill controls on the actual wrist.
            oxygen.gameObject.SetActive(false);
            power.gameObject.SetActive(false);
            foreach (var button in hud.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
            var background = hud.transform.Find("Panel Background").GetComponent<Image>();
            var size = new Vector2(1120, 110);
            hud.GetComponent<RectTransform>().sizeDelta = background.rectTransform.sizeDelta = size;
            background.color = new Color(.014f, .027f, .038f, .86f);
            Layout(warning, new Vector2(-94, 0), new Vector2(870, 96), 45);
            warning.GetComponent<TMP_Text>().color = Color.white;
            Layout(routeText, new Vector2(439, -18), new Vector2(186, 50), 35);
            Layout(temperature, new Vector2(0, 90), new Vector2(1080, 58), 32);
            temperature.GetComponent<TMP_Text>().color = new Color(1, .7f, .28f);
            routeArrow.anchoredPosition = new Vector2(439, 24);
            routeArrow.localScale = Vector3.one * .8f;
            routeLine.enabled = false;
        }

        private static void Layout(LocalizedText binding, Vector2 position, Vector2 size, int fontSize)
        {
            var label = binding.GetComponent<TMP_Text>();
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * .76f;
            label.fontSizeMax = label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
        }

        private void OnEnable() { Subscribe(); Refresh(); nextRouteUpdate = 0; }
        private void OnDisable()
        {
            Unsubscribe();
            if (hud != null) hud.SetActive(false);
            if (routeLine != null) routeLine.enabled = false;
            RouteGuide?.SetVisible(false);
        }

        private void Subscribe()
        {
            if (life == null) return;
            life.Changed -= Refresh; life.Changed += Refresh;
            life.Station.Changed -= Refresh; life.Station.Changed += Refresh;
            life.Inventory.Changed -= Refresh; life.Inventory.Changed += Refresh;
            flight.Changed -= Refresh; flight.Changed += Refresh;
        }

        private void Unsubscribe()
        {
            if (life == null) return;
            life.Changed -= Refresh;
            life.Station.Changed -= Refresh;
            life.Inventory.Changed -= Refresh;
            flight.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (hud == null) return;
            bool active;
            if (day != null)
            {
                active = day.Current == DayOneMission.Stage.Working || day.Current == DayOneMission.Stage.Evening;
                temperature.gameObject.SetActive(false);
            }
            else
            {
                if (life == null || flight == null) return;
                bool ground = !flight.IsLocked && !life.Station.IsTerminal;
                active = ground && life.IsGroundActive && (!life.SuitWorn || life.HudPowered);
                wristControls.SetActive(ground && life.SuitWorn);
                foreach (var button in oxygenButtons) if (button != null) button.interactable = life.CanUseSupply(CargoKind.Oxygen);
                foreach (var button in batteryButtons) if (button != null) button.interactable = life.CanUseSupply(CargoKind.Battery);
                bool alert = !life.CanBreathe || life.SuitWorn && (life.SuitOxygen < 25 || life.SuitPower < 15);
                temperature.gameObject.SetActive(alert);
                if (alert) temperature.SetKey(!life.CanBreathe ? "life.hud.hypoxia" : life.SuitOxygen < 25 ? "life.hud.oxygen" : "life.hud.battery", ResourceReadout.Clock(life.SuffocationRemaining));
            }
            if (hud.activeSelf != active) nextRouteUpdate = 0;
            hud.SetActive(active);
            RouteGuide?.SetVisible(active);
        }

        private void LateUpdate()
        {
            if (day != null) Refresh();
            if (hud == null || !hud.activeSelf || playerCamera == null) return;
            var eye = playerCamera.transform;
            var objective = day != null ? day.Guidance() : expansion != null ? expansion.GroundObjective()
                : life.DoorOpen ? ("guide.ship", (Transform)null) : ("guide.hatch", life.DoorControl);
            if (Time.unscaledTime >= nextRouteUpdate || (eye.position - routeOrigin).sqrMagnitude > .09f
                || objective.Item1 != previousInstruction || objective.Item2 != previousTarget)
            {
                nextRouteUpdate = Time.unscaledTime + .12f;
                routeOrigin = eye.position;
                previousInstruction = objective.Item1;
                previousTarget = objective.Item2;
                if (objective.Item2 == null) BuildLanderPath(eye.position);
                else
                {
                    var goal = objective.Item2.position;
                    StationWalkway.Build(path, eye.position, Inside(eye.position), goal, Inside(goal));
                }
                warning.SetKey(objective.Item1);
                float distance = 0;
                for (int i = 1; i < path.Count; i++) distance += Vector3.Distance(path[i - 1], path[i]);
                routeText.SetKey("guide.distance", Mathf.CeilToInt(distance));
                // The raw polyline stays available to the route guide and is never rendered.
                routeLine.positionCount = path.Count;
                for (int i = 0; i < path.Count; i++) routeLine.SetPosition(i, path[i]);
                RouteGuide?.Draw(path);
                pointing = path.Count > 1 && distance > .65f;
                routeArrow.gameObject.SetActive(pointing);
                if (pointing) nextPoint = path[1];
            }
            if (pointing)
            {
                var forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
                var direction = Vector3.ProjectOnPlane(nextPoint - eye.position, Vector3.up);
                routeArrow.localRotation = Quaternion.Euler(0, 0, -Vector3.SignedAngle(forward, direction, Vector3.up));
            }
        }

        private bool Inside(Vector3 point)
        {
            point.y = 1f;
            return day != null ? day.InsideHabitat(point) : life.ContainsHabitat(point);
        }

        private void BuildLanderPath(Vector3 position)
        {
            path.Clear(); position.y = .065f; path.Add(position);
            if (waypoints == null || waypoints.Length < 2) return;
            int next = 1;
            float minimum = float.PositiveInfinity;
            for (int i = 1; i < waypoints.Length; i++)
            {
                var a = waypoints[i - 1].position; a.y = position.y;
                var b = waypoints[i].position; b.y = position.y;
                var segment = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(position - a, segment) / Mathf.Max(.001f, segment.sqrMagnitude));
                float distance = (position - a - segment * t).sqrMagnitude;
                if (distance < minimum) { minimum = distance; next = i; }
            }
            for (int i = next; i < waypoints.Length; i++)
            {
                var point = waypoints[i].position; point.y = position.y;
                if (Vector3.Distance(path[^1], point) > .7f) path.Add(point);
            }
        }
    }
}
