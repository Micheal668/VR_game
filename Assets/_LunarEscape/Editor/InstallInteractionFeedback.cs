using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace LunarEscape.Editor
{
    // 把震动与音效反馈组件装进已有场景，不重建场景、不改动任何玩法组件的参数。
    // 可以重复执行：已存在的反馈组件只重新连接引用。重新生成场景（Build* 菜单）后需再执行一次。
    public static class InstallInteractionFeedback
    {
        private const string SceneFolder = "Assets/_LunarEscape/Scenes/";
        private static readonly string[] SceneNames =
        {
            "05_LunarStation_Repair", "06_LunarStation_Evacuation", "07_LunarStation_CargoBoarding",
            "08_LunarStation_Ascent", "09_LunarStation_Orbit", "10_LunarStation_Docking", "11_LunarStation_LifeSupport"
        };

        [MenuItem("Lunar Escape/Install Interaction Feedback (All Scenes)")]
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
            // 结束时停在当前主场景，方便直接点 Play。
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            Debug.Log("INTERACTION_FEEDBACK_INSTALLED");
        }

        [MenuItem("Lunar Escape/Install Interaction Feedback (Open Scene)")]
        public static void InstallOpenScene()
        {
            ApplyToCurrentScene();
            Debug.Log("INTERACTION_FEEDBACK_INSTALLED_OPEN_SCENE");
        }

        public static void ApplyToCurrentScene()
        {
            // 官方 XR Origin 预制体在左右手柄上各有一个 HapticImpulsePlayer。
            var hands = Find<HapticImpulsePlayer>();

            foreach (var button in Find<Selectable>().Where(s => s is Button))
                Ensure<ButtonPressFeedback>(button.gameObject);

            // 只给游戏自己的工具和物资加声音，不碰官方样例里的可抓取物体。
            foreach (var tool in Find<RepairTool>()) Ensure<GrabFeedback>(tool.gameObject);
            foreach (var item in Find<CargoItem>()) Ensure<GrabFeedback>(item.gameObject);

            foreach (var contact in Find<RepairContact>()) Ensure<RepairFeedback>(contact.gameObject);

            foreach (var inventory in Find<CargoInventory>())
            {
                var feedback = Ensure<CargoFeedback>(inventory.gameObject);
                feedback.Configure(inventory);
                EditorUtility.SetDirty(feedback);
            }

            foreach (var session in Find<StationMissionSession>())
            {
                var feedback = Ensure<AlarmFeedback>(session.gameObject);
                feedback.Configure(session, hands);
                EditorUtility.SetDirty(feedback);
            }

            var thrusters = Find<DockingThrustButton>().Select(b => Ensure<ButtonPressFeedback>(b.gameObject)).ToArray();
            foreach (var flight in Find<AscentMission>())
            {
                var feedback = Ensure<FlightFeedback>(flight.gameObject);
                feedback.Configure(flight, hands, thrusters);
                EditorUtility.SetDirty(feedback);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static T[] Find<T>() where T : Component
        {
            // 只取当前场景中的对象，排除预制体资源本身。
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.gameObject.scene.IsValid()).ToArray();
        }

        private static T Ensure<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();
        }
    }
}
