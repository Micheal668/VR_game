using UnityEngine;

namespace LunarEscape
{
    // 小面积低频定位灯：发光网格提供远处可见性，局部光源照亮舷梯和舱体。
    public sealed class LanderNavigationBeacon : MonoBehaviour
    {
        [SerializeField] private Renderer[] lenses;
        [SerializeField] private Light[] lights;
        [SerializeField] private Color color = new(.25f, .85f, 1);
        [SerializeField, Min(.5f)] private float period = 1.4f;
        private MaterialPropertyBlock block;
        public bool Lit { get; private set; }
        public void Configure(Renderer[] lamps, Light[] illumination) { lenses = lamps; lights = illumination; }
        private void Update()
        {
            float phase = Mathf.Repeat(Time.unscaledTime, period);
            Lit = phase < .24f || phase > .45f && phase < .64f;
            block ??= new MaterialPropertyBlock();
            foreach (var lens in lenses)
            {
                if (lens == null) continue;
                lens.GetPropertyBlock(block); block.SetColor("_BaseColor", color * (Lit ? 1 : .16f));
                block.SetColor("_EmissionColor", color * (Lit ? 5 : .05f)); lens.SetPropertyBlock(block);
            }
            foreach (var light in lights) if (light != null) light.intensity = Lit ? 4 : .15f;
        }
    }
}
