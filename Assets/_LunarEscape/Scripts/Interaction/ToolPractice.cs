using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 只负责这次抓取练习的反馈；以后真正的任务流程会由单独的任务系统管理。
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class ToolPractice : MonoBehaviour
    {
        // SerializeField 让私有字段也显示在 Inspector（右侧属性面板）中。
        // 这里引用房间里的提示文字，抓取后由本脚本更新它。
        [SerializeField] private TMP_Text statusLabel;
        // 其他脚本可以读取练习进度，但不能随意修改它。
        public bool HasBeenGrabbed { get; private set; }
        public bool HasBeenReleased { get; private set; }
        private XRGrabInteractable grab;

        public void Configure(TMP_Text label) => statusLabel = label;

        // Awake 在对象初始化时执行一次，先取得同一物体上的官方抓取组件。
        private void Awake() => grab = GetComponent<XRGrabInteractable>();

        private void OnEnable()
        {
            // 监听官方抓取组件的事件，而不是自己重复编写 VR 手柄识别。
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            // 禁用对象时取消监听，避免再次启用后同一个操作被处理多次。
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            HasBeenGrabbed = true;
            SetStatus("TOOL ACQUIRED\nRelease it onto the workbench.", new Color(0.24f, 0.95f, 0.85f));
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            // 场景关闭或对象被禁用导致的取消，不算玩家主动完成了放手。
            if (args.isCanceled || !HasBeenGrabbed) return;
            HasBeenReleased = true;
            SetStatus("GRAB + RELEASE COMPLETE\nYou can pick up the tool again.", new Color(0.24f, 0.95f, 0.85f));
        }

        private void SetStatus(string text, Color color)
        {
            // 工具预制体可以单独使用；未连接场景提示板时，也仍然能够抓取。
            if (statusLabel == null) return;
            statusLabel.text = text;
            statusLabel.color = color;
        }
    }
}
