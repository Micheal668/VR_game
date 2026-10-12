using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 扳机工具的公共底座：侧键握持、扳机深浅控制力度（0–1 连续量）。
    //   · 握持改为“粘手”：按一下侧键拿起后松开也不会掉，再按一下才放下——长时间按住 VIVE 侧键很累，
    //     扳机则留给工具本身；只在拿着工具的这只手上临时切换，放下后恢复原来的握持方式。
    //   · 过热：持续大力使用会升温，满了强制停机，降到一定程度才恢复；子类决定升温速度。
    //   · 手柄震动、循环音效的音量和音高跟随力度。
    // 具体效果（喷气、钻进、切割）由子类在 Work 中实现。
    [RequireComponent(typeof(XRGrabInteractable))]
    public abstract class TriggerTool : MonoBehaviour
    {
        [Tooltip("工具的作用端：喷嘴、钻头或激光出口；局部 +Z 为作用方向。")]
        [SerializeField] protected Transform muzzle;
        [Tooltip("低于此值视为没扣扳机，避免手指轻放在扳机上就开始工作。")]
        [SerializeField, Range(0f, 0.3f)] private float deadZone = 0.08f;
        [Tooltip("过热后降到此温度以下才恢复工作。")]
        [SerializeField, Range(0f, 1f)] private float recoverHeat = 0.35f;
        [SerializeField, Min(0f)] private float coolPerSecond = 0.28f;
        [Tooltip("握持时把这只手切换为“粘手”握持。")]
        [SerializeField] private bool stickyGrip = true;

        private readonly HapticRumble rumble = new();
        private XRBaseInputInteractor stickyHand;
        private XRBaseInputInteractor.InputTriggerType previousTrigger;
        private float overrideTrigger = -1f;
        protected XRGrabInteractable grab;
        protected AudioSource loop;
        protected AudioSource oneShot;

        public XRGrabInteractable Grab => grab;
        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public bool IsHeld => grab != null && grab.isSelected && !(grab.firstInteractorSelecting is XRSocketInteractor);
        // 扳机深度（去掉死区后重新映射到 0–1）；没拿在手里时为 0。
        public float Trigger { get; private set; }
        public float Heat { get; private set; }
        public bool Overheated { get; private set; }
        public bool Working => IsHeld && !Overheated && Trigger > 0f && CanWork;
        public HapticImpulsePlayer HoldingHand => IsHeld ? HandHaptics.FromInteractor(grab.firstInteractorSelecting) : null;

        // 子类附加的工作条件（例如气瓶空了）。
        protected virtual bool CanWork => true;
        protected virtual FeedbackSound LoopSound => FeedbackSound.ThrusterLoop;

        // 开发菜单与测试：不经手柄直接给出扳机深度；负数表示恢复读取手柄。
        public void SetTriggerOverride(float value) => overrideTrigger = value;

        protected virtual void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            loop = FeedbackSounds.CreateLoop(Muzzle, name + " Loop", LoopSound, 1f);
            oneShot = FeedbackSounds.CreateSource(Muzzle, name + " Audio", 1f);
            // 工具声通过手和航天服传导：舱外真空中也听得到，不受舱外闷音处理影响。
            loop.bypassListenerEffects = oneShot.bypassListenerEffects = true;
        }

        protected virtual void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
            if (!loop.isPlaying) loop.Play();
        }

        protected virtual void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
            RestoreGrip();
            loop.volume = 0f;
            rumble.Clear();
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!stickyGrip || !(args.interactorObject is XRBaseInputInteractor hand) || args.interactorObject is XRSocketInteractor) return;
            RestoreGrip();
            stickyHand = hand;
            previousTrigger = hand.selectActionTrigger;
            hand.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject as Object == stickyHand) RestoreGrip();
        }

        private void RestoreGrip()
        {
            if (stickyHand != null) stickyHand.selectActionTrigger = previousTrigger;
            stickyHand = null;
        }

        private float ReadTrigger()
        {
            if (!IsHeld) return 0f;
            float raw = overrideTrigger >= 0f ? overrideTrigger
                : grab.firstInteractorSelecting is XRBaseInputInteractor hand ? hand.activateInput.ReadValue() : 0f;
            return raw <= deadZone ? 0f : Mathf.Clamp01((raw - deadZone) / (1f - deadZone));
        }

        protected virtual void Update()
        {
            Trigger = ReadTrigger();
            float dt = Time.deltaTime;
            float heating = Working ? HeatPerSecond(Trigger) : 0f;
            Heat = Mathf.Clamp01(Heat + (heating - (heating > 0f ? 0f : coolPerSecond)) * dt);
            if (!Overheated && Heat >= 1f) { Overheated = true; OnOverheat(); }
            else if (Overheated && Heat <= recoverHeat) { Overheated = false; OnRecovered(); }

            float effort = Work(Working ? Trigger : 0f, dt);
            loop.volume = Mathf.MoveTowards(loop.volume, effort > 0f ? LoopVolume(effort) : 0f, dt * 5f);
            loop.pitch = LoopPitch(effort);
            var hand = HoldingHand;
            if (hand != null && effort > 0f) rumble.Drive(hand, RumbleAmplitude(effort));
        }

        // 子类实现工作效果；返回本帧“出力”（0 = 没在工作），用于声音和震动。
        protected abstract float Work(float power, float deltaTime);
        protected virtual float HeatPerSecond(float power) => 0f;
        protected virtual float LoopVolume(float effort) => 0.25f + 0.45f * effort;
        protected virtual float LoopPitch(float effort) => 0.7f + 0.6f * effort;
        protected virtual float RumbleAmplitude(float effort) => 0.12f + 0.45f * effort;

        protected virtual void OnOverheat()
        {
            FeedbackSounds.Play(oneShot, FeedbackSound.Deny, 0.8f);
            var hand = HoldingHand;
            if (hand != null && isActiveAndEnabled) StartCoroutine(HandHaptics.DenyPattern(hand, 0.8f));
        }

        protected virtual void OnRecovered() => FeedbackSounds.Play(oneShot, FeedbackSound.Click, 0.6f);

        // 重新开始时清零温度。
        public virtual void ResetTool()
        {
            Heat = 0f; Overheated = false; Trigger = 0f; overrideTrigger = -1f;
        }
    }
}
