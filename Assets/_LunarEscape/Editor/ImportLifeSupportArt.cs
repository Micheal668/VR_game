using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    // 按导出的米制锚点烘焙网格，避免 FBX 轴向、负缩放和单位转换影响驾驶舱布局。
    public static class ImportLifeSupportArt
    {
        public const string Root = "Assets/_LunarEscape/Art/LifeSupport";
        public const string CabinPrefab = Root + "/Dragon Inspired Cabin.prefab";
        public const string UniformPrefab = Root + "/Station Work Uniform.prefab";
        [Serializable] private class MaterialData { public string name; public float[] color; public float metallic, roughness; public float[] emission; }
        [Serializable] private class Manifest { public MaterialData[] materials; }
        private static readonly string[] BoneNames = { "Hips", "Chest", "Head", "LeftUpper", "LeftFore", "LeftHand", "RightUpper", "RightFore", "RightHand" };
        private static readonly int[] Parents = { -1, 0, 1, 1, 3, 4, 1, 6, 7 };
        private static readonly Vector3[] Joints = { new(0,.9f,-.06f),new(0,1.26f,-.06f),new(0,1.64f,-.06f),new(-.235f,1.36f,-.06f),new(-.325f,1.15f,.015f),new(-.325f,1.025f,.14f),new(.235f,1.36f,-.06f),new(.325f,1.15f,.015f),new(.325f,1.025f,.14f) };
        public static void ImportCabinLayout()
        {
            AssetDatabase.Refresh();
            var descriptions=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/life-art-manifest.json"));
            var materials=descriptions.materials.ToDictionary(entry=>entry.name,
                entry=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+entry.name+".mat"));
            Bake("DragonInspiredCabin.fbx",CabinPrefab,false,materials);
            AssetDatabase.SaveAssets();
        }
        public static void Import()
        {
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Meshes"); AssetDatabase.Refresh();
            var descriptions = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/life-art-manifest.json"));
            var materials = new Dictionary<string, Material>();
            foreach (var entry in descriptions.materials)
            {
                string path = Root + "/Materials/" + entry.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
                material.name = entry.name;
                material.SetColor("_BaseColor", new Color(entry.color[0], entry.color[1], entry.color[2], entry.color[3]).gamma);
                material.SetFloat("_Metallic", entry.metallic); material.SetFloat("_Smoothness", 1 - entry.roughness);
                var emission = new Color(entry.emission[0], entry.emission[1], entry.emission[2]);
                material.SetColor("_EmissionColor", emission);
                if (emission.maxColorComponent > 0) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                EditorUtility.SetDirty(material); materials.Add(entry.name, material);
            }
            Bake("DragonInspiredCabin.fbx", CabinPrefab, false, materials);
            Bake("StationWorkUniform.fbx", UniformPrefab, true, materials);
            AssetDatabase.SaveAssets(); Debug.Log("LIFE_SUPPORT_ART_IMPORTED");
        }
        private static void Bake(string file, string prefabPath, bool uniform, Dictionary<string, Material> materials)
        {
            string path = Root + "/" + file;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.isReadable = true; importer.addCollider = false; importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var all = source.GetComponentsInChildren<Transform>(true);
            Vector3 Anchor(string name) => all.Single(t => t.name == "Anchor_" + name).position;
            var origin = Anchor("Origin"); var matrix = Matrix4x4.identity;
            matrix.SetColumn(0, Anchor("AxisX") - origin); matrix.SetColumn(1, Anchor("AxisY") - origin);
            matrix.SetColumn(2, Anchor("AxisZ") - origin); matrix.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
            if (Mathf.Abs(matrix.determinant) < 1e-9f) throw new InvalidDataException("生命保障模型缺少有效的米制坐标锚点。");
            var sourceToUnity = matrix.inverse;
            var baked = source.GetComponentsInChildren<MeshFilter>(true).Select(filter =>
                (filter.name, mesh: BakeMesh(filter.sharedMesh, sourceToUnity * filter.transform.localToWorldMatrix),
                    material: materials[filter.GetComponent<MeshRenderer>().sharedMaterial.name])).ToArray();
            var root = new GameObject(uniform ? "Station Work Uniform" : "Dragon Inspired Cabin");
            try
            {
                if (uniform)
                {
                    var bones = new Transform[9];
                    for (int i = 0; i < bones.Length; i++)
                    { bones[i] = new GameObject(BoneNames[i]).transform; bones[i].SetParent(Parents[i] < 0 ? root.transform : bones[Parents[i]], false); bones[i].position = Joints[i]; }
                    SkinnedMeshRenderer Skin(bool head)
                    {
                        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var weights = new List<BoneWeight>();
                        var triangles = new List<int[]>(); var skinMaterials = new List<Material>();
                        foreach (var part in baked)
                        {
                            string key = part.name.Split(new[] { "__" }, StringSplitOptions.None)[0];
                            int bone = Array.IndexOf(BoneNames, key);
                            if (bone < 0) throw new InvalidDataException("生活服网格缺少关节分组：" + part.name);
                            if ((bone == 2) != head) continue;
                            int offset = vertices.Count; vertices.AddRange(part.mesh.vertices); normals.AddRange(part.mesh.normals);
                            weights.AddRange(Enumerable.Repeat(new BoneWeight { boneIndex0 = bone, weight0 = 1 }, part.mesh.vertexCount));
                            triangles.Add(part.mesh.triangles.Select(index => index + offset).ToArray()); skinMaterials.Add(part.material);
                        }
                        var mesh = new Mesh { name = head ? "Uniform Head" : "Uniform Body", indexFormat = IndexFormat.UInt32 };
                        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.boneWeights = weights.ToArray();
                        mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                        mesh.subMeshCount = triangles.Count;
                        for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
                        mesh.RecalculateBounds(); mesh = Save(mesh, Root + "/Meshes/" + mesh.name + ".asset");
                        var renderer = new GameObject(mesh.name).AddComponent<SkinnedMeshRenderer>(); renderer.transform.SetParent(root.transform, false);
                        renderer.sharedMesh = mesh; renderer.sharedMaterials = skinMaterials.ToArray(); renderer.bones = bones; renderer.rootBone = bones[0];
                        renderer.localBounds = new Bounds(Vector3.up, Vector3.one * 3); renderer.updateWhenOffscreen = true; return renderer;
                    }
                    var body = Skin(false); var head = Skin(true); root.AddComponent<CrewSuitRig>().Configure(bones, body, head);
                }
                else
                {
                    foreach (var part in baked)
                    {
                        var piece = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer)); piece.transform.SetParent(root.transform, false);
                        var mesh = Object.Instantiate(part.mesh); mesh.name = part.name;
                        piece.GetComponent<MeshFilter>().sharedMesh = Save(mesh, Root + "/Meshes/" + part.name + ".asset");
                        piece.GetComponent<MeshRenderer>().sharedMaterial = part.material;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { Object.DestroyImmediate(root); foreach (var part in baked) Object.DestroyImmediate(part.mesh); }
        }
        private static Mesh BakeMesh(Mesh source, Matrix4x4 matrix)
        {
            var mesh = Object.Instantiate(source); mesh.hideFlags = HideFlags.None;
            mesh.vertices = source.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var normalMatrix = matrix.inverse.transpose; mesh.normals = source.normals.Select(v => normalMatrix.MultiplyVector(v).normalized).ToArray();
            if (matrix.determinant < 0)
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                { var indices = mesh.GetTriangles(sub); for (int i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]); mesh.SetTriangles(indices, sub); }
            mesh.RecalculateBounds(); return mesh;
        }
        private static T Save<T>(T asset, string path) where T : Object
        {
            var previous = AssetDatabase.LoadAssetAtPath<T>(path);
            if (previous == null) AssetDatabase.CreateAsset(asset, path);
            else { EditorUtility.CopySerialized(asset, previous); Object.DestroyImmediate(asset); asset = previous; EditorUtility.SetDirty(asset); }
            return asset;
        }
    }
}
