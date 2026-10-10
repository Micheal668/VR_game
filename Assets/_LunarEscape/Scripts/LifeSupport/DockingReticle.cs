using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 摄像机视口坐标和对接口的世界位置决定目标标记，不允许用假居中的贴图替代。
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DockingReticle : Graphic
    {
        [SerializeField] private Camera optics;
        [SerializeField] private OrbiterView orbiter;
        [SerializeField] private DockingMission docking;
        private Vector2 target;
        private bool targetVisible;
        public Vector2 TargetViewport { get; private set; }
        public bool TargetVisible => targetVisible;
        public void Configure(Camera camera, OrbiterView view, DockingMission controller)
        { optics = camera; orbiter = view; docking = controller; raycastTarget = false; }
        private void LateUpdate()
        {
            if (optics == null || orbiter == null || orbiter.Target == null) return;
            var point = optics.WorldToViewportPoint(orbiter.Target.position); TargetViewport = point;
            targetVisible = point.z > 0 && orbiter.Target.gameObject.activeInHierarchy;
            var size = rectTransform.rect.size;
            target = new Vector2(Mathf.Clamp(point.x, .04f, .96f) - .5f, Mathf.Clamp(point.y, .06f, .94f) - .5f) * size;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var size = rectTransform.rect.size;
            Color c = new(.62f, .89f, 1, .75f);
            void Line(Vector2 a, Vector2 b, float width, Color tint)
            {
                var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
                int index = mesh.currentVertCount;
                mesh.AddVert(a - normal, tint, Vector2.zero); mesh.AddVert(a + normal, tint, Vector2.zero);
                mesh.AddVert(b + normal, tint, Vector2.zero); mesh.AddVert(b - normal, tint, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index + 2, index + 3, index);
            }
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI / 24, b = (i + 1) * Mathf.PI / 24;
                Line(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 65, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 65, 2, c);
            }
            Line(new(-100,0),new(-22,0),2,c); Line(new(22,0),new(100,0),2,c);
            Line(new(0,-100),new(0,-22),2,c); Line(new(0,22),new(0,100),2,c);
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                Line(new(i * 115, -7), new(i * 115, 7), 2, c * .6f);
                Line(new(-7, i * 65), new(7, i * 65), 2, c * .6f);
            }
            if (!targetVisible) return;
            bool assisting = docking != null && (docking.CanAssist || docking.State==DockingState.Capturing || docking.State==DockingState.Docked);
            Color tracking = assisting ? new(.35f,1,.65f,1) : new(1,.70f,.25f,1);
            const float radius = 40;
            Line(Vector2.zero,target,2,tracking*.65f);
            if(assisting)
                for(int i=0;i<48;i++)
                {
                    float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;
                    float ring=52+(docking.State==DockingState.Capturing ? 8*docking.CaptureProgress : 3*Mathf.Sin(Time.time*4));
                    Line(target+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*ring,target+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*ring,4,tracking);
                }
            var a0 = target + Vector2.up * radius; var a1 = target + Vector2.right * radius;
            var a2 = target + Vector2.down * radius; var a3 = target + Vector2.left * radius;
            Line(a0,a1,3,tracking); Line(a1,a2,3,tracking); Line(a2,a3,3,tracking); Line(a3,a0,3,tracking);
            // 姿态横滚用第二条短横线表示，平移目标与角度提示保持独立。
            float roll = docking != null ? -Mathf.DeltaAngle(0, docking.Attitude.eulerAngles.z) * Mathf.Deg2Rad : 0;
            var direction = new Vector2(Mathf.Cos(roll), Mathf.Sin(roll));
            Line(target - direction * 44, target - direction * 30, 3, tracking);
            Line(target + direction * 30, target + direction * 44, 3, tracking);
        }
    }
}
