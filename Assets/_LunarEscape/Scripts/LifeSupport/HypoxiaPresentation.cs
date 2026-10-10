using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    public sealed class HypoxiaPresentation : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private Image edge;
        private AudioSource breathing;
        public float Pulse { get; private set; }
        public AudioSource Breathing => breathing;
        public void Configure(LifeSupportMission source, Image border) { life = source; edge = border; }
        private void Awake()
        {
            breathing = FeedbackSounds.CreateLoop(transform, "Rapid Breathing", FeedbackSound.HurriedBreath, 0);
            breathing.volume = .85f;
        }
        private void Update()
        {
            bool distress = life != null && life.IsGroundActive && !life.CanBreathe;
            if (distress && !breathing.isPlaying) breathing.Play();
            if (!distress && breathing.isPlaying) breathing.Stop();
            // Read the actual audio playback position: the red edge and audible inhale
            // stay in phase even if rendering misses frames.
            float t = breathing.isPlaying ? breathing.time : 0;
            Pulse = distress ? .28f + .55f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .48f))), 1.4f) : 0;
            if (edge != null) edge.color = new Color(.85f, .015f, .015f, Pulse);
        }
        private void OnDisable() { if (breathing != null) breathing.Stop(); if (edge != null) edge.color = Color.clear; }
    }
}
