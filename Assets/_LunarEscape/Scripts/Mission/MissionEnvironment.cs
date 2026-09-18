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

        private void OnEnable()
        {
            if (mission == null) return;
            mission.PhaseChanged += Refresh;
            Refresh(mission.Phase);
        }

        private void OnDisable()
        {
            if (mission != null) mission.PhaseChanged -= Refresh;
            if (alarmAudio != null) alarmAudio.Stop();
        }

        private void Refresh(StationMissionPhase phase)
        {
            bool open = phase == StationMissionPhase.Evacuation || phase == StationMissionPhase.Completed
                || phase == StationMissionPhase.Failed;
            bool alarm = phase == StationMissionPhase.Evacuation;
            if (door != null) door.SetActive(!open);
            // 通道在警报前不接受传送，防止绕过关闭的门。
            if (corridorTeleport != null) corridorTeleport.enabled = open;
            if (exitLabel != null) exitLabel.SetKey(phase == StationMissionPhase.Failed
                ? "mission.exit.failed" : open ? "mission.exit.open" : "mission.exit.locked");
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
