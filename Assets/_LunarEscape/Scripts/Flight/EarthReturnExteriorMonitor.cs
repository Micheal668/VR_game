using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LunarEscape
{
    // 独立单目外部摄像机渲染真实舱体，UI 层排除以避免监视器递归。
    public sealed class EarthReturnExteriorMonitor : MonoBehaviour
    {
        [SerializeField] private EarthReturnMission mission;
        [SerializeField] private Transform cabin;
        [SerializeField] private GameObject screen;
        [SerializeField] private RawImage display;
        [SerializeField] private LocalizedText stage;
        private Camera trackingCamera;
        private RenderTexture feed;
        public Camera TrackingCamera => trackingCamera;
        public RenderTexture Feed => feed;
        public bool UsesStereo => trackingCamera!=null && trackingCamera.GetUniversalAdditionalCameraData().allowXRRendering;
        public void Configure(EarthReturnMission task, Transform target, GameObject panel, RawImage image, LocalizedText label)
        { mission=task; cabin=target; screen=panel; display=image; stage=label; }
        private void LateUpdate()
        {
            if(mission==null || cabin==null)return;
            bool active=mission.InReturnCabin;
            screen.SetActive(active);
            if(!active){Release();return;}
            if(trackingCamera==null)
            {
                feed=new RenderTexture(768,432,24){name="Return exterior live feed",antiAliasing=1};feed.Create();
                var obj=new GameObject("Return exterior tracking camera");obj.transform.SetParent(transform,false);
                trackingCamera=obj.AddComponent<Camera>();trackingCamera.targetTexture=feed;
                trackingCamera.nearClipPlane=.08f;trackingCamera.farClipPlane=150000;trackingCamera.fieldOfView=50;
                trackingCamera.cullingMask=~(1<<5);trackingCamera.clearFlags=CameraClearFlags.Skybox;
                var data=trackingCamera.GetUniversalAdditionalCameraData();data.allowXRRendering=false;data.renderPostProcessing=false;
                display.texture=feed;
            }
            // 镜头随任务进度变焦，暂停时保持原位；开伞后把完整伞组纳入画面。
            float opening = mission.ParachutesDeployed ? Mathf.SmoothStep(0, 1, mission.ParachuteAge / 2) : 0;
            float entry = mission.Phase == EarthReturnPhase.Entry ? Mathf.SmoothStep(0, 1, mission.Elapsed / 3)
                : mission.HasEnteredAtmosphere || mission.Phase == EarthReturnPhase.Breakup || mission.Phase == EarthReturnPhase.SkipOut || mission.IsFailed ? 1 : 0;
            var offset = Vector3.Lerp(new Vector3(15,9,-22), new Vector3(8,4.5f,-11), entry);
            var target = Vector3.Lerp(new Vector3(0,2,0), new Vector3(0,1.6f,0), entry);
            offset = Vector3.Lerp(offset, new Vector3(20,12,-28), opening);
            target = Vector3.Lerp(target, new Vector3(0,7,0), opening);
            float release = mission.HeatShieldReleased ? Mathf.SmoothStep(0, 1, mission.HeatShieldAge / 1.5f) : 0;
            offset = Vector3.Lerp(offset, new Vector3(22,9,-31), release);
            target = Vector3.Lerp(target, new Vector3(0,5,0), release);
            var yaw=Quaternion.Euler(0,-mission.HeadingDegrees,0);
            trackingCamera.transform.SetPositionAndRotation(cabin.TransformPoint(yaw*offset),cabin.rotation*Quaternion.LookRotation(yaw*(target-offset)));
            stage.SetKey("return.phase."+mission.Phase);
        }
        private void Release()
        {
            if(trackingCamera!=null){trackingCamera.enabled=false;trackingCamera.targetTexture=null;Destroy(trackingCamera.gameObject);trackingCamera=null;}
            if(display!=null)display.texture=null;
            if(feed!=null){feed.Release();Destroy(feed);feed=null;}
        }
        private void OnDisable()=>Release();
    }
}
