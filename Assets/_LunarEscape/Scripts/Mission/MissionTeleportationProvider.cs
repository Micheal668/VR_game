using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape
{
    // 官方传送组件停用后仍会保留请求；失败和重试必须主动清理，避免重开后突然补跳。
    public sealed class MissionTeleportationProvider : TeleportationProvider
    {
        public bool CanTeleport { get; set; } = true;

        public override bool QueueTeleportRequest(TeleportRequest request)
        {
            return CanTeleport && isActiveAndEnabled && base.QueueTeleportRequest(request);
        }

        public void CancelPendingTeleport()
        {
            validRequest = false;
            if (isLocomotionActive) TryEndLocomotion();
        }

        protected override void OnDisable()
        {
            CancelPendingTeleport();
            base.OnDisable();
        }
    }
}
