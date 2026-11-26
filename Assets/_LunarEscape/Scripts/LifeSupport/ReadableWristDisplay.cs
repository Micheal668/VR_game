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
            Vector3 wrist = hand.position - eyes.position;
            bool inspecting = eyes.forward.y < -.25f || wrist.y > -.25f;
            Visible = inspecting && wrist.sqrMagnitude > .015f && wrist.sqrMagnitude < 1f
                && Vector3.Dot(eyes.forward, wrist.normalized) > .90f;
            Vector3 delta = wrist + eyes.TransformDirection(offset);
            if (delta.sqrMagnitude < .0001f) delta = eyes.forward;
            var point = eyes.position + delta.normalized * Mathf.Max(minimumDistance, delta.magnitude);
            transform.SetPositionAndRotation(point, Quaternion.LookRotation(point - eyes.position, eyes.up));
            if (display != null) display.enabled = Visible;
            foreach (var renderer in housing) if (renderer != null) renderer.enabled = Visible;
        }
    }
}
