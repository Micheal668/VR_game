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
}
