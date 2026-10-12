using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 手持取芯钻：把钻头竖直顶在取芯点的孔底，扳机控制转速。
    //   · 转速落在当前地层的合适区间（指示灯绿）钻得最快、几乎不发热；
    //   · 太慢（琥珀）钻头打滑、几乎不进；太快（红）钻得稍快但迅速发热，过热会卡钻，需松开扳机冷却；
    //   · 越往下越硬：区间上移、发热更快。
    // 手持姿态：钻像挂在把手上的重物一样始终竖直，只跟随手的朝向（绕竖直轴），手腕怎么转都不会把钻拧歪；
    // 靠近取芯点（定位环附近）时自动落进导向套、竖直插进孔里，之后只需扣扳机，钻随孔深自动下沉；
    // 手离开孔位或明显抬起就脱开。键鼠模拟器和 VR 用同一套规则。
    public sealed class CoreDrill : TriggerTool
    {
        [SerializeField] private CockpitLamp zoneLamp;
        [Tooltip("转速条：按扳机深度缩放、按区间着色。")]
        [SerializeField] private Renderer rpmBar;
        [SerializeField] private TMPro.TMP_Text depthReadout;
        [SerializeField] private Transform bit;
        [SerializeField, Min(0.01f)] private float metersPerSecond = 0.09f;
        [SerializeField, Range(0f, 60f)] private float maxTiltDegrees = 35f;
        [Tooltip("钻头离定位环中心多近（水平）时落进导向套。")]
        [SerializeField, Min(0.05f)] private float captureRadius = 0.45f;
        [Tooltip("钻头离地面多高以内才会落进导向套。")]
        [SerializeField, Min(0.05f)] private float captureHeight = 0.7f;
        [Tooltip("手离开孔位（水平）超过此距离就脱开。")]
        [SerializeField, Min(0.1f)] private float releaseDistance = 0.8f;
        private bool savedTracking, trackPosition, trackRotation;
        private Transform hand;
        private float seatedYaw;
        private AudioSource grind;
        private MaterialPropertyBlock block;
        private float contactHeat;
        private float spin;

        public DrillSite Site { get; private set; }
        // 当前插在其导向套里的取芯点；没插时为空。
        public DrillSite Seated { get; private set; }
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

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!grind.isPlaying) grind.Play();
            grab.selectEntered.AddListener(OnHandGrab);
            grab.selectExited.AddListener(OnHandRelease);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            grind.volume = 0f;
            grab.selectEntered.RemoveListener(OnHandGrab);
            grab.selectExited.RemoveListener(OnHandRelease);
            RestoreTracking();
        }

        // 手拿起时由本组件接管姿态（关闭抓取组件自己的位置/旋转跟随）；插回工具架时仍由插座摆放。
        private void OnHandGrab(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            if (!savedTracking) { trackPosition = grab.trackPosition; trackRotation = grab.trackRotation; savedTracking = true; }
            grab.trackPosition = false; grab.trackRotation = false;
            hand = args.interactorObject.GetAttachTransform(grab);
            Seated = null;
        }

        private void OnHandRelease(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            RestoreTracking();
        }

        private void RestoreTracking()
        {
            if (savedTracking) { grab.trackPosition = trackPosition; grab.trackRotation = trackRotation; savedTracking = false; }
            hand = null; Seated = null;
        }

        protected override void Update()
        {
            if (hand != null && IsHeld) Pose();
            base.Update();
        }

        private void Pose()
        {
            var tipLocal = transform.InverseTransformPoint(Muzzle.position);
            var attach = grab.attachTransform;
            if (Seated != null)
            {
                var flat = new Vector2(hand.position.x - Seated.Surface.x, hand.position.z - Seated.Surface.z);
                bool lifted = hand.position.y > Seated.Surface.y - tipLocal.y + 0.8f;
                if (Seated.Completed || flat.magnitude > releaseDistance || lifted) Seated = null;
            }
            if (Seated != null)
            {
                // 插在孔里：钻头抵住孔底，随深度下沉。
                transform.rotation = Quaternion.Euler(0f, seatedYaw, 0f);
                transform.position = new Vector3(Seated.Surface.x, Seated.Bottom, Seated.Surface.z) - transform.rotation * tipLocal;
                return;
            }
            // 在手里：始终竖直，只跟随手的朝向。
            float yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(hand.forward, Vector3.up).sqrMagnitude > 0.001f ? Vector3.ProjectOnPlane(hand.forward, Vector3.up) : Vector3.forward).eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Inverse(attach.localRotation);
            transform.position = hand.position - transform.rotation * attach.localPosition;
            var tip = Muzzle.position;
            foreach (var site in DrillSite.All)
            {
                if (site == null || site.Completed) continue;
                var d = new Vector2(tip.x - site.Surface.x, tip.z - site.Surface.z);
                if (d.magnitude > captureRadius || tip.y > site.Surface.y + captureHeight) continue;
                Seated = site; seatedYaw = yaw;
                FeedbackSounds.Play(oneShot, FeedbackSound.Clunk, 0.7f);
                HandHaptics.Pulse(HoldingHand, 0.6f, 0.15f);
                Pose();
                return;
            }
        }

        protected override float HeatPerSecond(float power) => contactHeat;

        protected override float Work(float power, float deltaTime)
        {
            var tip = Muzzle;
            Site = Seated != null ? Seated : Nearest(tip.position);
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
            if (bit != null) bit.localRotation = Quaternion.Euler(0f, spin, 0f);
            ShowZone(power);
            return power;
        }

        // 冲击钻声音偏低沉：转速只把音高从 0.75 推到 1.1，响度更大。
        protected override float LoopPitch(float effort) => 0.75f + 0.35f * effort;
        protected override float LoopVolume(float effort) => 0.35f + 0.5f * effort;

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
