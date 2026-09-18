using Unity.XR.CoreUtils;
using UnityEngine;

namespace LunarEscape
{
    // 小物资包跟随身体水平位置，低头时不会翻到眼前，蹲下时仍可触及。
    [DefaultExecutionOrder(-10)]
    public sealed class CargoPackFollower : MonoBehaviour
    {
        [SerializeField] private XROrigin player;
        private Vector3 lastForward = Vector3.forward;
        public void Configure(XROrigin origin) { player = origin; Follow(); }
        private void LateUpdate() => Follow();
        private void Follow()
        {
            if (player == null || player.Camera == null) return;
            var head = player.Camera.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.1f) lastForward = forward.normalized;
            var rotation = Quaternion.LookRotation(lastForward, Vector3.up);
            float height = Mathf.Clamp(head.position.y - player.Origin.transform.position.y - 0.7f, 0.4f, 1.05f);
            var anchor = new Vector3(head.position.x, player.Origin.transform.position.y + height, head.position.z);
            // 放在正前下方，避免玩家转头找左侧口袋时，口袋又跟着向左逃开。
            transform.SetPositionAndRotation(anchor + rotation * new Vector3(0f, 0f, 0.45f), rotation);
        }
    }
}
