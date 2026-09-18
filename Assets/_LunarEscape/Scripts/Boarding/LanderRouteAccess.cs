using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape
{
    public sealed class LanderRouteAccess : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private TeleportationArea[] areas;
        public void Configure(StationMission task, TeleportationArea[] surfaces) { mission=task; areas=surfaces; }
        private void OnEnable() { if(mission!=null) { mission.PhaseChanged+=Refresh; Refresh(mission.Phase); } }
        private void OnDisable() { if(mission!=null) mission.PhaseChanged-=Refresh; }
        private void Refresh(StationMissionPhase phase)
        {
            foreach(var area in areas) if(area!=null) area.enabled=phase==StationMissionPhase.Evacuation;
        }
    }
}
