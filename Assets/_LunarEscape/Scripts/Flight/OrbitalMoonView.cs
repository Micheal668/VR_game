using UnityEngine;

namespace LunarEscape
{
    // 月面近景有真实起伏；进入高空后淡入球形月球，保持相同的姿态与地平线方向。
    // 缩小远景的渲染坐标以保留深度精度，不改变仪表中的公里数和轨道判定。
    [DefaultExecutionOrder(10002)]
    public sealed class OrbitalMoonView : MonoBehaviour
    {
        [SerializeField] private AscentMission mission;
        [SerializeField] private Transform moon;
        [SerializeField] private GameObject terrain;
        [SerializeField] private Renderer[] terrainRenderers;
        private MaterialPropertyBlock property;
        private float orbitAge;
        public float Blend {get;private set;}
        public Transform Moon=>moon;
        public void Configure(AscentMission task,Transform sphere,GameObject localTerrain)
        {mission=task;moon=sphere;terrain=localTerrain;terrainRenderers=terrain.GetComponentsInChildren<Renderer>(true);}
        private void OnEnable(){property=new MaterialPropertyBlock();orbitAge=0;Apply();}
        private void LateUpdate(){if(mission.Phase==AscentPhase.Orbit)orbitAge+=Time.deltaTime;Apply();}
        private void Apply()
        {
            if(mission==null || moon==null)return;
            var sample=OrbitalTrajectory.Sample(mission);
            Blend=OrbitalTrajectory.Smooth((sample.Altitude-900)/2100);
            moon.gameObject.SetActive(Blend>0);terrain.SetActive(Blend<1);
            foreach(var r in terrainRenderers){r.GetPropertyBlock(property);property.SetFloat("_Visibility",1-Blend);r.SetPropertyBlock(property);}
            var renderer=moon.GetComponent<Renderer>();renderer.GetPropertyBlock(property);property.SetFloat("_Visibility",Blend);renderer.SetPropertyBlock(property);
            Quaternion attitude=Quaternion.AngleAxis(sample.Pitch-OrbitalTrajectory.ViewBank(sample.Altitude),Vector3.forward),inverse=Quaternion.Inverse(attitude);
            float radius=OrbitalTrajectory.MoonRadiusKm*10;
            moon.localPosition=inverse*(Vector3.down*(radius+sample.Altitude*.01f));
            moon.localRotation=inverse*Quaternion.Euler(20,-40+sample.Downrange/1000/OrbitalTrajectory.MoonRadiusKm*Mathf.Rad2Deg+orbitAge*.051f,90);
            moon.localScale=Vector3.one*radius;
        }
    }
}
