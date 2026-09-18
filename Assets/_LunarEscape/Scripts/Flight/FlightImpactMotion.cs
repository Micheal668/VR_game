using UnityEngine;

namespace LunarEscape
{
    // 轻微冲击只作用于舱体和窗外表现，不写入头显的追踪姿态，也不改变任务时钟。
    [DefaultExecutionOrder(10001)]
    public sealed class FlightImpactMotion : MonoBehaviour
    {
        [SerializeField] private AscentMission mission;
        [SerializeField] private bool motionEnabled=true;
        [SerializeField,Range(0,1)] private float strength=1;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float age=-1;
        public float Amplitude { get; private set; }
        public float Duration { get; private set; }
        public bool MotionEnabled { get=>motionEnabled; set { motionEnabled=value; if(!value) Restore(); } }
        public void Configure(AscentMission task) { mission=task; }
        private void Awake() { restPosition=transform.localPosition; restRotation=transform.localRotation; }
        private void OnEnable() { if(mission!=null) mission.Exploded+=OnImpact; age=-1; }
        private void OnDisable() { if(mission!=null) mission.Exploded-=OnImpact; Restore(); }
        private void OnImpact()
        {
            Amplitude=mission.Damage switch { AscentDamage.Heavy=>.014f, AscentDamage.Light=>.006f, _=>.002f };
            Duration=mission.Damage switch { AscentDamage.Heavy=>.85f, AscentDamage.Light=>.55f, _=>.35f };
            age=0;
        }
        private void LateUpdate()
        {
            if(!motionEnabled || mission.Phase==AscentPhase.Failed || age<0 || age>=Duration) { Restore(); return; }
            age+=Time.deltaTime;
            float envelope=Mathf.Pow(Mathf.Clamp01(1-age/Duration),2)*strength;
            Vector3 offset=new Vector3(Mathf.Sin(age*71),Mathf.Sin(age*93+.7f)*.6f,Mathf.Sin(age*57)*.3f)*Amplitude*envelope;
            transform.localPosition=restPosition+offset;
            transform.localRotation=restRotation*Quaternion.Euler(Mathf.Sin(age*65)*Amplitude*10*envelope,0,Mathf.Sin(age*81)*Amplitude*17*envelope);
        }
        private void Restore() { transform.localPosition=restPosition; transform.localRotation=restRotation; }
    }
}
