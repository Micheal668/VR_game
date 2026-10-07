using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace LunarEscape
{
    // 腰带上的一个磁吸插槽：显示已收纳物资的缩小模型和数量；伸手抓住插槽即取回一件该物资。
    // 是否允许收纳、额度如何计算仍由 CargoInventory 决定，插槽只是看得见、摸得着的入口。
    public sealed class BeltSlot : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
    {
        [SerializeField] private CargoBelt belt;
        [SerializeField] private Transform replicaRoot;
        [SerializeField] private Renderer ring;
        [SerializeField] private TMP_Text count;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;

        public Transform ReplicaRoot => replicaRoot;
        public CargoKind? Kind { get; internal set; }

        public void Configure(CargoBelt owner, Transform replicas, Renderer highlight, TMP_Text quantity)
        { belt = owner; replicaRoot = replicas; ring = highlight; count = quantity; }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            belt.RequestTakeOut(this, args.interactorObject);
        }

        internal void ShowCount(int quantity) { if (count != null) count.text = quantity > 1 ? quantity.ToString() : string.Empty; }

        // 0=空闲暗色，1=已占用，2=手中物资正靠近、松手即会吸入。
        internal void ShowRing(int state)
        {
            if (ring == null) return;
            block ??= new MaterialPropertyBlock();
            ring.GetPropertyBlock(block);
            block.SetColor(BaseColor, state == 2 ? new Color(0.45f, 1f, 0.95f) : state == 1 ? new Color(0.2f, 0.55f, 0.6f) : new Color(0.12f, 0.16f, 0.18f));
            ring.SetPropertyBlock(block);
        }
    }
}
