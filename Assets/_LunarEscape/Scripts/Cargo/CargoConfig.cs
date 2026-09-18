using System;
using UnityEngine;

namespace LunarEscape
{
    public enum CargoKind { Oxygen, RepairKit, Battery, MedicalKit, DataCore, LunarSample }
    public enum CargoState { World, Held, Packed, Loaded, Consumed }
    public enum CargoRejection
    {
        None, WrongPhase, TypeLimit, WeightLimit, QuantityLimit,
        InvalidItem, NotHeld, NotStored, NotAtShip
    }

    [CreateAssetMenu(fileName = "Cargo Config", menuName = "Lunar Escape/Cargo Config")]
    public sealed class CargoConfig : ScriptableObject
    {
        [Serializable]
        private struct Rule
        {
            public CargoKind kind;
            public float weightKg;
            public int maxQuantity;

            public Rule(CargoKind kind, float weightKg, int maxQuantity)
            {
                this.kind = kind;
                this.weightKg = weightKg;
                this.maxQuantity = maxQuantity;
            }
        }

        [SerializeField, Range(1, 6)] private int maxTypes = 3;
        [SerializeField, Min(0.01f)] private float maxWeightKg = 12f;
        [SerializeField] private Rule[] rules = DefaultRules();

        public int MaxTypes => maxTypes;
        public float MaxWeightKg => maxWeightKg;
        public float GetWeightKg(CargoKind kind) => rules[FindRule(kind)].weightKg;
        public int GetMaxQuantity(CargoKind kind) => rules[FindRule(kind)].maxQuantity;

        public void Configure(int typeLimit, float weightLimitKg)
        {
            if (typeLimit < 1 || typeLimit > 6) throw new ArgumentOutOfRangeException(nameof(typeLimit));
            ValidateWeight(weightLimitKg, nameof(weightLimitKg));
            maxTypes = typeLimit;
            maxWeightKg = weightLimitKg;
        }

        public void ConfigureItem(CargoKind kind, float weightKg, int quantityLimit)
        {
            int index = FindRule(kind);
            ValidateWeight(weightKg, nameof(weightKg));
            if (quantityLimit < 1) throw new ArgumentOutOfRangeException(nameof(quantityLimit));
            rules[index] = new Rule(kind, weightKg, quantityLimit);
        }

        private int FindRule(CargoKind kind)
        {
            if (!Enum.IsDefined(typeof(CargoKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            for (int i = 0; i < rules.Length; i++)
                if (rules[i].kind == kind) return i;
            throw new InvalidOperationException("物资配置缺少类型：" + kind);
        }

        private static void ValidateWeight(float weight, string parameter)
        {
            if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight))
                throw new ArgumentOutOfRangeException(parameter, "物资重量必须为有限正数。");
        }

        private static Rule[] DefaultRules() => new[]
        {
            new Rule(CargoKind.Oxygen, 3f, 2), new Rule(CargoKind.RepairKit, 4f, 1),
            new Rule(CargoKind.Battery, 4f, 1), new Rule(CargoKind.MedicalKit, 2f, 1),
            new Rule(CargoKind.DataCore, 1f, 1), new Rule(CargoKind.LunarSample, 5f, 1)
        };

        private void OnValidate()
        {
            maxTypes = Mathf.Clamp(maxTypes, 1, 6);
            if (maxWeightKg <= 0f || float.IsNaN(maxWeightKg) || float.IsInfinity(maxWeightKg)) maxWeightKg = 12f;
            // 保留各类型的有效调参，同时补齐缺项，避免 Inspector 重排破坏类型对应关系。
            Rule[] valid = DefaultRules();
            if (rules != null)
                foreach (Rule rule in rules)
                {
                    int index = (int)rule.kind;
                    if (index < 0 || index >= valid.Length || rule.weightKg <= 0f ||
                        float.IsNaN(rule.weightKg) || float.IsInfinity(rule.weightKg) || rule.maxQuantity < 1) continue;
                    valid[index] = rule;
                }
            rules = valid;
        }
    }
}
