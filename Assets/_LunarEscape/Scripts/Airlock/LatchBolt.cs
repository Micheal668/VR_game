using UnityEngine;
using UnityEngine.InputSystem;

namespace LunarEscape
{
    // 舱门上一颗卡死的锁销螺栓。用扳手：工具头套在螺栓上，绕螺栓轴转动扳手（手腕或整条手臂）。
    // 累计同一方向转过 releaseDegrees 即松开，螺栓弹出一截并变绿。轴为本物体局部 Z（指向舱内）。
    public sealed class LatchBolt : MonoBehaviour
    {
        [SerializeField] private RepairTool[] tools = new RepairTool[0];
        [SerializeField] private Transform head;
        [SerializeField] private Renderer ring;
        [SerializeField, Min(0.01f)] private float reach = 0.07f;
        [SerializeField, Min(10f)] private float releaseDegrees = 180f;
        [SerializeField, Min(5f)] private float tickDegrees = 30f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private AudioSource source;
        private RepairTool engagedTool;
        private float lastToolAngle;
        private float lastTick;
        private Vector3 headRest;
        private Quaternion headRestRotation;
        private bool captured;

        // 带符号的累计转角；任一方向达到阈值即可（新手不知道“左松右紧”也能完成）。
        public float Turned { get; private set; }
        public bool Released { get; private set; }
        public bool Engaged => engagedTool != null;
        public bool Enabled { get; set; }
        public float ReleaseDegrees => releaseDegrees;

        public event System.Action<LatchBolt> Changed;

        public void Configure(RepairTool[] wrenches, Transform boltHead, Renderer indicator)
        {
            tools = wrenches; head = boltHead; ring = indicator;
            Capture();
        }

        private void Awake()
        {
            Capture();
            source = FeedbackSounds.CreateSource(transform, "Latch Bolt Audio", 1f);
            ShowState();
        }

        private void Capture()
        {
            if (captured || head == null) return;
            headRest = head.localPosition;
            headRestRotation = head.localRotation;
            captured = true;
        }

        private void Update()
        {
            if (Released || !Enabled) { Disengage(); return; }
            var tool = FindTool();
            // 扳手几乎顺着螺栓轴（握成“戳”的姿势）时无法判断转向，视为没有套上。
            float angle = 0f;
            if (tool != null && !TryToolAngle(tool, out angle)) tool = null;
            if (tool != engagedTool)
            {
                engagedTool = tool;
                if (tool == null) return;
                // 套上螺栓：一声金属轻碰，让玩家知道对准了。
                lastToolAngle = angle;
                FeedbackSounds.Play(source, FeedbackSound.Knock, 0.5f);
                HandHaptics.Pulse(HandHaptics.FromGrab(tool.Grab), 0.35f, 0.04f);
                return;
            }
            if (tool == null) return;

            float delta = Mathf.DeltaAngle(lastToolAngle, angle);
            lastToolAngle = angle;
#if UNITY_EDITOR
            // 键鼠模拟器无法绕螺栓轴转动手柄：编辑器中扳手套在螺栓上时按住 F 代替转动。真机构建不包含此捷径。
            if (Keyboard.current != null && Keyboard.current.fKey.isPressed) delta = 150f * Time.deltaTime;
#endif
            // 单帧过大的跳变通常是追踪丢失或扳手滑脱，不计入。
            if (Mathf.Abs(delta) > 45f) return;
            Turned += delta;
            if (Mathf.Abs(Turned - lastTick) >= tickDegrees)
            {
                lastTick = Mathf.Round(Turned / tickDegrees) * tickDegrees;
                FeedbackSounds.Play(source, FeedbackSound.Click, 0.7f);
                HandHaptics.Pulse(HandHaptics.FromGrab(tool.Grab), 0.45f, 0.03f);
            }
            if (Mathf.Abs(Turned) >= releaseDegrees)
            {
                Released = true;
                FeedbackSounds.Play(source, FeedbackSound.Clunk, 0.6f);
                HandHaptics.Pulse(HandHaptics.FromGrab(tool.Grab), 0.7f, 0.12f);
                Disengage();
                Changed?.Invoke(this);
            }
            ShowState();
        }

        private void Disengage() => engagedTool = null;

        private RepairTool FindTool()
        {
            float reachSquared = reach * reach;
            foreach (var tool in tools)
                if (tool != null && tool.isActiveAndEnabled && tool.IsHeld && (tool.Tip.position - transform.position).sqrMagnitude <= reachSquared)
                    return tool;
            return null;
        }

        // 扳手长轴（工具局部 Z）投影到螺栓端面后的方向角。
        private bool TryToolAngle(RepairTool tool, out float angle)
        {
            Vector3 local = transform.InverseTransformDirection(tool.transform.forward);
            angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            return new Vector2(local.x, local.y).sqrMagnitude > 0.3f * 0.3f;
        }

        public void ResetBolt()
        {
            Turned = 0f;
            lastTick = 0f;
            Released = false;
            engagedTool = null;
            ShowState();
        }

        private void ShowState()
        {
            if (head != null)
            {
                // 松开后螺栓向舱内退出 2.5 cm，远处也看得出哪颗已经处理。
                head.localPosition = headRest + (Released ? Vector3.forward * 0.025f : Vector3.zero);
                head.localRotation = headRestRotation * Quaternion.AngleAxis(Turned, Vector3.forward);
            }
            if (ring == null) return;
            block ??= new MaterialPropertyBlock();
            ring.GetPropertyBlock(block);
            block.SetColor(BaseColor, Released ? new Color(0.2f, 1f, 0.45f) : new Color(0.95f, 0.18f, 0.12f));
            ring.SetPropertyBlock(block);
        }
    }
}
