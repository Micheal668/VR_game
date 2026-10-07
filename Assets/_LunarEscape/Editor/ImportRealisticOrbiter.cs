using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    // The editable Blender model remains outside Assets. Only baked, compressed
    // meshes and shared maps are referenced by the runtime prefab.
    public static class ImportRealisticOrbiter
    {
        public const string Root = "Assets/_LunarEscape/Art/LunarOrbiter";
        public const string PrefabPath = Root + "/Realistic Lunar Orbiter.prefab";

        [Serializable] private sealed class MaterialDescription
        {
            public string name, baseColorTexture, normalTexture, metallicSmoothnessTexture;
            public float[] color, emission;
            public float metallic, roughness, normalScale = 1;
        }
        [Serializable] private sealed class ModelDescription
        { public string id, fbx; public int meshCount, triangleCount; }
        [Serializable] private sealed class Manifest
        { public MaterialDescription[] materials; public ModelDescription[] models; }

        public static void Import()
        {
            var path = Root + "/orbiter-manifest.json";
            if (!File.Exists(path)) throw new FileNotFoundException("Orbiter export manifest is missing.", path);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if (manifest?.materials == null || manifest.models == null || manifest.materials.Length > 8 ||
                !manifest.models.Select(model => model.id).OrderBy(id => id)
                    .SequenceEqual(new[] { "capture", "lod0", "lod1", "lod2" }))
                throw new InvalidDataException("Expected three orbiter LODs, one independent capture lamp and at most eight shared materials.");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Meshes");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var description in manifest.materials)
            {
                ConfigureTexture(description.baseColorTexture, true, false);
                ConfigureTexture(description.normalTexture, false, true);
                ConfigureTexture(description.metallicSmoothnessTexture, false, false);
            }
            var materials = manifest.materials.ToDictionary(description => description.name, BuildMaterial);
            foreach (var model in manifest.models) ConfigureModel(model, materials);

            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Realistic Lunar Orbiter");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var renderersByLod = new Dictionary<string, Renderer[]>();
                var triangleCounts = new Dictionary<string, long>();
                foreach (var model in manifest.models)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + model.fbx);
                    var matrix = AnchorFrame(source.transform).inverse;
                    var filters = source.GetComponentsInChildren<MeshFilter>(true);
                    if (filters.Length != model.meshCount)
                        throw new InvalidDataException($"Orbiter {model.id}: expected {model.meshCount} meshes, found {filters.Length}.");
                    Transform parent = root.transform;
                    if (model.id != "capture")
                    {
                        parent = new GameObject(model.id.ToUpperInvariant()).transform;
                        parent.SetParent(root.transform, false);
                    }
                    var renderers = new List<Renderer>();
                    long triangles = 0;
                    foreach (var filter in filters)
                    {
                        var mesh = BakeMesh(filter.sharedMesh, matrix * filter.transform.localToWorldMatrix,
                            model.id + "_" + filter.name);
                        for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += mesh.GetIndexCount(sub) / 3;
                        mesh = SaveMesh(mesh, Root + "/Meshes/" + SafeName(mesh.name) + ".asset");
                        var part = new GameObject(model.id == "capture" ? "Capture Indicator" : filter.name,
                            typeof(MeshFilter), typeof(MeshRenderer));
                        part.transform.SetParent(parent, false);
                        part.GetComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = part.GetComponent<MeshRenderer>();
                        renderer.sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials.Select(material =>
                            material != null && materials.TryGetValue(material.name, out var mapped) ? mapped :
                            throw new InvalidDataException("Unknown orbiter material on " + filter.name)).ToArray();
                        renderer.shadowCastingMode = model.id == "capture" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                        renderers.Add(renderer);
                    }
                    triangleCounts.Add(model.id, triangles);
                    if (model.id != "capture") renderersByLod.Add(model.id, renderers.ToArray());
                    else if (renderers.Count != 1) throw new InvalidDataException("The capture indicator must have exactly one renderer.");

                    foreach (var anchor in source.GetComponentsInChildren<Transform>(true)
                                 .Where(transform => transform.name.StartsWith("Anchor_", StringComparison.Ordinal)))
                    {
                        var position = matrix.MultiplyPoint3x4(anchor.position);
                        var existing = root.transform.Find(anchor.name);
                        if (existing != null)
                        {
                            if (Vector3.Distance(existing.localPosition, position) > .0002f)
                                throw new InvalidDataException("Orbiter LODs disagree on " + anchor.name);
                        }
                        else
                        {
                            var copy = new GameObject(anchor.name).transform;
                            copy.SetParent(root.transform, false);
                            copy.localPosition = position;
                        }
                    }
                }
                VerifyAnchor(root.transform, "Origin", Vector3.zero);
                VerifyAnchor(root.transform, "AxisX", Vector3.right);
                VerifyAnchor(root.transform, "AxisY", Vector3.up);
                VerifyAnchor(root.transform, "AxisZ", Vector3.forward);
                VerifyAnchor(root.transform, "DockingCenter", Vector3.zero);
                VerifyAnchor(root.transform, "DockingPort", Vector3.zero);
                VerifyAnchor(root.transform, "PortNormal", Vector3.back);
                VerifyAnchor(root.transform, "CaptureIndicator", new Vector3(0, .9f, -.1f));
                if (triangleCounts["lod0"] > 110000 || triangleCounts["lod1"] > 30000 || triangleCounts["lod2"] > 10000 ||
                    triangleCounts["lod0"] <= triangleCounts["lod1"] || triangleCounts["lod1"] <= triangleCounts["lod2"])
                    throw new InvalidDataException("Orbiter meshes exceed the LOD budget or fail to simplify at distance.");
                var lodGroup = root.AddComponent<LODGroup>();
                // Hard swaps avoid rendering two large LODs simultaneously in VR.
                // The mission-driven capture indicator is deliberately excluded.
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.SetLODs(new[] { new LOD(.4f, renderersByLod["lod0"]),
                    new LOD(.12f, renderersByLod["lod1"]), new LOD(.008f, renderersByLod["lod2"]) });
                lodGroup.RecalculateBounds();
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new IOException("Could not save the realistic orbiter prefab.");
                AssetDatabase.SaveAssets();
                Debug.Log($"REALISTIC_ORBITER_IMPORTED LOD triangles={triangleCounts["lod0"]}/{triangleCounts["lod1"]}/{triangleCounts["lod2"]}; capture={triangleCounts["capture"]}; materials={materials.Count}; {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void ConfigureTexture(string relativePath, bool srgb, bool normal)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            var path = Root + "/" + relativePath;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new FileNotFoundException("Orbiter map is missing.", path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 1024;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            var platform = importer.GetPlatformTextureSettings("Standalone");
            platform.name = "Standalone"; platform.overridden = true; platform.maxTextureSize = 1024;
            platform.format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial(MaterialDescription description)
        {
            if (description.color == null || description.color.Length != 4)
                throw new InvalidDataException("Missing orbiter material colour: " + description.name);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit is unavailable.");
            var path = Root + "/Materials/" + description.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            { material = new Material(shader) { name = description.name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            var c = description.color;
            material.SetColor("_BaseColor", string.IsNullOrEmpty(description.baseColorTexture)
                ? new Color(c[0], c[1], c[2], c[3]).gamma : Color.white);
            material.SetFloat("_Metallic", Mathf.Clamp01(description.metallic));
            material.SetFloat("_Smoothness", string.IsNullOrEmpty(description.metallicSmoothnessTexture)
                ? Mathf.Clamp01(1 - description.roughness) : 1);
            material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_Cull", (float)CullMode.Back); material.SetFloat("_ZWrite", 1);
            material.renderQueue = -1;
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetTexture("_BaseMap", Texture(description.baseColorTexture));
            material.SetTexture("_BumpMap", Texture(description.normalTexture));
            material.SetTexture("_MetallicGlossMap", Texture(description.metallicSmoothnessTexture));
            material.SetFloat("_BumpScale", Mathf.Clamp(description.normalScale, 0, 1));
            Keyword(material, "_NORMALMAP", !string.IsNullOrEmpty(description.normalTexture));
            Keyword(material, "_METALLICSPECGLOSSMAP", !string.IsNullOrEmpty(description.metallicSmoothnessTexture));
            bool emits = description.emission != null && description.emission.Length >= 3;
            material.SetColor("_EmissionColor", emits ? new Color(description.emission[0], description.emission[1], description.emission[2]).gamma : Color.black);
            Keyword(material, "_EMISSION", emits);
            material.globalIlluminationFlags = emits ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Texture(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + relativePath);
            return texture != null ? texture : throw new FileNotFoundException("Orbiter texture was not imported.", relativePath);
        }
        private static void Keyword(Material material, string keyword, bool enabled)
        { if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword); }

        private static void ConfigureModel(ModelDescription description, IReadOnlyDictionary<string, Material> materials)
        {
            var path = Root + "/" + description.fbx;
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                throw new FileNotFoundException("Orbiter FBX is missing.", path);
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
            importer.addCollider = false; importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var material in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.Key), material.Value);
            importer.SaveAndReimport();
        }

        private static Matrix4x4 AnchorFrame(Transform source)
        {
            var origin = Anchor(source, "Origin").position;
            var x = Anchor(source, "AxisX").position - origin;
            var y = Anchor(source, "AxisY").position - origin;
            var z = Anchor(source, "AxisZ").position - origin;
            var frame = Matrix4x4.identity;
            frame.SetColumn(0, new Vector4(x.x, x.y, x.z, 0));
            frame.SetColumn(1, new Vector4(y.x, y.y, y.z, 0));
            frame.SetColumn(2, new Vector4(z.x, z.y, z.z, 0));
            frame.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
            if (!float.IsFinite(frame.determinant) || Mathf.Abs(frame.determinant) < .0000001f ||
                Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > .001f ||
                Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) > .001f ||
                Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) > .001f)
                throw new InvalidDataException("Orbiter does not have a valid orthogonal metre frame.");
            return frame;
        }

        private static Mesh BakeMesh(Mesh source, Matrix4x4 matrix, string name)
        {
            if (source == null) throw new InvalidDataException("Orbiter contains a missing mesh.");
            var mesh = Object.Instantiate(source); mesh.name = name; mesh.hideFlags = HideFlags.None;
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices;
            var normals = mesh.normals; var normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            if (normals.Length == vertices.Length) mesh.normals = normals; else mesh.RecalculateNormals();
            bool mirrored = matrix.determinant < 0;
            if (mirrored)
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
                    mesh.SetTriangles(indices, sub, false);
                }
            var tangents = mesh.tangents;
            for (int i = 0; i < tangents.Length; i++)
            {
                var t = tangents[i]; var xyz = matrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                tangents[i] = new Vector4(xyz.x, xyz.y, xyz.z, mirrored ? -t.w : t.w);
            }
            if (tangents.Length == vertices.Length) mesh.tangents = tangents; else mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); existing = mesh; }
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); }
            existing.UploadMeshData(true);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        private static Transform Anchor(Transform root, string name)
        {
            var anchors = root.GetComponentsInChildren<Transform>(true).Where(transform => transform.name == "Anchor_" + name).ToArray();
            return anchors.Length == 1 ? anchors[0] : throw new InvalidDataException("Expected one orbiter anchor: " + name);
        }
        private static void VerifyAnchor(Transform root, string name, Vector3 expected)
        {
            if (Vector3.Distance(Anchor(root, name).localPosition, expected) > .0002f)
                throw new InvalidDataException("Orbiter metre frame is inconsistent at " + name);
        }
        private static string SafeName(string name)
        { foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_'); return name; }
    }
}
