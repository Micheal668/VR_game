using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 墙挂解锁握把和鼠标长按共用四秒穿戴进度；松手、失焦和取消都撤销持续输入。
    public sealed class SuitRackInteractor : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
    {
        [SerializeField] private SuitInteractionController controller;
        [SerializeField] private XRSimpleInteractable physical;
        private bool pointerHeld, xrHeld, listening;
        public XRSimpleInteractable Physical => physical;
        public void Configure(SuitInteractionController source, XRSimpleInteractable interactable = null)
        { Unsubscribe(); controller = source; physical = interactable; if (isActiveAndEnabled) Subscribe(); }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (listening || controller == null) return;
            if (physical != null) { physical.selectEntered.AddListener(Selected); physical.selectExited.AddListener(Released); }
            listening = true;
        }
        private void Unsubscribe()
        {
            if (physical != null && listening) { physical.selectEntered.RemoveListener(Selected); physical.selectExited.RemoveListener(Released); }
            pointerHeld = xrHeld = false; controller?.SetHeld(this, false); listening = false;
        }
        private void OnDisable() => Unsubscribe();
        private void Selected(SelectEnterEventArgs args) { xrHeld = true; Sync(); }
        private void Released(SelectExitEventArgs args) { xrHeld = physical != null && physical.isSelected; Sync(); }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) { pointerHeld = true; Sync(); } }
        public void OnPointerUp(PointerEventData data) { pointerHeld = false; Sync(); }
        public void OnPointerExit(PointerEventData data) { pointerHeld = false; Sync(); }
        public void OnCancel(BaseEventData data) { pointerHeld = xrHeld = false; Sync(); }
        private void OnApplicationFocus(bool focused) { if (!focused) { pointerHeld = xrHeld = false; controller?.SetHeld(this, false); } }
        private void OnApplicationPause(bool paused) { if (paused) OnApplicationFocus(false); }
        private void Sync() => controller?.SetHeld(this, pointerHeld || xrHeld);
    }
}
