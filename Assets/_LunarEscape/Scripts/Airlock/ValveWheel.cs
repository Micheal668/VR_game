using UnityEngine;

namespace LunarEscape
{
    // 手轮：握住轮缘绕轮心转动。转角按手（或射线末端）绕轮轴扫过的角度累计，
    // 可转多圈；每 20° 一声棘轮并轻震，让手感到“转动了一格”。轮轴为本物体的局部 Z。
    public sealed class ValveWheel : CockpitControl
    {
        [SerializeField] private Transform wheel;
        [SerializeField, Min(10f)] private float maxAngle = 1080f;
        [SerializeField, Min(5f)] private float tickDegrees = 20f;
        private float lastHandAngle;
        private bool tracking;
        private float lastTick;
        private Quaternion restRotation;
        private bool captured;

        // 累计转角（度），0 为初始全关。
        public float Angle { get; private set; }
        public float MaxAngle => maxAngle;
        public bool Enabled { get; set; } = true;

        public void Configure(Transform visual, float limit)
        {
            wheel = visual;
            maxAngle = limit;
            Capture();
        }

        protected override void Awake()
        {
            base.Awake();
            Capture();
        }

        private void Capture()
        {
            if (captured || wheel == null) return;
            restRotation = wheel.localRotation;
            captured = true;
        }

        public void ResetAngle()
        {
            Angle = 0f;
            lastTick = 0f;
            ShowAngle();
        }

        protected override void OnGrabbed()
        {
            tracking = TryHandAngle(out lastHandAngle);
            Play(FeedbackSound.Grab, 0.6f);
        }

        protected override void WhileHeld()
        {
            if (!TryHandAngle(out float handAngle)) { tracking = false; return; }
            if (!tracking) { lastHandAngle = handAngle; tracking = true; return; }
            float delta = Mathf.DeltaAngle(lastHandAngle, handAngle);
            lastHandAngle = handAngle;
            if (!Enabled) return;
            float next = Mathf.Clamp(Angle + delta, 0f, maxAngle);
            if (Mathf.Approximately(next, Angle) && Mathf.Abs(delta) > 0.5f)
            {
                // 到头了：拧不动，给一下沉闷的顶住感。
                if (Time.time - lastStop > 0.4f) { Pulse(0.5f, 0.05f); Play(FeedbackSound.Knock, 0.5f); lastStop = Time.time; }
                return;
            }
            Angle = next;
            if (Mathf.Abs(Angle - lastTick) >= tickDegrees)
            {
                lastTick = Mathf.Round(Angle / tickDegrees) * tickDegrees;
                Play(FeedbackSound.Click, 0.5f);
                Pulse(0.25f, 0.025f);
            }
            ShowAngle();
        }

        private float lastStop;

        // 手离轮心太近时角度没有意义（例如握住轮毂），此时不累计。
        private bool TryHandAngle(out float angle)
        {
            Vector3 local = transform.InverseTransformPoint(HandPoint);
            angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            return new Vector2(local.x, local.y).sqrMagnitude > 0.03f * 0.03f;
        }

        private void ShowAngle()
        {
            // 顺着手转的方向显示：局部 X→Y 为正角，对应绕 +Z 旋转。
            if (wheel != null) wheel.localRotation = restRotation * Quaternion.AngleAxis(Angle, Vector3.forward);
        }
    }
}
