using System;
using UnityEngine;

namespace LunarEscape
{
    // 按键、拨动开关和保护盖：抓住（握持键或扳机）即“按下”。
    // 可见部件在“关”和“开”两个姿态之间平滑移动；姿态由按住状态或外部设置的锁定状态决定。
    public sealed class CockpitSwitch : CockpitControl
    {
        [Tooltip("会移动的可见部件：按键帽、拨杆或保护盖。")]
        [SerializeField] private Transform moving;
        [SerializeField] private Vector3 onOffset;
        [SerializeField] private Vector3 onEuler;
        [Tooltip("保护盖：每次按下在打开和关闭之间切换。")]
        [SerializeField] private bool toggleOnPress;
        [SerializeField, Range(0f, 1f)] private float pressAmplitude = 0.4f;
        private Vector3 offPosition;
        private Quaternion offRotation;
        private bool captured;

        // 按住期间为真；刹车、主推等“按住才有效”的操作读取它。
        public bool IsHeld { get; private set; }

        // 开关已经拨到“开”或保护盖已打开。拨动开关由任务状态决定，保护盖由自己切换。
        public bool Latched { get; set; }

        public override bool IsEngaged => IsHeld;

        public event Action<CockpitSwitch> Pressed;
        public event Action<CockpitSwitch> Released;

        public void Configure(Transform movingPart, Vector3 positionWhenOn, Vector3 rotationWhenOn, bool toggles)
        {
            moving = movingPart;
            onOffset = positionWhenOn;
            onEuler = rotationWhenOn;
            toggleOnPress = toggles;
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
            offPosition = moving.localPosition;
            offRotation = moving.localRotation;
            captured = true;
        }

        protected override void OnGrabbed()
        {
            IsHeld = true;
            if (toggleOnPress) Latched = !Latched;
            Play(FeedbackSound.Click);
            Pulse(pressAmplitude, 0.04f);
            Pressed?.Invoke(this);
        }

        protected override void OnReleased()
        {
            IsHeld = false;
            Released?.Invoke(this);
        }

        protected override void WhileHeld() => Animate(Time.deltaTime);
        protected override void WhileFree(float deltaTime) => Animate(deltaTime);

        private void Animate(float deltaTime)
        {
            if (moving == null) return;
            bool on = IsHeld && !toggleOnPress || Latched;
            float blend = 1f - Mathf.Exp(-deltaTime * 18f);
            Vector3 position = on ? offPosition + onOffset : offPosition;
            Quaternion rotation = on ? offRotation * Quaternion.Euler(onEuler) : offRotation;
            moving.SetLocalPositionAndRotation(Vector3.Lerp(moving.localPosition, position, blend),
                Quaternion.Slerp(moving.localRotation, rotation, blend));
        }
    }
}
