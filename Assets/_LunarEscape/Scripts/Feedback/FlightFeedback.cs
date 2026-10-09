using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace LunarEscape
{
    // 上升器与对接的“身体感受”：舱门关闭、点火、离地推力、基地爆炸冲击、RCS 喷气、
    // 对接锁定和撞击都转换为声音与双手震动。只读取 AscentMission / DockingMission 的状态和事件，
    // 不改变任何飞行规则、资源或时钟。
    public sealed class FlightFeedback : MonoBehaviour
    {
        [SerializeField] private AscentMission flight;
        [Tooltip("玩家左右手柄上的官方震动组件。")]
        [SerializeField] private HapticImpulsePlayer[] hands = new HapticImpulsePlayer[0];
        [Tooltip("RCS 推进按钮；按住它们的那只手会额外感到喷气震动。")]
        [SerializeField] private ButtonPressFeedback[] thrustButtons = new ButtonPressFeedback[0];
        [Tooltip("驾驶台上的手控器、刹车与主推按钮；握住并接通推进的手会感到喷气震动。")]
        [SerializeField] private CockpitControl[] cockpitThrust = new CockpitControl[0];
        [Header("音量")]
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float thrusterVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float eventVolume = 0.8f;
        [Header("震动幅度")]
        [SerializeField, Range(0f, 1f)] private float engineRumble = 0.4f;
        [SerializeField, Range(0f, 1f)] private float rcsRumble = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hatchAmplitude = 0.45f;
        [SerializeField, Range(0f, 1f)] private float captureAmplitude = 0.6f;
        [SerializeField, Range(0f, 1f)] private float dockedAmplitude = 0.8f;
        private readonly HapticRumble rumble = new();
        private AudioSource engine;
        private AudioSource thruster;
        private AudioSource oneShot;
        private float shake;
        private int lastSuppliesUsed;

        public void Configure(AscentMission mission, HapticImpulsePlayer[] controllers, ButtonPressFeedback[] thrusters)
        {
            flight = mission;
            hands = controllers ?? new HapticImpulsePlayer[0];
            thrustButtons = thrusters ?? new ButtonPressFeedback[0];
        }

        public void ConfigureCockpit(CockpitControl[] controls) => cockpitThrust = controls ?? new CockpitControl[0];

        private void Awake()
        {
            // 舱内声音不做空间化：发动机和喷嘴声来自整个船体，而不是某个点。
            engine = FeedbackSounds.CreateLoop(transform, "Engine Loop Audio", FeedbackSound.EngineLoop, 0f);
            thruster = FeedbackSounds.CreateLoop(transform, "Thruster Loop Audio", FeedbackSound.ThrusterLoop, 0f);
            oneShot = FeedbackSounds.CreateSource(transform, "Flight Feedback Audio", 0f);
        }

        private void OnEnable()
        {
            if (flight == null) return;
            flight.PhaseChanged += OnPhaseChanged;
            flight.Exploded += OnExploded;
            flight.Changed += OnChanged;
            lastSuppliesUsed = flight.SuppliesUsed;
            engine.Play();
            thruster.Play();
        }

        private void OnDisable()
        {
            if (flight != null)
            {
                flight.PhaseChanged -= OnPhaseChanged;
                flight.Exploded -= OnExploded;
                flight.Changed -= OnChanged;
            }
            engine.volume = thruster.volume = 0f;
            shake = 0f;
            rumble.Clear();
        }

        private void Update()
        {
            if (flight == null) return;
            float level = EngineLevel();
            engine.volume = Mathf.MoveTowards(engine.volume, level * engineVolume, Time.deltaTime * 1.5f);
            engine.pitch = 0.8f + 0.4f * level;

            var docking = flight.Docking;
            bool thrusting = docking != null && docking.Active && docking.ActiveCommandCount > 0;
            thruster.volume = Mathf.MoveTowards(thruster.volume, thrusting ? thrusterVolume : 0f, Time.deltaTime * 6f);

            // 爆炸冲击按指数衰减，约 1 秒后基本消失。
            shake *= Mathf.Exp(-Time.deltaTime * 2.5f);
            if (shake < 0.01f) shake = 0f;

            // 同一只手只发送一个合成幅度，避免多个来源抢占同一个震动周期。
            float shared = Mathf.Clamp01(level * engineRumble + shake);
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                float amplitude = shared + (thrusting && IsPressingThrust(hand) ? rcsRumble : 0f);
                rumble.Drive(hand, Mathf.Clamp01(amplitude));
            }
        }

        // 推力档位 0~1：点火时逐渐增强，离地头几秒最强，随后降为巡航隆隆声；入轨修正与主推加速时重新响起。
        private float EngineLevel()
        {
            switch (flight.Phase)
            {
                case AscentPhase.Ignition:
                    float duration = Mathf.Max(0.01f, flight.Config.IgnitionSeconds);
                    return Mathf.Lerp(0.3f, 1f, 1f - flight.PhaseRemaining / duration);
                case AscentPhase.Ascent:
                case AscentPhase.Recovery:
                    return Mathf.Lerp(1f, 0.45f, Mathf.Clamp01((flight.AirborneSeconds - 3f) / 4f));
                case AscentPhase.Circularizing:
                    return 0.8f;
                case AscentPhase.Rendezvous:
                    var docking = flight.Docking;
                    return docking != null && docking.IsCommandActive(DockCommand.MainBoost) && docking.CanBoost ? 0.7f : 0f;
                default:
                    return 0f;
            }
        }

        private bool IsPressingThrust(HapticImpulsePlayer hand)
        {
            foreach (var button in thrustButtons)
                if (button != null && button.IsHeld && button.HeldBy == hand) return true;
            foreach (var control in cockpitThrust)
                if (control != null && control.IsEngaged && control.HoldingHand == hand) return true;
            return false;
        }

        private void OnPhaseChanged(AscentPhase phase)
        {
            switch (phase)
            {
                case AscentPhase.AwaitingBoarding:
                    // 重新开始：清除残留冲击，不播放任何声音。
                    shake = 0f;
                    lastSuppliesUsed = flight.SuppliesUsed;
                    rumble.Clear();
                    break;
                case AscentPhase.FadeOut:
                    FeedbackSounds.Play(oneShot, FeedbackSound.Clunk, eventVolume);
                    PulseAll(hatchAmplitude, 0.12f);
                    break;
                case AscentPhase.Docking:
                    // 进入捕获：对接机构接触的第一下。
                    FeedbackSounds.Play(oneShot, FeedbackSound.Clunk, eventVolume);
                    PulseAll(captureAmplitude, 0.2f);
                    break;
                case AscentPhase.Docked:
                    FeedbackSounds.Play(oneShot, FeedbackSound.Clunk, eventVolume);
                    FeedbackSounds.Play(oneShot, FeedbackSound.Chime, eventVolume);
                    if (isActiveAndEnabled) StartCoroutine(DoublePulse(dockedAmplitude));
                    break;
                case AscentPhase.Failed:
                    if (flight.Failure == AscentFailure.DockingCollision)
                    {
                        FeedbackSounds.Play(oneShot, FeedbackSound.Clunk, 1f);
                        shake = 1f;
                    }
                    else PulseAll(0.5f, 0.5f);
                    break;
            }
        }

        private void OnExploded()
        {
            // 冲击强度与已有的损伤档位一致；未离地时被爆炸波及按最强处理。
            shake = flight.Phase == AscentPhase.Failed ? 1f : flight.Damage switch
            {
                AscentDamage.Heavy => 1f,
                AscentDamage.Light => 0.65f,
                _ => 0.35f
            };
        }

        private void OnChanged()
        {
            if (flight.SuppliesUsed > lastSuppliesUsed)
                FeedbackSounds.Play(oneShot, FeedbackSound.Hiss, eventVolume * 0.7f);
            lastSuppliesUsed = flight.SuppliesUsed;
        }

        private void PulseAll(float amplitude, float seconds)
        {
            foreach (var hand in hands) HandHaptics.Pulse(hand, amplitude, seconds);
        }

        private IEnumerator DoublePulse(float amplitude)
        {
            PulseAll(amplitude, 0.15f);
            yield return new WaitForSeconds(0.25f);
            PulseAll(amplitude, 0.15f);
        }
    }
}
