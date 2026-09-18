using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LunarEscape.Editor
{
    // 仅替换模拟器的输入适配层，保留官方输入绑定、界面和场景中的编辑内容。
    public static class ConfigureSimulatorMovement
    {
        public static void Configure(GameObject simulatorObject, GameObject rig)
        {
            var original = simulatorObject.GetComponent<XRInteractionSimulator>();
            if (original == null) throw new InvalidOperationException("场景缺少 XR 模拟器。");
            var adapter = original as CollisionAwareSimulator;
            if (adapter == null)
            {
                adapter = simulatorObject.AddComponent<CollisionAwareSimulator>();
                // 按字段名复制继承的序列化配置，新类型自己的字段保持默认值。
                EditorUtility.CopySerializedManagedFieldsOnly(original, adapter);
                adapter.enabled = original.enabled;
                UnityEngine.Object.DestroyImmediate(original);
            }

            adapter.Configure(rig.GetComponentInChildren<XRBodyTransformer>(true),
                rig.GetComponentInChildren<Camera>(true).transform);
            simulatorObject.tag = "EditorOnly";
            EditorUtility.SetDirty(adapter);
            if (PrefabUtility.IsPartOfPrefabInstance(adapter))
                PrefabUtility.RecordPrefabInstancePropertyModifications(adapter);
        }

        // 幂等迁移：只更新现有两课的模拟器，不重新生成房间或任务物体。
        public static void UpgradeScenes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play。");
            foreach (string path in new[] { BuildFoundation.ScenePath, BuildRepairScene.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var simulator = UnityEngine.Object.FindAnyObjectByType<XRInteractionSimulator>();
                var body = UnityEngine.Object.FindAnyObjectByType<XRBodyTransformer>();
                if (simulator == null || body == null)
                    throw new InvalidOperationException("场景缺少模拟器或玩家移动组件：" + path);
                Configure(simulator.gameObject, body.transform.root.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_SIMULATOR_COLLISION_CONFIGURED");
        }
    }
}
