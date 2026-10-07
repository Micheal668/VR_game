using UnityEngine;

namespace LunarEscape
{
    // 手动开门拉杆：握住手柄向下、向自己拉。手柄跟随手绕枢轴转动；
    // 拉到底才算“拉下”，松手后弹回。气闸放行后由 AirlockRepair 锁在拉下位置。
    // 本物体局部：+Y 为拉杆静止时的朝向（向上），+Z 指向舱内，枢轴绕 +X 转。
    public sealed class ReleaseLever : CockpitControl
    {
        [SerializeField] private Transform arm;
        [SerializeField, Min(10f)] private float maxAngle = 110f;
        private Quaternion armRest;
        private bool captured;
        private float grabHandAngle;
        private float grabLeverAngle;
        private bool wasPulled;

        public float Angle { get; private set; }
        public bool Pulled => isSelected && Angle >= maxAngle * 0.9f;
        public bool LockedDown { get; set; }

        public void Configure(Transform movingArm, float limit)
        {
            arm = movingArm;
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
            if (captured || arm == null) return;
            armRest = arm.localRotation;
            captured = true;
        }

        protected override void OnGrabbed()
        {
            grabHandAngle = HandAngle();
            grabLeverAngle = Angle;
            Play(FeedbackSound.Grab, 0.7f);
        }

        protected override void OnReleased() => wasPulled = false;

        protected override void WhileHeld()
        {
            if (LockedDown) { Angle = maxAngle; Show(); return; }
            // 相对握住时的角度变化：从手柄任何位置握住都不会突然跳动。
            Angle = Mathf.Clamp(grabLeverAngle + Mathf.DeltaAngle(grabHandAngle, HandAngle()), 0f, maxAngle);
            bool pulled = Pulled;
            if (pulled && !wasPulled) { Play(FeedbackSound.Knock, 0.8f); Pulse(0.5f, 0.06f); }
            wasPulled = pulled;
            Show();
        }

        protected override void WhileFree(float deltaTime)
        {
            float target = LockedDown ? maxAngle : 0f;
            Angle = Mathf.MoveTowards(Angle, target, deltaTime * 240f);
            Show();
        }

        // 手在枢轴 YZ 平面内的方位角：正上方为 0°，正对舱内为 90°。
        private float HandAngle()
        {
            Vector3 local = transform.InverseTransformPoint(HandPoint);
            return Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg;
        }

        private void Show()
        {
            if (arm != null) arm.localRotation = armRest * Quaternion.AngleAxis(Angle, Vector3.right);
        }

        public void ResetLever()
        {
            LockedDown = false;
            Angle = 0f;
            Show();
        }
    }
}
