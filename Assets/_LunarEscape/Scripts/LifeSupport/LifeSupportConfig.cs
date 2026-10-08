using UnityEngine;

namespace LunarEscape
{
    // 百分比对应可调的游戏时长；月面极值作为热环境参考，变化速度为玩法压缩值。
    [CreateAssetMenu(fileName = "Life Support Config", menuName = "Lunar Escape/Life Support Config")]
    public sealed class LifeSupportConfig : ScriptableObject
    {
        [Header("每轮初始余量（百分比）")]
        [SerializeField] private Vector2 baseOxygenRange = new(30, 55);
        [SerializeField] private Vector2 basePowerRange = new(35, 65);
        [SerializeField] private Vector2 suitOxygenRange = new(20, 50);
        [SerializeField] private Vector2 suitPowerRange = new(20, 50);
        [Header("满储量支持秒数")]
        [SerializeField, Min(1)] private float baseOxygenSeconds = 240;
        [SerializeField, Min(1)] private float basePowerSeconds = 300;
        [SerializeField, Min(1)] private float suitOxygenSeconds = 240;
        [SerializeField, Min(1)] private float suitPowerSeconds = 300;
        [Header("穿戴与失效")]
        [SerializeField, Range(3, 5)] private float donSeconds = 4;
        [SerializeField, Min(.1f)] private float doorRepairSeconds = 5;
        [SerializeField, Min(1)] private float suffocationSeconds = 6;
        [SerializeField, Min(1)] private float unpoweredOxygenMultiplier = 2.5f;
        [SerializeField] private float daylightTemperature = 127;
        [SerializeField] private float nightTemperature = -173;
        [SerializeField, Min(.01f)] private float exposedBodyDegreesPerSecond = .55f;
        [SerializeField, Min(.01f)] private float baseDegreesPerSecond = 9;
        [Header("补给")]
        [SerializeField, Range(1, 100)] private float oxygenRefillPercent = 60;
        [SerializeField, Range(1, 100)] private float batteryRefillPercent = 60;
        public Vector2 BaseOxygenRange => Range(baseOxygenRange);
        public Vector2 BasePowerRange => Range(basePowerRange);
        public Vector2 SuitOxygenRange => Range(suitOxygenRange);
        public Vector2 SuitPowerRange => Range(suitPowerRange);
        public float BaseOxygenRate => 100 / Mathf.Max(1, baseOxygenSeconds);
        public float BasePowerRate => 100 / Mathf.Max(1, basePowerSeconds);
        public float SuitOxygenRate => 100 / Mathf.Max(1, suitOxygenSeconds);
        public float SuitPowerRate => 100 / Mathf.Max(1, suitPowerSeconds);
        public float DonSeconds => Mathf.Clamp(donSeconds, 3, 5);
        public float DoorRepairSeconds => Mathf.Max(.1f, doorRepairSeconds);
        public float SuffocationSeconds => Mathf.Max(1, suffocationSeconds);
        public float UnpoweredOxygenMultiplier => Mathf.Max(1, unpoweredOxygenMultiplier);
        public float DaylightTemperature => daylightTemperature;
        public float NightTemperature => nightTemperature;
        public float ExposedBodyDegreesPerSecond => Mathf.Max(.01f, exposedBodyDegreesPerSecond);
        public float BaseDegreesPerSecond => Mathf.Max(.01f, baseDegreesPerSecond);
        public float OxygenRefillPercent => Mathf.Clamp(oxygenRefillPercent, 1, 100);
        public float BatteryRefillPercent => Mathf.Clamp(batteryRefillPercent, 1, 100);
        private static Vector2 Range(Vector2 value)
        {
            float a = float.IsFinite(value.x) ? Mathf.Clamp(value.x, 1, 100) : 20;
            float b = float.IsFinite(value.y) ? Mathf.Clamp(value.y, 1, 100) : 50;
            return new Vector2(Mathf.Min(a, b), Mathf.Max(a, b));
        }
    }
}
