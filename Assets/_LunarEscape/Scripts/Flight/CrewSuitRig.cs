using UnityEngine;

namespace LunarEscape
{
    // 原始模型没有骨骼；这些显式关节由导入工具绑定，不依赖 Animator 或运行时模型插件。
    public sealed class CrewSuitRig : MonoBehaviour
    {
        [SerializeField] private Transform[] bones;
        [SerializeField] private SkinnedMeshRenderer body;
        [SerializeField] private SkinnedMeshRenderer helmet;
        public Transform[] Bones => bones;
        public SkinnedMeshRenderer Body => body;
        public SkinnedMeshRenderer Helmet => helmet;
        public void Configure(Transform[] joints, SkinnedMeshRenderer suit, SkinnedMeshRenderer head)
        { bones = joints; body = suit; helmet = head; }
    }
}
