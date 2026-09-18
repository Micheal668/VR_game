using System;
using UnityEngine;

namespace LunarEscape
{
    [CreateAssetMenu(fileName = "StationMissionConfig", menuName = "Lunar Escape/Station Mission Config")]
    public sealed class StationMissionConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float repairWindowSeconds = 90f;
        [SerializeField, Min(0.01f)] private float stabilizedSeconds = 4f;
        [SerializeField, Min(0.01f)] private float baseEvacuationSeconds = 60f;
        [SerializeField, Min(0f)] private float repairBonusSeconds = 30f;

        public float RepairWindowSeconds => repairWindowSeconds;
        public float StabilizedSeconds => stabilizedSeconds;
        public float BaseEvacuationSeconds => baseEvacuationSeconds;
        public float RepairBonusSeconds => repairBonusSeconds;

        public void Configure(float repairWindow, float stabilized, float evacuation, float bonus)
        {
            // 先检查所有值，再统一赋值，避免错误参数留下半份配置。
            ValidateDuration(repairWindow, nameof(repairWindow));
            ValidateDuration(stabilized, nameof(stabilized));
            ValidateDuration(evacuation, nameof(evacuation));
            ValidateDuration(bonus, nameof(bonus), true);
            if (float.IsInfinity(evacuation + bonus))
                throw new ArgumentOutOfRangeException(nameof(bonus), "撤离总时长必须为有限数值。");

            repairWindowSeconds = repairWindow;
            stabilizedSeconds = stabilized;
            baseEvacuationSeconds = evacuation;
            repairBonusSeconds = bonus;
        }

        private static void ValidateDuration(float value, string parameter, bool allowZero = false)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || (!allowZero && value == 0f))
                throw new ArgumentOutOfRangeException(parameter,
                    allowZero ? "奖励时长必须为有限非负数。" : "阶段时长必须为有限正数。");
        }

        private void OnValidate()
        {
            // Inspector 中的手动输入也不能造成无穷计时或负时长。
            repairWindowSeconds = ValidatedInspectorDuration(repairWindowSeconds, 90f);
            stabilizedSeconds = ValidatedInspectorDuration(stabilizedSeconds, 4f);
            baseEvacuationSeconds = ValidatedInspectorDuration(baseEvacuationSeconds, 60f);
            if (float.IsNaN(repairBonusSeconds) || float.IsInfinity(repairBonusSeconds) ||
                float.IsInfinity(baseEvacuationSeconds + repairBonusSeconds))
                repairBonusSeconds = 30f;
            repairBonusSeconds = Mathf.Max(0f, repairBonusSeconds);
        }

        private static float ValidatedInspectorDuration(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0.01f, value);
        }
    }
}
