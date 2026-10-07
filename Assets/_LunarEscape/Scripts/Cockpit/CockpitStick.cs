using UnityEngine;

namespace LunarEscape
{
    public enum StickMode { Translation, Rotation }

    // RCS 手控器。平移：握住手柄向想去的方向推/拉/抬，手柄跟着移动一小段；
    // 姿态：握住圆球转动手腕，球跟着转。松手后弹回中位。
    // 输出 Axes 为三轴 -1..1（局部 X/Y/Z）；越过 engageThreshold 的轴视为接通该方向推进，
    // 接通/断开时有咔哒声与震动，让玩家不看面板也知道推进是否在工作。
    public sealed class CockpitStick : CockpitControl
    {
        [SerializeField] private StickMode mode;
        [SerializeField] private Transform moving;
        [Tooltip("平移手柄最大行程（米）。")]
        [SerializeField, Min(0.005f)] private float maxTravel = 0.06f;
        [Tooltip("姿态球最大转角（度）。")]
        [SerializeField, Min(1f)] private float maxAngle = 30f;
        [SerializeField, Range(0.05f, 0.95f)] private float engageThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float detentAmplitude = 0.35f;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 axes;
        private Vector3Int engaged;
        private bool captured;

        public StickMode Mode => mode;
        public Vector3 Axes => axes;

        // 每轴 -1、0 或 1：哪些方向的推进当前接通。
        public Vector3Int Engaged => engaged;
        public override bool IsEngaged => engaged != Vector3Int.zero;

        public void Configure(StickMode stickMode, Transform movingPart, float travel, float angle)
        {
            mode = stickMode;
            moving = movingPart;
            maxTravel = travel;
            maxAngle = angle;
            Capture();
        }

        protected override void Awake()
        {
            base.Awake();
            Capture();
        }

        private void Capture()
        {
            if (captured || moving == null) return;
            restPosition = moving.localPosition;
            restRotation = moving.localRotation;
            captured = true;
        }

        protected override void OnGrabbed() => Play(FeedbackSound.Grab, 0.7f);

        protected override void OnReleased()
        {
            axes = Vector3.zero;
            UpdateEngaged();
        }

        protected override void WhileHeld()
        {
            if (mode == StickMode.Translation)
            {
                Vector3 offset = Vector3.ClampMagnitude(LocalHandOffset(), maxTravel);
                axes = offset / maxTravel;
                if (moving != null) moving.localPosition = restPosition + offset;
            }
            else
            {
                Vector3 rotation = LocalHandRotation();
                axes = new Vector3(
                    Mathf.Clamp(rotation.x / maxAngle, -1f, 1f),
                    Mathf.Clamp(rotation.y / maxAngle, -1f, 1f),
                    Mathf.Clamp(rotation.z / maxAngle, -1f, 1f));
                if (moving != null)
                {
                    Vector3 shown = axes * maxAngle;
                    moving.localRotation = Quaternion.AngleAxis(shown.magnitude, shown.sqrMagnitude > 0.0001f ? shown.normalized : Vector3.up) * restRotation;
                }
            }
            UpdateEngaged();
        }

        protected override void WhileFree(float deltaTime)
        {
            if (moving == null) return;
            // 弹簧回中：只影响显示，输出在松手时已经清零。
            float blend = 1f - Mathf.Exp(-deltaTime * 12f);
            moving.SetLocalPositionAndRotation(Vector3.Lerp(moving.localPosition, restPosition, blend),
                Quaternion.Slerp(moving.localRotation, restRotation, blend));
        }

        private void UpdateEngaged()
        {
            var next = new Vector3Int(Step(axes.x, engaged.x), Step(axes.y, engaged.y), Step(axes.z, engaged.z));
            if (next == engaged) return;
            bool more = Mathf.Abs(next.x) + Mathf.Abs(next.y) + Mathf.Abs(next.z) >
                Mathf.Abs(engaged.x) + Mathf.Abs(engaged.y) + Mathf.Abs(engaged.z);
            engaged = next;
            // 接通方向时“咔”一下更明显，断开时轻一些。
            Play(FeedbackSound.Click, more ? 0.8f : 0.4f);
            Pulse(more ? detentAmplitude : detentAmplitude * 0.5f, 0.03f);
        }

        // 带回差的门限：接通需要越过门限，断开需要回到门限的 70% 以内，避免在边界抖动。
        private int Step(float value, int current)
        {
            float release = engageThreshold * 0.7f;
            if (current != 0 && Mathf.Abs(value) > release && Mathf.Sign(value) == current) return current;
            if (value > engageThreshold) return 1;
            if (value < -engageThreshold) return -1;
            return 0;
        }
    }
}
