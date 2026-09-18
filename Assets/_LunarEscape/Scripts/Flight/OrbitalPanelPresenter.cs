using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 同一飞行面板上的入轨仪表和操作按钮；时钟仍由 StationMissionSession 统一推进。
    public sealed class OrbitalPanelPresenter : MonoBehaviour
    {
        [SerializeField] private AscentMission mission;
        [SerializeField] private LocalizedText telemetry;
        [SerializeField] private LocalizedText guidance;
        [SerializeField] private Button circularize;
        public Button CircularizeButton=>circularize;
        public void Configure(AscentMission task,LocalizedText instruments,LocalizedText instructions,Button control)
        {mission=task;telemetry=instruments;guidance=instructions;circularize=control;}
        private void OnEnable(){if(mission!=null){mission.Changed+=Refresh;Refresh();}}
        private void OnDisable(){if(mission!=null)mission.Changed-=Refresh;}
        private void Refresh()
        {
            var sample=OrbitalTrajectory.Sample(mission);
            telemetry.SetKey("orbit.telemetry",(sample.Altitude/1000).ToString("0.0"),sample.SpeedKmS.ToString("0.00"),sample.Pitch.ToString("0"));
            if(mission.Docking!=null)telemetry.SetKey("dock.ascent.telemetry",(sample.Altitude/1000).ToString("0.0"),sample.SpeedKmS.ToString("0.00"),sample.Pitch.ToString("0"),mission.MainFuel.ToString("0"),mission.Docking.RcsFuel.ToString("0"));
            guidance.SetKey(mission.Phase==AscentPhase.Orbit ? "orbit.guidance.stable" : mission.Phase==AscentPhase.Circularizing ? "orbit.guidance.burning"
                : mission.CanCircularize ? "orbit.guidance.ready" : mission.Phase==AscentPhase.Startup || mission.Phase==AscentPhase.Ignition ? "orbit.guidance.startup" : "orbit.guidance.ascent",
                Mathf.CeilToInt(mission.Phase==AscentPhase.Circularizing ? mission.PhaseRemaining : Mathf.Max(0,mission.OrbitAscentSeconds-mission.AirborneSeconds)));
            circularize.interactable=mission.CanCircularize;
            circularize.gameObject.SetActive(mission.Phase!=AscentPhase.Orbit);
        }
    }
}
