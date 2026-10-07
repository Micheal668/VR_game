using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape
{
    // 阶段决定门、传送面和警报表现；它们不能反过来决定任务是否成功。
    public sealed class MissionEnvironment : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private GameObject door;
        [SerializeField] private TeleportationArea corridorTeleport;
        [SerializeField] private Light[] alarmLights;
        [SerializeField] private Renderer exitIndicator;
        [SerializeField] private LocalizedText exitLabel;
        [SerializeField] private AudioSource alarmAudio;
        [Tooltip("主场景：撤离阶段只有气闸放行后才开门。为空时警报一响就开门（旧场景）。")]
        [SerializeField] private AirlockRepair airlock;
        private MaterialPropertyBlock properties;

        public bool IsDoorOpen => door != null && !door.activeSelf;

        public void Configure(StationMission source, GameObject exitDoor, TeleportationArea teleport,
            Light[] lights, Renderer indicator, LocalizedText label, AudioSource audio)
        {
            if (isActiveAndEnabled && mission != null) mission.PhaseChanged -= Refresh;
            mission = source;
            door = exitDoor;
            corridorTeleport = teleport;
            alarmLights = lights;
            exitIndicator = indicator;
            exitLabel = label;
            alarmAudio = audio;
            if (isActiveAndEnabled && mission != null) mission.PhaseChanged += Refresh;
            if (mission != null) Refresh(mission.Phase);
        }

        public void ConfigureAirlock(AirlockRepair repair)
        {
            if (isActiveAndEnabled && airlock != null) airlock.Changed -= OnAirlockChanged;
            airlock = repair;
            if (isActiveAndEnabled && airlock != null) airlock.Changed += OnAirlockChanged;
            if (mission != null) Refresh(mission.Phase);
        }

        private void OnEnable()
        {
            if (mission == null) return;
            mission.PhaseChanged += Refresh;
            if (airlock != null) airlock.Changed += OnAirlockChanged;
            Refresh(mission.Phase);
        }

        private void OnDisable()
        {
            if (mission != null) mission.PhaseChanged -= Refresh;
            if (airlock != null) airlock.Changed -= OnAirlockChanged;
            if (alarmAudio != null) alarmAudio.Stop();
        }

        // 撤离中途修好气闸时立即开门，不必等下一个阶段事件。
        private void OnAirlockChanged() { if (mission != null) Refresh(mission.Phase); }

        private void Refresh(StationMissionPhase phase)
        {
            bool open = phase == StationMissionPhase.Evacuation || phase == StationMissionPhase.Completed
                || phase == StationMissionPhase.Failed;
            // 卡死的气闸不会因为警报响起而自己打开。
            bool jammed = airlock != null && !airlock.IsReleased && phase != StationMissionPhase.Completed;
            if (jammed) open = false;
            bool alarm = phase == StationMissionPhase.Evacuation;
            if (door != null) door.SetActive(!open);
            // 通道在警报前不接受传送，防止绕过关闭的门。
            if (corridorTeleport != null) corridorTeleport.enabled = open;
            if (exitLabel != null) exitLabel.SetKey(phase == StationMissionPhase.Failed
                ? "mission.exit.failed" : open ? "mission.exit.open" : jammed ? "airlock.exit.jammed" : "mission.exit.locked");
            if (alarmLights != null)
                foreach (var light in alarmLights)
                    if (light != null) light.enabled = alarm || phase == StationMissionPhase.Failed;

            if (exitIndicator != null)
            {
                properties ??= new MaterialPropertyBlock();
                exitIndicator.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", phase == StationMissionPhase.Failed ? new Color(1f, 0.25f, 0.15f)
                    : open ? new Color(0.15f, 1f, 0.6f) : new Color(1f, 0.65f, 0.18f));
                exitIndicator.SetPropertyBlock(properties);
            }
            // 稳定红光与轻提示音，不摇晃摄像机，也不使用闪烁效果。
            if (alarmAudio == null || !Application.isPlaying) return;
            if (alarm && !alarmAudio.isPlaying) alarmAudio.Play();
            else if (!alarm) alarmAudio.Stop();
        }
    }
}
