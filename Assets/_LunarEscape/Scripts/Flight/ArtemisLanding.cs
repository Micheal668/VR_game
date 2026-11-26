using UnityEngine;

namespace LunarEscape
{
    public enum LandingPhase { Docked, Separated, DepartureBurn, Approach, Retry, Landed }
    public enum LandingFault { None, OutsideSite, FastDescent, Sideways, Tilted, LeftArea }

    public sealed class ArtemisLanding : MonoBehaviour
    {
        public const string SceneName = "00_ArtemisLunarLanding";
        public const float TargetRadius = 15.65f, SafeDescent = 2.3f, SafeDrift = 2.1f, SafeTilt = 11f;
        [SerializeField] private Transform craft, orbiter, orbitMoon, earth, touchdown, exhaust;
        [SerializeField] private ArtemisLanderVisual model;
        [SerializeField] private GameObject orbitSet, surfaceSet;
        [SerializeField] private LayerMask siteCollision;
        private float yaw, pitch, roll, phaseTime, orbitTime, separationTime;
        private Vector3 velocity, dockedOrigin, releaseAxis;
        public LandingPhase Phase { get; private set; }
        public LandingFault Fault { get; private set; }
        public Transform Craft => craft;
        public Transform Orbiter => orbiter;
        public Transform Target => touchdown;
        public ArtemisLanderVisual Model => model;
        public Vector3 Velocity => velocity;
        public float Height => LowestFoot() - touchdown.position.y;
        public Vector3 OffsetToPad => Vector3.ProjectOnPlane(touchdown.position - craft.position, Vector3.up);
        public float Range => OffsetToPad.magnitude;
        public float Drift => Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
        public float Tilt => Vector3.Angle(craft.up, Vector3.up);
        public float Heading => yaw;
        public float Thrust { get; private set; }
        public bool EngineOn { get; private set; }
        public float PhaseTime => phaseTime;

        public void Configure(Transform vehicle, ArtemisLanderVisual landerModel, Transform motherShip, Transform moon, Transform earthGlobe, Transform marker,
            Transform plume, GameObject orbitalEnvironment, GameObject surfaceEnvironment, int obstacleMask)
        {
            craft = vehicle;
            model = landerModel;
            orbiter = motherShip;
            orbitMoon = moon;
            earth = earthGlobe;
            touchdown = marker;
            exhaust = plume;
            orbitSet = orbitalEnvironment;
            surfaceSet = surfaceEnvironment;
            siteCollision = obstacleMask;
            AlignDockingPorts();
        }

        private void AlignDockingPorts()
        {
            var port = model.DockingPort;
            var mating = model.OrbiterDockingPort;
            var aligned = Quaternion.LookRotation(port.forward, -port.up);
            orbiter.rotation = aligned * Quaternion.Inverse(mating.rotation) * orbiter.rotation;
            orbiter.position += port.position - mating.position;
            dockedOrigin = orbiter.position;
            releaseAxis = port.up;
        }

        private float LowestFoot()
        {
            float height = float.PositiveInfinity;
            foreach (var foot in model.Feet) height = Mathf.Min(height, foot.position.y);
            return height;
        }

        public bool FeetInsidePad()
        {
            foreach (var foot in model.Feet)
                if (Vector3.ProjectOnPlane(foot.position - touchdown.position, Vector3.up).sqrMagnitude > TargetRadius * TargetRadius)
                    return false;
            return true;
        }

        private void Start()
        {
            orbitSet.SetActive(true);
            surfaceSet.SetActive(false);
            craft.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            AlignDockingPorts();
            SetThrust(0);
        }

        public void SeparateOrRetry()
        {
            if (Phase == LandingPhase.Docked) Enter(LandingPhase.Separated);
            else if (Phase == LandingPhase.Retry) BeginApproach();
        }

        public void ToggleEngine()
        {
            if (Phase == LandingPhase.Separated)
            {
                EngineOn = true;
                Enter(LandingPhase.DepartureBurn);
            }
            else if (Phase == LandingPhase.Approach)
                EngineOn = !EngineOn;
        }

        // A short, staged departure burn leads into the continuous manual terminal approach.
        public void Advance(float seconds, Vector2 attitude, Vector2 engineAndYaw)
        {
            if (!float.IsFinite(seconds) || seconds <= 0) return;
            earth.Rotate(Vector3.up, seconds * .72f, Space.Self);
            attitude = Vector2.ClampMagnitude(attitude, 1);
            engineAndYaw = Vector2.ClampMagnitude(engineAndYaw, 1);
            while (seconds > .00001f)
            {
                float dt = Mathf.Min(seconds, .04f);
                seconds -= dt;
                phaseTime += dt;
                if (Phase == LandingPhase.Approach)
                {
                    Orient(dt, attitude, engineAndYaw.x);
                    Fly(dt, engineAndYaw.y);
                }
                else if (Phase == LandingPhase.Docked || Phase == LandingPhase.Separated || Phase == LandingPhase.DepartureBurn)
                {
                    orbitTime += dt;
                    orbitMoon.localRotation = Quaternion.Euler(4f, 110f + orbitTime * .18f, 9f);
                    if (Phase == LandingPhase.Docked) continue;
                    separationTime += dt;
                    // Clear both docking rings axially before the vehicles turn or drift across one another.
                    var axial = releaseAxis * Mathf.Min(4.2f, separationTime * 3f);
                    var tangent = Vector3.ProjectOnPlane(new Vector3(-10, 0, 15), releaseAxis);
                    var lateral = Vector3.ClampMagnitude(tangent, Mathf.Max(0, separationTime - 1.4f) * 3.2f);
                    orbiter.position = dockedOrigin + axial + lateral;
                    if (separationTime > .8f) Orient(dt, attitude, engineAndYaw.x);
                    if (Phase == LandingPhase.DepartureBurn)
                    {
                        if (separationTime > .8f)
                        {
                            SetThrust(.76f);
                            craft.position += new Vector3(.3f, -.5f, 1.4f) * dt;
                        }
                        if (phaseTime >= 4f) BeginApproach();
                    }
                }
            }
        }

        private void BeginApproach()
        {
            orbitSet.SetActive(false);
            surfaceSet.SetActive(true);
            yaw = pitch = roll = 0;
            craft.SetPositionAndRotation(touchdown.position + new Vector3(-14, 68, -65), Quaternion.identity);
            velocity = new Vector3(0, -1.2f, 1.5f);
            EngineOn = true;
            Fault = LandingFault.None;
            SetThrust(.5f);
            Enter(LandingPhase.Approach);
        }

        private void Orient(float dt, Vector2 attitude, float turn)
        {
            yaw = Mathf.Repeat(yaw + turn * 34f * dt, 360f);
            pitch = Mathf.MoveTowards(pitch, attitude.y * 20f, 24f * dt);
            roll = Mathf.MoveTowards(roll, -attitude.x * 20f, 24f * dt);
            craft.rotation = Quaternion.Euler(pitch, yaw, roll);
        }

        private void Fly(float dt, float throttle)
        {
            // Centered controls request hover and cancel drift. Tilt commands lateral maneuvering jets.
            const float gravity = 1.62f;
            var up = craft.up;
            float demand = .5f + throttle * .86f - velocity.y * .12f;
            SetThrust(EngineOn ? Mathf.Clamp01(demand / Mathf.Max(.8f, up.y)) : 0);
            var acceleration = Vector3.up * (2 * gravity * Thrust * up.y - gravity);
            if (EngineOn)
                acceleration += Vector3.ProjectOnPlane(up, Vector3.up) * (6.8f + 2 * gravity * Thrust)
                              - Vector3.ProjectOnPlane(velocity, Vector3.up) * .6f;
            var from = craft.position;
            var next = from + velocity * dt + .5f * acceleration * dt * dt;
            velocity += acceleration * dt;
            var travel = next - from;
            bool obstacle = travel.sqrMagnitude > 0 && Physics.SphereCast(from + Vector3.up * 2.25f,
                1.8f, travel.normalized, out _, travel.magnitude, siteCollision, QueryTriggerInteraction.Ignore);
            if (!obstacle && Height < 18 && travel.sqrMagnitude > 0)
                foreach (var foot in model.Feet)
                    if (Physics.Raycast(foot.position + Vector3.up * .025f, travel.normalized, travel.magnitude + .025f, siteCollision, QueryTriggerInteraction.Ignore))
                    {
                        obstacle = true;
                        break;
                    }
            craft.position = next;
            // The marked clearing is flat. Elsewhere follow the actual crater and rock colliders.
            if (obstacle || (Range <= TargetRadius && Height <= 0))
            {
                if (!FeetInsidePad() || (obstacle && Height > 1)) Fault = LandingFault.OutsideSite;
                else if (Mathf.Abs(velocity.y) > SafeDescent) Fault = LandingFault.FastDescent;
                else if (Drift > SafeDrift) Fault = LandingFault.Sideways;
                else if (Tilt > SafeTilt) Fault = LandingFault.Tilted;
                if (Fault == LandingFault.None)
                {
                    craft.rotation = Quaternion.Euler(0, yaw, 0);
                    craft.position -= Vector3.up * Height;
                }
                Stop(Fault == LandingFault.None ? LandingPhase.Landed : LandingPhase.Retry);
            }
            else if (Height > 190 || Height < -45 || Range > 235)
            {
                Fault = LandingFault.LeftArea;
                Stop(LandingPhase.Retry);
            }
        }

        private void Stop(LandingPhase phase)
        {
            velocity = Vector3.zero;
            EngineOn = false;
            SetThrust(0);
            Enter(phase);
        }

        private void SetThrust(float value)
        {
            Thrust = value;
            bool visible = value > .015f;
            if (exhaust.gameObject.activeSelf != visible) exhaust.gameObject.SetActive(visible);
            if (visible) exhaust.localScale = new Vector3(.65f + .3f * value, .36f + 1.9f * value, .65f + .3f * value);
        }

        private void Enter(LandingPhase phase)
        {
            Phase = phase;
            phaseTime = 0;
        }
    }
}
