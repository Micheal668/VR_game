using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LunarEscape
{
    // 面板按钮的手感：按下时在按钮位置发出咔哒声，并让按下它的那只手轻震一下；
    // 按钮不可用时改为“嗡嗡”声和两次震动，让玩家不看文字也知道这次操作没有生效。
    // 只做反馈，不调用按钮事件，也不改变按钮是否可用。
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0f, 1f)] private float pressAmplitude = 0.35f;
        [SerializeField, Min(0.01f)] private float pressSeconds = 0.04f;
        [SerializeField, Range(0f, 1f)] private float denyAmplitude = 0.6f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        private Selectable control;
        private AudioSource source;

        // 当前按住此按钮的手；鼠标点击时为空。推进按钮的持续震动由飞行反馈读取它。
        public HapticImpulsePlayer HeldBy { get; private set; }
        public bool IsHeld { get; private set; }

        private void Awake()
        {
            control = GetComponent<Selectable>();
            source = FeedbackSounds.CreateSource(transform, "Button Feedback Audio", 1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // XR 射线点击带有交互器信息；鼠标点击没有，只播放声音。
            var hand = HandHaptics.FromInteractor((eventData as TrackedDeviceEventData)?.interactor);
            if (control.IsInteractable())
            {
                FeedbackSounds.Play(source, FeedbackSound.Click, volume);
                HandHaptics.Pulse(hand, pressAmplitude, pressSeconds);
                HeldBy = hand;
                IsHeld = true;
            }
            else
            {
                FeedbackSounds.Play(source, FeedbackSound.Deny, volume * 0.8f);
                if (hand != null) StartCoroutine(HandHaptics.DenyPattern(hand, denyAmplitude));
            }
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();
        private void OnDisable() => Release();

        private void Release()
        {
            HeldBy = null;
            IsHeld = false;
        }
    }
}
