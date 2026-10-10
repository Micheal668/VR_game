using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LunarEscape.Editor
{
    public static class ImportStationExpansion
    {
        public const string Root = "Assets/_LunarEscape/Art/StationExpansion";
        public const string PrefabPath = Root + "/Station Expansion.prefab";
        [Serializable] public sealed class MaterialSpec { public string name; public float[] color; public float metallic, roughness, emission; }
        [Serializable] public sealed class BoxSpec { public string name; public float[] position, size; public bool floor; }
        [Serializable] public sealed class Manifest { public MaterialSpec[] materials; public BoxSpec[] colliders, rooms; public int meshCount; }
        public static Manifest Read() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/expansion-manifest.json"));
        public static Vector3 Vector(float[] v) => new(v[0], v[1], v[2]);
        public static void Import()
        {
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Meshes");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest = Read();
            var materials = manifest.materials.ToDictionary(m => m.name, m =>
            {
                string path = Root + "/Materials/" + m.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
                var color = new Color(m.color[0], m.color[1], m.color[2], 1);
                material.SetColor("_BaseColor", color.gamma); material.SetFloat("_Metallic", m.metallic); material.SetFloat("_Smoothness", 1 - m.roughness);
                material.SetColor("_EmissionColor", color * m.emission);
                material.globalIlluminationFlags = m.emission > 0 ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if (m.emission > 0) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
                EditorUtility.SetDirty(material); return material;
            });
            string modelPath = Root + "/StationExpansion.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importCameras = importer.importLights = importer.importAnimation = importer.addCollider = false;
            importer.isReadable = true; importer.importNormals = ModelImporterNormals.Import;
            foreach (var pair in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var frame = ImportStationGeometry.AnchorFrame(model.transform).inverse;
            var staging = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Station Expansion");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, staging);
            try
            {
                var filters = model.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length != manifest.meshCount) throw new InvalidDataException("Expansion mesh count does not match manifest.");
                foreach (var filter in filters)
                {
                    var mesh = ImportStationGeometry.BakeMesh(filter.sharedMesh, frame * filter.transform.localToWorldMatrix, filter.name);
                    mesh = ImportStationGeometry.SaveMesh(mesh, Root + "/Meshes/" + filter.name + ".asset");
                    var part = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(root.transform, false);
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    part.GetComponent<MeshRenderer>().sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials.Select(m => materials[m.name]).ToArray();
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(staging); }
            AssetDatabase.SaveAssets();
            Debug.Log("STATION_EXPANSION_IMPORTED");
        }
    }
}
