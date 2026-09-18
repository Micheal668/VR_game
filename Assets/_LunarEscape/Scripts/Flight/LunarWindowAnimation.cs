using UnityEngine;

namespace LunarEscape
{
    // 月面是远处的贴图曲面；基地与碎片仅为远景，不推动玩家相机。
    public sealed class LunarWindowAnimation : MonoBehaviour
    {
        [SerializeField] private AscentMission mission;
        [SerializeField] private Transform landscape;
        [SerializeField] private Transform distantBase;
        [SerializeField] private Transform blast;
        [SerializeField] private Transform[] debris;
        [SerializeField] private AudioSource hullSound;
        [SerializeField] private Transform outsideWorld;
        [SerializeField] private Vector3 launchPosition;
        [SerializeField] private float explosionScale=1;
        [SerializeField] private bool continuousSurface;
        public void ConfigureContinuousSurface(bool enabled)=>continuousSurface=enabled;
        public float Altitude { get; private set; }
        public float PitchDegrees { get; private set; }
        public float Downrange { get; private set; }
        public Vector3 VirtualCraftPosition { get; private set; }
        public Quaternion VirtualCraftAttitude { get; private set; }
        public Transform OutsideWorld=>outsideWorld;
        private Vector3 landscapeStart, baseStart, blastStart;
        private float explosionAge = -1;
        private Material blastMaterial;
        public bool ExplosionVisible => blast != null && blast.gameObject.activeSelf;
        public float VisualProgress => mission != null ? Mathf.Clamp01(mission.AirborneSeconds / mission.Config.AscentVisualSeconds) : 0;
        public void Configure(AscentMission task, Transform terrain, Transform station, Transform explosion,
            Transform[] fragments, AudioSource audio)
        {
            mission = task; landscape = terrain; distantBase = station; blast = explosion; debris = fragments; hullSound = audio;
            landscapeStart = terrain.localPosition; baseStart = station.localPosition; blastStart = explosion.localPosition;
        }
        public void ConfigureWorld(Transform environment, Vector3 start, Transform baseVisual, Transform explosion,
            Transform[] fragments)
        {
            outsideWorld=environment; launchPosition=start; distantBase=baseVisual; blast=explosion; debris=fragments;
            explosionScale=14;
            baseStart=baseVisual.localPosition; blastStart=explosion.localPosition;
        }
        private void Awake()
        {
            landscapeStart = landscape.localPosition; baseStart = distantBase.localPosition; blastStart = blast.localPosition;
        }
        private void OnEnable()
        {
            if (mission == null) return;
            mission.Exploded += Explode;
            explosionAge = mission.BaseExploded ? 0 : -1;
            if (Application.isPlaying && blastMaterial == null) blastMaterial = blast.GetComponent<Renderer>().material;
            Apply(0);
        }
        private void OnDisable() { if (mission != null) mission.Exploded -= Explode; }
        private void OnDestroy() { if (blastMaterial != null) Destroy(blastMaterial); }
        private void Explode()
        {
            explosionAge = 0;
            Apply(0);
            if (hullSound != null && mission.Damage != AscentDamage.None) hullSound.Play();
        }
        private void Update() => Apply(Time.deltaTime);
        private void Apply(float delta)
        {
            float t = Mathf.SmoothStep(0, 1, VisualProgress);
            if(outsideWorld!=null)
            {
                // 先垂直脱离平台，再逐渐倾斜并增加水平位移。这是可调的游戏演出轨迹。
                float seconds=mission.AirborneSeconds;
                float vertical=Mathf.Clamp01(seconds/3f);
                float turn=Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-3)/Mathf.Max(1,mission.Config.AscentVisualSeconds-3)));
                Altitude=4*vertical+46*turn; Downrange=16*turn;
                float pitchBlend=Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-3)/3));
                PitchDegrees=Mathf.Atan2(Altitude,Mathf.Max(1,launchPosition.x-Downrange))*Mathf.Rad2Deg*pitchBlend;
                if(mission.Config.OrbitalFlight)
                {
                    var sample=OrbitalTrajectory.Sample(mission);Altitude=sample.Altitude;Downrange=sample.Downrange;PitchDegrees=sample.Pitch;
                }
                VirtualCraftPosition=launchPosition+Vector3.up*Altitude+Vector3.left*Downrange;
                VirtualCraftAttitude=Quaternion.AngleAxis(PitchDegrees,Vector3.forward);
                if(mission.Config.OrbitalFlight)VirtualCraftAttitude=Quaternion.AngleAxis(PitchDegrees-OrbitalTrajectory.ViewBank(Altitude),Vector3.forward);
                if(continuousSurface)
                {
                    ContinuousMoonView.EvaluatePose(OrbitalTrajectory.Sample(mission),launchPosition,out var position,out var attitude);
                    VirtualCraftPosition=position;VirtualCraftAttitude=attitude;
                    if(mission.Docking!=null && mission.OrbitBurnProgress>=1)
                    {VirtualCraftPosition+=attitude*(mission.Docking.Position-mission.Docking.Config.StartPosition);VirtualCraftAttitude*=mission.Docking.Attitude;}
                }
                Quaternion inverse=Quaternion.Inverse(VirtualCraftAttitude);
                // 相机与舱体维持稳定；把真实尺寸的外界转换到飞船坐标，距离自然决定视觉大小。
                float renderScale=1;
                Vector3 anchor=Vector3.zero;
                if(continuousSurface)
                {
                    // 远景等比缩放到相机附近，避免百万米坐标与厘米近裁面同时渲染丢失深度精度。
                    // 围绕当前观察点缩放，基地、陨石坑和整颗月球的视角大小保持连续。
                    float reduced=Mathf.Clamp(700/(700+Mathf.Max(0,Altitude-300)),.01f,1);
                    renderScale=Mathf.Lerp(1,reduced,OrbitalTrajectory.Smooth((Altitude-300)/1700));
                    if(Camera.main!=null)anchor=outsideWorld.parent.InverseTransformPoint(Camera.main.transform.position);
                }
                outsideWorld.localScale=Vector3.one*renderScale;
                outsideWorld.SetLocalPositionAndRotation(anchor*(1-renderScale)-(inverse*VirtualCraftPosition)*renderScale,inverse);
                distantBase.localPosition=baseStart;
                distantBase.localScale=Vector3.one;
            }
            else
            {
                landscape.localPosition=landscapeStart+Vector3.down*(t*1.8f);
                landscape.localScale=Vector3.one*Mathf.Lerp(1,1.1f,t);
                distantBase.localPosition=baseStart+Vector3.down*(t*.8f);
                distantBase.localScale=Vector3.one*Mathf.Lerp(1,.3f,t);
            }
            distantBase.gameObject.SetActive(!mission.BaseExploded);
            if (explosionAge >= 0) explosionAge += delta;
            bool visible = explosionAge >= 0 && explosionAge < 6;
            blast.gameObject.SetActive(visible);
            blast.localPosition = outsideWorld!=null ? blastStart : blastStart+Vector3.down*(t*.8f);
            blast.localScale = Vector3.one * explosionScale*(1.2f + Mathf.Max(0, explosionAge)*.3f);
            if(outsideWorld!=null && Camera.main!=null) blast.rotation=Quaternion.LookRotation(blast.position-Camera.main.transform.position,Vector3.up);
            if (blastMaterial != null) blastMaterial.SetFloat("_Age", Mathf.Max(0, explosionAge));
            for (int i = 0; i < debris.Length; i++)
            {
                debris[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float angle = i * 2.39996f;
                Vector3 velocity = new Vector3(Mathf.Cos(angle), 0.8f+i*.08f, Mathf.Sin(angle))*(outsideWorld!=null ? 5f : 1.1f);
                debris[i].localPosition = blast.localPosition + velocity * explosionAge + Vector3.down * (0.14f * explosionAge * explosionAge);
                debris[i].localRotation = Quaternion.Euler(explosionAge * (30 + i * 4), i * 37, 0);
            }
        }
    }
}
