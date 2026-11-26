using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace LunarEscape
{
    public sealed class ArtemisViewRig : MonoBehaviour
    {
        [SerializeField] private Transform trackingSpace;
        [SerializeField] private Camera eye;
        private XRHMD reference;
        public Camera Eye => eye;
        public bool HasHeadset => reference != null;

        public void Configure(Transform tracking, Camera camera)
        {
            trackingSpace = tracking;
            eye = camera;
        }

        public void Follow(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            var headset = InputSystem.GetDevice<XRHMD>();
            if (headset != null && headset != reference && headset.isTracked.isPressed)
            {
                // Remove the room's starting position and yaw once. Preserve every subsequent head movement.
                var forward = Vector3.ProjectOnPlane(headset.centerEyeRotation.ReadValue() * Vector3.forward, Vector3.up);
                var basis = forward.sqrMagnitude > .001f ? Quaternion.Inverse(Quaternion.LookRotation(forward)) : Quaternion.identity;
                trackingSpace.SetLocalPositionAndRotation(-(basis * headset.centerEyePosition.ReadValue()), basis);
                reference = headset;
            }
            else if (headset == null && reference != null)
            {
                reference = null;
                trackingSpace.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                eye.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }
    }
}
