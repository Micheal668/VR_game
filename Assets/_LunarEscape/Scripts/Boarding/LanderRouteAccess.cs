using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape
{
    public sealed class LanderRouteAccess : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private TeleportationArea[] areas;
        public void Configure(StationMission task, TeleportationArea[] surfaces) { mission=task; areas=surfaces; }
        private void OnEnable() { if(mission!=null) { mission.PhaseChanged+=Refresh; if(mission.LifeSupport!=null)mission.LifeSupport.Changed+=RefreshLife; Refresh(mission.Phase); } }
        private void OnDisable() { if(mission!=null) { mission.PhaseChanged-=Refresh; if(mission.LifeSupport!=null)mission.LifeSupport.Changed-=RefreshLife; } }
        private void RefreshLife()=>Refresh(mission.Phase);
        private void Refresh(StationMissionPhase phase)
        {
            foreach(var area in areas) if(area!=null) area.enabled=mission.LifeSupport!=null
                ?mission.LifeSupport.IsGroundActive&&mission.LifeSupport.DoorOpen:phase==StationMissionPhase.Evacuation;
        }
    }
}
