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
        [Tooltip("舱内自发光灯具（灯带、应急标识）：断电时按各自的残留亮度变暗。")]
        [SerializeField] private Renderer[] glowRenderers = System.Array.Empty<Renderer>();
        [Tooltip("与 glowRenderers 一一对应：断电时保留的自发光比例（0=全灭，夜光标识可保留少许）。")]
        [SerializeField] private float[] glowBlackoutLevels = System.Array.Empty<float>();
        [Tooltip("断电时舱内的环境光；只留下勉强辨认轮廓的亮度。")]
        [SerializeField] private Color blackoutAmbient = new(.004f, .005f, .008f);
        [Tooltip("断电时天空盒反射的强度。天空盒反射会把金属墙面照得很亮，必须一起压低。")]
        [SerializeField, Range(0f, 1f)] private float blackoutReflection = .03f;
        private float[] normalIntensity;
        private Color[][] glowColors;
        private MaterialPropertyBlock glowBlock;
        private float lastBrightness = -1f;
        public Light Sun => sun;
        public System.Collections.Generic.IReadOnlyList<Light> HabitatLights => habitatLights;
        public System.Collections.Generic.IReadOnlyList<Light> ShoulderLights => shoulderLights;
        public void Configure(LifeSupportMission source, AscentMission ascent, Light sunlight, Light[] lamps)
        {
            Unsubscribe(); life = source; flight = ascent; sun = sunlight; habitatLights = lamps;
            normalIntensity = null; CacheIntensities(); if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        public void ConfigureBreaker(HabitatBreaker mainBreaker) { breaker = mainBreaker; Refresh(); }
        public void ConfigureBlackout(Renderer[] glowing, float[] blackoutLevels)
        {
            ApplyGlow(1f); glowRenderers = glowing ?? System.Array.Empty<Renderer>();
            glowBlackoutLevels = blackoutLevels ?? System.Array.Empty<float>(); glowColors = null; Refresh();
        }
        public System.Collections.Generic.IReadOnlyList<Renderer> GlowRenderers => glowRenderers;
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
        private void OnDisable() { Unsubscribe(); ApplyGlow(1f); }
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
            // 断电的舱内几乎全黑：总闸未合时压低环境光、天空盒反射与灯具自发光，
            // 只剩总闸的红色应急灯和屏幕。登舱后（飞行舱）或走出基地后不受影响。
            float brightness = breaker == null ? 1f : breaker.Brightness;
            lastBrightness = brightness;
            float lit = life.BasePower > 0 ? brightness : 0f;
            bool indoors = breaker != null && flight != null && !flight.IsLocked && (!life.DoorOpen || life.IsInsideHabitat);
            if (indoors) RenderSettings.ambientLight = Color.Lerp(blackoutAmbient,
                life.ExpandedStation ? new Color(.16f,.20f,.23f) : RenderSettings.ambientLight, lit);
            // 舱壳不完全遮挡日光，月昼时阳光会透进舱内；舱门关闭、照明未恢复时一并压暗（舱内看不到舱外）。
            if (indoors && !life.DoorOpen) sun.intensity *= lit;
            RenderSettings.reflectionIntensity = indoors ? Mathf.Lerp(blackoutReflection, 1f, lit) : 1f;
            ApplyGlow(indoors ? lit : 1f);
            if (shoulderLampRig != null) shoulderLampRig.SetActive(life.SuitWorn && !flight.IsLocked);
            foreach (var lamp in shoulderLights) if (lamp != null) lamp.enabled = life.HudPowered && !flight.IsLocked;
            if (normalIntensity != null)
                for (int i = 0; i < habitatLights.Length; i++) if (habitatLights[i] != null)
                    habitatLights[i].intensity = life.BasePower > 0 ? normalIntensity[i] * brightness : 0;
        }
        // 只改属性块里的 _EmissionColor，不修改共享材质资产（编辑器运行时改材质会写回资源）。
        private void ApplyGlow(float lit)
        {
            if (glowRenderers == null || glowRenderers.Length == 0) return;
            if (glowColors == null)
            {
                glowColors = new Color[glowRenderers.Length][];
                for (int i = 0; i < glowRenderers.Length; i++)
                {
                    var materials = glowRenderers[i] != null ? glowRenderers[i].sharedMaterials : System.Array.Empty<Material>();
                    glowColors[i] = new Color[materials.Length];
                    for (int m = 0; m < materials.Length; m++)
                        glowColors[i][m] = materials[m] != null && materials[m].HasProperty("_EmissionColor") && materials[m].IsKeywordEnabled("_EMISSION")
                            ? materials[m].GetColor("_EmissionColor") : Color.clear;
                }
            }
            glowBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                var renderer = glowRenderers[i];
                if (renderer == null) continue;
                float level = Mathf.Lerp(i < glowBlackoutLevels.Length ? glowBlackoutLevels[i] : 0f, 1f, lit);
                if (Application.isPlaying && life != null && life.ExpandedStation && life.BasePower <= 0) level = 0;
                for (int m = 0; m < glowColors[i].Length; m++)
                {
                    if (glowColors[i][m].a <= 0f) continue;
                    renderer.GetPropertyBlock(glowBlock, m);
                    glowBlock.SetColor("_EmissionColor", glowColors[i][m] * level);
                    renderer.SetPropertyBlock(glowBlock, m);
                }
            }
        }
    }
}
