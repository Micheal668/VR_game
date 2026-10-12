using System;
using System.Collections.Generic;
using UnityEngine;

namespace LunarEscape
{
    // 取芯点：地面上的定位环。钻头竖直顶在孔底并保持合适转速，孔逐渐加深；
    // 自上而下三层：浮尘、月壤、角砾岩，越往下越硬、合适的转速区间越高。钻到目标深度后岩芯弹出地面。
    public sealed class DrillSite : MonoBehaviour
    {
        public static readonly List<DrillSite> All = new();

        [SerializeField, Min(0.05f)] private float targetDepth = 0.4f;
        [SerializeField, Min(0.02f)] private float radius = 0.13f;
        [Tooltip("孔洞：深色圆片，随深度变大变深。")]
        [SerializeField] private Transform hole;
        [SerializeField] private CockpitLamp beacon;
        [SerializeField] private ParticleSystem cuttings;
        [SerializeField] private CoreSample core;

        private Vector3 corePose;
        private Quaternion coreRotation;
        private float popTime = -1f;

        // 三层：深度比例上限、合适转速区间、硬度（越硬钻得越慢、越容易发热）。
        private static readonly (float until, Vector2 band, float hardness)[] Layers =
        {
            (0.3f, new Vector2(0.25f, 0.7f), 0.6f),
            (0.7f, new Vector2(0.4f, 0.8f), 1f),
            (1.01f, new Vector2(0.6f, 0.92f), 1.6f)
        };

        public float Depth { get; private set; }
        public float Progress => Depth / targetDepth;
        public bool Completed { get; private set; }
        public float Radius => radius;
        public float TargetDepth => targetDepth;
        public Vector3 Surface => transform.position;
        public float Bottom => transform.position.y - Depth;
        public int Layer { get { for (int i = 0; i < Layers.Length; i++) if (Progress < Layers[i].until) return i; return Layers.Length - 1; } }
        public Vector2 Band => Layers[Layer].band;
        public float Hardness => Layers[Layer].hardness;
        public CoreSample Core => core;
        public event Action<DrillSite> Drilled;
        public event Action<DrillSite, int> LayerReached;

        public void Configure(Transform holeVisual, CockpitLamp lamp, ParticleSystem dust, CoreSample sample, float depth)
        { hole = holeVisual; beacon = lamp; cuttings = dust; core = sample; targetDepth = depth; }

        private void Awake()
        {
            if (core != null) { corePose = core.transform.position; coreRotation = core.transform.rotation; }
            ResetSite();
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        // 钻头尖端是否顶在本孔底部：水平在定位环内，竖直方向贴着孔底。
        public bool Touches(Vector3 tip)
        {
            if (Completed) return false;
            var flat = new Vector2(tip.x - transform.position.x, tip.z - transform.position.z);
            return flat.magnitude <= radius && tip.y <= Bottom + 0.05f && tip.y >= Bottom - 0.3f;
        }

        public void Advance(float meters)
        {
            if (Completed || meters <= 0f) return;
            int before = Layer;
            Depth = Mathf.Min(targetDepth, Depth + meters);
            if (cuttings != null) cuttings.Emit(1);
            UpdateHole();
            if (Layer != before) LayerReached?.Invoke(this, Layer);
            if (Depth >= targetDepth - 0.0001f) Complete();
        }

        private void Complete()
        {
            Completed = true;
            if (beacon != null) beacon.State = LampState.Done;
            if (core != null)
            {
                core.gameObject.SetActive(true);
                core.transform.SetPositionAndRotation(transform.position + Vector3.down * 0.15f, Quaternion.identity);
                core.Body.isKinematic = true;
                popTime = 0f;
            }
            Drilled?.Invoke(this);
        }

        // 测试与开发菜单：直接钻完。
        public void CompleteInstantly() { if (!Completed) Advance(targetDepth); }

        private void Update()
        {
            if (popTime < 0f || core == null) return;
            // 岩芯在 0.5 秒内从孔里顶出，然后交给物理落在孔边，便于捡起。
            popTime += Time.deltaTime / 0.5f;
            core.transform.position = transform.position + Vector3.up * Mathf.Lerp(-0.15f, 0.35f, Mathf.SmoothStep(0, 1, popTime));
            if (popTime >= 1f)
            {
                popTime = -1f;
                core.Body.isKinematic = false;
                core.Body.linearVelocity = new Vector3(0.25f, 0.3f, 0.1f);
            }
        }

        private void UpdateHole()
        {
            if (hole == null) return;
            float open = Mathf.Clamp01(Depth / 0.05f);
            hole.localScale = new Vector3(radius * 1.4f * open, 0.002f, radius * 1.4f * open);
        }

        public void ResetSite()
        {
            Depth = 0f; Completed = false; popTime = -1f;
            if (beacon != null) beacon.State = LampState.Next;
            if (core != null)
            {
                core.gameObject.SetActive(false);
                core.transform.SetPositionAndRotation(corePose, coreRotation);
            }
            UpdateHole();
        }
    }
}
