using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LunarEscape
{
    // 只适配编辑器模拟器：身体移动走玩家碰撞系统，官方组件继续处理转头和手柄。
    // 顺序与官方模拟器一致，确保移动先排队，再由 XRBodyTransformer 在本帧执行。
    [DefaultExecutionOrder(-29991)]
    public sealed class CollisionAwareSimulator : XRInteractionSimulator
    {
        [SerializeField] private XRBodyTransformer bodyTransformer;
        [SerializeField] private Transform view;
        [SerializeField, Tooltip("手柄操控模式下 WASD 仍用于行走，Q/E 保留手柄上下移动。")]
        private bool walkInControllerMode = true;
        private readonly XROriginMovement movement = new XROriginMovement { forceUnconstrained = false };
        private bool configurationErrorReported;
        // 结算时只停止模拟行走，头手追踪和 UI 输入仍由官方模拟器处理。
        public bool BodyMovementEnabled { get; set; } = true;

        public void Configure(XRBodyTransformer transformer, Transform camera)
        {
            bodyTransformer = transformer;
            view = camera;
            configurationErrorReported = false;
        }

        protected override void ProcessPoseInput()
        {
            // HMD 可与手柄标志组合；只要包含头部，就必须接管它的平移。
            bool movingBody = currentState.manipulatingFPS
                || (currentState.targetedDeviceInput & TargetedDevices.HMD) != 0;
            if (!movingBody && !walkInControllerMode)
            {
                // 单独操纵手柄时，保留 Q/E 上下移动工具等官方行为。
                base.ProcessPoseInput();
                return;
            }

            float xSpeed = translateXSpeed;
            float ySpeed = translateYSpeed;
            float zSpeed = translateZSpeed;
            float xInput = translateXInput.ReadValue();
            float zInput = translateZInput.ReadValue();

            // 禁止官方直接挪动模拟头显，避免绕过胶囊体穿墙。
            // 只在这一调用期间归零速度；旋转、追踪状态和后续手柄操作保持可用。
            try
            {
                translateXSpeed = translateZSpeed = 0f;
                if (movingBody) translateYSpeed = 0f;
                base.ProcessPoseInput();
            }
            finally
            {
                translateXSpeed = xSpeed;
                translateYSpeed = ySpeed;
                translateZSpeed = zSpeed;
            }

            if (!BodyMovementEnabled || !HasCollisionMovement()) return;
            // 沿用官方 FPS 首秒等待，避免模拟设备初始化时产生意外位移。
            if (currentState.manipulatingFPS && Time.time <= 1f) return;

            var up = view.parent != null ? view.parent.up : Vector3.up;
            var forward = Vector3.ProjectOnPlane(view.forward, up);
            // 直视天花板或地板时，前方投影接近零；用相机上方恢复稳定的水平朝向。
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(-view.up, up);
            forward.Normalize();
            var right = Vector3.Cross(up, forward);

            // 身体仅作水平移动；高度交给已有重力系统，Q/E 不再让玩家钻入地板。
            movement.motion = (right * (xInput * xSpeed) + forward * (zInput * zSpeed))
                * (bodyTranslateMultiplier * Time.deltaTime);
            if (movement.motion.sqrMagnitude > 0f)
                bodyTransformer.QueueTransformation(movement);
        }

        private bool HasCollisionMovement()
        {
            // 缺少碰撞约束时停止移动，不能悄悄退回直接修改 Transform 的无碰撞路径。
            bool available = view != null && bodyTransformer != null && bodyTransformer.isActiveAndEnabled
                && bodyTransformer.constrainedBodyManipulator != null;
            if (available && bodyTransformer.constrainedBodyManipulator is CharacterControllerBodyManipulator capsule)
                available = capsule.characterController != null && capsule.characterController.enabled
                    && capsule.characterController.gameObject.activeInHierarchy;

            if (!available && !configurationErrorReported)
            {
                Debug.LogError("模拟行走已停止：请连接相机与 XRBodyTransformer，并启用玩家碰撞约束。", this);
                configurationErrorReported = true;
            }
            return available;
        }
    }
}
