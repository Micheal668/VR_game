using System;
using UnityEngine;

namespace LunarEscape
{
    public enum LaunchPhase { Boarding, Liftoff, BoosterSeparation, CoreSeparation, EarthOrbit, LunarTransfer, LunarOrbit }

    // Authored shot choreography. Positions are local scene metres and the celestial distances
    // are compressed for rendering. No launch controls or orbital mechanics are simulated.
    public sealed class LaunchCinematic : MonoBehaviour
    {
        [Serializable] public struct Stage
        {
            public Transform part;
            public Vector3 home;
            public Quaternion facing;
            public float release;
            public Vector3 drift, tumble;
        }

        [SerializeField] private ArtemisRocketVisual rocket;
        [SerializeField] private Transform pad, bridgeStart, bridgeEnd, bridgePivot;
        [SerializeField] private Transform crewView, rocketView;
        [SerializeField] private LaunchCrewActor[] actors;
        [SerializeField] private Stage[] stages;
        [SerializeField] private Transform earth, moon;
        [SerializeField] private LaunchExhaust exhaust;
        [SerializeField] private Material sky;
        [SerializeField] private Vector3 vehicleStart;
        [SerializeField] private Quaternion vehicleOrientation;
        private static readonly int Atmosphere = Shader.PropertyToID("_AtmosphereBlend");
        private bool playing, firstPerson;
        private Material skyInstance;
        private Material previousSky;
        private Renderer earthSurface, moonSurface;
        private MaterialPropertyBlock globeProperties;
        public Transform CrewView => crewView;
        public Transform RocketView => rocketView;
        public Transform Earth => earth;
        public Transform Moon => moon;
        public ArtemisRocketVisual Rocket => rocket;
        public LaunchCrewActor[] Actors => actors;
        public float Elapsed { get; private set; }
        public float Duration => 80f;
        public LaunchPhase Phase { get; private set; }
        public event Action Completed;

        public void Configure(ArtemisRocketVisual vehicle, Transform scenery, Transform start, Transform end, Transform pivot,
            Transform crewEye, Transform outsideEye, LaunchCrewActor[] team, Stage[] trackedStages,
            Transform earthGlobe, Transform moonGlobe, LaunchExhaust effects, Material skyMaterial)
        {
            rocket = vehicle; pad = scenery; bridgeStart = start; bridgeEnd = end; bridgePivot = pivot;
            crewView = crewEye; rocketView = outsideEye; actors = team; stages = trackedStages;
            earth = earthGlobe; moon = moonGlobe; exhaust = effects; sky = skyMaterial;
            vehicleStart = transform.InverseTransformPoint(vehicle.transform.position);
            vehicleOrientation = Quaternion.Inverse(transform.rotation) * vehicle.transform.rotation;
        }

        public void Play()
        {
            Evaluate(0);
            playing = true;
        }

        private void Update()
        {
            if (!playing) return;
            Evaluate(Elapsed + Time.deltaTime);
            if (Elapsed < Duration) return;
            playing = false;
            Completed?.Invoke();
        }

        public void SetCrewView(bool value)
        {
            firstPerson = value;
            if (actors != null && actors.Length > 0) actors[^1].SetFirstPerson(value);
        }

        public void Evaluate(float seconds)
        {
            Elapsed = Mathf.Clamp(seconds, 0, Duration);
            float t = Elapsed;
            Phase = t < 17.5f ? LaunchPhase.Boarding : t < 30 ? LaunchPhase.Liftoff : t < 36.5f ? LaunchPhase.BoosterSeparation
                : t < 43 ? LaunchPhase.CoreSeparation : t < 54 ? LaunchPhase.EarthOrbit : t < 69 ? LaunchPhase.LunarTransfer : LaunchPhase.LunarOrbit;
            var ship = rocket.transform;
            float rise = Ease(19.1f, 47, t), coast = Ease(46, 78, t);
            var offset = new Vector3(38 * rise + 140 * coast, 1450 * rise + 110 * coast, 730 * rise * rise + 2320 * coast);
            ship.SetPositionAndRotation(transform.TransformPoint(vehicleStart + vehicleOrientation * offset),
                transform.rotation * vehicleOrientation * Quaternion.Euler(23 * Ease(23, 47, t) + 8 * coast, 8 * coast, -5 * rise));
            pad.gameObject.SetActive(t < 37);
            bridgePivot.localRotation = Quaternion.Euler(0, 72 * Ease(16.7f, 19.1f, t), 0);
            foreach (var stage in stages) PlaceStage(stage, t);
            rocket.SetSolarDeployment(Ease(44, 49, t));
            PlaceCrew(t);
            if (rocket.HatchDoor != null) rocket.HatchDoor.SetActive(t >= 16.6f);
            PlaceWorlds(t);
            PlaceExteriorView(t);
            exhaust.Evaluate(t);
            if (Application.isPlaying)
            {
                if (skyInstance == null)
                {
                    previousSky = RenderSettings.skybox;
                    skyInstance = new Material(sky) { name = "Launch sky instance" };
                    RenderSettings.skybox = skyInstance;
                }
                skyInstance.SetFloat(Atmosphere, 1 - Ease(23, 38, t));
            }
        }

        private void OnDestroy()
        {
            if (skyInstance == null) return;
            if (RenderSettings.skybox == skyInstance) RenderSettings.skybox = previousSky;
            Destroy(skyInstance);
        }

        private void PlaceStage(Stage stage, float t)
        {
            var part = stage.part;
            float age = Mathf.Max(0, t - stage.release);
            bool released = t >= stage.release;
            Transform parent = released ? transform : rocket.transform;
            if (part.parent != parent) part.SetParent(parent, true);
            part.gameObject.SetActive(age < 11);
            if (!released)
            {
                part.localPosition = stage.home;
                part.localRotation = stage.facing;
                return;
            }
            var trail = stage.drift * age * (1 + age * .32f);
            part.SetPositionAndRotation(rocket.transform.TransformPoint(stage.home + trail),
                rocket.transform.rotation * stage.facing * Quaternion.Euler(stage.tumble * age));
        }

        private void PlaceCrew(float t)
        {
            var lane = Vector3.ProjectOnPlane(bridgeEnd.position - bridgeStart.position, Vector3.up);
            var forward = lane.normalized;
            var facing = Quaternion.LookRotation(forward, Vector3.up);
            for (int i = 0; i < actors.Length; i++)
            {
                // Two crewmates precede the observer along the same walkway.
                float lead = (2 - i) * 1.4f;
                float begin = i * .42f;
                float board = 11.7f + i * 1.6f;
                float fraction = Mathf.InverseLerp(begin, board, t);
                float distance = lead + (lane.magnitude + .72f - lead) * fraction;
                var feet = bridgeStart.position + forward * distance;
                bool aboard = t >= board;
                actors[i].Place(feet, facing, distance, Mathf.Min(1, Mathf.Max(0, t - begin) * 2), aboard, firstPerson && i == 2);
                if (i != 2) continue;
                var eyes = feet + Vector3.up * 1.63f + forward * .11f;
                float settle = Ease(board - .35f, 17, t);
                crewView.SetPositionAndRotation(Vector3.Lerp(eyes, rocket.Seat.position, settle),
                    Quaternion.Slerp(facing, rocket.Seat.rotation, settle));
            }
        }

        private void PlaceWorlds(float t)
        {
            // Changes in apparent angular size convey departure/arrival without enormous world coordinates.
            float climb = Ease(23, 43, t), orbit = Ease(43, 54, t), transfer = Ease(54, 70, t), arrival = Ease(69, 80, t);
            var frame = rocket.Seat;
            float transferArc = Mathf.Sin(Mathf.PI * transfer);
            float earthYaw = -6 - 16 * climb + 18 * orbit + 19 * transfer + 4 * arrival;
            float earthPitch = -86 + 66 * climb + 10 * orbit + 19 * transfer + 2 * arrival - 8 * transferArc;
            float earthRadius = Mathf.Lerp(60, 24, climb) - 4 * orbit - 18.7f * transfer;
            Globe(earth, frame, earthYaw, earthPitch, earthRadius, 11500, Quaternion.Euler(8, -68 + 1.55f * t, -23));
            float moonYaw = 22 - 20 * transfer - 17 * arrival;
            float moonPitch = 15 - 19 * transfer - 6 * arrival + 10 * transferArc;
            float moonRadius = .32f + 15 * transfer + 7 * arrival;
            Globe(moon, frame, moonYaw, moonPitch, moonRadius, 6500, Quaternion.Euler(3, 18 + .38f * t + 30 * arrival, 7));
            if (earthSurface == null)
            {
                earthSurface = earth.GetComponent<Renderer>(); moonSurface = moon.GetComponent<Renderer>();
                globeProperties = new MaterialPropertyBlock();
            }
            FadeGlobe(earthSurface, Ease(25.5f, 34, t));
            FadeGlobe(moonSurface, Ease(32, 38, t));
        }

        private void FadeGlobe(Renderer surface, float opacity)
        {
            surface.enabled = opacity > .001f;
            globeProperties.SetFloat("_Visibility", opacity); surface.SetPropertyBlock(globeProperties);
        }

        private static void Globe(Transform body, Transform eye, float yaw, float pitch, float radiusDegrees, float distance, Quaternion rotation)
        {
            Vector3 ray = Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward;
            body.SetPositionAndRotation(eye.position + eye.rotation * ray * distance, rotation);
            body.localScale = Vector3.one * (2 * distance * Mathf.Sin(radiusDegrees * Mathf.Deg2Rad));
        }

        private void PlaceExteriorView(float t)
        {
            var ship = rocket.transform;
            if (t < 3.55f)
            {
                // A separate establishing shot avoids sweeping the VR camera the full distance to the bridge.
                Vector3 establishing = ship.TransformPoint(Vector3.Lerp(new Vector3(-99, 91, -165),
                    new Vector3(-94, 90, -162), Ease(0, 3.55f, t)));
                Vector3 launchComplex = ship.TransformPoint(new Vector3(7, 40, 0));
                rocketView.SetPositionAndRotation(establishing, Quaternion.LookRotation(launchComplex - establishing, ship.up));
                return;
            }
            Vector3 bridgeEye = bridgeEnd.position + ship.right * 6.4f - ship.forward * 6f + ship.up * 2.5f;
            Vector3 bridgeAim = Vector3.Lerp(bridgeStart.position, bridgeEnd.position, Mathf.Lerp(.14f, .93f, Ease(0, 14, t))) + ship.up * 1.12f;
            float widen = Ease(14.9f, 20.5f, t), staging = Ease(24, 31, t), cabinShot = Ease(41, 47, t);
            var wide = Vector3.Lerp(new Vector3(-47, 40, -102), new Vector3(-21, 56, -90), staging);
            var aim = Vector3.Lerp(new Vector3(0, 46, 0), new Vector3(0, 49, 0), staging);
            var position = Vector3.Lerp(bridgeEye, ship.TransformPoint(wide), widen);
            var target = Vector3.Lerp(bridgeAim, ship.TransformPoint(aim), widen);
            var framing = Quaternion.LookRotation(target - position, ship.up);
            // Pan the mounted camera along the transfer so Earth stays inside the frame as the Moon grows.
            float transferPan = Ease(50, 66, t);
            var mountedFacing = rocket.ExternalCamera.rotation * Quaternion.Euler(-3 * transferPan, 13 * transferPan, 0);
            rocketView.SetPositionAndRotation(Vector3.Lerp(position, rocket.ExternalCamera.position, cabinShot),
                Quaternion.Slerp(framing, mountedFacing, cabinShot));
        }

        internal static float Ease(float from, float to, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));
    }
}
