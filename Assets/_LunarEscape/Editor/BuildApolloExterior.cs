using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LunarEscape.Editor
{
    // Blender is needed only when exporting a changed source model. Unity imports
    // the checked-in FBX/textures directly, without a glTF or Blender dependency.
    public static class BuildApolloExterior
    {
        public const string Root = "Assets/_LunarEscape/Art/ApolloExterior";
        public const string PrefabPath = Root + "/Apollo Exterior.prefab";
        public const string VisualName = "Apollo Exterior - Blender";
        [Serializable] private sealed class MaterialDescription
        { public string name; public float[] color; public float metallic, roughness; public bool textured; }
        [Serializable] private sealed class Manifest { public MaterialDescription[] materials; }

        [MenuItem("Lunar Escape/Install Blender Apollo Exterior")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            Import();
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("APOLLO_EXTERIOR_INSTALLED");
        }

        [MenuItem("Lunar Escape/View Blender Apollo Exterior")]
        public static void Open()
        {
            BuildDockingScene.Open();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != BuildDockingScene.ScenePath) return;
            var layout = UnityEngine.Object.FindAnyObjectByType<LunarViewLayout>();
            if (layout == null || layout.Lander.Find(VisualName) == null) return;
            Selection.activeGameObject = layout.Lander.Find(VisualName).gameObject;
            EditorApplication.delayCall += () =>
            {
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                view.LookAt(layout.Lander.position + Vector3.up * 3, Quaternion.Euler(12, -35, 0), 9);
                view.Focus();
            };
            Debug.Log("APOLLO_EXTERIOR_SCENE_OPENED");
        }

        public static GameObject Import()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Root + "/Materials");
            ConfigureTexture("StructureBaseColor", true, false);
            ConfigureTexture("StructureNormal", false, true);
            ConfigureTexture("StructureMetallicSmoothness", false, false);
            var data = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/materials.json"));
            var materials = data.materials.ToDictionary(m => m.name, BuildMaterial);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Root + "/ApolloExterior.fbx");
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
            importer.addCollider = false; importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/ApolloExterior.fbx");
            var root = new GameObject(VisualName);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Geometry"; model.transform.SetParent(root.transform, false);
                var origin = Anchor(model.transform, "Origin");
                var up = (Anchor(model.transform, "Up").position - origin.position).normalized;
                var front = (Anchor(model.transform, "Front").position - origin.position).normalized;
                model.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(front, up));
                var bounds = BoundsOf(model);
                model.transform.localScale *= 7 / bounds.size.y;
                bounds = BoundsOf(model);
                model.transform.position -= new Vector3(origin.position.x, bounds.min.y, origin.position.z);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => materials[m.name]).ToArray();
                    renderer.gameObject.isStatic = true;
                }
                bounds = BoundsOf(model);
                Debug.Log($"APOLLO_EXTERIOR_BOUNDS {bounds}; hatch={Anchor(model.transform, "Hatch").position}; ladder={Anchor(model.transform, "LadderFoot").position}");
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void ApplyToCurrentScene()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            var layout = session.GetComponent<LunarViewLayout>();
            var lander = layout.Lander;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? Import();
            // Keep the same lander, hatch and persistent interaction callbacks.
            // Unpack the old prefab before removing only its obsolete visual parts.
            if (PrefabUtility.IsPartOfPrefabInstance(lander))
                PrefabUtility.UnpackPrefabInstance(lander.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var previous = lander.Find(VisualName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach (var part in lander.GetComponentsInChildren<MeshFilter>(true)
                         .Where(f => f.name.StartsWith("Apollo Material Group ")).ToArray())
                UnityEngine.Object.DestroyImmediate(part.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.name = VisualName; visual.transform.SetParent(lander, false);
            layout.Door.position = Anchor(visual.transform, "Hatch").position;
            // Lift the existing door prompt clear of the new porch rails when
            // viewed from the boarding zone; keep the door's interaction target.
            var prompt = (RectTransform)layout.Door.Find("Hatch Control");
            prompt.anchoredPosition3D = new Vector3(0, .75f, -.5f);
            var ladder = lander.Find("Ladder - boarding by hatch only");
            var foot = lander.InverseTransformPoint(Anchor(visual.transform, "LadderFoot").position);
            var porch = lander.InverseTransformPoint(Anchor(visual.transform, "Porch").position);
            ladder.localPosition = (foot + porch) * .5f;
            ladder.localRotation = Quaternion.FromToRotation(Vector3.up, porch - foot);
            ladder.GetComponent<BoxCollider>().size = new Vector3(.95f, Vector3.Distance(foot, porch) + .2f, .32f);
            var cabin = lander.Find("Closed Ascent Cabin");
            cabin.localPosition = new Vector3(0, 4.45f, -.08f);
            cabin.GetComponent<BoxCollider>().size = new Vector3(3.7f, 3.15f, 4.35f);
            var footColliders = lander.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "Landing Foot Collision").ToArray();
            var footAnchors = visual.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Anchor_Foot")).OrderBy(t => t.name).ToArray();
            if (footAnchors.Length != footColliders.Length) throw new InvalidDataException("Landing foot anchors do not match the collision layout.");
            for (int i = 0; i < footColliders.Length; i++)
            {
                footColliders[i].transform.position = footAnchors[i].position;
                footColliders[i].size = new Vector3(1.05f, .45f, 1.05f);
            }
            EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        }

        private static Transform Anchor(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Anchor_" + name);
        private static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        private static void ConfigureTexture(string name, bool srgb, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Textures/" + name + ".png");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true; importer.anisoLevel = 4;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        private static Material BuildMaterial(MaterialDescription description)
        {
            string path = Root + "/Materials/" + description.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = description.name };
                AssetDatabase.CreateAsset(material, path);
            }
            var c = description.color;
            material.SetColor("_BaseColor", new Color(c[0], c[1], c[2], c[3]).gamma);
            material.SetFloat("_Metallic", description.metallic);
            material.SetFloat("_Smoothness", 1 - description.roughness);
            // Source geometry includes thin foil and signage; preserve both faces.
            material.SetFloat("_Cull", 0);
            if (description.textured)
            {
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/StructureBaseColor.png"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/StructureNormal.png"));
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/StructureMetallicSmoothness.png"));
                material.SetFloat("_BumpScale", 1); material.SetFloat("_Smoothness", 1);
                material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
