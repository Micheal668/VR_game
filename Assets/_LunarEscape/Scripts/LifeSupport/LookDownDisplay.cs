using UnityEngine;

namespace LunarEscape
{
    // Keep equipment out of the forward view, without disabling its supply controller.
    public sealed class LookDownDisplay : MonoBehaviour
    {
        [SerializeField] private Camera eye;
        [SerializeField] private float minimumPitch = 25, minimumDistance = .32f;
        private Canvas display;
        private Renderer[] housing;
        public bool Visible { get; private set; }
        public void Configure(Camera camera, float pitch, float distance)
        { eye=camera; minimumPitch=pitch; minimumDistance=distance; }
        private void Awake() { display=GetComponent<Canvas>();housing=GetComponentsInChildren<Renderer>(true); }
        private void LateUpdate()
        {
            if(eye==null || display==null) return;
            var delta=transform.position-eye.transform.position;
            Visible=eye.transform.forward.y < -Mathf.Sin(minimumPitch*Mathf.Deg2Rad)
                && delta.magnitude>=minimumDistance && Vector3.Dot(eye.transform.forward,delta.normalized)>.6f;
            display.enabled=Visible;
            foreach(var item in housing) if(item!=null)item.enabled=Visible;
        }
    }
}
