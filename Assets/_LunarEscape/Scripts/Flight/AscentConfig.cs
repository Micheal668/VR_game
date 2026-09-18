using System;
using UnityEngine;

namespace LunarEscape
{
    [CreateAssetMenu(fileName = "Ascent Config", menuName = "Lunar Escape/Ascent Config")]
    public sealed class AscentConfig : ScriptableObject
    {
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float blackSeconds = 0.15f;
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.45f;
        [SerializeField, Min(1)] private float initialOxygenSeconds = 130;
        [SerializeField, Min(1)] private float oxygenCapacitySeconds = 240;
        [SerializeField, Range(1, 100)] private float initialHealth = 100;
        [SerializeField, Range(1, 100)] private float initialPower = 100;
        [SerializeField, Min(0.1f)] private float navigationSeconds = 2;
        [SerializeField, Min(0.1f)] private float engineSeconds = 2;
        [SerializeField, Min(0.1f)] private float ignitionSeconds = 3;
        [SerializeField, Min(1)] private float recoverySeconds = 25;
        [SerializeField, Min(1)] private float ascentVisualSeconds = 30;
        [SerializeField, Min(1)] private float safeDepartureSeconds = 30;
        [SerializeField, Min(1)] private float lightDepartureSeconds = 12;
        [Header("第六步：简化月球入轨（旧教学场景默认关闭）")]
        [SerializeField] private bool orbitalFlight;
        [SerializeField, Min(30)] private float minimumOrbitAscentSeconds=80;
        [SerializeField, Min(10)] private float orbitalClimbSeconds=40;
        [SerializeField, Min(1)] private float blastObservationSeconds=6;
        [SerializeField, Min(1)] private float circularizationSeconds=6;
        [SerializeField, Min(20)] private float orbitAltitudeKm=100;
        public float FadeOutSeconds => fadeOutSeconds;
        public float BlackSeconds => blackSeconds;
        public float FadeInSeconds => fadeInSeconds;
        public float InitialOxygen => initialOxygenSeconds;
        public float OxygenCapacity => oxygenCapacitySeconds;
        public float InitialHealth => initialHealth;
        public float InitialPower => initialPower;
        public float NavigationSeconds => navigationSeconds;
        public float EngineSeconds => engineSeconds;
        public float IgnitionSeconds => ignitionSeconds;
        public float RecoverySeconds => recoverySeconds;
        public float AscentVisualSeconds => ascentVisualSeconds;
        public float SafeDepartureSeconds => safeDepartureSeconds;
        public float LightDepartureSeconds => lightDepartureSeconds;
        public bool OrbitalFlight=>orbitalFlight;
        public float MinimumOrbitAscentSeconds=>minimumOrbitAscentSeconds;
        public float OrbitalClimbSeconds=>orbitalClimbSeconds;
        public float BlastObservationSeconds=>blastObservationSeconds;
        public float CircularizationSeconds=>circularizationSeconds;
        public float OrbitAltitudeKm=>orbitAltitudeKm;
        public void ConfigureOrbit(bool enabled,float minimumSeconds=80)
        {
            if(!FinitePositive(minimumSeconds) || minimumSeconds<30)throw new ArgumentOutOfRangeException(nameof(minimumSeconds));
            orbitalFlight=enabled;minimumOrbitAscentSeconds=minimumSeconds;
        }

        public void ConfigureVitals(float oxygen, float health, float power)
        {
            if (!FinitePositive(oxygen) || oxygen > oxygenCapacitySeconds || !FinitePositive(health)
                || health > 100 || !FinitePositive(power) || power > 100) throw new ArgumentOutOfRangeException();
            initialOxygenSeconds = oxygen; initialHealth = health; initialPower = power;
        }
        public void ConfigureOxygenCapacity(float seconds)
        {
            if(!FinitePositive(seconds) || seconds<initialOxygenSeconds)throw new ArgumentOutOfRangeException(nameof(seconds));
            oxygenCapacitySeconds=seconds;
        }
        private static bool FinitePositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
        private void OnValidate()
        {
            fadeOutSeconds = Valid(fadeOutSeconds, 0.35f); blackSeconds = Valid(blackSeconds, 0.15f);
            fadeInSeconds = Valid(fadeInSeconds, 0.45f); oxygenCapacitySeconds = Valid(oxygenCapacitySeconds, 240);
            initialOxygenSeconds = Mathf.Min(Valid(initialOxygenSeconds, 130), oxygenCapacitySeconds);
            initialHealth = Mathf.Clamp(Valid(initialHealth, 100), 1, 100);
            initialPower = Mathf.Clamp(Valid(initialPower, 100), 1, 100);
            navigationSeconds = Valid(navigationSeconds, 2); engineSeconds = Valid(engineSeconds, 2);
            ignitionSeconds = Valid(ignitionSeconds, 3); recoverySeconds = Valid(recoverySeconds, 25);
            ascentVisualSeconds = Valid(ascentVisualSeconds, 30);
            lightDepartureSeconds = Valid(lightDepartureSeconds, 12);
            safeDepartureSeconds = Mathf.Max(lightDepartureSeconds, Valid(safeDepartureSeconds, 30));
            minimumOrbitAscentSeconds=Mathf.Max(30,Valid(minimumOrbitAscentSeconds,80));
            orbitalClimbSeconds=Mathf.Max(10,Valid(orbitalClimbSeconds,40));blastObservationSeconds=Valid(blastObservationSeconds,6);
            circularizationSeconds=Valid(circularizationSeconds,6);orbitAltitudeKm=Mathf.Max(20,Valid(orbitAltitudeKm,100));
        }
        private static float Valid(float value, float fallback) => FinitePositive(value) ? value : fallback;
    }
}
