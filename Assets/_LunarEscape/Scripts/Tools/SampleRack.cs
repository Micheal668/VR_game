using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 实验室的岩芯样品架：一排插槽，只接受岩芯；每放进一根亮一盏绿灯。
    public sealed class SampleRack : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [SerializeField] private XRSocketInteractor[] slots = new XRSocketInteractor[0];
        [SerializeField] private CockpitLamp[] lamps = new CockpitLamp[0];
        private AudioSource source;

        public int Stored { get; private set; }
        public int Capacity => slots.Length;
        public XRSocketInteractor[] Slots => slots;
        public bool canProcess => isActiveAndEnabled;
        public event Action<SampleRack> Changed;

        public void Configure(XRSocketInteractor[] sockets, CockpitLamp[] indicators) { slots = sockets; lamps = indicators; }

        private void Awake() => source = FeedbackSounds.CreateSource(transform, "Sample Rack Audio", 1f);

        private void OnEnable()
        {
            foreach (var slot in slots)
            {
                slot.selectFilters.Add(this); slot.hoverFilters.Add(this);
                slot.selectEntered.AddListener(OnStored); slot.selectExited.AddListener(OnTaken);
            }
            Count();
        }

        private void OnDisable()
        {
            foreach (var slot in slots)
            {
                slot.selectFilters.Remove(this); slot.hoverFilters.Remove(this);
                slot.selectEntered.RemoveListener(OnStored); slot.selectExited.RemoveListener(OnTaken);
            }
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => IsCore(interactable);
        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => IsCore(interactable);
        private static bool IsCore(object interactable) => interactable is Component c && c.TryGetComponent(out CoreSample _);

        private void OnStored(SelectEnterEventArgs args) { FeedbackSounds.Play(source, FeedbackSound.Click, 0.8f); FeedbackSounds.Play(source, FeedbackSound.Beep, 0.6f); Count(); }
        private void OnTaken(SelectExitEventArgs args) => Count();

        private void Count()
        {
            int stored = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                bool full = slots[i] != null && slots[i].hasSelection;
                if (full) stored++;
                if (i < lamps.Length && lamps[i] != null) lamps[i].State = full ? LampState.Done : LampState.Next;
            }
            if (stored == Stored) return;
            Stored = stored;
            Changed?.Invoke(this);
        }
    }
}
