using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 第一天的航天服柜：握住挂着的航天服把手约 2.5 秒即穿好（松开暂停）。
    // 穿好后：挂着的那套消失，玩家身体换成航天服、肩灯可用。
    public sealed class SuitLocker : MonoBehaviour
    {
        [SerializeField] private XRBaseInteractable handle;
        [SerializeField] private Renderer[] hanging = new Renderer[0];
        [SerializeField] private Renderer[] playerSuit = new Renderer[0];
        [SerializeField] private Renderer[] playerUniform = new Renderer[0];
        [SerializeField] private GameObject shoulderLamps;
        [SerializeField] private TMPro.TMP_Text progressText;
        [SerializeField, Min(0.1f)] private float donSeconds = 2.5f;
        private readonly HapticRumble rumble = new();
        private AudioSource source;

        public bool SuitWorn { get; private set; }
        public float Progress { get; private set; }
        public XRBaseInteractable Handle => handle;
        public event Action<SuitLocker> Donned;

        public void Configure(XRBaseInteractable grip, Renderer[] rackSuit, Renderer[] suit, Renderer[] uniform, GameObject lamps, TMPro.TMP_Text readout)
        { handle = grip; hanging = rackSuit; playerSuit = suit; playerUniform = uniform; shoulderLamps = lamps; progressText = readout; }

        private void Awake()
        {
            source = FeedbackSounds.CreateSource(transform, "Suit Locker Audio", 1f);
            Show();
        }

        private void Update()
        {
            if (SuitWorn) return;
            bool holding = handle != null && handle.isSelected;
            Progress = holding ? Mathf.Min(1f, Progress + Time.deltaTime / donSeconds) : Progress;
            if (holding)
            {
                rumble.Drive(HandHaptics.FromInteractor(handle.firstInteractorSelecting), 0.15f + 0.3f * Progress);
                if (Progress >= 1f) Don();
            }
            if (progressText != null) progressText.text = Progress > 0f ? Mathf.RoundToInt(Progress * 100f) + "%" : "";
        }

        public void Don()
        {
            if (SuitWorn) return;
            SuitWorn = true; Progress = 1f;
            FeedbackSounds.Play(source, FeedbackSound.Hiss, 0.7f);
            FeedbackSounds.Play(source, FeedbackSound.Clunk, 0.6f);
            HandHaptics.Pulse(handle != null ? HandHaptics.FromInteractor(handle.firstInteractorSelecting) : null, 0.7f, 0.25f);
            if (progressText != null) progressText.text = "";
            Show();
            Donned?.Invoke(this);
        }

        private void Show()
        {
            foreach (var r in hanging) if (r != null) r.enabled = !SuitWorn;
            foreach (var r in playerSuit) if (r != null) r.enabled = SuitWorn;
            foreach (var r in playerUniform) if (r != null) r.enabled = !SuitWorn;
            if (shoulderLamps != null) shoulderLamps.SetActive(SuitWorn);
        }
    }
}
