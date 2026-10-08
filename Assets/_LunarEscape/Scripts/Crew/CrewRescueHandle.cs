using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 实体把手选择和鼠标长按共用持续操作；失焦、取消或停用都会释放，不把点击当瞬间救援。
    public sealed class CrewRescueHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
    {
        [SerializeField] private GroundCrewController controller;
        [SerializeField] private XRSimpleInteractable interactable;
        private bool pointerHeld;
        private bool listening;
        public XRSimpleInteractable Interactable => interactable;
        public void Configure(GroundCrewController owner, XRSimpleInteractable physical = null)
        { Unsubscribe(); pointerHeld = false; controller?.SetHandleHeld(this, false); controller = owner; interactable = physical; if (isActiveAndEnabled) Subscribe(); }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (interactable == null || listening) return;
            interactable.selectEntered.AddListener(Selected);
            interactable.selectExited.AddListener(Released);
            listening = true;
        }
        private void OnDisable()
        {
            pointerHeld = false;
            Unsubscribe();
            controller?.SetHandleHeld(this, false);
        }
        private void Unsubscribe()
        {
            if (listening && interactable != null) { interactable.selectEntered.RemoveListener(Selected); interactable.selectExited.RemoveListener(Released); }
            listening = false;
        }
        private void Selected(SelectEnterEventArgs args) => Sync();
        private void Released(SelectExitEventArgs args) => Sync();
        private void Sync() => controller?.SetHandleHeld(this, pointerHeld || (interactable != null && interactable.isSelected));
        public void OnPointerDown(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) { pointerHeld = true; Sync(); } }
        public void OnPointerUp(PointerEventData eventData) { pointerHeld = false; Sync(); }
        public void OnPointerExit(PointerEventData eventData) { pointerHeld = false; Sync(); }
        public void OnCancel(BaseEventData eventData) { pointerHeld = false; Sync(); }
        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            pointerHeld = false;
            // 失焦后即使 XR 仍缓存选中状态，也必须重新选择才能继续救援。
            controller?.SetHandleHeld(this, false);
        }
        private void OnApplicationPause(bool paused) { if (paused) OnApplicationFocus(false); }
    }
}
