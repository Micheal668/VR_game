using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 为可抓取物体补充维修所需的信息，不负责推进任务。
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class RepairTool : MonoBehaviour
    {
        [SerializeField] private Transform tip;
        [SerializeField] private string toolType = "maintenance";
        private XRGrabInteractable grab;

        // 独立的工具头定位点可以适配不同长度、形状的工具。
        public Transform Tip => tip != null ? tip : transform;
        public string ToolType => toolType;
        public bool IsHeld => grab != null && grab.isSelected;

        private void Awake() => grab = GetComponent<XRGrabInteractable>();

        public void Configure(Transform toolTip, string type)
        {
            tip = toolTip;
            toolType = type ?? string.Empty;
        }
    }
}
