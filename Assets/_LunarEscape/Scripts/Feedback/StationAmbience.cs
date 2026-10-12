using System;
using UnityEngine;

namespace LunarEscape
{
    // 各舱段自己的背景声：大厅与走廊是通风气流，工程舱是配电设备的工频嗡声，实验室是仪器风扇和提示音，
    // 卧室只有很轻的通风。断电（总闸未合或基地没电）时降到很低，合闸后各舱段“活过来”；泄压后舱内没有空气，声音也随之消失。
    public sealed class StationAmbience : MonoBehaviour
    {
        [Serializable]
        public sealed class Zone
        {
            public string name;
            public Vector3 center;
            public FeedbackSound sound = FeedbackSound.VentLoop;
            [Range(0f, 1f)] public float volume = 0.25f;
            [Min(0.5f)] public float reach = 5f;
        }

        [SerializeField] private Zone[] zones = new Zone[0];
        [SerializeField] private HabitatBreaker breaker;
        [SerializeField] private LifeSupportMission life;
        [Tooltip("断电时保留的音量比例（应急设备仍在运转）。")]
        [SerializeField, Range(0f, 1f)] private float blackoutLevel = 0.12f;
        private AudioSource[] sources = new AudioSource[0];

        public float Level { get; private set; } = 1f;
        public Zone[] Zones => zones;

        public void Configure(Zone[] rooms, HabitatBreaker mainBreaker, LifeSupportMission support)
        { zones = rooms; breaker = mainBreaker; life = support; }

        private void Awake()
        {
            sources = new AudioSource[zones.Length];
            for (int i = 0; i < zones.Length; i++)
            {
                var anchor = new GameObject("Ambience " + zones[i].name).transform;
                anchor.SetParent(transform, false);
                anchor.position = zones[i].center;
                sources[i] = FeedbackSounds.CreateLoop(anchor, "Loop", zones[i].sound, 1f);
                sources[i].minDistance = zones[i].reach * 0.45f;
                sources[i].maxDistance = zones[i].reach;
                // 每个声源从不同位置开始播放，避免几处通风声完全同相。
                sources[i].timeSamples = (i * 9973) % Mathf.Max(1, sources[i].clip.samples);
            }
        }

        private void OnEnable() { foreach (var s in sources) if (s != null && !s.isPlaying) s.Play(); }

        private void Update()
        {
            float powered = breaker == null || breaker.IsOn ? 1f : blackoutLevel;
            if (life != null && life.IsConfigured && life.BasePower <= 0f) powered = blackoutLevel * 0.5f;
            // 泄压后舱内是真空：设备仍在转，但空气里已经没有声音。
            if (life != null && life.DoorOpen) powered = 0f;
            Level = Mathf.MoveTowards(Level, powered, Time.deltaTime * 0.6f);
            for (int i = 0; i < sources.Length; i++) if (sources[i] != null) sources[i].volume = zones[i].volume * Level;
        }
    }
}
