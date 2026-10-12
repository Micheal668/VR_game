using System;
using UnityEngine;

namespace LunarEscape
{
    // 蒙着月尘的太阳能板。尘层是一张运行时生成的透明贴图，压缩氮气吹到哪里，哪里的尘就变薄；
    // 清洁度达到阈值后剩余的薄尘自动散去，板侧指示灯变绿。不依赖网格 UV：命中点按板的局部坐标换算。
    public sealed class DustyPanel : MonoBehaviour
    {
        [Tooltip("尘层四边形（局部 XY 平面，尺寸 1×1，由 Transform 缩放到板面大小）。")]
        [SerializeField] private Renderer dustLayer;
        [SerializeField] private CockpitLamp lamp;
        [SerializeField, Range(0.5f, 1f)] private float cleanThreshold = 0.92f;
        [SerializeField, Min(8)] private int resolutionX = 64;
        [SerializeField, Min(8)] private int resolutionY = 40;
        [SerializeField] private int seed = 1;

        private float[] dust;
        private float initialDust;
        private Texture2D texture;
        private Color32[] pixels;
        private Material material;
        private bool dirty;
        private float fadeOut = -1f;
        private AudioSource source;

        public bool IsClean { get; private set; }
        public float Cleanliness { get; private set; }
        public Renderer DustLayer => dustLayer;
        public event Action<DustyPanel> Cleaned;
        public event Action<DustyPanel> Changed;

        public void Configure(Renderer layer, CockpitLamp indicator, int noiseSeed)
        { dustLayer = layer; lamp = indicator; seed = noiseSeed; }

        private void Awake()
        {
            source = FeedbackSounds.CreateSource(transform, "Panel Audio", 1f);
            source.bypassListenerEffects = true;
            texture = new Texture2D(resolutionX, resolutionY, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name + " Dust" };
            pixels = new Color32[resolutionX * resolutionY];
            material = new Material(dustLayer.sharedMaterial);
            material.SetTexture("_BaseMap", texture);
            dustLayer.sharedMaterial = material;
            ResetDust();
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (material != null) Destroy(material);
        }

        // 月尘不是均匀一层：低频噪声叠出几块厚斑，边缘积得更多。
        public void ResetDust()
        {
            dust ??= new float[resolutionX * resolutionY];
            var random = new System.Random(seed);
            float ox = (float)random.NextDouble() * 100f, oy = (float)random.NextDouble() * 100f;
            initialDust = 0f;
            for (int y = 0; y < resolutionY; y++)
                for (int x = 0; x < resolutionX; x++)
                {
                    float u = (float)x / (resolutionX - 1), v = (float)y / (resolutionY - 1);
                    float patches = Mathf.PerlinNoise(ox + u * 3.1f, oy + v * 2.3f);
                    float grain = Mathf.PerlinNoise(ox + u * 17f, oy + v * 13f);
                    float edge = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f;
                    float value = Mathf.Clamp01(0.55f + 0.35f * patches + 0.15f * grain + 0.15f * edge * edge);
                    dust[y * resolutionX + x] = value;
                    initialDust += value;
                }
            IsClean = false; Cleanliness = 0f; fadeOut = -1f;
            if (lamp != null) lamp.State = LampState.Next;
            dirty = true;
            Upload();
        }

        // 在命中点按半径清掉尘（amount 为本帧最多清掉的厚度），返回实际吹落的量，供喷出尘雾。
        public float Blow(Vector3 worldPoint, float radius, float amount)
        {
            if (IsClean || amount <= 0f || radius <= 0f) return 0f;
            var size = dustLayer.transform.lossyScale;
            var local = dustLayer.transform.InverseTransformPoint(worldPoint);
            float cu = (local.x + 0.5f) * (resolutionX - 1), cv = (local.y + 0.5f) * (resolutionY - 1);
            float rx = radius / Mathf.Max(0.01f, Mathf.Abs(size.x)) * (resolutionX - 1);
            float ry = radius / Mathf.Max(0.01f, Mathf.Abs(size.y)) * (resolutionY - 1);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cu - rx)), x1 = Mathf.Min(resolutionX - 1, Mathf.CeilToInt(cu + rx));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cv - ry)), y1 = Mathf.Min(resolutionY - 1, Mathf.CeilToInt(cv + ry));
            float removed = 0f;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cu) / rx, dy = (y - cv) / ry;
                    float d2 = dx * dx + dy * dy;
                    if (d2 > 1f) continue;
                    int i = y * resolutionX + x;
                    float take = Mathf.Min(dust[i], amount * (1f - d2));
                    dust[i] -= take; removed += take;
                }
            if (removed <= 0f) return 0f;
            dirty = true;
            float remaining = 0f;
            foreach (float d in dust) remaining += d;
            Cleanliness = 1f - remaining / Mathf.Max(0.0001f, initialDust);
            Changed?.Invoke(this);
            if (Cleanliness >= cleanThreshold) Finish();
            return removed / Mathf.Max(1f, (x1 - x0 + 1) * (y1 - y0 + 1));
        }

        private void Finish()
        {
            IsClean = true; Cleanliness = 1f; fadeOut = 0f;
            if (lamp != null) lamp.State = LampState.Done;
            FeedbackSounds.Play(source, FeedbackSound.Chime, 0.7f);
            Cleaned?.Invoke(this);
        }

        // 测试与开发菜单：直接吹干净。
        public void CleanInstantly() { if (!IsClean) Finish(); }

        private void LateUpdate()
        {
            // 达标后剩下的薄尘在半秒内散去，避免玩家去找最后几个像素。
            if (fadeOut >= 0f && fadeOut < 1f)
            {
                fadeOut = Mathf.Min(1f, fadeOut + Time.deltaTime / 0.5f);
                for (int i = 0; i < dust.Length; i++) dust[i] *= 1f - fadeOut;
                dirty = true;
            }
            Upload();
        }

        private void Upload()
        {
            if (!dirty || texture == null) return;
            dirty = false;
            for (int i = 0; i < dust.Length; i++)
            {
                // 月尘偏灰褐色；厚处更不透明。
                byte a = (byte)(Mathf.Clamp01(dust[i] * 0.95f) * 255f);
                pixels[i] = new Color32(118, 112, 104, a);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }
}
