using UnityEngine;

namespace LunarEscape
{
    public sealed class OrbiterView : MonoBehaviour
    {
        [SerializeField] private AscentMission flight;
        [SerializeField] private DockingMission docking;
        [SerializeField] private Transform target;
        [SerializeField] private Transform explosion;
        [SerializeField] private Renderer captureLight;
        [SerializeField] private Vector3 playerPort=new(-.9f,1.65f,2.7f);
        private float explosionAge=-1;
        private Material explosionMaterial;
        public Transform Target=>target;
        public bool ExplosionVisible=>explosion!=null&&explosion.gameObject.activeSelf;
        public void Configure(AscentMission task,DockingMission controller,Transform craft,Transform blast,Renderer lamp)
        {flight=task;docking=controller;target=craft;explosion=blast;captureLight=lamp;}
        private void OnEnable(){if(flight!=null)flight.PhaseChanged+=OnPhase;Apply(0);}
        private void OnDisable(){if(flight!=null)flight.PhaseChanged-=OnPhase;}
        private void OnDestroy(){if(explosionMaterial!=null)Destroy(explosionMaterial);}
        private void OnPhase(AscentPhase phase)
        {
            if(phase==AscentPhase.AwaitingBoarding)explosionAge=-1;
            if(phase==AscentPhase.Failed && flight.Failure==AscentFailure.DockingCollision)explosionAge=0;
        }
        private void LateUpdate()=>Apply(Time.deltaTime);
        private void Apply(float delta)
        {
            if(flight==null||target==null)return;
            bool inOrbit=flight.AirborneSeconds>=flight.OrbitAscentSeconds-4 && flight.IsLocked;
            target.gameObject.SetActive(inOrbit);
            Quaternion inverse=Quaternion.Inverse(docking.Attitude);
            target.localPosition=playerPort+inverse*(-docking.Position);target.localRotation=inverse;
            if(captureLight!=null)captureLight.enabled=docking.CanAssist||docking.State==DockingState.Capturing||docking.State==DockingState.Docked;
            if(explosion==null)return;
            if(explosionAge>=0)explosionAge+=delta;
            explosion.gameObject.SetActive(explosionAge>=0&&explosionAge<3);
            if(explosionAge<0)return;
            explosion.localPosition=playerPort+Vector3.forward*.3f;explosion.localScale=Vector3.one*(2+explosionAge*2);
            if(explosionMaterial==null)explosionMaterial=explosion.GetComponent<Renderer>().material;
            explosionMaterial.SetFloat("_Age",explosionAge);
            if(Camera.main!=null)explosion.rotation=Quaternion.LookRotation(explosion.position-Camera.main.transform.position,Vector3.up);
        }
    }
}
