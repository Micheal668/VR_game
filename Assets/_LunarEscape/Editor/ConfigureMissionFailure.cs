using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape.Editor
{
    // 只升级撤离课的结算与传送适配器，保留已有房间、摆放和任务参数。
    public static class ConfigureMissionFailure
    {
        // 仅编辑器验收入口：使用同一个任务时钟快进，方便反复检查结算排版。
        [MenuItem("Lunar Escape/Preview Failure Summary (Play Mode)")]
        public static void PreviewFailure()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            if (!EditorApplication.isPlaying || session == null)
                throw new InvalidOperationException("请先运行撤离场景。");
            session.RetryMission();
            session.BeginMission();
            var config = session.Mission.Config;
            session.Advance(config.RepairWindowSeconds + config.StabilizedSeconds
                + config.BaseEvacuationSeconds + config.RepairBonusSeconds);
        }

        [MenuItem("Lunar Escape/Preview Failure Summary (Play Mode)", true)]
        private static bool CanPreviewFailure() => EditorApplication.isPlaying
            && UnityEngine.Object.FindAnyObjectByType<StationMissionSession>() != null;

        [MenuItem("Lunar Escape/Upgrade Evacuation Failure Summary")]
        public static void UpgradeScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play。");
            BuildRepairScene.RefreshFont();
            var scene = EditorSceneManager.OpenScene(BuildEvacuationScene.ScenePath);
            Install(UnityEngine.Object.FindAnyObjectByType<StationMissionSession>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_FAILURE_SUMMARY_INSTALLED");
        }

        public static void Install(StationMissionSession session)
        {
            if (session == null) throw new InvalidOperationException("场景缺少任务会话。");
            var provider = session.Player.GetComponentInChildren<TeleportationProvider>(true);
            if (provider == null) throw new InvalidOperationException("玩家缺少传送组件。");
            if (provider is not MissionTeleportationProvider)
            {
                var replacement = provider.gameObject.AddComponent<MissionTeleportationProvider>();
                EditorUtility.CopySerializedManagedFieldsOnly(provider, replacement);
                replacement.enabled = provider.enabled;
                // 保留场景中指向旧传送组件的引用，包括用户在 Inspector 手动连接的引用。
                foreach (var component in session.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true)))
                {
                    if (component == null || component == provider) continue;
                    var serialized = new SerializedObject(component);
                    var field = serialized.GetIterator();
                    bool changed = false;
                    while (field.Next(true))
                    {
                        if (field.propertyType != SerializedPropertyType.ObjectReference
                            || field.objectReferenceValue != provider) continue;
                        field.objectReferenceValue = replacement;
                        changed = true;
                    }
                    if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                UnityEngine.Object.DestroyImmediate(provider);
                EditorUtility.SetDirty(replacement);
            }
            BuildFailureSummary.Install(session);
            var failureLock = session.GetComponent<MissionFailureLock>();
            if (failureLock == null) failureLock = session.gameObject.AddComponent<MissionFailureLock>();
            failureLock.Configure(session.Mission, session.Player, session.GetComponent<MissionFailurePresenter>().Root);
            EditorUtility.SetDirty(failureLock);
        }
    }
}
