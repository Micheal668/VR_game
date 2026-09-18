using System;
using UnityEngine;

namespace LunarEscape
{
    // 腰包只报告空间范围；是否收纳仍由清单按任务阶段和物品状态决定。
    public sealed class CargoPackZone : MonoBehaviour
    {
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private BoxCollider volume;
        public BoxCollider Volume => volume;

        public void Configure(CargoInventory owner, BoxCollider bounds)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (bounds == null) throw new ArgumentNullException(nameof(bounds));
            inventory = owner;
            volume = bounds;
            inventory.RegisterPackZone(this);
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.RegisterPackZone(this);
        }

        public bool Contains(CargoItem item)
        {
            if (!isActiveAndEnabled || volume == null || !volume.enabled ||
                !volume.gameObject.activeInHierarchy || item == null || !item.isActiveAndEnabled ||
                item.Inventory != inventory || item.Body == null) return false;
            Vector3 size = volume.size;
            Vector3 scale = volume.transform.lossyScale;
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f ||
                scale.x == 0f || scale.y == 0f || scale.z == 0f) return false;
            // XRI 的视觉 Transform 可能比插值刚体领先一帧；判断玩家实际看见的松手位置。
            Vector3 center = item.transform.TransformPoint(item.Body.centerOfMass);
            Vector3 point = volume.transform.InverseTransformPoint(center) - volume.center;
            // NaN 比较自然为 false；局部空间检查也能支持腰包随身体旋转。
            return Mathf.Abs(point.x) <= size.x * 0.5f && Mathf.Abs(point.y) <= size.y * 0.5f &&
                Mathf.Abs(point.z) <= size.z * 0.5f;
        }

        public bool IsNearOpening(CargoItem item)
        {
            if (!isActiveAndEnabled || volume == null || !volume.enabled || item == null || item.Inventory != inventory) return false;
            var point = volume.transform.InverseTransformPoint(item.transform.TransformPoint(item.Body.centerOfMass)) - volume.center;
            var half = volume.size * .5f;
            return Mathf.Abs(point.x) <= half.x + .12f && Mathf.Abs(point.z) <= half.z + .12f
                && point.y >= -half.y && point.y <= half.y + .65f;
        }
    }
}
