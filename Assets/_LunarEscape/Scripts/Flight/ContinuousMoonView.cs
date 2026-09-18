using System;
using UnityEngine;

namespace LunarEscape
{
    [DefaultExecutionOrder(10003)]
    public sealed class ContinuousMoonView : MonoBehaviour
    {
        [SerializeField] private AscentMission mission;
        [SerializeField] private Transform outsideWorld;
        [SerializeField] private Vector3 launchPosition=new(48,0,1.7f);
        [SerializeField] private Renderer[] surfaces;
        [Header("环绕视觉；度/秒，0 可暂停，负数反转")]
        [SerializeField,Range(-2,2)] private float surfaceDegreesPerSecond=.12f;
        private MaterialPropertyBlock properties;
        public float SurfaceAngle {get;private set;}
        public float SurfaceDegreesPerSecond {get=>surfaceDegreesPerSecond;set{if(float.IsFinite(value))surfaceDegreesPerSecond=Mathf.Clamp(value,-2,2);}}
        public void Configure(AscentMission task,Transform world,Renderer[] renderers)
        {mission=task;outsideWorld=world;surfaces=renderers;}
        private void OnEnable(){properties=new MaterialPropertyBlock();if(mission!=null)mission.PhaseChanged+=OnPhase;Apply();}
        private void OnDisable(){if(mission!=null)mission.PhaseChanged-=OnPhase;}
        private void OnPhase(AscentPhase phase){if(phase==AscentPhase.AwaitingBoarding)SurfaceAngle=0;}
        private void LateUpdate()
        {
            if(mission==null)return;
            if(mission.Phase==AscentPhase.AwaitingBoarding)SurfaceAngle=0;
            if(mission.AirborneSeconds>=mission.OrbitAscentSeconds && mission.Phase!=AscentPhase.Failed)AdvanceSurface(Time.deltaTime);
            Apply();
        }
        public void AdvanceSurface(float seconds)
        {if(float.IsFinite(seconds)&&seconds>0)SurfaceAngle=Mathf.Repeat(SurfaceAngle+seconds*surfaceDegreesPerSecond,360);}
        private void Apply()
        {
            if(mission==null || outsideWorld==null)return;
            properties??=new MaterialPropertyBlock();
            foreach(var surface in surfaces)
            {
                if(surface==null)continue;
                surface.GetPropertyBlock(properties);properties.SetFloat("_SurfaceAngle",SurfaceAngle);properties.SetFloat("_RenderScale",outsideWorld.localScale.x);surface.SetPropertyBlock(properties);
            }
        }
        public static void EvaluatePose(OrbitalSample sample,Vector3 launchPosition,out Vector3 position,out Quaternion attitude)
        {
            const double radius=1737400;
            double theta=sample.Downrange/radius,sin=Math.Sin(theta),cos=Math.Cos(theta);
            // 在月球曲面上前进，使用 double 计算近地小高度，避免大数相减丢精度。
            position=launchPosition+new Vector3((float)(-(radius+sample.Altitude)*sin),(float)(radius*(cos-1)+sample.Altitude*cos),0);
            float pitch=sample.Pitch-OrbitalTrajectory.ViewBank(sample.Altitude)+(float)(theta*Mathf.Rad2Deg);
            attitude=Quaternion.AngleAxis(pitch,Vector3.forward);
        }
    }
}
