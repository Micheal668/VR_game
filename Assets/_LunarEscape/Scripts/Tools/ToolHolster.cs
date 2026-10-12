using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 工具架上的卡槽：只接受指定的那一件工具，避免把钻头插进氮气接口。
    [RequireComponent(typeof(XRSocketInteractor))]
    public sealed class ToolHolster : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [SerializeField] private TriggerTool tool;
        private XRSocketInteractor socket;

        public TriggerTool Tool => tool;
        public XRSocketInteractor Socket => socket != null ? socket : socket = GetComponent<XRSocketInteractor>();
        public bool HasTool => Socket.hasSelection;
        public bool canProcess => isActiveAndEnabled;

        public void Configure(TriggerTool expected) => tool = expected;

        private void OnEnable() { Socket.selectFilters.Add(this); Socket.hoverFilters.Add(this); }
        private void OnDisable() { Socket.selectFilters.Remove(this); Socket.hoverFilters.Remove(this); }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => Matches(interactable);
        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => Matches(interactable);
        private bool Matches(object interactable) => tool != null && interactable is Component c && c.gameObject == tool.gameObject;
    }
}
