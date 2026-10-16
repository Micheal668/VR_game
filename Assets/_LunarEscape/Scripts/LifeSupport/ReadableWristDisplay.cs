using UnityEngine;

namespace LunarEscape
{
    // 腕屏跟随手的位置，但文字始终朝向双眼中点；过近时向前让出阅读距离。
    [DefaultExecutionOrder(250)]
    public sealed class ReadableWristDisplay : MonoBehaviour
    {
        [SerializeField] private Camera eye;
        [SerializeField] private Transform hand;
        [SerializeField] private Vector3 offset = new(0, .09f, -.06f);
        [SerializeField] private float minimumDistance = .40f;
        private Canvas display;
        private Renderer[] housing;
        public bool Visible { get; private set; }
        public void Configure(Camera camera, Transform controller) { eye = camera; hand = controller; }
        private void Awake() { display = GetComponent<Canvas>(); housing = GetComponentsInChildren<Renderer>(true); }
        private void LateUpdate()
        {
            if (eye == null || hand == null) return;
            var eyes = eye.transform;
            Vector3 point = hand.position + eyes.TransformDirection(offset), delta = point - eyes.position;
            if (delta.sqrMagnitude < .0001f) delta = eye.transform.forward;
            Visible = Vector3.Dot(eyes.forward, delta.normalized) > .30f;
            if (Visible)
            {
                // 给双眼视差及面板四角留出余量，手靠视野边缘时仍能读完整一行。
                var local = eyes.InverseTransformDirection(delta);
                local.x = Mathf.Clamp(local.x, -local.z * .35f, local.z * .35f);
                local.y = Mathf.Clamp(local.y, -local.z * .35f, local.z * .35f);
                delta = eyes.TransformDirection(local);
            }
            point = eyes.position + delta.normalized * Mathf.Max(minimumDistance, delta.magnitude);
            transform.SetPositionAndRotation(point, Quaternion.LookRotation(point - eyes.position, eyes.up));
            if (display != null) display.enabled = Visible;
            foreach (var renderer in housing) if (renderer != null) renderer.enabled = Visible;
        }
    }
}
