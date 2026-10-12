using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 钻出的岩芯：带三层颜色的圆柱，可以抓取，要送到实验室的样品架上。
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class CoreSample : MonoBehaviour
    {
        private Rigidbody body;
        private XRGrabInteractable grab;
        public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
        public XRGrabInteractable Grab => grab != null ? grab : grab = GetComponent<XRGrabInteractable>();
    }
}
