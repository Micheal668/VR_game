using UnityEngine;

namespace LunarEscape
{
    // 真空中的声音：玩家所在处没有空气时（舱外、泄压后的基地、抽空的气闸室），
    // 外界声音只剩经航天服传来的沉闷低频——在听者上加低通滤波，截止频率平滑下降。
    // 无线电语音、音乐、手中工具、航天服内的呼吸各自的声源设置了 bypassListenerEffects，不受影响。
    [RequireComponent(typeof(AudioListener))]
    public sealed class SpaceAudio : MonoBehaviour
    {
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private AscentMission flight;
        [SerializeField] private DayOneMission dayOne;
        [SerializeField] private SuitLocker dayOneSuit;
        [SerializeField, Range(200f, 3000f)] private float vacuumCutoff = 650f;
        private AudioLowPassFilter filter;

        public bool InVacuum { get; private set; }
        public float Cutoff => filter != null ? filter.cutoffFrequency : 22000f;

        public void Configure(LifeSupportMission support, AscentMission ascent, DayOneMission day, SuitLocker suit)
        { life = support; flight = ascent; dayOne = day; dayOneSuit = suit; }

        private void Awake()
        {
            filter = GetComponent<AudioLowPassFilter>();
            if (filter == null) filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
        }

        // 登上飞船后舱内有气；地面阶段按生命保障或第一天的气闸判断。
        private bool HasAir => flight != null && flight.IsLocked
            || (life != null ? life.HasBaseAir || !life.IsConfigured : dayOne == null || dayOne.PlayerHasAir);


        private void Update()
        {
            InVacuum = !HasAir;
            float target = InVacuum ? vacuumCutoff : 22000f;
            // 按对数平滑，避免截止频率线性变化时听感突兀。
            float current = Mathf.Log(filter.cutoffFrequency), goal = Mathf.Log(target);
            filter.cutoffFrequency = Mathf.Exp(Mathf.MoveTowards(current, goal, Time.deltaTime * 4f));
        }

        private void OnDisable() { if (filter != null) filter.cutoffFrequency = 22000f; }
    }
}
