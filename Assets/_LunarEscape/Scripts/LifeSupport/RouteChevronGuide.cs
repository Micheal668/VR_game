using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class RouteChevronGuide : MonoBehaviour
    {
        [SerializeField] private Material material;
        private Mesh mesh;
        private MeshRenderer display;
        private readonly List<Vector3> vertices=new();
        private readonly List<int> triangles=new();
        public int ArrowCount { get; private set; }
        public bool Visible => display!=null && display.enabled;
        public void Configure(Material value) { material=value; }
        private void Awake()
        {
            mesh=new Mesh{name="Local escape chevrons"};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;
            display=GetComponent<MeshRenderer>();display.sharedMaterial=material;display.shadowCastingMode=ShadowCastingMode.Off;
            display.receiveShadows=false;display.enabled=false;
        }
        public void SetVisible(bool value) { if(display!=null)display.enabled=value; }
        public void Draw(IReadOnlyList<Vector3> path)
        {
            if(mesh==null)return;
            vertices.Clear();triangles.Clear();ArrowCount=0;
            float offset=1.3f,total=0;
            for(int i=1;i<path.Count && total<20;i++)
            {
                var delta=path[i]-path[i-1];float length=delta.magnitude;if(length<.001f)continue;
                var forward=delta/length;var right=Vector3.Cross(Vector3.up,forward);
                for(;offset<length && total+offset<20;offset+=2.2f)
                {
                    var tip=path[i-1]+forward*offset;
                    Stroke(tip-forward*.32f-right*.23f,tip,.055f);
                    Stroke(tip,tip-forward*.32f+right*.23f,.055f);ArrowCount++;
                }
                offset-=length;total+=length;
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        private void Stroke(Vector3 start,Vector3 end,float width)
        {
            var side=Vector3.Cross(Vector3.up,(end-start).normalized)*width*.5f;int n=vertices.Count;
            vertices.Add(transform.InverseTransformPoint(start-side));vertices.Add(transform.InverseTransformPoint(start+side));
            vertices.Add(transform.InverseTransformPoint(end+side));vertices.Add(transform.InverseTransformPoint(end-side));
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }

    // The two shifts use the same walkable gallery and the same exterior gate. Follow the
    // doorways and the perimeter of the habitat instead of pointing through room walls.
    internal static class StationWalkway
    {
        private enum Room { Gallery, Quarters, Lab, Engineering, Lobby, Chamber, Surface }
        private static readonly Vector3 Gate = Floor(9.3f, 1.7f);
        private static Vector3 Floor(float x, float z) => new(x, .065f, z);
        private static Room Locate(Vector3 p, bool inside)
        {
            // Day two's unpressurized passage remains walkable up to the outer hatch.
            if (p.x >= 4f && p.x <= 8.1f && Mathf.Abs(p.z - 1.7f) <= 1f) return Room.Chamber;
            if (!inside) return Room.Surface;
            if (p.x >= -4f) return Room.Lobby;
            if (p.x < -11f) return Room.Engineering;
            if (p.z >= 2.9f) return Room.Quarters;
            return p.z <= .5f ? Room.Lab : Room.Gallery;
        }

        private static Vector3 Passage(Room room, Vector3 point) => room switch
        {
            Room.Quarters => Floor(-7.5f, 3.3f),
            Room.Lab => Floor(-7.5f, .1f),
            Room.Engineering => Floor(-11.5f, 1.7f),
            Room.Lobby => Floor(Mathf.Clamp(point.x, -3.8f, 2.55f), 1.7f),
            Room.Chamber => Floor(point.x, 1.7f),
            Room.Surface => Gate,
            _ => Floor(point.x, 1.7f)
        };

        private static void Add(List<Vector3> path, Vector3 point)
        {
            if ((path[^1] - point).sqrMagnitude > .16f) path.Add(point);
        }

        private static void SkipPassedCorners(List<Vector3> path)
        {
            // A route rebuilt just after turning must continue along the aisle.
            // Keeping its entry corner would turn the compass back toward the player.
            while (path.Count > 2)
            {
                var aisle = (path[2] - path[1]).normalized;
                var traveled = path[0] - path[1];
                float along = Vector3.Dot(traveled, aisle);
                if (along <= 0 || (traveled - along * aisle).sqrMagnitude > .36f) break;
                path.RemoveAt(1);
            }
        }

        private static void AroundHabitat(List<Vector3> path, Vector3 from, Vector3 to)
        {
            // The rear array is west of engineering. The open north/south lanes lead
            // around the entire base, including the projecting quarters and lab.
            bool westbound = to.x < -16f && from.x >= -16f;
            bool eastbound = from.x < -16f && to.x >= -16f;
            if (westbound || eastbound)
            {
                float lane = from.z < -4.7f || to.z < -4.7f ? -6f : 9.2f;
                if (westbound && from.x > 8f || eastbound && from.x < -16f) Add(path, Floor(westbound ? 9.3f : -17f, lane));
                if (westbound || eastbound && to.x > 8f) Add(path, Floor(westbound ? -17f : 9.3f, lane));
            }
            else if (from.x >= -16f && from.x < 8f && to.x >= 8f && (from.z > 7.6f || from.z < -4.7f))
                Add(path, Floor(9.3f, from.z > 0 ? 9.2f : -6f));
            Add(path, to);
        }

        public static void Build(List<Vector3> path, Vector3 start, bool startInside, Vector3 goal, bool goalInside)
        {
            path.Clear();
            start.y = goal.y = .065f;
            path.Add(start);
            Room from = Locate(start, startInside), to = Locate(goal, goalInside);
            if (from == to)
            {
                if (from == Room.Surface) AroundHabitat(path, start, goal);
                else Add(path, goal);
                SkipPassedCorners(path);
                return;
            }
            var exit = Passage(from, start);
            var entry = Passage(to, goal);
            if (from == Room.Surface) AroundHabitat(path, start, Gate);
            else Add(path, exit);
            Add(path, Floor(exit.x, 1.7f));
            Add(path, Floor(entry.x, 1.7f));
            Add(path, entry);
            if (to == Room.Surface) AroundHabitat(path, Gate, goal);
            else Add(path, goal);
            SkipPassedCorners(path);
        }
    }
}
