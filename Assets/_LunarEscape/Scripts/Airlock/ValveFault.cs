using UnityEngine;

namespace LunarEscape
{
    // 故障二：气闸内外压差未平衡，压差存在时舱门在物理上推不开。
    // 转动平衡阀手轮，压力表指针进入绿区即修好；拧过头会触发泄压（嘶嘶声、红区），需要往回拧。
    public sealed class ValveFault : AirlockFault
    {
        [SerializeField] private ValveWheel valve;
        [SerializeField] private Transform needle;
        [Tooltip("指针从 0 到满量程的转角（度），绕指针局部 Z。")]
        [SerializeField] private float needleSweep = 240f;
        [Tooltip("绿区：手轮累计转角范围（度）。默认约两圈。")]
        [SerializeField] private Vector2 greenZone = new(630f, 810f);
        [SerializeField] private CockpitLamp lamp;
        [SerializeField] private bool randomizeEachAttempt;
        [SerializeField] private TMPro.TMP_Text turnHint;
        private System.Random random;
        public int TurnDirection { get; private set; } = 1;
        public float ProgressAngle => valve == null ? 0 : valve.Angle * TurnDirection;
        public bool Randomized => randomizeEachAttempt;
        public float OxygenLeakPerSecond => CanWork && OverPressure ? 2.5f : 0f;
        private Quaternion needleRest;
        private bool captured;
        private AudioSource hiss;
        private bool wasFixed;
        private bool wasOver;

        public ValveWheel Valve => valve;
        public Vector2 GreenZone => greenZone;
        public override bool IsFixed => valve != null && ProgressAngle >= greenZone.x && ProgressAngle <= greenZone.y;
        public bool OverPressure => valve != null && (ProgressAngle > greenZone.y || randomizeEachAttempt && ProgressAngle < -greenZone.y);

        // 显示用的压差（千帕）：全关时 18 kPa，进入绿区后接近 0。
        public float DifferentialKPa => valve == null ? 18f : Mathf.Max(0f, 18f * (1f - ProgressAngle / greenZone.x));

        public void ConfigureRandomization(TMPro.TMP_Text hint)
        { randomizeEachAttempt = true; turnHint = hint; }
        public void SetRandomSeed(int seed) => random = new System.Random(seed);

        public void Configure(ValveWheel wheel, Transform gaugeNeedle, CockpitLamp indicator)
        {
            valve = wheel; needle = gaugeNeedle; lamp = indicator;
            Capture();
        }

        private void Awake()
        {
            Capture();
            hiss = FeedbackSounds.CreateLoop(valve.transform, "Pressure Relief Hiss", FeedbackSound.ThrusterLoop, 1f);
            if (randomizeEachAttempt) ResetFault();
        }

        private void Capture()
        {
            if (captured || needle == null) return;
            needleRest = needle.localRotation;
            captured = true;
        }

        private void OnEnable() { if (!hiss.isPlaying) hiss.Play(); }
        private void OnDisable() => hiss.volume = 0f;

        private void Update()
        {
            valve.Enabled = CanWork;
            if (needle != null)
                // Keep the physical green band meaningful for every randomized target.
                needle.localRotation = needleRest * Quaternion.AngleAxis(-needleSweep * Mathf.Clamp01(GaugeAngle() / valve.MaxAngle), Vector3.forward);
            if (turnHint != null)
                turnHint.text = (TurnDirection > 0 ? "CCW" : "CW") + "  " + Mathf.RoundToInt(Mathf.Max(0, ProgressAngle)) + "° / "
                    + Mathf.RoundToInt(greenZone.x) + "–" + Mathf.RoundToInt(greenZone.y) + "°";
            bool over = OverPressure;
            // 泄压阀在超压时持续嘶嘶作响，往回拧到绿区后停止。
            hiss.volume = Mathf.MoveTowards(hiss.volume, over ? 0.35f : 0f, Time.deltaTime * 2f);
            if (lamp != null) lamp.State = IsFixed ? LampState.Done : over ? LampState.Fault : CanWork ? LampState.Next : LampState.Off;
            bool fixedNow = IsFixed;
            if (fixedNow != wasFixed || over != wasOver)
            {
                wasFixed = fixedNow; wasOver = over;
                RaiseChanged();
            }
        }

        public override void ResetFault()
        {
            if (randomizeEachAttempt)
            {
                random ??= new System.Random();
                TurnDirection = random.Next(2) == 0 ? -1 : 1;
                float center = random.Next(4, 11) * 45f;
                greenZone = new Vector2(center - 55f, center + 55f);
                valve.ConfigureAttempt(true, (float)random.NextDouble() * 360f);
            }
            else valve.ResetAngle();
            wasFixed = wasOver = false;
            RaiseChanged();
        }

        private float GaugeAngle()
        {
            if (!randomizeEachAttempt) return ProgressAngle;
            if (ProgressAngle <= greenZone.x) return Mathf.Max(0, ProgressAngle) / greenZone.x * 630f;
            if (ProgressAngle <= greenZone.y) return Mathf.Lerp(630f, 810f, Mathf.InverseLerp(greenZone.x, greenZone.y, ProgressAngle));
            return Mathf.Lerp(810f, 1080f, Mathf.InverseLerp(greenZone.y, valve.MaxAngle, ProgressAngle));
        }
    }
}
