using System;
using UnityEngine;

namespace LunarEscape
{
    // 把“正确工具被拿着且工具头足够近”转换为任务可使用的布尔条件。
    // 使用明确的引用和距离检测，不依赖全场景搜索或碰撞触发顺序。
    [RequireComponent(typeof(TimedRepairTask))]
    public sealed class RepairContact : MonoBehaviour
    {
        [SerializeField] private RepairTool[] tools;
        [SerializeField] private Transform repairPoint;
        [SerializeField] private string requiredToolType = "maintenance";
        [SerializeField, Min(0.01f)] private float contactRadius = 0.14f;
        private TimedRepairTask task;

        public Transform RepairPoint => repairPoint;
        public float ContactRadius => contactRadius;

        private void Awake() => task = GetComponent<TimedRepairTask>();

        private void Update()
        {
            if (task != null && task.isActiveAndEnabled)
                task.Tick(HasValidContact(), Time.deltaTime);
        }

        public bool HasValidContact()
        {
            // 场景引用尚未连接时保持等待，避免空引用或意外推进进度。
            if (repairPoint == null || tools == null || string.IsNullOrEmpty(requiredToolType))
                return false;

            float radiusSquared = contactRadius * contactRadius;
            foreach (var tool in tools)
            {
                if (tool == null || !tool.isActiveAndEnabled || !tool.IsHeld ||
                    !string.Equals(tool.ToolType, requiredToolType, StringComparison.Ordinal)) continue;

                // 比较距离平方可省去开平方运算；范围单位仍是 Unity 米。
                if ((tool.Tip.position - repairPoint.position).sqrMagnitude <= radiusSquared)
                    return true;
            }
            return false;
        }

        public void Configure(Transform point, RepairTool[] candidates, string requiredType, float radius)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius), "维修接触半径必须为有限正数。");

            repairPoint = point;
            tools = candidates;
            requiredToolType = requiredType ?? string.Empty;
            contactRadius = radius;
        }

        private void OnValidate()
        {
            if (float.IsNaN(contactRadius) || float.IsInfinity(contactRadius))
                contactRadius = 0.14f;
            contactRadius = Mathf.Max(0.01f, contactRadius);
        }
    }
}
