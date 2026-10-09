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
        private Quaternion needleRest;
        private bool captured;
        private AudioSource hiss;
        private bool wasFixed;
        private bool wasOver;

        public ValveWheel Valve => valve;
        public Vector2 GreenZone => greenZone;
        public override bool IsFixed => valve != null && valve.Angle >= greenZone.x && valve.Angle <= greenZone.y;
        public bool OverPressure => valve != null && valve.Angle > greenZone.y;

        // 显示用的压差（千帕）：全关时 18 kPa，进入绿区后接近 0。
        public float DifferentialKPa => valve == null ? 18f : Mathf.Max(0f, 18f * (1f - valve.Angle / greenZone.x));

        public void Configure(ValveWheel wheel, Transform gaugeNeedle, CockpitLamp indicator)
        {
            valve = wheel; needle = gaugeNeedle; lamp = indicator;
            Capture();
        }

        private void Awake()
        {
            Capture();
            hiss = FeedbackSounds.CreateLoop(valve.transform, "Pressure Relief Hiss", FeedbackSound.ThrusterLoop, 1f);
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
                needle.localRotation = needleRest * Quaternion.AngleAxis(-needleSweep * Mathf.Clamp01(valve.Angle / valve.MaxAngle), Vector3.forward);
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
            valve.ResetAngle();
            wasFixed = wasOver = false;
            RaiseChanged();
        }
    }
}
