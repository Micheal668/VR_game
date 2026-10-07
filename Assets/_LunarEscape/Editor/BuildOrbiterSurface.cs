using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarEscape.Editor
{
    public static class BuildOrbiterSurface
    {
        public const string OrbiterVisualName = "Realistic Orbiter - Blender";

        [MenuItem("Lunar Escape/Install Realistic Orbiter and Lunar Surface")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            ImportRealisticOrbiter.Import();
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            ArtResourceAudit.ReportAfter();
            Debug.Log("REALISTIC_ORBITER_SURFACE_INSTALLED");
        }

        public static void ApplyToCurrentScene()
        {
            ApplyOrbiter();
            BuildRealisticLunarSurface.ApplyToCurrentScene();
        }

        private static void ApplyOrbiter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportRealisticOrbiter.PrefabPath);
            if (prefab == null)
            {
                ImportRealisticOrbiter.Import();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportRealisticOrbiter.PrefabPath);
            }
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            var presentation = session.GetComponent<FlightScenePresenter>();
            var view = presentation.FlightWorld.GetComponent<OrbiterView>();
            if (view == null || view.Target == null)
                throw new InvalidOperationException("The main scene must have its docking target before installing orbiter art.");
            var target = view.Target;
            // The target root is also the mathematical docking port. Replacing
            // only its presentation preserves approach coordinates and timing.
            foreach (Transform child in target.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.name = OrbiterVisualName;
            visual.transform.SetParent(target, false);
            var indicator = visual.GetComponentsInChildren<MeshRenderer>(true).Single(renderer => renderer.name == "Capture Indicator");
            var blast = presentation.FlightWorld.transform.Find("Docking Collision Flash");
            if (blast == null) throw new InvalidOperationException("The existing docking collision flash is missing.");
            view.Configure(session.GetComponent<AscentMission>(), session.GetComponent<DockingMission>(), target, blast, indicator);
            indicator.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(indicator);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        }

        [MenuItem("Lunar Escape/View Updated Lunar Surface")]
        public static void Open()
        {
            BuildDockingScene.Open();
            if (SceneManager.GetActiveScene().path != BuildDockingScene.ScenePath) return;
            var layout = UnityEngine.Object.FindAnyObjectByType<LunarTerrainLayout>();
            if (layout == null) return;
            Selection.activeGameObject = layout.GroundVisual;
            EditorApplication.delayCall += () =>
            {
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                view.LookAt(new Vector3(22, 1, 0), Quaternion.Euler(23, -30, 0), 43, false);
                view.Focus();
            };
            Debug.Log("REALISTIC_ORBITER_SURFACE_SCENE_OPENED");
        }

        [MenuItem("Lunar Escape/View Realistic Orbiter Prefab")]
        public static void OpenOrbiter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportRealisticOrbiter.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Install the realistic orbiter before opening its prefab.");
            AssetDatabase.OpenAsset(prefab);
            EditorApplication.delayCall += () =>
            {
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                view.LookAt(new Vector3(0, 0, 4), Quaternion.Euler(15, -25, 0), 12, false);
                view.Focus();
            };
        }

        // Record the old scene's referenced resources before replacing its art.
        public static void Baseline()
        {
            EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            ArtResourceAudit.ReportBefore();
            Debug.Log("ORBITER_SURFACE_BASELINE_RECORDED");
        }
    }
}
