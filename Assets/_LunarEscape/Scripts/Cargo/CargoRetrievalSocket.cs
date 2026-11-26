using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // A single item is presented above the pocket and stays within reach until the player grips it.
    public sealed class CargoRetrievalSocket : XRSocketInteractor
    {
        private CargoItem presented;
        public bool Accepts(CargoItem item) => presented != null && item == presented;
        public override bool CanHover(IXRHoverInteractable interactable) => false;
        public override bool CanSelect(IXRSelectInteractable interactable) =>
            presented != null && interactable == (IXRSelectInteractable)presented.Grab && base.CanSelect(interactable);

        public void Present(CargoItem item)
        {
            presented = item;
            item.RestoreAt(transform.position, transform.rotation);
            interactionManager.SelectEnter((IXRSelectInteractor)this, (IXRSelectInteractable)item.Grab);
        }
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            presented = null;
            base.OnSelectExited(args);
        }
    }
}
