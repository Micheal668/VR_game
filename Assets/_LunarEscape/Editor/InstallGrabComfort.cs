using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Editor
{
    // 抓取手感：官方 Near-Far 交互器默认“远距离抓取后物体停在射线命中处”（Far），
    // 玩家看到的是东西悬在半空、跟着射线晃，而不是落到手里。改为 Near：远距离抓取时物体被吸到手上。
    // 只改交互器的远抓模式，不改各物体的移动方式与碰撞规则。可重复执行；重建场景后需再执行一次。
    public static class InstallGrabComfort
    {
        private const string SceneFolder = "Assets/_LunarEscape/Scenes/";
        private static readonly string[] SceneNames =
        {
            "05_LunarStation_Repair", "06_LunarStation_Evacuation", "07_LunarStation_CargoBoarding",
            "08_LunarStation_Ascent", "09_LunarStation_Orbit", "10_LunarStation_Docking", "11_LunarStation_LifeSupport"
        };

        [MenuItem("Lunar Escape/Install Grab Comfort: Items Snap To Hand (All Scenes)")]
        public static void InstallAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var name in SceneNames)
            {
                string path = SceneFolder + name + ".unity";
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path);
                ApplyToCurrentScene();
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            Debug.Log("GRAB_COMFORT_INSTALLED");
        }

        public static void ApplyToCurrentScene()
        {
            foreach (var interactor in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(i => i.gameObject.scene.IsValid()))
            {
                interactor.farAttachMode = InteractorFarAttachMode.Near;
                EditorUtility.SetDirty(interactor);
                if (PrefabUtility.IsPartOfPrefabInstance(interactor)) PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
