using System;
using UnityEngine;

namespace LunarEscape
{
    // 手持取芯钻：把钻头竖直顶在取芯点的孔底，扳机控制转速。
    //   · 转速落在当前地层的合适区间（指示灯绿）钻得最快、几乎不发热；
    //   · 太慢（琥珀）钻头打滑、几乎不进；太快（红）钻得稍快但迅速发热，过热会卡钻，需松开扳机冷却；
    //   · 越往下越硬：区间上移、发热更快。孔越深，手要跟着往下压。
    public sealed class CoreDrill : TriggerTool
    {
        [SerializeField] private CockpitLamp zoneLamp;
        [Tooltip("转速条：按扳机深度缩放、按区间着色。")]
        [SerializeField] private Renderer rpmBar;
        [SerializeField] private TMPro.TMP_Text depthReadout;
        [SerializeField] private Transform bit;
        [SerializeField, Min(0.01f)] private float metersPerSecond = 0.09f;
        [SerializeField, Range(0f, 60f)] private float maxTiltDegrees = 35f;
        private AudioSource grind;
        private MaterialPropertyBlock block;
        private float contactHeat;
        private float spin;

        public DrillSite Site { get; private set; }
        public bool InContact { get; private set; }
        // -1 太慢，0 合适，1 太快；没顶在孔底时为 -2。
        public int Zone { get; private set; } = -2;
        public event Action<CoreDrill> Jammed;
        protected override FeedbackSound LoopSound => FeedbackSound.DrillLoop;

        public void Configure(Transform tip, Transform spinningBit, CockpitLamp lamp, Renderer bar, TMPro.TMP_Text readout)
        { muzzle = tip; bit = spinningBit; zoneLamp = lamp; rpmBar = bar; depthReadout = readout; }

        protected override void Awake()
        {
            base.Awake();
            grind = FeedbackSounds.CreateLoop(Muzzle, "Drill Grind", FeedbackSound.GrindLoop, 1f);
            grind.bypassListenerEffects = true;
        }

        protected override void OnEnable() { base.OnEnable(); if (!grind.isPlaying) grind.Play(); }
        protected override void OnDisable() { base.OnDisable(); grind.volume = 0f; }

        protected override float HeatPerSecond(float power) => contactHeat;

        protected override float Work(float power, float deltaTime)
        {
            var tip = Muzzle;
            Site = Nearest(tip.position);
            bool upright = Vector3.Angle(tip.forward, Vector3.down) <= maxTiltDegrees;
            InContact = Site != null && upright && Site.Touches(tip.position);
            contactHeat = 0f;
            Zone = -2;
            if (InContact && power > 0f)
            {
                var band = Site.Band;
                Zone = power < band.x ? -1 : power > band.y ? 1 : 0;
                float efficiency = Zone == 0 ? 1f : Zone < 0 ? 0.15f : 0.6f;
                Site.Advance(metersPerSecond * efficiency / Site.Hardness * deltaTime);
                contactHeat = Zone == 0 ? 0.05f * Site.Hardness
                    : Zone > 0 ? (0.35f + 0.9f * (power - band.y) / Mathf.Max(0.05f, 1f - band.y)) * Site.Hardness : 0f;
            }
            grind.volume = Mathf.MoveTowards(grind.volume, InContact && power > 0f ? 0.25f + 0.35f * power : 0f, deltaTime * 6f);
            spin += power * 1400f * deltaTime;
            if (bit != null) bit.localRotation = Quaternion.Euler(0f, 0f, spin);
            ShowZone(power);
            return power;
        }

        // 顶在孔底时震动更重，并按地层硬度加一点颗粒感。
        protected override float RumbleAmplitude(float effort)
            => InContact ? Mathf.Clamp01(0.25f + 0.4f * effort * (Site != null ? Site.Hardness : 1f) * UnityEngine.Random.Range(0.8f, 1.2f)) : 0.08f + 0.15f * effort;

        protected override void OnOverheat()
        {
            base.OnOverheat();
            FeedbackSounds.Play(oneShot, FeedbackSound.Clunk, 0.8f);
            Jammed?.Invoke(this);
        }

        private static DrillSite Nearest(Vector3 tip)
        {
            DrillSite best = null; float bestDistance = 0.6f;
            foreach (var site in DrillSite.All)
            {
                if (site == null || site.Completed) continue;
                float d = Vector2.Distance(new Vector2(tip.x, tip.z), new Vector2(site.Surface.x, site.Surface.z));
                if (d < bestDistance) { bestDistance = d; best = site; }
            }
            return best;
        }

        private void ShowZone(float power)
        {
            if (zoneLamp != null)
                zoneLamp.State = Overheated ? LampState.Fault : !InContact ? LampState.Off : Zone == 0 ? LampState.Done : Zone > 0 ? LampState.Fault : LampState.Next;
            if (rpmBar != null)
            {
                rpmBar.transform.localScale = new Vector3(rpmBar.transform.localScale.x, Mathf.Max(0.001f, power) * 0.08f, rpmBar.transform.localScale.z);
                block ??= new MaterialPropertyBlock();
                var color = Overheated ? new Color(1f, 0.1f, 0.05f) : Zone == 0 ? new Color(0.2f, 1f, 0.45f) : Zone > 0 ? new Color(1f, 0.25f, 0.1f) : new Color(1f, 0.7f, 0.15f);
                rpmBar.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_EmissionColor", color * 1.5f);
                rpmBar.SetPropertyBlock(block);
            }
            if (depthReadout != null)
                depthReadout.text = Site != null ? Mathf.RoundToInt(Site.Depth * 100f) + " / " + Mathf.RoundToInt(Site.TargetDepth * 100f) + " cm" : "-- cm";
        }
    }
}
