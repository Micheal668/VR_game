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
    /// <summary>
    /// Imports the editable Blender station as ordinary Unity mesh/material assets.
    /// This asset-only operation never opens, saves or changes the gameplay scene.
    /// Four exported metre anchors determine scale and handedness, independently
    /// of model bounds and Unity/FBX axis-conversion defaults.
    /// </summary>
    public static class ImportStationGeometry
    {
        public const string Root = "Assets/_LunarEscape/Art/LunarBaseGeometry";
        public const string ExteriorPrefabPath = Root + "/Prefabs/Station Realistic Exterior.prefab";
        public const string InteriorPrefabPath = Root + "/Prefabs/Station Realistic Interior.prefab";

        [Serializable]
        private sealed class MaterialDescription
        {
            public string name;
            public float[] color;
            public float metallic;
            public float roughness;
            public string baseColorTexture;
            public string normalTexture;
            public string metallicSmoothnessTexture;
            public float[] emission;
        }

        [Serializable]
        private sealed class ModelDescription
        {
            public string id;
            public string fbx;
            public int meshCount;
        }

        [Serializable]
        private sealed class Manifest
        {
            public MaterialDescription[] materials;
            public ModelDescription[] models;
            public string[] hideExistingRendererNames;
        }

        public static string[] HiddenRendererNames =>
            ReadManifest().hideExistingRendererNames ?? Array.Empty<string>();

        public static void Import()
        {
            var manifest = ReadManifest();
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Meshes");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var description in manifest.materials)
            {
                ConfigureTexture(description.baseColorTexture, true, false);
                ConfigureTexture(description.normalTexture, false, true);
                ConfigureTexture(description.metallicSmoothnessTexture, false, false);
            }

            var materials = manifest.materials.ToDictionary(m => m.name, BuildMaterial,
                StringComparer.Ordinal);
            foreach (var model in manifest.models)
            {
                string prefabPath;
                string name;
                switch (model.id)
                {
                    case "exterior":
                        prefabPath = ExteriorPrefabPath;
                        name = "Station Realistic Exterior";
                        break;
                    case "interior":
                        prefabPath = InteriorPrefabPath;
                        name = "Station Realistic Interior";
                        break;
                    default:
                        throw new InvalidDataException("Unknown station model id: " + model.id);
                }

                ImportModel(model, prefabPath, name, materials);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("STATION_GEOMETRY_IMPORTED " + ExteriorPrefabPath + " | " + InteriorPrefabPath);
        }

        private static Manifest ReadManifest()
        {
            string path = Root + "/base-manifest.json";
            if (!File.Exists(path)) throw new FileNotFoundException("Station art manifest is missing.", path);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if (manifest?.materials == null || manifest.materials.Length == 0 ||
                manifest.models == null || manifest.models.Length != 2)
                throw new InvalidDataException("Station manifest must contain materials and exterior/interior models.");
            if (manifest.models.Count(m => m.id == "exterior") != 1 ||
                manifest.models.Count(m => m.id == "interior") != 1)
                throw new InvalidDataException("Station manifest must contain exactly one exterior and one interior.");
            return manifest;
        }

        private static void ConfigureTexture(string relativePath, bool srgb, bool normal)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            string path = Root + "/" + relativePath;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Station texture is missing.", path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 1024;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial(MaterialDescription description)
        {
            if (description.color == null || description.color.Length != 4)
                throw new InvalidDataException("Invalid station material color: " + description.name);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit is unavailable.");
            string path = Root + "/Materials/" + description.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = description.name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;

            var c = description.color;
            // Blender's numeric material colors are linear. Texture base colors
            // already contain their authored tint, so do not multiply it twice.
            material.SetColor("_BaseColor", string.IsNullOrEmpty(description.baseColorTexture)
                ? new Color(c[0], c[1], c[2], c[3]).gamma : Color.white);
            material.SetFloat("_Metallic", Mathf.Clamp01(description.metallic));
            material.SetFloat("_Smoothness", Mathf.Clamp01(1f - description.roughness));
            material.SetFloat("_Surface", 0);
            material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", 1);
            material.renderQueue = -1;
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            material.SetTexture("_BaseMap", Texture(description.baseColorTexture));
            material.SetTexture("_BumpMap", Texture(description.normalTexture));
            material.SetTexture("_MetallicGlossMap", Texture(description.metallicSmoothnessTexture));
            SetKeyword(material, "_NORMALMAP", !string.IsNullOrEmpty(description.normalTexture));
            SetKeyword(material, "_METALLICSPECGLOSSMAP", !string.IsNullOrEmpty(description.metallicSmoothnessTexture));
            material.SetFloat("_BumpScale", description.name == "LB_PaintedAlloy" ? .07f : .4f);
            if (!string.IsNullOrEmpty(description.metallicSmoothnessTexture)) material.SetFloat("_Smoothness", 1);

            bool emits = description.emission != null && description.emission.Length >= 3;
            var emission = emits
                ? new Color(description.emission[0], description.emission[1], description.emission[2], 1).gamma
                : Color.black;
            material.SetColor("_EmissionColor", emission);
            SetKeyword(material, "_EMISSION", emits && emission.maxColorComponent > 0);
            material.globalIlluminationFlags = emits ? MaterialGlobalIlluminationFlags.BakedEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Texture(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + relativePath);
            if (texture == null) throw new FileNotFoundException("Station material texture was not imported.", relativePath);
            return texture;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        private static void ImportModel(ModelDescription description, string prefabPath, string displayName,
            IReadOnlyDictionary<string, Material> materials)
        {
            string modelPath = Root + "/" + description.fbx;
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("Station FBX is missing.", modelPath);
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.addCollider = false;
            // Only needed by the editor bake; runtime prefabs use independent meshes.
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new InvalidDataException("Station model failed to import: " + modelPath);
            var sourceToUnity = AnchorFrame(source.transform).inverse;
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != description.meshCount)
                throw new InvalidDataException($"Station {description.id} has {filters.Length} meshes; manifest expects {description.meshCount}.");

            // The staging scene is an invisible, isolated asset workspace. The
            // currently open gameplay scene and its dirty flag remain untouched.
            var staging = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(displayName);
                SceneManager.MoveGameObjectToScene(root, staging);
                long triangles = 0;
                foreach (var filter in filters)
                {
                    var sourceRenderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || sourceRenderer == null)
                        throw new InvalidDataException("Station mesh is missing its mesh or renderer: " + filter.name);
                    var matrix = sourceToUnity * filter.transform.localToWorldMatrix;
                    var mesh = BakeMesh(filter.sharedMesh, matrix, description.id + "_" + filter.name);
                    mesh = SaveMesh(mesh, Root + "/Meshes/" + SafeName(mesh.name) + ".asset");

                    var part = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(root.transform, false);
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = sourceRenderer.sharedMaterials.Select(m =>
                    {
                        if (m == null || !materials.TryGetValue(m.name, out var remapped))
                            throw new InvalidDataException("Unmapped station material on " + filter.name + ": " + (m == null ? "null" : m.name));
                        return remapped;
                    }).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    if (renderer.sharedMaterials.Length != mesh.subMeshCount)
                        throw new InvalidDataException("Station material/submesh mismatch: " + filter.name);
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        if (mesh.GetTopology(sub) == MeshTopology.Triangles) triangles += (long)mesh.GetIndexCount(sub) / 3;
                }

                foreach (var anchor in source.GetComponentsInChildren<Transform>(true)
                             .Where(t => t.name.StartsWith("Anchor_", StringComparison.Ordinal)))
                {
                    var copy = new GameObject(anchor.name).transform;
                    copy.SetParent(root.transform, false);
                    copy.localPosition = sourceToUnity.MultiplyPoint3x4(anchor.position);
                }
                VerifyAnchor(root.transform, "Origin", Vector3.zero);
                VerifyAnchor(root.transform, "AxisX", Vector3.right);
                VerifyAnchor(root.transform, "AxisY", Vector3.up);
                VerifyAnchor(root.transform, "AxisZ", Vector3.forward);
                VerifyAnchor(root.transform, "AirlockDoor", new Vector3(4, 1.25f, 1.7f));
                VerifyAnchor(root.transform, "RepairContact", new Vector3(.58f, 1.22f, .49f));
                VerifyAnchor(root.transform, "SupplyShelf", new Vector3(0, .92f, 3.1f));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null) throw new IOException("Could not save station prefab: " + prefabPath);
                Debug.Log($"STATION_GEOMETRY_BAKED {description.id}: {filters.Length} meshes, {triangles} triangles; metre anchors verified; source determinant={sourceToUnity.determinant:G6}");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(staging);
            }
        }

        private static Matrix4x4 AnchorFrame(Transform source)
        {
            var origin = Anchor(source, "Origin").position;
            var x = Anchor(source, "AxisX").position - origin;
            var y = Anchor(source, "AxisY").position - origin;
            var z = Anchor(source, "AxisZ").position - origin;
            if (x.sqrMagnitude < 1e-10f || y.sqrMagnitude < 1e-10f || z.sqrMagnitude < 1e-10f ||
                Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > .001f ||
                Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) > .001f ||
                Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) > .001f)
                throw new InvalidDataException("Station FBX does not contain a valid orthogonal metre frame.");
            var matrix = Matrix4x4.identity;
            matrix.SetColumn(0, new Vector4(x.x, x.y, x.z, 0));
            matrix.SetColumn(1, new Vector4(y.x, y.y, y.z, 0));
            matrix.SetColumn(2, new Vector4(z.x, z.y, z.z, 0));
            matrix.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
            if (float.IsNaN(matrix.determinant) || float.IsInfinity(matrix.determinant) ||
                Mathf.Abs(matrix.determinant) < 1e-12f)
                throw new InvalidDataException("Station FBX metre frame is singular or nonfinite.");
            return matrix;
        }

        private static Mesh BakeMesh(Mesh source, Matrix4x4 matrix, string name)
        {
            var mesh = Object.Instantiate(source);
            mesh.name = name;
            mesh.hideFlags = HideFlags.None;
            var vertices = source.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices;

            var normalMatrix = matrix.inverse.transpose;
            var normals = source.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            if (normals.Length == vertices.Length) mesh.normals = normals;

            bool mirrored = matrix.determinant < 0;
            if (mirrored)
            {
                // Baking a reflection into vertices reverses orientation. Reverse
                // winding to keep backface culling and the authored outside intact.
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int swap = indices[i + 1];
                        indices[i + 1] = indices[i + 2];
                        indices[i + 2] = swap;
                    }
                    mesh.SetTriangles(indices, sub, false);
                }
            }
            if (normals.Length != vertices.Length) mesh.RecalculateNormals();

            var tangents = source.tangents;
            for (int i = 0; i < tangents.Length; i++)
            {
                var t = tangents[i];
                var xyz = matrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                tangents[i] = new Vector4(xyz.x, xyz.y, xyz.z, mirrored ? -t.w : t.w);
            }
            if (tangents.Length == vertices.Length) mesh.tangents = tangents;
            else if (mesh.uv.Length == vertices.Length) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Transform Anchor(Transform root, string name)
        {
            var anchors = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Anchor_" + name).ToArray();
            if (anchors.Length != 1) throw new InvalidDataException("Expected one station anchor: " + name);
            return anchors[0];
        }

        private static void VerifyAnchor(Transform root, string name, Vector3 expected)
        {
            var actual = Anchor(root, name).localPosition;
            if (Vector3.Distance(actual, expected) > .0002f)
                throw new InvalidDataException($"Station anchor {name} is {actual}; expected {expected} metres.");
        }

        private static string SafeName(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name;
        }
    }
}
