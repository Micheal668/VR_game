using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LunarEscape.Editor
{
    public static class BuildArtemisPrologue
    {
        private const string Root = "Assets/_LunarEscape";
        public const string ScenePath = Root + "/Scenes/" + ArtemisPrologue.SceneName + ".unity";

        [MenuItem("Lunar Escape/Build Artemis Opening Film")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildRepairScene.RefreshFont();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var set = new GameObject("Artemis Flight").transform;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(38f, 34f, 0f);
            sun.color = new Color(1f, .95f, .86f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.56f, .66f, .78f);
            RenderSettings.ambientEquatorColor = new Color(.31f, .34f, .39f);
            RenderSettings.ambientGroundColor = new Color(.18f, .16f, .14f);

            var pad = BuildArtemisLaunchPad.Build(set);
            var rocket = ArtemisRocketBuilder.Create(set);
            var timeline = LaunchCinematicBuilder.Build(set, rocket, pad);
            var rig = ViewRig();
            var observer = rig.transform;
            var localization = observer.gameObject.AddComponent<LocalizationService>();
            localization.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Localization/repair-texts.json"), GameLanguage.Russian);
            var display = Display(rig.Eye);
            var panel = Rect(display, "Chapter", new Vector2(1000, 114), new Vector2(0, -480));
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(.017f, .029f, .043f, .86f);
            background.raycastTarget = false;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var heading = Caption(panel, "Current chapter", 43, 21, new Color(.95f, .98f, 1));
            var hint = Caption(panel, "Controller hint", 25, -27, new Color(.72f, .88f, .92f));
            var curtain = Rect(display, "Fade to lunar orbit", display.sizeDelta, Vector2.zero).gameObject.AddComponent<Image>();
            curtain.color = Color.clear;
            curtain.raycastTarget = false;
            display.gameObject.AddComponent<HelmetHudOverlay>().Configure(
                Shader.Find("LunarEscape/Visor UI Overlay"), Shader.Find("TextMeshPro/Mobile/Distance Field Overlay"));
            observer.gameObject.AddComponent<ArtemisPrologue>().Configure(timeline, rig, localization, heading, hint, group, curtain);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new System.IO.IOException("Unable to save " + ScenePath);
            BuildArtemisLanding.BuildScene();
            PutPrologueFirst();
            if (!Application.isBatchMode) EditorSceneManager.OpenScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("ARTEMIS_PROLOGUE_BUILT");
        }

        internal static void PutPrologueFirst()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            var scenes = EditorBuildSettings.scenes.Where(entry => entry.path != ScenePath && entry.path != BuildArtemisLanding.ScenePath).ToList();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildArtemisLanding.ScenePath) != null)
                scenes.Insert(0, new EditorBuildSettingsScene(BuildArtemisLanding.ScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        internal static ArtemisViewRig ViewRig()
        {
            var observer = new GameObject("Cinematic observer").transform;
            var tracking = LunarToolBuilder.Frame(observer, "Tracking reference", Vector3.zero);
            var camera = LunarToolBuilder.Frame(tracking, "Main Camera", Vector3.zero).gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 32000f;
            camera.fieldOfView = 62f;
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            camera.GetUniversalAdditionalCameraData().allowXRRendering = true;
            camera.gameObject.AddComponent<AudioListener>();
            var pose = camera.gameObject.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(new InputAction("Eye position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            pose.rotationInput = new InputActionProperty(new InputAction("Eye rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            pose.trackingStateInput = new InputActionProperty(new InputAction("Head tracking state", InputActionType.Value, "<XRHMD>/trackingState", expectedControlType: "Integer"));

            var rig = observer.gameObject.AddComponent<ArtemisViewRig>();
            rig.Configure(tracking, camera);
            return rig;
        }

        internal static RectTransform Display(Camera camera)
        {
            var canvas = new GameObject("Flight captions", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.gameObject.layer = LayerMask.NameToLayer("UI");
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var display = (RectTransform)canvas.transform;
            display.SetParent(camera.transform, false);
            display.localPosition = Vector3.forward * 1.8f;
            display.localScale = Vector3.one * .001f;
            display.sizeDelta = new Vector2(9000, 9000);
            return display;
        }

        internal static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        internal static TMP_Text Caption(Transform parent, string name, int size, float y, Color color)
        {
            var text = Rect(parent, name, new Vector2(970, 54), new Vector2(0, y)).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset");
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}
