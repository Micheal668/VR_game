using System;
using UnityEngine;

namespace LunarEscape
{
    // 手持激光切割器：扳机深浅即功率。光束很短，必须贴近钢梁，沿红色切割线移动光点；
    // 功率越大切得越快，但发热按功率平方增长，一直满功率几秒就会过热停机。
    public sealed class LaserCutter : TriggerTool
    {
        [SerializeField] private LineRenderer beam;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private Light hotSpot;
        [SerializeField] private CockpitLamp heatLamp;
        [SerializeField, Min(0.05f)] private float range = 0.55f;
        [Tooltip("满功率下每秒给切割线一段加的热量（1 = 切开一段）。")]
        [SerializeField, Min(0f)] private float cutRate = 2.2f;
        [SerializeField, Min(0f)] private float heatPerSecondAtFull = 0.26f;
        private AudioSource sizzle;

        public bool OnLine { get; private set; }
        public bool Hitting { get; private set; }
        public event Action<LaserCutter> OverheatedEvent;
        protected override FeedbackSound LoopSound => FeedbackSound.LaserLoop;

        public void Configure(Transform emitter, LineRenderer ray, ParticleSystem spray, Light glow, CockpitLamp lamp)
        { muzzle = emitter; beam = ray; sparks = spray; hotSpot = glow; heatLamp = lamp; }

        protected override void Awake()
        {
            base.Awake();
            sizzle = FeedbackSounds.CreateLoop(Muzzle, "Laser Sizzle", FeedbackSound.RepairLoop, 1f);
            sizzle.bypassListenerEffects = true;
            if (beam != null) beam.enabled = false;
            if (hotSpot != null) hotSpot.enabled = false;
        }

        protected override void OnEnable() { base.OnEnable(); if (!sizzle.isPlaying) sizzle.Play(); }
        protected override void OnDisable() { base.OnDisable(); sizzle.volume = 0f; if (beam != null) beam.enabled = false; if (hotSpot != null) hotSpot.enabled = false; }

        protected override float HeatPerSecond(float power) => heatPerSecondAtFull * power * power;

        protected override float Work(float power, float deltaTime)
        {
            Hitting = OnLine = false;
            bool firing = power > 0f;
            var origin = Muzzle;
            Vector3 end = origin.position + origin.forward * range;
            if (firing && Physics.Raycast(origin.position, origin.forward, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point; Hitting = true;
                var line = hit.collider.GetComponentInParent<CutBeam>() is CutBeam steel ? Closest(steel, hit.point) : null;
                OnLine = line != null && line.Heat(hit.point, cutRate * power * deltaTime);
                if (sparks != null)
                {
                    sparks.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
                    sparks.Emit(Mathf.RoundToInt((OnLine ? 3f : 1f) * (1f + 3f * power)));
                }
                if (hotSpot != null)
                {
                    hotSpot.transform.position = hit.point + hit.normal * 0.03f;
                    hotSpot.intensity = (0.4f + 1.4f * power) * UnityEngine.Random.Range(0.7f, 1.1f);
                }
            }
            if (beam != null)
            {
                beam.enabled = firing;
                if (firing)
                {
                    beam.SetPosition(0, origin.position); beam.SetPosition(1, end);
                    beam.widthMultiplier = Mathf.Lerp(0.003f, 0.009f, power);
                }
            }
            if (hotSpot != null) hotSpot.enabled = firing && Hitting;
            sizzle.volume = Mathf.MoveTowards(sizzle.volume, firing && Hitting ? (OnLine ? 0.55f : 0.3f) : 0f, deltaTime * 8f);
            if (heatLamp != null) heatLamp.State = Overheated ? LampState.Fault : Heat > 0.7f ? LampState.Busy : LampState.Done;
            return power;
        }

        private static CutLine Closest(CutBeam steel, Vector3 point)
        {
            CutLine best = null; float bestDistance = float.MaxValue;
            foreach (var line in steel.Cuts)
            {
                if (line == null || line.Severed) continue;
                Vector3 a = line.WorldFrom, ab = line.WorldTo - a;
                float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                float d = Vector3.Distance(point, a + ab * t);
                if (d < bestDistance) { bestDistance = d; best = line; }
            }
            return best;
        }

        protected override float RumbleAmplitude(float effort) => Hitting ? 0.15f + 0.35f * effort : 0.05f + 0.1f * effort;

        protected override void OnOverheat()
        {
            base.OnOverheat();
            if (heatLamp != null) heatLamp.Flash();
            OverheatedEvent?.Invoke(this);
        }
    }
}
