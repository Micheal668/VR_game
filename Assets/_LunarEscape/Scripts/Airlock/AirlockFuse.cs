using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 可抓取的保险丝：烧坏的那根开局插在配电盒里，备用的那根放在站内别处。
    // 配电盒盖子合着时，插在座里的保险丝拿不出来。
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public sealed class AirlockFuse : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] private bool good;
        [SerializeField] private FuseFault fault;
        private XRGrabInteractable grab;
        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private bool captured;

        public bool Good => good;
        public XRGrabInteractable Grab => grab;
        public bool canProcess => isActiveAndEnabled;

        public void Configure(bool working, FuseFault owner) { good = working; fault = owner; }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            Capture();
        }

        private void OnEnable() => grab.selectFilters.Add(this);
        private void OnDisable() => grab.selectFilters.Remove(this);

        private void Capture()
        {
            if (captured) return;
            startPosition = transform.position;
            startRotation = transform.rotation;
            captured = true;
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            // 已有的选择（插座正夹着、手正拿着）不受影响；只拦截“盖子没开就伸手去拔”。
            if (interactor.IsSelecting(interactable) || fault == null || interactor is XRSocketInteractor) return true;
            return !fault.IsSeated(this) || fault.CoverOpen;
        }

        internal void RestoreStart()
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.position = startPosition;
            body.rotation = startRotation;
            transform.SetPositionAndRotation(startPosition, startRotation);
        }
    }
}
