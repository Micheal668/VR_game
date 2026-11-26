using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

#pragma warning disable CS0618
namespace VRLabs
{
    // Проверка логики без шлема. При подключённом XR-дисплее этот компонент ничего не меняет.
    public class LabDesktopPreview : MonoBehaviour
    {
        public Camera head;
        public Transform leftHand;
        public Transform rightHand;
        public ActionBasedController leftController;
        public ActionBasedController rightController;
        public CharacterController character;
        public Behaviour moveProvider;
        public Behaviour snapProvider;
        public Behaviour continuousProvider;
        bool desktop;
        float pitch;
        float verticalSpeed;
        float handReach = .7f;

        void Start()
        {
            var displays = new System.Collections.Generic.List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            desktop = !displays.Exists(display => display.running);
            if (!desktop) return;
            var pose = head.GetComponent<TrackedPoseDriver>();
            if (pose != null) pose.enabled = false;
            leftController.enableInputTracking = false;
            rightController.enableInputTracking = false;
            if (moveProvider != null) moveProvider.enabled = false;
            if (snapProvider != null) snapProvider.enabled = false;
            if (continuousProvider != null) continuousProvider.enabled = false;
            head.transform.parent.localPosition = new Vector3(0, 1.65f, 0);
            head.transform.localPosition = Vector3.zero;
            head.transform.localRotation = Quaternion.identity;
        }

        void Update()
        {
            if (!desktop || Keyboard.current == null || Mouse.current == null) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * .12f;
                transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -75, 75);
                head.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            float x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            float z = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            Vector3 motion = transform.TransformDirection(Vector3.ClampMagnitude(new Vector3(x, 0, z), 1)) * 1.5f;
            verticalSpeed = character.isGrounded && verticalSpeed < 0 ? -1 : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            motion.y = verticalSpeed;
            character.Move(motion * Time.deltaTime);
            handReach = Mathf.Clamp(handReach + mouse.scroll.ReadValue().y * .001f, .3f, 2f);
            leftHand.position = head.transform.TransformPoint(new Vector3(-.22f, -.2f, handReach));
            leftHand.rotation = head.transform.rotation;
            rightHand.position = head.transform.TransformPoint(new Vector3(.2f, -.18f, .45f));
            Ray pointer = head.ScreenPointToRay(mouse.position.ReadValue());
            Vector3 aim = pointer.GetPoint(8);
            if (Physics.Raycast(pointer, out RaycastHit hit, 20, ~(1 << 2), QueryTriggerInteraction.Ignore)) aim = hit.point;
            rightHand.rotation = Quaternion.LookRotation(aim - rightHand.position, Vector3.up);
        }

        void OnGUI()
        {
            if (!desktop) return;
            GUI.Box(new Rect(12, 12, 650, 52), "Без шлема: WASD — ходьба; ПКМ + мышь — обзор\nЛКМ — правый луч / UI; пробел — левая рука; колесо — вынос левой руки");
        }
    }
}
#pragma warning restore CS0618
