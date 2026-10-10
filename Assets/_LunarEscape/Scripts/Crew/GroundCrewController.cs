using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 将实体救援操作和既定撤离通路连接到乘员规则。只接受任务发出的实际时间步。
    public sealed class GroundCrewController : MonoBehaviour
    {
        [SerializeField] private CrewMission crew;
        [SerializeField] private Transform commander, medicalPort;
        [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
        [SerializeField] private GameObject restraint, flightCommander;
        [SerializeField] private CrewRescueHandle rescueHandle;
        [SerializeField, Min(.1f)] private float walkingSpeed = 1.7f;
        private readonly HashSet<CrewRescueHandle> heldHandles = new();
        private readonly List<XRGrabInteractable> medicalItems = new();
        private bool listening;
        private float distanceAlongRoute;
        private float[] segmentStarts;
        private float routeLength;
        public Transform CommanderTransform => commander;
        public Transform MedicalPort => medicalPort;
        public CrewRescueHandle RescueHandle => rescueHandle;
        public IReadOnlyList<Transform> Waypoints => waypoints;
        public bool ReachedBoarding => routeLength > 0 && distanceAlongRoute >= routeLength - .15f;
        public float RouteProgress => distanceAlongRoute;

        public void Configure(CrewMission task, Transform model, Transform aidPort, Transform[] route,
            GameObject securingBar, GameObject cabinModel, CrewRescueHandle handle)
        {
            Unsubscribe();
            crew = task; commander = model; medicalPort = aidPort; waypoints = route;
            restraint = securingBar; flightCommander = cabinModel; rescueHandle = handle;
            BuildRoute();
            if (isActiveAndEnabled) Subscribe();
            ResetPresentation(); Refresh();
        }

        private void OnEnable()
        {
            if (crew == null) return;
            BuildRoute(); Subscribe();
            if (crew.Station.Phase == StationMissionPhase.Briefing && !Expanded) ResetPresentation();
            Refresh();
        }
        private void OnDisable() { Unsubscribe(); heldHandles.Clear(); crew?.SetRescueHeld(false); }
        private void Subscribe()
        {
            if (listening || crew == null) return;
            crew.ConfigureProbes(HasHeldHandle, () => ReachedBoarding);
            crew.Changed += Refresh;
            crew.FollowingTimeAdvanced += AdvanceFollowing;
            crew.Station.Changed += StationChanged;
            foreach (var item in crew.Inventory.Items)
                if (item != null && item.Kind == CargoKind.MedicalKit)
                { medicalItems.Add(item.Grab); item.Grab.activated.AddListener(UseMedical); }
            listening = true;
        }
        private void Unsubscribe()
        {
            if (!listening) return;
            crew.Changed -= Refresh; crew.FollowingTimeAdvanced -= AdvanceFollowing;
            crew.Station.Changed -= StationChanged;
            crew.ConfigureProbes(null, null);
            foreach (var item in medicalItems) if (item != null) item.activated.RemoveListener(UseMedical);
            medicalItems.Clear(); listening = false;
        }
        private bool HasHeldHandle()
        {
            heldHandles.RemoveWhere(handle => handle == null || !handle.isActiveAndEnabled);
            return heldHandles.Count > 0;
        }
        public void SetHandleHeld(CrewRescueHandle source, bool held)
        {
            if (source == null || crew == null) return;
            if (held && source.isActiveAndEnabled) heldHandles.Add(source); else heldHandles.Remove(source);
            crew.SetRescueHeld(HasHeldHandle());
        }
        private void UseMedical(ActivateEventArgs args)
        {
            if (args.interactableObject is not XRGrabInteractable grab || !grab.TryGetComponent<CargoItem>(out var item)) return;
            // 接口有明确的小范围，规则层还会复核真抓取、楼层、生命状态及消费权限。
            if (Vector3.Distance(item.transform.position, medicalPort.position) <= .48f)
                crew.TryTreatCommander(item);
        }
        public bool TryTreatHeldMedical()
        {
            if (crew == null || medicalPort == null) return false;
            foreach (var item in crew.Inventory.Items)
                if (item != null && item.Kind == CargoKind.MedicalKit &&
                    Vector3.Distance(item.transform.position, medicalPort.position) <= .48f && crew.TryTreatCommander(item)) return true;
            return false;
        }
        public void TreatHeldMedical() => TryTreatHeldMedical();
        public void UseLoadedMedicalOnCommander() => crew.TryUseLoadedMedicalOnCommander();
        private void BuildRoute()
        {
            if (waypoints == null || waypoints.Length < 2) return;
            segmentStarts = new float[waypoints.Length]; routeLength = 0;
            for (int i = 1; i < waypoints.Length; i++)
            { routeLength += Vector3.Distance(waypoints[i - 1].position, waypoints[i].position); segmentStarts[i] = routeLength; }
        }
        private void StationChanged()
        {
            if (crew.Station.Phase == StationMissionPhase.Briefing && !Expanded) ResetPresentation();
            Refresh();
        }
        private bool Expanded => crew != null && crew.Station.LifeSupport != null && crew.Station.LifeSupport.ExpandedStation;
        public void ResetRoute() => ResetPresentation();
        private void ResetPresentation()
        {
            distanceAlongRoute = 0; heldHandles.Clear();
            if (commander != null && waypoints.Length > 0)
                commander.SetPositionAndRotation(waypoints[0].position, waypoints[0].rotation);
        }
        private void AdvanceFollowing(float seconds)
        {
            if (crew.CommanderState != CrewState.Following || seconds <= 0 || segmentStarts == null) return;
            // 沿同一条气闸和月面折线跟随；玩家未走过的路段不提前穿越，也不直线追踪穿墙。
            Vector3 player = crew.Player.transform.TransformPoint(crew.Player.center); player.y = 0;
            float closest = float.PositiveInfinity, playerProgress = 0;
            for (int i = 1; i < waypoints.Length; i++)
            {
                Vector3 a = waypoints[i - 1].position, b = waypoints[i].position;
                Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(player - a, ab) / Mathf.Max(.0001f, ab.sqrMagnitude));
                float sqr = (player - (a + ab * t)).sqrMagnitude;
                if (sqr < closest) { closest = sqr; playerProgress = segmentStarts[i - 1] + ab.magnitude * t; }
            }
            float allowed = playerProgress >= routeLength - 1.6f ? routeLength : Mathf.Max(0, playerProgress - .8f);
            distanceAlongRoute = Mathf.Min(routeLength, Mathf.MoveTowards(distanceAlongRoute, Mathf.Max(distanceAlongRoute, allowed), walkingSpeed * seconds));
            for (int i = 1; i < waypoints.Length; i++)
            {
                if (distanceAlongRoute > segmentStarts[i] && i < waypoints.Length - 1) continue;
                var a = waypoints[i - 1].position; var direction = waypoints[i].position - a;
                float t = Mathf.InverseLerp(segmentStarts[i - 1], segmentStarts[i], distanceAlongRoute);
                commander.position = Vector3.Lerp(a, waypoints[i].position, t);
                if (direction.sqrMagnitude > .001f) commander.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                break;
            }
        }
        private void Refresh()
        {
            if (crew == null || commander == null) return;
            bool groundVisible = crew.Station.Phase != StationMissionPhase.Completed && !crew.IsOutcomeResolved && crew.CommanderState != CrewState.Dead;
            commander.gameObject.SetActive(groundVisible);
            if (restraint != null) restraint.SetActive(!Expanded && groundVisible && crew.CommanderState == CrewState.Trapped);
            if (flightCommander != null) flightCommander.SetActive(crew.CommanderBoarded && crew.CommanderState != CrewState.Dead);
            if (crew.CommanderState != CrewState.Trapped) heldHandles.Clear();
        }
    }
}
