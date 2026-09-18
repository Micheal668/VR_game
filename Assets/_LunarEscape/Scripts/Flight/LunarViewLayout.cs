using UnityEngine;

namespace LunarEscape
{
    // 外景与舷窗共用米制尺寸。这些引用也方便在 Inspector 中核对距离和朝向。
    public sealed class LunarViewLayout : MonoBehaviour
    {
        [SerializeField] private Transform lander;
        [SerializeField] private GameObject baseExterior;
        [SerializeField] private GameObject baseProxy;
        [SerializeField] private Transform outsideWorld;
        [SerializeField] private Transform door;
        [SerializeField] private Vector3 launchPosition;
        public Transform Lander=>lander;
        public GameObject BaseExterior=>baseExterior;
        public GameObject BaseProxy=>baseProxy;
        public Transform OutsideWorld=>outsideWorld;
        public Transform Door=>door;
        public Vector3 LaunchPosition=>launchPosition;
        public void Configure(Transform craft, GameObject exterior, GameObject proxy, Transform outside, Transform hatch, Vector3 start)
        { lander=craft; baseExterior=exterior; baseProxy=proxy; outsideWorld=outside; door=hatch; launchPosition=start; }
    }
}
