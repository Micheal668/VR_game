using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarEscape.Editor
{
    // 只从现有 Blender 轨道器提取服务舱，返回舱使用自己的顶部对接通道。
    // 只生成返航专用资产，不改朋友原有的轨道器预制体。
    public static class BuildReturnAssembly
    {
        public const float PortHeight = EarthReturnPresenter.DockingPortHeight;
        public static readonly Vector3 ServicePosition = new(0, 3.30f, 0);
        private const float CutZ = 3.55f;
        private const string Art = "Assets/_LunarEscape/Art/EarthReturn";
        public static Transform ServiceModule(Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ImportRealisticOrbiter.PrefabPath);
            var service = Part(parent, "Service and Propulsion Module", ServicePosition);
            service.localRotation = Quaternion.Euler(90, 0, 0);
            foreach (var filter in source.transform.Find("LOD0").GetComponentsInChildren<MeshFilter>())
                CopyServicePart(filter, service);
            Adapter(service, "Service separation adapter", 3.47f, 2.14f, 3.55f, 1.75f);
            return service;
        }
        [MenuItem("Lunar Escape/Reconnect Return Capsule Without Legacy Cone (Life Support Scene)")]
        public static void Reconnect()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            var view = Object.FindAnyObjectByType<EarthReturnPresenter>();
            var flight = view.GetComponent<FlightScenePresenter>();
            var target = flight.FlightWorld.GetComponent<OrbiterView>().Target;
            var assembly = target.Find("Complete Orbital Return Assembly");
            if (assembly == null) throw new System.InvalidOperationException("缺少已安装的返航对接模型。");
            // 定向更新已有对象，保留服务舱、返回舱内饰、界面和原对接引导引用。
            RemoveLegacyCone(view.World.transform);
            RemoveLegacyCone(assembly);
            assembly.localPosition = new Vector3(0, 0, PortHeight);
            view.Lander.localPosition = new Vector3(0, PortHeight + EarthReturnPresenter.AscentPortHeight, 0);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("RETURN_CAPSULE_RECONNECTED_WITHOUT_LEGACY_CONE");
        }
        private static void RemoveLegacyCone(Transform parent)
        {
            var cone = parent.Find("Orbital Transfer Module");
            if (cone != null) Object.DestroyImmediate(cone.gameObject);
        }
        public static void UpdateDockingTarget(FlightScenePresenter flight)
        {
            var view = flight.FlightWorld.GetComponent<OrbiterView>();
            if (view == null || view.Target == null) throw new System.InvalidOperationException("场景缺少原有月球轨道对接目标。");
            var guidance = view.GetComponent<DockingApproachGuide>();
            var marker = guidance != null && guidance.Ring != null ? guidance.Ring.transform : null;
            foreach (Transform old in view.Target.Cast<Transform>().Where(t => t != marker).ToArray()) Object.DestroyImmediate(old.gameObject);
            var assembly = Part(view.Target, "Complete Orbital Return Assembly", new Vector3(0, 0, PortHeight));
            assembly.localRotation = Quaternion.Euler(-90, 0, 0);
            var capsule = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ImportReturnCapsule.Prefab));
            capsule.transform.SetParent(assembly, false);
            ServiceModule(assembly);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(ImportRealisticOrbiter.PrefabPath).transform.Find("Capture Indicator");
            var lamp = Object.Instantiate(original.gameObject, view.Target, false).GetComponent<Renderer>();
            lamp.name = "Capture Indicator";
            view.Configure(flight.GetComponent<AscentMission>(), flight.GetComponent<DockingMission>(), view.Target,
                flight.FlightWorld.transform.Find("Docking Collision Flash"), lamp);
            EditorUtility.SetDirty(view);
        }
        private static void CopyServicePart(MeshFilter source, Transform parent)
        {
            var mesh = SliceService(source.sharedMesh);
            if (mesh.vertexCount == 0) { Object.DestroyImmediate(mesh); return; }
            string name = "Service " + source.name;
            string path = Art + "/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); mesh = saved; EditorUtility.SetDirty(saved); }
            var part = Part(parent, name, Vector3.zero);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
        }
        private struct Vertex
        {
            public Vector3 p, n;
            public Vector2 uv;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new() { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t) };
        }
        private static Mesh SliceService(Mesh source)
        {
            var p = source.vertices; var n = source.normals; var uv = source.uv;
            var points = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>();
            var submeshes = new List<int>[source.subMeshCount];
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var output = submeshes[sub] = new List<int>(); var triangles = source.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var polygon = new List<Vertex>();
                    for (int j = 0; j < 3; j++)
                    {
                        int a = triangles[i + j], b = triangles[i + (j + 1) % 3];
                        var va = new Vertex { p = p[a], n = n[a], uv = uv.Length == p.Length ? uv[a] : Vector2.zero };
                        var vb = new Vertex { p = p[b], n = n[b], uv = uv.Length == p.Length ? uv[b] : Vector2.zero };
                        bool insideA = va.p.z >= CutZ, insideB = vb.p.z >= CutZ;
                        if (insideA) polygon.Add(va);
                        if (insideA != insideB) polygon.Add(Vertex.Lerp(va, vb, (CutZ - va.p.z) / (vb.p.z - va.p.z)));
                    }
                    for (int j = 1; j + 1 < polygon.Count; j++)
                        foreach (var v in new[] { polygon[0], polygon[j], polygon[j + 1] })
                        { output.Add(points.Count); points.Add(v.p); normals.Add(v.n); uvs.Add(v.uv); }
                }
            }
            var mesh = new Mesh { name = "Service " + source.name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(points); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds(); return mesh;
        }
        private static Transform Part(Transform parent, string name, Vector3 position)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }
        private static void Adapter(Transform parent, string name, float z1, float r1, float z2, float r2)
        {
            var vertices = new Vector3[130]; var triangles = new List<int>();
            for (int i = 0; i <= 64; i++)
            {
                float a = i * Mathf.PI / 32;
                vertices[i] = new Vector3(Mathf.Cos(a) * r1, Mathf.Sin(a) * r1, z1);
                vertices[65 + i] = new Vector3(Mathf.Cos(a) * r2, Mathf.Sin(a) * r2, z2);
                if (i < 64) triangles.AddRange(new[] { i, i + 1, i + 65, i + 1, i + 66, i + 65 });
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = Art + "/" + name + ".asset"; var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, stored); Object.DestroyImmediate(mesh); mesh = stored; EditorUtility.SetDirty(stored); }
            var part = Part(parent, name, Vector3.zero); part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ImportRealisticOrbiter.Root + "/Materials/Orb_Silver.mat");
        }
    }
}
