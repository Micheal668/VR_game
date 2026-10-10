using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    [RequireComponent(typeof(XRSocketInteractor))]
    public sealed class StationPatchSocket : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [SerializeField] private StationPatchPuzzle puzzle;
        [SerializeField] private int identity;
        [SerializeField] private CockpitLamp lamp;
        private XRSocketInteractor socket;
        private AudioSource audioSource;
        public XRSocketInteractor Socket => socket != null ? socket : GetComponent<XRSocketInteractor>();
        public int Identity => identity;
        public bool Correct => puzzle != null && !puzzle.IsResetting && Socket.hasSelection && Socket.firstInteractableSelected is Component c
            && c.TryGetComponent<StationPatchModule>(out var plug) && plug.Puzzle == puzzle && plug.Identity == identity;
        public bool canProcess => isActiveAndEnabled;
        public void Configure(StationPatchPuzzle owner, int id, CockpitLamp indicator)
        { puzzle = owner; identity = id; lamp = indicator; }
        private void Awake()
        {
            socket = GetComponent<XRSocketInteractor>();
            audioSource = FeedbackSounds.CreateSource(transform, "Patch Socket Audio", 1);
        }
        private void OnEnable()
        {
            Socket.selectFilters.Add(this); Socket.hoverFilters.Add(this);
            Socket.selectEntered.AddListener(Inserted);
        }
        private void OnDisable()
        {
            Socket.selectFilters.Remove(this); Socket.hoverFilters.Remove(this);
            Socket.selectEntered.RemoveListener(Inserted);
        }
        private bool Accept(object value) => puzzle != null && puzzle.Active && value is Component c
            && c.TryGetComponent<StationPatchModule>(out var module) && module.Puzzle == puzzle;
        public bool Process(IXRSelectInteractor hand, IXRSelectInteractable item) => hand.IsSelecting(item) || Accept(item);
        public bool Process(IXRHoverInteractor hand, IXRHoverInteractable item) => Accept(item);
        private void Inserted(SelectEnterEventArgs args)
        {
            if (!Accept(args.interactableObject)) { Clear(); return; }
            FeedbackSounds.Play(audioSource, Correct ? FeedbackSound.Click : FeedbackSound.Knock, .7f);
            puzzle.Inserted(this);
        }
        private void Update()
        {
            if (lamp != null) lamp.State = Correct ? LampState.Done : Socket.hasSelection ? LampState.Fault
                : puzzle != null && puzzle.Active ? LampState.Next : LampState.Off;
        }
        public void Clear()
        {
            if (Socket.hasSelection && Socket.interactionManager != null)
                Socket.interactionManager.SelectExit((IXRSelectInteractor)Socket, Socket.firstInteractableSelected);
        }
    }
}
