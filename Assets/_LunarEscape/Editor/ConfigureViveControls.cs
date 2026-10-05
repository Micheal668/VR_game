using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace LunarEscape.Editor
{
    // 只迁移主场景，并保留官方样例输入文件，便于后续升级。
    public static class ConfigureViveControls
    {
        public const string InputPath = "Assets/_LunarEscape/Input/Lunar Vive Controls.inputactions";
        private const string SampleInput = "Assets/Samples/XR Interaction Toolkit/3.6.0/Starter Assets/XRI Default Input Actions.inputactions";

        [MenuItem("Lunar Escape/Configure Vive Press To Walk")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play。");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(InputPath));
            if (!File.Exists(InputPath)) File.Copy(SampleInput, InputPath);
            var input = InputActionAsset.FromJson(File.ReadAllText(InputPath));
            input.name = "Lunar Vive Controls";
            foreach (string side in new[] { "LeftHand", "RightHand" })
            {
                string map = side == "LeftHand" ? "XRI Left Locomotion" : "XRI Right Locomotion";
                var action = input.FindAction(map + "/Move", true);
                // 按压和有效追踪都成立才读取圆盘；触摸后再按下也应立即起步。
                while (action.bindings.Count > 0) action.ChangeBinding(0).Erase();
                action.AddCompositeBinding("TwoModifiers(modifiersOrder=2)",
                        processors: "scaleVector2(x=0,y=1),stickDeadzone(min=0.25,max=1)")
                    .With("Modifier1", "<ViveController>{" + side + "}/trackpadClicked")
                    .With("Modifier2", "<ViveController>{" + side + "}/isTracked")
                    .With("Binding", "<ViveController>{" + side + "}/trackpad");
            }
            File.WriteAllText(InputPath, input.ToJson());
            UnityEngine.Object.DestroyImmediate(input);
            AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            var references = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>()
                .GroupBy(r => r.action.id).ToDictionary(g => g.Key, g => g.First());

            // 同步替换整套引用：管理器启用的输入必须与姿态、抓取和移动读取的输入一致。
            foreach (var component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = property.objectReferenceValue;
                    if (value == null || AssetDatabase.GetAssetPath(value) != SampleInput) continue;
                    if (value is InputActionReference reference) property.objectReferenceValue = references[reference.action.id];
                    else if (value is InputActionAsset) property.objectReferenceValue = asset;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var manager in UnityEngine.Object.FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(manager);
                serialized.FindProperty("m_SmoothMotionEnabled").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var move in UnityEngine.Object.FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                move.gameObject.SetActive(true); move.enabled = true;
                move.moveSpeed = 1.4f; move.enableStrafe = false; move.enableFly = false;
                // 释放圆盘即停止水平移动，月面腾空时也不残留输入惯性。
                move.inAirControlModifier = 1;
                Save(move); Save(move.gameObject);
            }
            foreach (var turn in UnityEngine.Object.FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { turn.enabled = false; Save(turn); }
            foreach (var turn in UnityEngine.Object.FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { turn.enabled = false; Save(turn); }
            foreach (var teleport in UnityEngine.Object.FindObjectsByType<TeleportationProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { teleport.enabled = false; Save(teleport); }
            foreach (var simulator in UnityEngine.Object.FindObjectsByType<XRInteractionSimulator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { simulator.gameObject.SetActive(false); Save(simulator.gameObject); }

            // 固定已在真机验证有效的 Windows 渲染与手柄配置。
            var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (xr != null)
            {
                xr.renderMode = OpenXRSettings.RenderMode.MultiPass;
                var vive = xr.GetFeature<HTCViveControllerProfile>();
                if (vive != null) { vive.enabled = true; EditorUtility.SetDirty(vive); }
                EditorUtility.SetDirty(xr);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_VIVE_PRESS_TO_WALK_CONFIGURED");
        }

        private static void Save(UnityEngine.Object value)
        {
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
    }
}
