using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape.Editor
{
    // 只在编辑器运行：生成可编辑的场景资源，不在游戏运行时临时搭房间。
    public static class BuildFoundation
    {
        public const string ScenePath = "Assets/_LunarEscape/Scenes/04_LunarStation_Foundation.unity";
        private const string Root = "Assets/_LunarEscape";
        private const string Samples = "Assets/Samples/XR Interaction Toolkit/3.6.0";
        private static Transform room;
        private static Material shell, dark, teal, orange, white;

        public static void ImportSamples()
        {
            foreach (var sample in Sample.FindByPackage("com.unity.xr.interaction.toolkit", "3.6.0"))
            {
                if (sample.displayName == "Starter Assets" || sample.displayName == "XR Interaction Simulator")
                {
                    if (!sample.isImported && !sample.Import(Sample.ImportOptions.HideImportWindow))
                        throw new InvalidOperationException("Cannot import " + sample.displayName);
                }
            }

            // 导入 TMP 自带字体资源；TMP 是 Unity 的文字显示组件。
            if (!Directory.Exists("Assets/TextMesh Pro/Resources"))
            {
                var resources = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                if (resources.Length == 0) throw new FileNotFoundException("TMP essential resources not found.");
                AssetDatabase.ImportPackage(resources[0], false);
            }
            AssetDatabase.Refresh();
            Debug.Log("LUNAR_FOUNDATION_SAMPLES_IMPORTED");
        }

        [MenuItem("Lunar Escape/Open Foundation Scene")]
        public static void OpenFoundation()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        // 自动化入口只允许初次生成，避免覆盖用户后来在场景中做的修改。
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Foundation already exists; refusing to overwrite it.");
            foreach (string folder in new[] { "Scenes", "Prefabs", "Materials", "Art", "Audio", "UI" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerSettings.productName = "Lunar Escape VR";
            PlayerSettings.companyName = "Lunar Escape Project";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;

            shell = MakeMaterial("Station Shell", new Color(0.36f, 0.44f, 0.49f));
            dark = MakeMaterial("Station Graphite", new Color(0.055f, 0.085f, 0.11f));
            teal = MakeMaterial("Guide Teal", new Color(0.10f, 0.80f, 0.75f));
            orange = MakeMaterial("Safety Amber", new Color(1f, 0.53f, 0.12f));
            white = MakeMaterial("Panel White", new Color(0.77f, 0.85f, 0.88f));
            room = new GameObject("Station Room - editable blockout").transform;
            CreateRoom();
            CreateLighting();

            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            // 显式放置场景 UI 系统，避免 XRI 自动创建跨场景保留的系统。
            new GameObject("XR UI Event System", typeof(EventSystem), typeof(XRUIInputModule));
            // XR Origin 是玩家根对象：里面包括头部摄像机、左右手和移动组件。
            // Prefab（预制体）是可以重复放进场景的一组预先配置好的对象。
            var rig = InstantiatePrefab(Samples + "/Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
            rig.transform.position = new Vector3(0f, 0f, -1.8f);
            ConfigureComfort(rig);
            var camera = rig.GetComponentInChildren<Camera>();
            // 最近可见距离设为 5 厘米，避免拿近的工具太早被裁掉。
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.06f);

            // EditorOnly 标签让模拟器只存在于编辑器，打包游戏时会自动剔除。
            var simulator = InstantiatePrefab(Samples + "/XR Interaction Simulator/XR Interaction Simulator.prefab");
            simulator.name = "XR Interaction Simulator - Editor Only";
            simulator.tag = "EditorOnly";
            ConfigureSimulatorMovement.Configure(simulator, rig);

            CreateWorkbench();
            var status = Label("Practice Status", "PICK UP THE MAINTENANCE TOOL\nThen release it onto the workbench.",
                new Vector3(0f, 1.95f, 3.83f), new Vector2(5.4f, 0.8f), 10f, Color.white);
            CreateTool(status);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_FOUNDATION_CREATED " + ScenePath);
        }

        private static void CreateRoom()
        {
            // 约定 1 Unity 单位等于 1 米；地板上表面是 y = 0。
            var floor = Box("Floor - teleport surface", new Vector3(0, -0.1f, 0), new Vector3(8, 0.2f, 8), dark);
            // 传送移动让玩家在明确位置之间移动，避免强制拖动头部视角。
            var teleport = floor.AddComponent<TeleportationArea>();
            // 官方玩家预制体把第 31 个交互层留给传送，地板必须与它匹配。
            teleport.interactionLayers = 1 << 31;
            Box("Wall Rear", new Vector3(0, 1.65f, -4), new Vector3(8, 3.3f, 0.2f), shell);
            Box("Wall Systems", new Vector3(0, 1.65f, 4), new Vector3(8, 3.3f, 0.2f), shell);
            Box("Wall Left", new Vector3(-4, 1.65f, 0), new Vector3(0.2f, 3.3f, 8), shell);
            Box("Wall Right", new Vector3(4, 1.65f, 0), new Vector3(0.2f, 3.3f, 8), shell);
            Box("Ceiling", new Vector3(0, 3.4f, 0), new Vector3(8, 0.2f, 8), shell);
            for (int z = -3; z <= 3; z += 2)
            {
                Box("Ceiling Rib", new Vector3(0, 3.2f, z), new Vector3(8, 0.13f, 0.12f), dark);
                Box("Floor Seam", new Vector3(0, 0.005f, z), new Vector3(7.6f, 0.005f, 0.025f), shell, false);
                Box("Left Rib", new Vector3(-3.85f, 1.6f, z), new Vector3(0.12f, 3.2f, 0.12f), dark);
                Box("Right Rib", new Vector3(3.85f, 1.6f, z), new Vector3(0.12f, 3.2f, 0.12f), dark);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Floor Guidance", new Vector3(side * 2.6f, 0.012f, 0), new Vector3(0.05f, 0.01f, 7.6f), teal, false);
                Box("Ceiling Light Strip", new Vector3(side * 2.4f, 3.27f, 0), new Vector3(0.18f, 0.04f, 7f), white, false);
            }
            Box("Systems Display", new Vector3(0, 2.2f, 3.88f), new Vector3(6, 1.5f, 0.06f), dark);
            Label("Station Header", "LUNAR STATION  /  SYSTEMS BAY", new Vector3(0, 2.69f, 3.83f), new Vector2(5.6f, 0.4f), 14f, new Color(0.25f, 0.94f, 0.87f));
            Label("Station Footer", "MAINTENANCE 01     /     TOOL CHECK", new Vector3(0, 1.52f, 3.83f), new Vector2(5.6f, 0.2f), 7f, new Color(0.7f, 0.8f, 0.85f));
            for (int i = 0; i < 3; i++)
                Box("Storage Panel", new Vector3(-3.82f, 1.1f, -1.5f + i * 1.5f), new Vector3(0.18f, 1.8f, 1.15f), dark);
            Box("Reserved Airlock", new Vector3(3.82f, 1.25f, 1.7f), new Vector3(0.18f, 2.5f, 1.5f), dark);
        }

        private static void CreateWorkbench()
        {
            // 桌面上表面约 1 米高，方便站立时观察和拿取工具。
            Box("Workbench Surface", new Vector3(0, 0.93f, 0.55f), new Vector3(2.2f, 0.14f, 0.85f), white);
            Box("Workbench Base", new Vector3(0, 0.43f, 0.55f), new Vector3(1.7f, 0.86f, 0.6f), dark);
            Box("Workbench Safety Edge", new Vector3(0, 0.94f, 0.11f), new Vector3(2.2f, 0.05f, 0.025f), orange, false);
            Box("Tool Placement Mat", new Vector3(0, 1.005f, 0.55f), new Vector3(0.8f, 0.009f, 0.5f), dark, false);
        }

        private static void ConfigureComfort(GameObject rig)
        {
            // 默认使用传送和分段转向。通过序列化字段配置官方样例，保留其原始代码。
            foreach (var component in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component.GetType().Name != "ControllerInputActionManager") continue;
                var settings = new SerializedObject(component);
                settings.FindProperty("m_SmoothMotionEnabled").boolValue = false;
                settings.FindProperty("m_SmoothTurnEnabled").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            // 本次房间练习不需要跳跃、攀爬；先关闭样例附带的这些移动组件。
            foreach (var child in rig.GetComponentsInChildren<Transform>(true))
                if (child.name == "Jump" || child.name == "Climb" || child.name == "Climb Teleport")
                    child.gameObject.SetActive(false);
        }

        private static void CreateTool(TMP_Text status)
        {
            // 两个简单盒子组成工具模型；它们的碰撞体共同归属于父对象的刚体。
            var tool = new GameObject("Maintenance Tool");
            tool.transform.position = new Vector3(0, 1.09f, 0.45f);
            var handle = Box("Grip", Vector3.zero, new Vector3(0.065f, 0.055f, 0.3f), teal);
            handle.transform.SetParent(tool.transform, false);
            var head = Box("Tool Head", new Vector3(0, 0, 0.19f), new Vector3(0.16f, 0.06f, 0.12f), white);
            head.transform.SetParent(tool.transform, false);
            var body = tool.AddComponent<Rigidbody>();
            // 质量是练习用暂定值；连续碰撞检测降低快速移动时穿过桌面的概率。
            body.mass = 0.4f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = tool.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            // 动态抓取点保留玩家实际抓到的位置，避免抓起时工具突然跳到中心。
            grab.useDynamicAttach = true;
            grab.interactionLayers = 1;
            // 放手继承实际手部速度，不额外放大；下落快慢由项目的月球重力决定。
            grab.throwVelocityScale = 1f;
            // 工具预制体只保存工具本身；场景文字引用留在场景实例上。
            tool.AddComponent<ReturnFallenTool>();
            tool.AddComponent<ToolPractice>();
            var prefabPath = Root + "/Prefabs/MaintenanceTool.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(tool, prefabPath, InteractionMode.AutomatedAction);
            tool.GetComponent<ToolPractice>().Configure(status);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tool.GetComponent<ToolPractice>());
        }

        private static void CreateLighting()
        {
            // 原型阶段用环境光和少量灯光保证可见，最终美术阶段再调整光照。
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.64f, 0.72f);
            RenderSettings.ambientIntensity = 1f;
            var main = new GameObject("Main Light").AddComponent<Light>();
            main.type = LightType.Directional;
            main.intensity = 1.4f;
            main.color = new Color(0.85f, 0.93f, 1f);
            main.shadows = LightShadows.Soft;
            main.transform.rotation = Quaternion.Euler(42, -30, 0);
            var fill = new GameObject("Workbench Fill").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 7;
            fill.intensity = 4;
            fill.color = new Color(0.72f, 0.92f, 1f);
            fill.transform.position = new Vector3(0, 2.8f, -0.3f);
        }

        private static Material MakeMaterial(string name, Color color)
        {
            // 使用 URP 配套材质，避免渲染管线与材质不匹配出现粉色物体。
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.22f);
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat");
            return material;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool collide = true)
        {
            // 共用的小工具方法：创建一个有尺寸、材质、可选碰撞体的盒子。
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(room, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collide) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static TextMeshPro Label(string name, string text, Vector3 position, Vector2 size, float fontSize, Color color)
        {
            // 文字位于真实的三维场景中，固定在墙上，不随玩家头部强制移动。
            var label = new GameObject(name).AddComponent<TextMeshPro>();
            label.transform.SetParent(room, false);
            label.transform.position = position;
            label.rectTransform.sizeDelta = size;
            label.text = text;
            // 三维文字采用世界尺寸；缩小字号，避免套用屏幕 UI 的字号导致占满整面墙。
            label.fontSize = fontSize * 0.1f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (label.font == null) throw new InvalidOperationException("Import TMP essential resources first.");
            return label;
        }

        private static GameObject InstantiatePrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new FileNotFoundException("Sample prefab missing: " + path);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
    }
}
