using UnityEngine;

namespace LunarEscape
{
    public sealed class ArtemisLanderVisual : MonoBehaviour
    {
        [field: SerializeField] public GameObject Exterior { get; private set; }
        [field: SerializeField] public Transform PilotEye { get; private set; }
        // Local forward points out of the descent bell in the exhaust direction.
        [field: SerializeField] public Transform Nozzle { get; private set; }
        // Each port's local up axis is its outward mating normal.
        [field: SerializeField] public Transform DockingPort { get; private set; }
        [field: SerializeField] public Transform OrbiterDockingPort { get; private set; }
        [field: SerializeField] public Transform[] Feet { get; private set; }
        [field: SerializeField] public Transform[] FootAnchors { get; private set; }
        [field: SerializeField] public MeshRenderer[] AscentStage { get; private set; }
        [field: SerializeField] public MeshRenderer[] DescentStage { get; private set; }

        public void Configure(GameObject exterior, Transform eye, Transform nozzle, Transform port,
            Transform orbiterPort, Transform[] feet, Transform[] footAnchors,
            MeshRenderer[] ascentStage, MeshRenderer[] descentStage)
        {
            Exterior = exterior;
            PilotEye = eye;
            Nozzle = nozzle;
            DockingPort = port;
            OrbiterDockingPort = orbiterPort;
            Feet = feet;
            FootAnchors = footAnchors;
            AscentStage = ascentStage;
            DescentStage = descentStage;
        }
    }
}
