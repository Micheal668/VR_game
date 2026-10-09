using System.Collections;
using TMPro;
using UnityEngine;

namespace LunarEscape
{
    // 关键行动的时间奖励：恢复照明、完成气闸抢修、救出指挥官各奖励一次（每轮任务）。
    // 奖励同时延长任务倒计时与可呼吸空气（舱门关着补基地空气，开门后补航天服氧气），
    // 并在视线前方浮现“+30 s”，让玩家明白行动换来了时间。
    public sealed class MissionTimeBonus : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private HabitatBreaker breaker;
        [SerializeField] private AirlockRepair airlock;
        [SerializeField] private CrewMission crew;
        [SerializeField] private Transform head;
        [SerializeField] private TMP_Text popup;
        [SerializeField, Min(0f)] private float lightsSeconds = 30f;
        [SerializeField, Min(0f)] private float airlockSeconds = 45f;
        [SerializeField, Min(0f)] private float rescueSeconds = 45f;
        private bool lightsGiven, airlockGiven, rescueGiven;
        private AudioSource source;
        private Coroutine showing;

        public float LightsSeconds => lightsSeconds;
        public float AirlockSeconds => airlockSeconds;
        public float RescueSeconds => rescueSeconds;
        public float TotalGranted { get; private set; }

        public void Configure(StationMission task, LifeSupportMission support, HabitatBreaker mainBreaker, AirlockRepair repair,
            CrewMission commander, Transform viewer, TMP_Text text)
        { mission = task; life = support; breaker = mainBreaker; airlock = repair; crew = commander; head = viewer; popup = text; }

        private void Awake()
        {
            source = FeedbackSounds.CreateSource(transform, "Bonus Audio", 0f);
            if (popup != null) popup.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (mission != null) mission.PhaseChanged += OnPhaseChanged;
            if (breaker != null) breaker.SwitchedOn += OnLights;
            if (airlock != null) airlock.Changed += OnAirlock;
            if (crew != null) crew.RescueCompleted += OnRescue;
        }

        private void OnDisable()
        {
            if (mission != null) mission.PhaseChanged -= OnPhaseChanged;
            if (breaker != null) breaker.SwitchedOn -= OnLights;
            if (airlock != null) airlock.Changed -= OnAirlock;
            if (crew != null) crew.RescueCompleted -= OnRescue;
        }

        private void OnPhaseChanged(StationMissionPhase phase)
        {
            if (phase != StationMissionPhase.Briefing) return;
            lightsGiven = airlockGiven = rescueGiven = false;
            TotalGranted = 0f;
        }

        private void OnLights() { if (!lightsGiven) lightsGiven = Grant(lightsSeconds); }
        private void OnAirlock() { if (!airlockGiven && airlock.IsReleased) airlockGiven = Grant(airlockSeconds); }
        private void OnRescue() { if (!rescueGiven) rescueGiven = Grant(rescueSeconds); }

        private bool Grant(float seconds)
        {
            if (seconds <= 0f || mission == null || !mission.AddBonusTime(seconds)) return false;
            if (life != null) life.AddAirSeconds(seconds);
            TotalGranted += seconds;
            FeedbackSounds.Play(source, FeedbackSound.Chime, 0.6f);
            if (popup != null && isActiveAndEnabled)
            {
                if (showing != null) StopCoroutine(showing);
                showing = StartCoroutine(Show(seconds));
            }
            return true;
        }

        // 浮现在视线前下方，缓慢上升并淡出；不跟随头部转动，避免挡住视线。
        private IEnumerator Show(float seconds)
        {
            popup.text = "+" + Mathf.RoundToInt(seconds) + " s";
            popup.gameObject.SetActive(true);
            Vector3 forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 start = head != null ? head.position + forward * 0.9f + Vector3.down * 0.15f : transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 2.2f)
            {
                popup.transform.SetPositionAndRotation(start + Vector3.up * (0.12f * t), Quaternion.LookRotation(forward));
                popup.alpha = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
                yield return null;
            }
            popup.gameObject.SetActive(false);
            showing = null;
        }
    }
}
