using UnityEngine;

namespace LunarEscape
{
    public sealed class ArtemisRocketVisual : MonoBehaviour
    {
        [field: SerializeField] public Transform Core { get; private set; }
        [field: SerializeField] public Transform LeftBooster { get; private set; }
        [field: SerializeField] public Transform RightBooster { get; private set; }
        [field: SerializeField] public Transform UpperStage { get; private set; }
        [field: SerializeField] public Transform Orion { get; private set; }
        [field: SerializeField] public Transform EscapeTower { get; private set; }
        [field: SerializeField] public Transform CrewHatch { get; private set; }
        [field: SerializeField] public GameObject HatchDoor { get; private set; }
        [field: SerializeField] public Transform Seat { get; private set; }
        [field: SerializeField] public Transform ExternalCamera { get; private set; }
        [SerializeField] private GameObject orbitalHardware;
        [SerializeField] private Transform[] wingHinges;

        public void Configure(Transform stages, Transform hatch, Transform eye, Transform external)
        {
            Core = stages.Find("Core");
            LeftBooster = stages.Find("LeftBooster");
            RightBooster = stages.Find("RightBooster");
            UpperStage = stages.Find("UpperStage");
            Orion = stages.Find("Orion");
            EscapeTower = stages.Find("EscapeTower");
            CrewHatch = hatch;
            HatchDoor = Orion.Find("Orion crew compartment/Hatch door").gameObject;
            Seat = eye;
            ExternalCamera = external;
        }

        public void ConfigureSolarArrays(GameObject hardware, Transform[] hinges)
        {
            orbitalHardware = hardware;
            wingHinges = hinges;
            SetSolarDeployment(0);
        }

        public void SetSolarDeployment(float amount)
        {
            if (orbitalHardware == null) return;
            float fraction = Mathf.Clamp01(amount);
            orbitalHardware.SetActive(fraction > 0);
            if (fraction <= 0) return;
            var pitch = Quaternion.Euler(86f * (1f - fraction), 0, 0);
            foreach (var hinge in wingHinges) hinge.localRotation = pitch;
        }
    }
}
