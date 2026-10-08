using System;
using UnityEngine;

namespace LunarEscape
{
    [CreateAssetMenu(fileName = "Crew Config", menuName = "Lunar Escape/Crew Config")]
    public sealed class CrewConfig : ScriptableObject
    {
        [Header("实体救援与登舱，单位为秒和米")]
        [SerializeField, Min(.1f)] private float rescueSeconds = 5;
        [SerializeField, Min(.1f)] private float interactionDistance = 2.2f;
        [SerializeField, Min(.1f)] private float boardingDistance = 2.2f;
        [SerializeField, Min(.1f)] private float sameFloorTolerance = 1.25f;
        [SerializeField, Min(.1f)] private float medicalContactDistance = .85f;
        [Header("指挥官生命值独立于玩家，不增加整舱氧气消耗")]
        [SerializeField, Range(1, 100)] private float repairedHealth = 80;
        [SerializeField, Range(1, 100)] private float unrepairedHealth = 55;
        [SerializeField, Min(0)] private float repairedInjuryPerSecond = .06f;
        [SerializeField, Min(0)] private float unrepairedInjuryPerSecond = .2f;
        [SerializeField, Range(1, 100)] private float medicalRestore = 40;
        [Header("升空后爆炸对已登舱指挥官的影响")]
        [SerializeField, Range(0, 100)] private float lightBlastDamage = 12;
        [SerializeField, Range(0, 100)] private float heavyBlastDamage = 32;
        [SerializeField, Min(0)] private float lightBlastInjuryPerSecond = .08f;
        [SerializeField, Min(0)] private float heavyBlastInjuryPerSecond = .35f;
        [SerializeField] private bool dockingCollisionKillsPilot = true;

        public float RescueSeconds => rescueSeconds;
        public float InteractionDistance => interactionDistance;
        public float BoardingDistance => boardingDistance;
        public float SameFloorTolerance => sameFloorTolerance;
        public float MedicalContactDistance => medicalContactDistance;
        public float RepairedHealth => repairedHealth;
        public float UnrepairedHealth => unrepairedHealth;
        public float RepairedInjuryPerSecond => repairedInjuryPerSecond;
        public float UnrepairedInjuryPerSecond => unrepairedInjuryPerSecond;
        public float MedicalRestore => medicalRestore;
        public float LightBlastDamage => lightBlastDamage;
        public float HeavyBlastDamage => heavyBlastDamage;
        public float LightBlastInjuryPerSecond => lightBlastInjuryPerSecond;
        public float HeavyBlastInjuryPerSecond => heavyBlastInjuryPerSecond;
        public bool DockingCollisionKillsPilot => dockingCollisionKillsPilot;

        public void ConfigureRescue(float seconds)
        {
            Positive(seconds, nameof(seconds));
            rescueSeconds = seconds;
        }

        public void ConfigureDistances(float interaction, float boarding, float floor, float medicalContact)
        {
            Positive(interaction, nameof(interaction)); Positive(boarding, nameof(boarding));
            Positive(floor, nameof(floor)); Positive(medicalContact, nameof(medicalContact));
            interactionDistance = interaction; boardingDistance = boarding;
            sameFloorTolerance = floor; medicalContactDistance = medicalContact;
        }

        public void ConfigureHealth(float repaired, float unrepaired, float repairedInjury, float unrepairedInjury, float treatment = 40)
        {
            Health(repaired, nameof(repaired)); Health(unrepaired, nameof(unrepaired)); Health(treatment, nameof(treatment));
            Nonnegative(repairedInjury, nameof(repairedInjury)); Nonnegative(unrepairedInjury, nameof(unrepairedInjury));
            repairedHealth = repaired; unrepairedHealth = unrepaired;
            repairedInjuryPerSecond = repairedInjury; unrepairedInjuryPerSecond = unrepairedInjury;
            medicalRestore = treatment;
        }

        public void ConfigureBlast(float lightDamage, float heavyDamage, float lightInjury, float heavyInjury, bool killsPilot = true)
        {
            Nonnegative(lightDamage, nameof(lightDamage)); Nonnegative(heavyDamage, nameof(heavyDamage));
            Nonnegative(lightInjury, nameof(lightInjury)); Nonnegative(heavyInjury, nameof(heavyInjury));
            if (lightDamage > 100 || heavyDamage > 100) throw new ArgumentOutOfRangeException();
            lightBlastDamage = lightDamage; heavyBlastDamage = heavyDamage;
            lightBlastInjuryPerSecond = lightInjury; heavyBlastInjuryPerSecond = heavyInjury;
            dockingCollisionKillsPilot = killsPilot;
        }

        private static void Positive(float value, string name)
        { if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
        private static void Nonnegative(float value, string name)
        { if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name); }
        private static void Health(float value, string name)
        { Positive(value, name); if (value > 100) throw new ArgumentOutOfRangeException(name); }
        private static float Valid(float value, float fallback, bool zero = false)
        { return float.IsFinite(value) && (zero ? value >= 0 : value > 0) ? value : fallback; }

        private void OnValidate()
        {
            rescueSeconds = Valid(rescueSeconds, 5); interactionDistance = Valid(interactionDistance, 2.2f);
            boardingDistance = Valid(boardingDistance, 2.2f); sameFloorTolerance = Valid(sameFloorTolerance, 1.25f);
            medicalContactDistance = Valid(medicalContactDistance, .85f);
            repairedHealth = Mathf.Clamp(Valid(repairedHealth, 80), 1, 100);
            unrepairedHealth = Mathf.Clamp(Valid(unrepairedHealth, 55), 1, 100);
            repairedInjuryPerSecond = Valid(repairedInjuryPerSecond, .06f, true);
            unrepairedInjuryPerSecond = Valid(unrepairedInjuryPerSecond, .2f, true);
            medicalRestore = Mathf.Clamp(Valid(medicalRestore, 40), 1, 100);
            lightBlastDamage = Mathf.Clamp(Valid(lightBlastDamage, 12, true), 0, 100);
            heavyBlastDamage = Mathf.Clamp(Valid(heavyBlastDamage, 32, true), 0, 100);
            lightBlastInjuryPerSecond = Valid(lightBlastInjuryPerSecond, .08f, true);
            heavyBlastInjuryPerSecond = Valid(heavyBlastInjuryPerSecond, .35f, true);
        }
    }
}
