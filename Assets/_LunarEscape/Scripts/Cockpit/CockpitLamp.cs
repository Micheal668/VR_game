using UnityEngine;

namespace LunarEscape
{
    public enum LampState { Off, Next, Busy, Done, Fault }

    // 操纵件旁的指示灯：熄灭=不可用，琥珀色闪烁=下一步，琥珀色常亮=进行中，绿色=完成，红色=故障或拒绝。
    // 不看文字也能读懂启动顺序；状态由使用它的组件设置。
    public sealed class CockpitLamp : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        [SerializeField] private Renderer bulb;
        private MaterialPropertyBlock block;
        private float faultUntil;

        public LampState State { get; set; }

        public void Configure(Renderer renderer) => bulb = renderer;

        // 短暂亮红灯，表示刚才的操作被拒绝，随后恢复原状态。
        public void Flash() => faultUntil = Time.time + 0.6f;

        private void Update()
        {
            if (bulb == null) return;
            var state = Time.time < faultUntil ? LampState.Fault : State;
            Color color = state switch
            {
                LampState.Next => new Color(1f, 0.62f, 0.1f) * (Mathf.PingPong(Time.time * 2.5f, 1f) > 0.5f ? 1f : 0.15f),
                LampState.Busy => new Color(1f, 0.62f, 0.1f),
                LampState.Done => new Color(0.2f, 1f, 0.45f),
                LampState.Fault => new Color(1f, 0.15f, 0.1f),
                _ => new Color(0.08f, 0.08f, 0.08f)
            };
            block ??= new MaterialPropertyBlock();
            bulb.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(EmissionColor, color * 2f);
            bulb.SetPropertyBlock(block);
        }
    }
}
