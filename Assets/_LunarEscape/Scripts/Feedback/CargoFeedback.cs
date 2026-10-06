using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 物资收纳手感：物品真正进入腰包时发出闷响，并震动最后拿过它的手；
    // 超出数量、重量或类别额度时，正在尝试抓取的手收到“嗡嗡”两下。只监听清单，不改变额度规则。
    public sealed class CargoFeedback : MonoBehaviour
    {
        [SerializeField] private CargoInventory inventory;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float packAmplitude = 0.5f;
        [SerializeField, Range(0f, 1f)] private float denyAmplitude = 0.6f;
        private readonly Dictionary<CargoItem, CargoState> states = new();
        private readonly Dictionary<CargoItem, HapticImpulsePlayer> lastHands = new();
        private readonly Dictionary<CargoItem, UnityAction<SelectEnterEventArgs>> listeners = new();
        private CargoRejection lastRejection;
        private AudioSource source;

        public CargoInventory Inventory => inventory;

        public void Configure(CargoInventory owner) => inventory = owner;

        private void Awake()
        {
            // 腰包跟随玩家身体，声音放在玩家附近即可，使用较弱的空间化。
            source = FeedbackSounds.CreateSource(transform, "Cargo Feedback Audio", 0.2f);
        }

        private void OnEnable()
        {
            if (inventory == null) return;
            inventory.Changed += OnChanged;
            foreach (var item in inventory.Items)
            {
                if (item == null) continue;
                states[item] = item.State;
                var captured = item;
                // 收纳时抓取会被取消，届时已无法得知是哪只手，因此在抓起时记下。
                UnityAction<SelectEnterEventArgs> listener = args => lastHands[captured] = HandHaptics.FromInteractor(args.interactorObject);
                item.Grab.selectEntered.AddListener(listener);
                listeners[item] = listener;
            }
            lastRejection = inventory.LastRejection;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= OnChanged;
            // 只移除本组件添加的监听；CargoItem 自己的抓取监听必须保留。
            foreach (var pair in listeners)
                if (pair.Key != null) pair.Key.Grab.selectEntered.RemoveListener(pair.Value);
            listeners.Clear();
            states.Clear();
        }

        private void OnChanged()
        {
            foreach (var item in inventory.Items)
            {
                if (item == null) continue;
                states.TryGetValue(item, out var previous);
                if (item.State == CargoState.Packed && previous != CargoState.Packed)
                {
                    FeedbackSounds.Play(source, FeedbackSound.Pack, volume);
                    lastHands.TryGetValue(item, out var hand);
                    HandHaptics.Pulse(hand, packAmplitude, 0.08f);
                }
                states[item] = item.State;
            }

            var rejection = inventory.LastRejection;
            if (rejection != CargoRejection.None && rejection != lastRejection)
            {
                FeedbackSounds.Play(source, FeedbackSound.Deny, volume * 0.8f);
                var hand = FindGrabbingHand();
                if (hand != null && isActiveAndEnabled) StartCoroutine(HandHaptics.DenyPattern(hand, denyAmplitude));
            }
            lastRejection = rejection;
        }

        // 被拒绝的抓取不会产生选择事件；正悬停在物品上并按下握持键的那只手就是尝试抓取的手。
        private HapticImpulsePlayer FindGrabbingHand()
        {
            foreach (var item in inventory.Items)
            {
                if (item == null || !item.isActiveAndEnabled) continue;
                foreach (var hover in item.Grab.interactorsHovering)
                    if (hover is IXRSelectInteractor select && select.isSelectActive)
                        return HandHaptics.FromInteractor(select);
            }
            return null;
        }
    }
}
