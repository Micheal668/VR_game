using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape
{
    // 每轮昼夜只改变光照与可见设备；危险判定始终由生命保障规则负责。
    public sealed class LifeSupportEnvironment : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private AscentMission flight;
        [SerializeField] private Light sun;
        [SerializeField] private Light[] shoulderLights = System.Array.Empty<Light>();
        [SerializeField] private GameObject shoulderLampRig;
        [SerializeField] private Light[] habitatLights;
        [Tooltip("基地照明总闸；为空时照明只受基地电量控制（旧行为）。")]
        [SerializeField] private HabitatBreaker breaker;
        private float[] normalIntensity;
        private float lastBrightness = -1f;
        public Light Sun => sun;
        public System.Collections.Generic.IReadOnlyList<Light> ShoulderLights => shoulderLights;
        public void Configure(LifeSupportMission source, AscentMission ascent, Light sunlight, Light[] lamps)
        {
            Unsubscribe(); life = source; flight = ascent; sun = sunlight; habitatLights = lamps;
            normalIntensity = null; CacheIntensities(); if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        public void ConfigureBreaker(HabitatBreaker mainBreaker) { breaker = mainBreaker; Refresh(); }
        // 合闸后的闪烁与渐亮需要逐帧更新；稳定后不再重复刷新。
        private void Update()
        {
            if (breaker == null) return;
            float brightness = breaker.Brightness;
            if (!Mathf.Approximately(brightness, lastBrightness)) Refresh();
        }
        public void ConfigureShoulderLights(GameObject rig, Light[] lamps)
        { shoulderLampRig = rig; shoulderLights = lamps; Refresh(); }
        private void OnEnable() { CacheIntensities(); Subscribe(); Refresh(); }
        private void OnDisable() => Unsubscribe();
        private void CacheIntensities()
        {
            if (habitatLights == null || normalIntensity != null) return;
            normalIntensity = new float[habitatLights.Length];
            for (int i = 0; i < habitatLights.Length; i++) normalIntensity[i] = habitatLights[i] != null ? habitatLights[i].intensity : 0;
        }
        private void Subscribe() { if (life != null) { life.Changed -= Refresh; life.Changed += Refresh; } if (flight != null) { flight.Changed -= Refresh; flight.Changed += Refresh; } }
        private void Unsubscribe() { if (life != null) life.Changed -= Refresh; if (flight != null) flight.Changed -= Refresh; }
        private void Refresh()
        {
            if (life == null || sun == null) return;
            sun.intensity = life.IsDaylight ? 1.9f : .035f;
            sun.color = life.IsDaylight ? new Color(1, .93f, .82f) : new Color(.45f, .60f, 1);
            sun.transform.rotation = Quaternion.Euler(life.IsDaylight ? 42 : 25, 330, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = life.IsDaylight ? new Color(.16f, .18f, .21f) : new Color(.025f, .037f, .058f);
            // 断电的舱内几乎全黑：总闸未合时压低环境光（登舱后在飞行舱内不受影响）。
            float brightness = breaker == null ? 1f : breaker.Brightness;
            lastBrightness = brightness;
            if (breaker != null && flight != null && !flight.IsLocked)
                RenderSettings.ambientLight *= Mathf.Lerp(0.3f, 1f, brightness);
            if (shoulderLampRig != null) shoulderLampRig.SetActive(life.SuitWorn && !flight.IsLocked);
            foreach (var lamp in shoulderLights) if (lamp != null) lamp.enabled = life.HudPowered && !flight.IsLocked;
            if (normalIntensity != null)
                for (int i = 0; i < habitatLights.Length; i++) if (habitatLights[i] != null)
                    habitatLights[i].intensity = life.BasePower > 0 ? normalIntensity[i] * brightness : 0;
        }
    }
}
