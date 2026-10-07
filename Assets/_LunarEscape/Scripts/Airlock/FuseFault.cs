using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 故障一：舱门驱动断电。掀开配电盒盖 → 拔出烧坏的保险丝 → 找到备用保险丝 → 插进插座。
    // 插座使用官方 XRSocketInteractor 吸附；这里只限制它只接受保险丝、且盖子打开时才接受新插入。
    public sealed class FuseFault : AirlockFault, IXRSelectFilter, IXRHoverFilter
    {
        [SerializeField] private CockpitSwitch cover;
        [SerializeField] private XRSocketInteractor socket;
        [SerializeField] private AirlockFuse burnt;
        [SerializeField] private AirlockFuse spare;
        [SerializeField] private CockpitLamp lamp;
        [Tooltip("备件架上的指示灯：断电未修好时闪烁，帮玩家找到备用保险丝。")]
        [SerializeField] private CockpitLamp spareLamp;
        private AudioSource source;

        public bool CoverOpen => cover != null && cover.Latched;
        public override bool IsFixed => socket != null && socket.hasSelection
            && socket.firstInteractableSelected is Component c && c.TryGetComponent(out AirlockFuse fuse) && fuse.Good;
        public CockpitSwitch Cover => cover;
        public XRSocketInteractor Socket => socket;
        public AirlockFuse Burnt => burnt;
        public AirlockFuse Spare => spare;
        public bool canProcess => isActiveAndEnabled;

        public void Configure(CockpitSwitch lid, XRSocketInteractor holder, AirlockFuse burntFuse, AirlockFuse spareFuse, CockpitLamp indicator)
        { cover = lid; socket = holder; burnt = burntFuse; spare = spareFuse; lamp = indicator; }

        private void Awake() => source = FeedbackSounds.CreateSource(socket != null ? socket.transform : transform, "Fuse Audio", 1f);

        private void OnEnable()
        {
            socket.selectFilters.Add(this);
            socket.hoverFilters.Add(this);
            socket.selectEntered.AddListener(OnInserted);
            socket.selectExited.AddListener(OnRemoved);
        }

        private void OnDisable()
        {
            socket.selectFilters.Remove(this);
            socket.hoverFilters.Remove(this);
            socket.selectEntered.RemoveListener(OnInserted);
            socket.selectExited.RemoveListener(OnRemoved);
        }

        public void ConfigureSpareLamp(CockpitLamp indicator) => spareLamp = indicator;

        private void Update()
        {
            if (lamp != null) lamp.State = IsFixed ? LampState.Done : CanWork ? LampState.Next : LampState.Off;
            if (spareLamp != null) spareLamp.State = !IsFixed && CanWork && !spare.Grab.isSelected ? LampState.Next : LampState.Off;
        }

        public bool IsSeated(AirlockFuse fuse) => socket != null && socket.IsSelecting(fuse.Grab);

        // 插座只认保险丝；已经插着的保持不变，新的插入需要盖子打开且任务进行中。
        // 开局与重开时烧坏的保险丝要能装回座里，因此它总是被接受；插回去也修不好故障。
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
            => interactor.IsSelecting(interactable) || IsBurnt(interactable) || IsFuse(interactable) && CoverOpen && CanWork;

        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
            => IsBurnt(interactable) || IsFuse(interactable) && CoverOpen;

        private bool IsBurnt(object interactable) => burnt != null && interactable is Component c && c.gameObject == burnt.gameObject;

        private static bool IsFuse(object interactable) => interactable is Component c && c.TryGetComponent(out AirlockFuse _);

        private void OnInserted(SelectEnterEventArgs args)
        {
            bool good = args.interactableObject is Component c && c.TryGetComponent(out AirlockFuse fuse) && fuse.Good;
            // 好的保险丝插到位：清脆的咔哒加通电的闷响；烧坏的插回去只有咔哒声。
            FeedbackSounds.Play(source, FeedbackSound.Click, 0.8f);
            if (good) FeedbackSounds.Play(source, FeedbackSound.Clunk, 0.7f);
            RaiseChanged();
        }

        private void OnRemoved(SelectExitEventArgs args)
        {
            FeedbackSounds.Play(source, FeedbackSound.Release, 0.6f);
            RaiseChanged();
        }

        public override void ResetFault()
        {
            if (socket == null || !isActiveAndEnabled) return;
            cover.Latched = false;
            var manager = socket.interactionManager;
            if (manager != null)
            {
                if (socket.hasSelection) manager.SelectExit((IXRSelectInteractor)socket, socket.firstInteractableSelected);
                foreach (var fuse in new[] { burnt, spare })
                    if (fuse.Grab.isSelected) manager.CancelInteractableSelection((IXRSelectInteractable)fuse.Grab);
            }
            spare.RestoreStart();
            burnt.RestoreStart();
            // 等官方抓取组件在本帧结束释放，再把烧坏的保险丝放回插座。
            StartCoroutine(ReseatBurnt());
        }

        private IEnumerator ReseatBurnt()
        {
            yield return null;
            var manager = socket.interactionManager;
            if (manager != null && !socket.hasSelection)
                manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)burnt.Grab);
            RaiseChanged();
        }
    }
}
