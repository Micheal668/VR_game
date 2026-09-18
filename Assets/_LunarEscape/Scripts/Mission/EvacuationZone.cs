using System;
using UnityEngine;

namespace LunarEscape
{
    // 只检查指定玩家身体的中心，工具或手柄进入区域不会触发撤离。
    public sealed class EvacuationZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider volume;
        [SerializeField] private CharacterController playerBody;

        public BoxCollider Volume => volume;
        public CharacterController PlayerBody => playerBody;

        public bool ContainsPlayer
        {
            get
            {
                if (!isActiveAndEnabled || volume == null || playerBody == null ||
                    !volume.enabled || !volume.gameObject.activeInHierarchy ||
                    !playerBody.enabled || !playerBody.gameObject.activeInHierarchy) return false;

                Vector3 size = volume.size;
                Vector3 scale = volume.transform.lossyScale;
                if (!HasFiniteComponents(size) || !HasFiniteComponents(scale) ||
                    size.x <= 0f || size.y <= 0f || size.z <= 0f ||
                    scale.x == 0f || scale.y == 0f || scale.z == 0f) return false;

                // 在盒子自身坐标系内判断，支持旋转和缩放，也支持直接传送进区。
                Vector3 worldCenter = playerBody.transform.TransformPoint(playerBody.center);
                Vector3 localOffset = volume.transform.InverseTransformPoint(worldCenter) - volume.center;
                Vector3 halfSize = size * 0.5f;
                return HasFiniteComponents(localOffset) && Mathf.Abs(localOffset.x) <= halfSize.x &&
                    Mathf.Abs(localOffset.y) <= halfSize.y && Mathf.Abs(localOffset.z) <= halfSize.z;
            }
        }

        public void Configure(BoxCollider bounds, CharacterController body)
        {
            if (bounds == null) throw new ArgumentNullException(nameof(bounds));
            if (body == null) throw new ArgumentNullException(nameof(body));
            volume = bounds;
            playerBody = body;
        }

        private static bool HasFiniteComponents(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
