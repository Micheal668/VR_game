using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 可抓取物体的声音：拿起、松手、碰到桌面或墙面时发声；手持物体撞到东西时让持握的手震一下。
    // 抓取瞬间的基础震动已由官方交互器上的 SimpleHapticFeedback 提供，这里不重复。
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public sealed class GrabFeedback : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [Tooltip("低于此相对速度（米/秒）的接触不发声，避免静置物体反复响。")]
        [SerializeField, Min(0f)] private float knockSpeed = 0.25f;
        [SerializeField, Range(0f, 1f)] private float heldImpactAmplitude = 0.5f;
        private XRGrabInteractable grab;
        private AudioSource source;
        private float quietUntil;
        private float nextKnock;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            source = FeedbackSounds.CreateSource(transform, "Grab Feedback Audio", 1f);
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
            // 场景加载、复位或从腰包取回时物体会落定一次，这段时间的接触不算“掉落”。
            quietUntil = Time.time + 0.5f;
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => FeedbackSounds.Play(source, FeedbackSound.Grab, volume);

        private void OnReleased(SelectExitEventArgs args)
        {
            // 收纳、拒绝、复位都会以“取消”结束抓取，它们有自己的反馈，这里只响应主动松手。
            if (!args.isCanceled) FeedbackSounds.Play(source, FeedbackSound.Release, volume * 0.7f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (Time.time < quietUntil || Time.time < nextKnock || speed < knockSpeed) return;
            nextKnock = Time.time + 0.08f;
            float strength = Mathf.Clamp01(speed / 2f);
            FeedbackSounds.Play(source, FeedbackSound.Knock, volume * (0.3f + 0.7f * strength));
            if (grab.isSelected) HandHaptics.Pulse(HandHaptics.FromGrab(grab), heldImpactAmplitude * (0.4f + 0.6f * strength), 0.05f);
        }
    }
}
