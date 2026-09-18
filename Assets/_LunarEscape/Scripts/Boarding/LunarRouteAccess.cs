using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape
{
    // 门未开放时，外面整条路线都不可传送；失败时也不能继续移动。
    public sealed class LunarRouteAccess : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private TeleportationArea[] areas;
        [SerializeField] private GameObject cabinDoor;
        [SerializeField] private TeleportationArea cabinTeleport;

        public void Configure(StationMission task, TeleportationArea[] surfaces, GameObject hatch, TeleportationArea insideCabin)
        {
            if (mission != null) mission.PhaseChanged -= Refresh;
            mission = task;
            areas = surfaces;
            cabinDoor = hatch;
            cabinTeleport = insideCabin;
            if (isActiveAndEnabled) mission.PhaseChanged += Refresh;
            Refresh(mission.Phase);
        }

        private void OnEnable()
        {
            if (mission == null) return;
            mission.PhaseChanged += Refresh;
            Refresh(mission.Phase);
        }
        private void OnDisable() { if (mission != null) mission.PhaseChanged -= Refresh; }
        private void Refresh(StationMissionPhase phase)
        {
            foreach (var area in areas)
                if (area != null) area.enabled = phase == StationMissionPhase.Evacuation
                    || (phase == StationMissionPhase.Completed && area == cabinTeleport);
            if (cabinDoor != null) cabinDoor.SetActive(phase == StationMissionPhase.Completed);
        }
    }
}
