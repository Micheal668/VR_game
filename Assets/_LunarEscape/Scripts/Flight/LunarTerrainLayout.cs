using UnityEngine;
namespace LunarEscape
{
    public sealed class LunarTerrainLayout : MonoBehaviour
    {
        [SerializeField] private MeshCollider walkableSurface;
        [SerializeField] private GameObject groundVisual,flightVisual;
        public MeshCollider WalkableSurface=>walkableSurface;
        public GameObject GroundVisual=>groundVisual;
        public GameObject FlightVisual=>flightVisual;
        public void Configure(MeshCollider collision,GameObject ground,GameObject flight)
        {walkableSurface=collision;groundVisual=ground;flightVisual=flight;}
    }
}
