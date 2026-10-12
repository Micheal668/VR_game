using UnityEngine;

namespace LunarEscape
{
    // 真空中的声音：玩家所在处没有空气时（舱外、泄压后的基地、抽空的气闸室），
    // 外界声音只剩经航天服传来的沉闷低频——在听者上加低通滤波，截止频率平滑下降。
    // 无线电语音、音乐、手中工具、航天服内的呼吸各自的声源设置了 bypassListenerEffects，不受影响。
    // 同时负责航天服内平稳的呼吸声：穿着航天服、处在真空中时才听得到。
    [RequireComponent(typeof(AudioListener))]
    public sealed class SpaceAudio : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private AscentMission flight;
        [SerializeField] private DayOneMission dayOne;
        [SerializeField] private SuitLocker dayOneSuit;
        [SerializeField, Range(200f, 3000f)] private float vacuumCutoff = 650f;
        [SerializeField, Range(0f, 1f)] private float breathVolume = 0.22f;
        private AudioLowPassFilter filter;
        private AudioSource breathing;

        public bool InVacuum { get; private set; }
        public float Cutoff => filter != null ? filter.cutoffFrequency : 22000f;

        public void Configure(LifeSupportMission support, AscentMission ascent, DayOneMission day, SuitLocker suit)
        { life = support; flight = ascent; dayOne = day; dayOneSuit = suit; }

        private void Awake()
        {
            filter = GetComponent<AudioLowPassFilter>();
            if (filter == null) filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
            breathing = FeedbackSounds.CreateLoop(transform, "Suit Breathing", FeedbackSound.CalmBreath, 0f);
            breathing.bypassListenerEffects = true;
        }

        private void OnEnable() { if (!breathing.isPlaying) breathing.Play(); }

        // 登上飞船后舱内有气；地面阶段按生命保障或第一天的气闸判断。
        private bool HasAir => flight != null && flight.IsLocked
            || (life != null ? life.HasBaseAir || !life.IsConfigured : dayOne == null || dayOne.PlayerHasAir);

        private bool Suited => life != null ? life.SuitWorn : dayOneSuit != null && dayOneSuit.SuitWorn;

        private void Update()
        {
            InVacuum = !HasAir;
            float target = InVacuum ? vacuumCutoff : 22000f;
            // 按对数平滑，避免截止频率线性变化时听感突兀。
            float current = Mathf.Log(filter.cutoffFrequency), goal = Mathf.Log(target);
            filter.cutoffFrequency = Mathf.Exp(Mathf.MoveTowards(current, goal, Time.deltaTime * 4f));
            float breath = InVacuum && Suited ? breathVolume : 0f;
            breathing.volume = Mathf.MoveTowards(breathing.volume, breath, Time.deltaTime * 0.5f);
        }

        private void OnDisable() { if (filter != null) filter.cutoffFrequency = 22000f; breathing.volume = 0f; }
    }
}
