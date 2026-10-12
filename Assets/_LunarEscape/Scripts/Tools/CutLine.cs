using System;
using UnityEngine;

namespace LunarEscape
{
    // 钢梁上的红色切割线：由若干小段组成，激光光点沿线移动，每段受热累计到 1 即切开（变成焦黑的切口）。
    // 光点必须落在线附近（容差内）才算数，打在别处只会冒火花。
    public sealed class CutLine : MonoBehaviour
    {
        [Tooltip("线的两端（局部坐标，线沿两点连线）。")]
        [SerializeField] private Vector3 from = new(-0.06f, 0f, 0f), to = new(0.06f, 0f, 0f);
        [SerializeField, Min(0.005f)] private float tolerance = 0.035f;
        [SerializeField] private Renderer[] segments = new Renderer[0];
        private float[] heat;
        private float[] glow;
        private MaterialPropertyBlock block;

        private static readonly Color Marked = new(0.75f, 0.05f, 0.03f);
        private static readonly Color Groove = new(0.05f, 0.04f, 0.04f);

        public bool Severed { get; private set; }
        public float Progress { get { if (heat == null) return 0f; float s = 0f; foreach (var h in heat) s += Mathf.Clamp01(h); return s / heat.Length; } }
        public Vector3 WorldFrom => transform.TransformPoint(from);
        public Vector3 WorldTo => transform.TransformPoint(to);
        public event Action<CutLine> Cut;

        public void Configure(Vector3 start, Vector3 end, Renderer[] parts) { from = start; to = end; segments = parts; }

        private void Awake() => ResetLine();

        public void ResetLine()
        {
            heat = new float[Mathf.Max(1, segments.Length)];
            glow = new float[heat.Length];
            Severed = false;
            Paint();
        }

        // 激光命中：返回是否落在线上（用于判断火花颜色与声音）。
        public bool Heat(Vector3 worldPoint, float amount)
        {
            if (Severed) return false;
            Vector3 a = WorldFrom, b = WorldTo, ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(worldPoint - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            if (Vector3.Distance(worldPoint, a + ab * t) > tolerance) return false;
            int index = Mathf.Min(heat.Length - 1, Mathf.FloorToInt(t * heat.Length));
            heat[index] = Mathf.Min(1f, heat[index] + amount);
            glow[index] = 1f;
            // 热量向相邻段少量传导，光点停在两段交界处也能推进。
            if (index > 0) heat[index - 1] = Mathf.Min(1f, heat[index - 1] + amount * 0.2f);
            if (index + 1 < heat.Length) heat[index + 1] = Mathf.Min(1f, heat[index + 1] + amount * 0.2f);
            foreach (var h in heat) if (h < 1f) return true;
            Severed = true;
            Paint();
            Cut?.Invoke(this);
            return true;
        }

        public void SeverInstantly() { if (Severed) return; for (int i = 0; i < heat.Length; i++) heat[i] = 1f; Severed = true; Paint(); Cut?.Invoke(this); }

        private void Update()
        {
            bool any = false;
            for (int i = 0; i < glow.Length; i++) if (glow[i] > 0f) { glow[i] = Mathf.Max(0f, glow[i] - Time.deltaTime * 1.5f); any = true; }
            if (any) Paint();
        }

        // 未切：暗红标记；正在受热：橙黄发光；切开：焦黑切口，余温慢慢褪去。
        private void Paint()
        {
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                float h = heat != null ? heat[i] : 0f, g = glow != null ? glow[i] : 0f;
                var baseColor = h >= 1f ? Groove : Color.Lerp(Marked, new Color(1f, 0.55f, 0.1f), h);
                var emission = new Color(1f, 0.45f, 0.08f) * (g * (1.5f + 2.5f * h)) + (h >= 1f ? Color.black : Marked * 2.2f);
                segments[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", baseColor);
                block.SetColor("_EmissionColor", emission);
                segments[i].SetPropertyBlock(block);
            }
        }
    }
}
