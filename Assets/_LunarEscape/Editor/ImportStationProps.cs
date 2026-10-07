using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LunarEscape.Editor
{
    // Imports the checked-in models and textures; Blender and network access are
    // not needed when opening the project or building the game.
    public static class ImportStationProps
    {
        public const string Root = "Assets/_LunarEscape/Art/StationProps";
        private const string ManifestPath = Root + "/material-manifest.json";

        [Serializable] private sealed class Manifest { public AssetDescription[] assets; }
        [Serializable] private sealed class AssetDescription
        {
            public string id, fbx;
            public float[] bounds_m;
            public MaterialDescription[] materials;
        }
        [Serializable] private sealed class MaterialDescription
        {
            public string name, base_color, normal, metallic_smoothness, occlusion;
            public float[] color;
            public float metallic, roughness, smoothness, alpha_cutoff;
            public bool alpha_clip;
        }

        public static string PrefabPath(string id)
        {
            ValidateId(id);
            return Root + "/" + id + "/" + id + ".prefab";
        }

        [MenuItem("Lunar Escape/Import Realistic Station Props")]
        public static void Import()
        {
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException("Station prop manifest is missing.", ManifestPath);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest?.assets == null || manifest.assets.Length == 0)
                throw new InvalidDataException("Station prop manifest contains no assets.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in manifest.assets)
            {
                ValidateDescription(asset);
                if (!ids.Add(asset.id)) throw new InvalidDataException("Duplicate station prop id: " + asset.id);
                Directory.CreateDirectory(Root + "/" + asset.id + "/Materials");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            var configuredTextures = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var asset in manifest.assets)
            {
                var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
                foreach (var description in asset.materials)
                {
                    ConfigureTexture(description.base_color, "base", description.alpha_clip, configuredTextures);
                    ConfigureTexture(description.normal, "normal", false, configuredTextures);
                    ConfigureTexture(description.metallic_smoothness, "metalSmooth", false, configuredTextures);
                    ConfigureTexture(description.occlusion, "ao", false, configuredTextures);
                    materials.Add(description.name, BuildMaterial(asset.id, description, shader));
                }
                ConfigureModel(asset, materials);
                CreateVisualPrefab(asset, materials);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("STATION_PROPS_IMPORTED " + manifest.assets.Length + " assets from " + ManifestPath);
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
                throw new ArgumentException("A station prop id must contain only letters, digits and underscores.", nameof(id));
        }

        private static void ValidateDescription(AssetDescription asset)
        {
            if (asset == null) throw new InvalidDataException("Null station prop description.");
            ValidateId(asset.id);
            ValidateSourcePath(asset.fbx);
            if (asset.bounds_m == null || asset.bounds_m.Length != 3 || asset.bounds_m.Any(v => v <= 0 || float.IsNaN(v) || float.IsInfinity(v)))
                throw new InvalidDataException("Invalid meter bounds for " + asset.id);
            if (asset.materials == null || asset.materials.Length == 0)
                throw new InvalidDataException("No materials specified for " + asset.id);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var material in asset.materials)
            {
                if (material == null || string.IsNullOrWhiteSpace(material.name) || !names.Add(material.name))
                    throw new InvalidDataException("Missing or duplicate material name for " + asset.id);
                if (material.name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidDataException("Unsafe material filename: " + material.name);
                if (material.color != null && material.color.Length != 3 && material.color.Length != 4)
                    throw new InvalidDataException("Invalid RGBA material color: " + material.name);
                foreach (string path in new[] { material.base_color, material.normal, material.metallic_smoothness, material.occlusion })
                    if (!string.IsNullOrEmpty(path)) ValidateSourcePath(path);
            }
        }

        private static void ValidateSourcePath(string path)
        {
            string root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (string.IsNullOrEmpty(path) || !path.StartsWith(Root + "/", StringComparison.Ordinal)
                || !Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new FileNotFoundException("Station prop source must be an existing file inside " + Root + ".", path);
        }

        private static void ConfigureTexture(string path, string role, bool alphaClip, Dictionary<string, string> configured)
        {
            if (string.IsNullOrEmpty(path)) return;
            string settingsKey = role + (alphaClip ? ":clip" : string.Empty);
            if (configured.TryGetValue(path, out string previous))
            {
                if (previous != settingsKey) throw new InvalidDataException("Texture used with incompatible roles: " + path);
                return;
            }
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Texture importer is missing: " + path);
            bool normal = role == "normal";
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = role == "base";
            importer.convertToNormalmap = false; // The downloaded maps already contain tangent-space normals.
            importer.maxTextureSize = 2048; // Does not upscale the battery's existing 1K maps.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.anisoLevel = 4;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.isReadable = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            // Never treat metallic-map alpha as transparency: it stores smoothness.
            importer.alphaIsTransparency = role == "base" && alphaClip;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.compressionQuality = 100;
            importer.SaveAndReimport();
            configured.Add(path, settingsKey);
        }

        private static Material BuildMaterial(string assetId, MaterialDescription description, Shader shader)
        {
            string path = Root + "/" + assetId + "/Materials/" + description.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = description.name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.name = description.name;
            material.shaderKeywords = Array.Empty<string>();
            Color color = Color.white;
            if (description.color != null)
            {
                var values = description.color;
                float alpha = values.Length == 4 ? values[3] : 1;
                // Blender scalar colors are linear. Unity material color inputs
                // use sRGB, while the original color textures import as sRGB.
                color = new Color(values[0], values[1], values[2], alpha).gamma;
                color.a = alpha;
            }
            var baseMap = LoadTexture(description.base_color);
            var normal = LoadTexture(description.normal);
            var metalSmooth = LoadTexture(description.metallic_smoothness);
            var occlusion = LoadTexture(description.occlusion);
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1);
            material.SetTexture("_MetallicGlossMap", metalSmooth);
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetFloat("_OcclusionStrength", 1);
            material.SetFloat("_WorkflowMode", 1); // Metallic workflow.
            material.SetFloat("_Metallic", metalSmooth != null ? 1 : Mathf.Clamp01(description.metallic));
            // Current scalar entries provide smoothness; roughness is also
            // accepted as its inverse (including a completely matte surface).
            float smoothness = description.smoothness > 0 ? description.smoothness : 1 - description.roughness;
            material.SetFloat("_Smoothness", metalSmooth != null ? 1 : Mathf.Clamp01(smoothness));
            material.SetFloat("_SmoothnessTextureChannel", 0); // Metallic alpha, never base-color alpha.
            material.SetFloat("_Surface", 0);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_ZWrite", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_AlphaClip", description.alpha_clip ? 1 : 0);
            material.SetFloat("_Cutoff", description.alpha_cutoff > 0 ? description.alpha_cutoff : .5f);
            material.SetColor("_EmissionColor", Color.black);
            material.SetTexture("_EmissionMap", null);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.enableInstancing = true;
            material.SetOverrideTag("RenderType", description.alpha_clip ? "TransparentCutout" : "Opaque");
            material.renderQueue = (int)(description.alpha_clip ? RenderQueue.AlphaTest : RenderQueue.Geometry);
            SetKeyword(material, "_NORMALMAP", normal != null);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", metalSmooth != null);
            SetKeyword(material, "_OCCLUSIONMAP", occlusion != null);
            SetKeyword(material, "_ALPHATEST_ON", description.alpha_clip);
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetShaderPassEnabled("DepthOnly", true);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidDataException("Imported texture not found: " + path);
            return texture;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }

        private static void ConfigureModel(AssetDescription asset, Dictionary<string, Material> materials)
        {
            var importer = AssetImporter.GetAtPath(asset.fbx) as ModelImporter;
            if (importer == null) throw new InvalidDataException("FBX importer is missing: " + asset.fbx);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }

        private static void CreateVisualPrefab(AssetDescription asset, Dictionary<string, Material> materials)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(asset.fbx);
            if (source == null) throw new InvalidDataException("Imported FBX prefab is missing: " + asset.fbx);
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(asset.id);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Geometry";
                model.transform.SetParent(root.transform, false);
                // Only visual components survive. MeshFilters keep their FBX
                // asset references; no copies of mesh data are generated.
                foreach (var component in model.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform || component is MeshFilter || component is MeshRenderer) continue;
                    UnityEngine.Object.DestroyImmediate(component);
                }
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    transform.gameObject.isStatic = false;
                    GameObjectUtility.SetStaticEditorFlags(transform.gameObject, 0);
                }
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidDataException("No static meshes in " + asset.fbx);
                foreach (var renderer in renderers)
                {
                    renderer.enabled = true;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    {
                        if (material == null || !materials.TryGetValue(material.name, out var mapped))
                            throw new InvalidDataException("Unmapped FBX material on " + asset.id + ": " + (material == null ? "null" : material.name));
                        return mapped;
                    }).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    renderer.lightmapIndex = -1;
                }

                Vector3 expected = new Vector3(asset.bounds_m[0], asset.bounds_m[1], asset.bounds_m[2]);
                Bounds bounds = BoundsOf(renderers);
                float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (largest <= .000001f) throw new InvalidDataException("Empty model bounds: " + asset.id);
                // Correct a uniform centimeters/meters import discrepancy only.
                // Never deform a model to make an incorrect axis conversion fit.
                float factor = Mathf.Max(expected.x, expected.y, expected.z) / largest;
                model.transform.localScale *= factor;
                bounds = BoundsOf(renderers);
                for (int axis = 0; axis < 3; axis++)
                    if (Mathf.Abs(bounds.size[axis] - expected[axis]) > Mathf.Max(.0005f, expected[axis] * .015f))
                        throw new InvalidDataException($"Station prop bounds disagree with manifest: {asset.id}, expected={expected}, actual={bounds.size}. Check FBX axis conversion.");
                model.transform.localPosition -= bounds.center;
                bounds = BoundsOf(renderers);
                if (bounds.center.sqrMagnitude > .000001f)
                    throw new InvalidDataException("Could not center prop pivot: " + asset.id);
                string path = PrefabPath(asset.id);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new IOException("Failed to save station prop prefab: " + path);
                Debug.Log($"STATION_PROP_READY {asset.id} bounds_m={bounds.size} center={bounds.center} unit_correction={factor:G7} prefab={path}");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static Bounds BoundsOf(MeshRenderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
