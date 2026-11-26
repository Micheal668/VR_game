using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LunarEscape
{
    // Physical steps arrive as head poses, outside XRI's usual locomotion queue.
    [DisallowMultipleComponent, DefaultExecutionOrder(-220)]
    public sealed class RoomScaleMovement : LocomotionProvider
    {
        private Vector3 headAnchor;
        private bool hasAnchor;

        protected override void OnEnable()
        {
            base.OnEnable();
            hasAnchor = false;
            Application.onBeforeRender += ApplyTrackedStep;
        }

        protected override void OnDisable()
        {
            Application.onBeforeRender -= ApplyTrackedStep;
            base.OnDisable();
        }

        private void Update() => ApplyTrackedStep();

        // Also consume the late pose that the XR input system supplies before rendering.
        [BeforeRenderOrder(100)]
        private void ApplyTrackedStep()
        {
            var transformer = mediator != null ? mediator.bodyTransformer : null;
            if (transformer == null || !transformer.isActiveAndEnabled ||
                transformer.constrainedBodyManipulator is not CharacterControllerBodyManipulator manipulator ||
                manipulator.characterController == null || !manipulator.characterController.enabled)
            {
                hasAnchor = false;
                return;
            }

            var origin = transformer.xrOrigin;
            var head = origin.CameraInOriginSpacePos;
            if (!hasAnchor)
            {
                headAnchor = head;
                hasAnchor = true;
                return;
            }

            var localStep = head - headAnchor;
            localStep.y = 0f;
            var motion = origin.Origin.transform.TransformVector(localStep);
            float threshold = Mathf.Max(0.0001f, manipulator.characterController.minMoveDistance);
            if (motion.sqrMagnitude <= threshold * threshold) return;

            // Leave the head and both controllers in their native tracking space. Undo the
            // raw step on their common parent, then sweep the whole body from its last
            // accepted position. Teleports and snap turns move the origin, not this anchor.
            origin.CameraFloorOffsetObject.transform.position -= motion;
            manipulator.MoveBody(motion);
        }
    }
}
