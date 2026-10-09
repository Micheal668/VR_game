using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 舱内实体操纵件的公共基础：抓住/按下由官方 XRI 交互器完成，这里负责记录持握的手、
    // 计算“手移动了多少”、悬停高亮以及声音和震动。具体规则（能否开锁等）由使用它的组件决定，
    // 操纵件本身不认识任务。
    public abstract class CockpitControl : XRBaseInteractable
    {
        [Tooltip("悬停或握住时高亮的可见部件。")]
        [SerializeField] private Renderer[] highlight = new Renderer[0];
        [SerializeField] private Color highlightColor = new(0.45f, 0.95f, 1f);
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private AudioSource source;
        private Transform grabAttach;
        private Vector3 grabPoint;
        private Quaternion grabRotation;
        private float rayLength;

        // 当前握住此操纵件的手；未握住时为空。飞行反馈据此给喷气震动。
        public HapticImpulsePlayer HoldingHand { get; private set; }

        // 正在产生推力等持续输出时为真，供飞行反馈判断是否震动。
        public virtual bool IsEngaged => false;

        protected override void Awake()
        {
            base.Awake();
            source = FeedbackSounds.CreateSource(transform, "Cockpit Control Audio", 1f);
        }

        public void ConfigureHighlight(Renderer[] renderers) => highlight = renderers ?? new Renderer[0];

        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);
            RefreshHighlight();
        }

        protected override void OnHoverExited(HoverExitEventArgs args)
        {
            base.OnHoverExited(args);
            RefreshHighlight();
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            HoldingHand = HandHaptics.FromInteractor(args.interactorObject);
            grabAttach = args.interactorObject.GetAttachTransform(this);
            // 近处直接握住时跟随手的位置；远处用射线选中时，把射线末端当作“手”，
            // 转动手柄即可拖动操纵件。键鼠模拟器的点选也走这一条路径。
            rayLength = MeasureRayLength();
            grabPoint = VirtualHandPoint();
            grabRotation = grabAttach.rotation;
            RefreshHighlight();
            OnGrabbed();
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            HoldingHand = null;
            grabAttach = null;
            RefreshHighlight();
            OnReleased();
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;
            if (isSelected && grabAttach != null) WhileHeld();
            else WhileFree(Time.deltaTime);
        }

        protected virtual void OnGrabbed() { }
        protected virtual void OnReleased() { }
        protected virtual void WhileHeld() { }
        protected virtual void WhileFree(float deltaTime) { }

        // 自握住以来手（或射线末端）的位移，以本操纵件的局部方向表示，单位米。
        // 操纵件根物体保持不动，只移动其中的可见部件，因此局部方向在握持期间稳定。
        protected Vector3 LocalHandOffset()
        {
            return grabAttach == null ? Vector3.zero : transform.InverseTransformDirection(VirtualHandPoint() - grabPoint);
        }

        // 自握住以来手的转动，以本操纵件的局部方向表示为“旋转向量”：方向是转轴，长度是角度（度）。
        protected Vector3 LocalHandRotation()
        {
            if (grabAttach == null) return Vector3.zero;
            Quaternion delta = grabAttach.rotation * Quaternion.Inverse(grabRotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || Mathf.Abs(angle) < 0.01f) return Vector3.zero;
            return transform.InverseTransformDirection(axis.normalized * angle);
        }

        // 手贴着操纵件（距最近碰撞面 10 cm 内）视为直接握住，射线长度为 0；
        // 否则沿手柄朝向打到操纵件的距离就是射线长度。大轮子、长拉杆也能正确区分近握和远选。
        private float MeasureRayLength()
        {
            Vector3 origin = grabAttach.position;
            float nearest = float.MaxValue;
            float hitDistance = float.MaxValue;
            var ray = new Ray(origin, grabAttach.forward);
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled) continue;
                nearest = Mathf.Min(nearest, Vector3.Distance(collider.ClosestPoint(origin), origin));
                if (collider.Raycast(ray, out var hit, 50f)) hitDistance = Mathf.Min(hitDistance, hit.distance);
            }
            if (nearest <= 0.1f) return 0f;
            return hitDistance < float.MaxValue ? hitDistance : Mathf.Max(0f, Vector3.Dot(transform.position - origin, grabAttach.forward));
        }

        // 当前“手”的世界坐标（近握为手，远处选中为射线末端）；未握住时为操纵件位置。
        protected Vector3 HandPoint => grabAttach != null ? VirtualHandPoint() : transform.position;

        private Vector3 VirtualHandPoint() => grabAttach.position + grabAttach.forward * rayLength;

        protected void Play(FeedbackSound sound, float scale = 1f) => FeedbackSounds.Play(source, sound, volume * scale);

        protected void Pulse(float amplitude, float seconds) => HandHaptics.Pulse(HoldingHand, amplitude, seconds);

        // 操作被规则拒绝时的统一反馈：“嗡嗡”声和两下震动。
        public void Deny()
        {
            Play(FeedbackSound.Deny, 0.8f);
            if (HoldingHand != null && isActiveAndEnabled) StartCoroutine(HandHaptics.DenyPattern(HoldingHand, 0.6f));
        }

        private void RefreshHighlight()
        {
            block ??= new MaterialPropertyBlock();
            bool lit = isHovered || isSelected;
            foreach (var renderer in highlight)
            {
                if (renderer == null) continue;
                if (lit)
                {
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColor, highlightColor);
                    renderer.SetPropertyBlock(block);
                }
                else renderer.SetPropertyBlock(null);
            }
        }
    }
}
