using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace LunarEscape.Editor
{
    // 在主场景舱内搭建实体驾驶台：启动开关条 + 左右两个手控器台，按阿波罗登月舱的站立驾驶方式布置在手边。
    // 原有面板按钮保留为键鼠/备用操作，面板仍显示全部仪表。可重复执行：旧驾驶台会先删除再重建。
    // 位置全部相对飞行操作位（Flight Seat）；真机试用后只需在 Inspector 中移动 Pilot Console 及其三个子物体。
    public static class InstallPilotConsole
    {
        private const string Root = "Assets/_LunarEscape";
        private const string ConsoleName = "Pilot Console";
        private static LocalizedPanelBuilder ui;
        private static int cockpitLayer;

        [MenuItem("Lunar Escape/Install Pilot Console (Docking Scene)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // 新增舱内文字需要进入多语言字体图集，否则中文/俄文会缺字。
            BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("PILOT_CONSOLE_INSTALLED");
        }

        public static void ApplyToCurrentScene()
        {
            cockpitLayer = InteractionLayerMask.GetMask("Cockpit");
            if (cockpitLayer == 0)
                throw new InvalidOperationException("缺少 XRI 交互层 Cockpit：Assets/XRI/Settings/Resources/InteractionLayerSettings.asset 第 2 层应命名为 Cockpit。");

            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            var flight = session.GetComponent<AscentMission>();
            var scene = session.GetComponent<FlightScenePresenter>();
            var seatLock = session.GetComponent<FlightSeatLock>();
            if (flight == null || scene == null || seatLock == null || flight.Docking == null)
                throw new InvalidOperationException("当前场景不是对接主场景（缺少飞行或对接组件）。");
            var language = UnityEngine.Object.FindAnyObjectByType<LocalizationService>(FindObjectsInactive.Include);
            ui = new LocalizedPanelBuilder(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset"), language);

            // 放在“Flight Controls”下：登舱黑屏时随面板一起出现，失败时随面板一起隐藏。
            var wrapper = scene.Panel.transform;
            var old = wrapper.Find(ConsoleName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var console = new GameObject(ConsoleName).transform;
            console.SetParent(wrapper, false);
            console.SetLocalPositionAndRotation(wrapper.InverseTransformPoint(scene.Seat.position), Quaternion.Inverse(wrapper.rotation) * scene.Seat.rotation);

            // —— 启动开关条：身前略低，倾斜朝向玩家，不挡住前方大面板 ——
            var strip = Frame(console, "Startup Strip", new Vector3(0f, 0.88f, 0.32f), Quaternion.Euler(-25f, 0f, 0f));
            Box(console, "Strip Pillar", new Vector3(0f, 0.43f, 0.35f), new Vector3(0.06f, 0.86f, 0.06f), "Station Shell");
            Box(strip, "Strip Body", new Vector3(0f, -0.015f, 0f), new Vector3(0.66f, 0.03f, 0.15f), "Station Graphite");
            var lamps = new CockpitLamp[4];
            var power = Toggle(strip, "Power Switch", new Vector3(-0.24f, 0f, 0.015f), "flight.start.power", out lamps[0]);
            var navigation = Toggle(strip, "Navigation Switch", new Vector3(-0.12f, 0f, 0.015f), "flight.start.navigation", out lamps[1]);
            var engine = Toggle(strip, "Engine Switch", new Vector3(0f, 0f, 0.015f), "flight.start.engine", out lamps[2]);
            Guarded(strip, "Ignition", new Vector3(0.12f, 0f, 0.015f), "flight.start.ignite", "Cockpit Red", out var ignitionCover, out var ignition, out lamps[3]);
            Guarded(strip, "Orbit Burn", new Vector3(0.24f, 0f, 0.015f), "cockpit.burn", "Safety Amber", out var burnCover, out var burn, out var burnLamp);

            // —— 左手台：平移手柄 + 按住制动 / 主推 ——
            var left = Frame(console, "Left Hand Pod", new Vector3(-0.33f, 0.925f, 0.12f), Quaternion.identity);
            Pod(left, "cockpit.translation");
            var translation = Stick(left, "Translation Controller", new Vector3(0f, 0f, 0.05f), StickMode.Translation);
            var brake = PushButton(left, "Brake Button", new Vector3(-0.05f, 0f, -0.06f), 0.05f, "Cockpit Red", "cockpit.brake", out _);
            var boost = PushButton(left, "Main Boost Button", new Vector3(0.05f, 0f, -0.06f), 0.04f, "Safety Amber", "cockpit.boost", out var boostLamp);

            // —— 右手台：姿态球 + 对接辅助开关 ——
            var right = Frame(console, "Right Hand Pod", new Vector3(0.33f, 0.925f, 0.12f), Quaternion.identity);
            Pod(right, "cockpit.rotation");
            var rotation = Stick(right, "Attitude Controller", new Vector3(0f, 0f, 0.05f), StickMode.Rotation);
            var assist = Toggle(right, "Assist Switch", new Vector3(0f, 0f, -0.06f), "cockpit.assist", out var assistLamp);

            var pilot = console.gameObject.AddComponent<PilotConsole>();
            pilot.ConfigureStartup(flight, power, navigation, engine, ignitionCover, ignition, lamps);
            pilot.ConfigureOrbit(burnCover, burn, burnLamp);
            pilot.ConfigureDocking(translation, rotation, brake, boost, assist, assistLamp, boostLamp);

            // 登舱后双手只能操作驾驶台这一层；手持物、传送等仍按原规则关闭。
            seatLock.ConfigureCockpitLayers(cockpitLayer);
            EditorUtility.SetDirty(seatLock);
            var feedback = flight.GetComponent<FlightFeedback>();
            if (feedback != null) { feedback.ConfigureCockpit(pilot.ThrustControls); EditorUtility.SetDirty(feedback); }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static Transform Frame(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var frame = new GameObject(name).transform;
            frame.SetParent(parent, false);
            frame.SetLocalPositionAndRotation(position, rotation);
            return frame;
        }

        // 手控器台：台面顶面位于 Frame 原点，正面贴一块说明牌。
        private static void Pod(Transform pod, string key)
        {
            Box(pod, "Pod Body", new Vector3(0f, -0.04f, 0f), new Vector3(0.22f, 0.08f, 0.26f), "Station Graphite");
            Box(pod, "Pod Pillar", new Vector3(0f, -0.5f, 0.04f), new Vector3(0.05f, 0.84f, 0.05f), "Station Shell");
            Label(pod, "Pod Label", key, new Vector3(0f, -0.04f, -0.1315f), Quaternion.identity, new Vector2(0.2f, 0.07f), 0.12f);
        }

        private static CockpitSwitch Toggle(Transform parent, string name, Vector3 position, string key, out CockpitLamp lamp)
        {
            var root = Control(parent, name, position, new Vector3(0.07f, 0.08f, 0.07f), new Vector3(0f, 0.03f, 0f));
            Box(root, "Switch Plate", new Vector3(0f, 0.004f, 0f), new Vector3(0.05f, 0.008f, 0.05f), "Station Shell");
            var pivot = Frame(root, "Lever Pivot", new Vector3(0f, 0.008f, 0f), Quaternion.Euler(-30f, 0f, 0f));
            Primitive(pivot, "Lever", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0f), new Vector3(0.01f, 0.025f, 0.01f), "Panel White");
            var knob = Primitive(pivot, "Lever Knob", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0f), Vector3.one * 0.02f, "Panel White");
            var control = root.gameObject.AddComponent<CockpitSwitch>();
            control.Configure(pivot, Vector3.zero, new Vector3(60f, 0f, 0f), false);
            Finish(control, knob);
            lamp = Lamp(parent, name + " Lamp", position + new Vector3(0f, 0f, 0.045f));
            Label(parent, name + " Label", key, position + new Vector3(0f, 0.002f, -0.045f), Quaternion.Euler(90f, 0f, 0f), new Vector2(0.115f, 0.035f), 0.1f);
            return control;
        }

        // 带保护盖的按钮：盖子绕远端铰链掀起，掀开后才能按下面的按钮。盖子的碰撞体随盖子转动，打开后不再挡住按钮。
        private static void Guarded(Transform parent, string name, Vector3 position, string key, string capMaterial,
            out CockpitSwitch cover, out CockpitSwitch button, out CockpitLamp lamp)
        {
            var group = Frame(parent, name, position, Quaternion.identity);
            Box(group, "Guard Base", new Vector3(0f, 0.004f, 0f), new Vector3(0.06f, 0.008f, 0.07f), "Station Shell");

            var buttonRoot = Control(group, name + " Button", Vector3.zero, null, Vector3.zero);
            buttonRoot.gameObject.AddComponent<SphereCollider>().center = new Vector3(0f, 0.014f, 0f);
            buttonRoot.GetComponent<SphereCollider>().radius = 0.016f;
            var cap = Primitive(buttonRoot, "Button Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.014f, 0f), new Vector3(0.03f, 0.006f, 0.03f), capMaterial);
            button = buttonRoot.gameObject.AddComponent<CockpitSwitch>();
            button.Configure(cap.transform, new Vector3(0f, -0.005f, 0f), Vector3.zero, false);
            Finish(button, cap);

            var coverRoot = Control(group, name + " Cover", Vector3.zero, null, Vector3.zero);
            var hinge = Frame(coverRoot, "Cover Hinge", new Vector3(0f, 0.026f, 0.035f), Quaternion.identity);
            var lid = Primitive(hinge, "Cover Lid", PrimitiveType.Cube, new Vector3(0f, 0f, -0.035f), new Vector3(0.05f, 0.006f, 0.07f), "Safety Amber");
            var lidCollider = lid.AddComponent<BoxCollider>();
            lidCollider.size = new Vector3(1.2f, 4f, 1.15f);
            cover = coverRoot.gameObject.AddComponent<CockpitSwitch>();
            cover.Configure(hinge, Vector3.zero, new Vector3(110f, 0f, 0f), true);
            Finish(cover, lid);

            lamp = Lamp(parent, name + " Lamp", position + new Vector3(0f, 0f, 0.05f));
            Label(parent, name + " Label", key, position + new Vector3(0f, 0.002f, -0.065f), Quaternion.Euler(90f, 0f, 0f), new Vector2(0.115f, 0.035f), 0.1f);
        }

        private static CockpitSwitch PushButton(Transform parent, string name, Vector3 position, float diameter, string material, string key, out CockpitLamp lamp)
        {
            var root = Control(parent, name, position, new Vector3(diameter + 0.02f, 0.05f, diameter + 0.02f), new Vector3(0f, 0.02f, 0f));
            Primitive(root, "Button Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.005f, 0f), new Vector3(diameter + 0.012f, 0.005f, diameter + 0.012f), "Station Shell");
            var cap = Primitive(root, "Button Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.018f, 0f), new Vector3(diameter, 0.008f, diameter), material);
            var control = root.gameObject.AddComponent<CockpitSwitch>();
            control.Configure(cap.transform, new Vector3(0f, -0.008f, 0f), Vector3.zero, false);
            Finish(control, cap);
            lamp = Lamp(parent, name + " Lamp", position + new Vector3(diameter * 0.5f + 0.012f, 0f, 0.025f));
            Label(parent, name + " Label", key, position + new Vector3(0f, 0.002f, -0.05f), Quaternion.Euler(90f, 0f, 0f), new Vector2(0.095f, 0.035f), 0.085f);
            return control;
        }

        private static CockpitStick Stick(Transform parent, string name, Vector3 position, StickMode mode)
        {
            var root = Control(parent, name, position, mode == StickMode.Translation ? new Vector3(0.1f, 0.17f, 0.1f) : new Vector3(0.11f, 0.15f, 0.11f), new Vector3(0f, 0.08f, 0f));
            Primitive(root, "Stick Boot", PrimitiveType.Cylinder, new Vector3(0f, 0.008f, 0f), new Vector3(0.07f, 0.008f, 0.07f), "Station Shell");
            CockpitStick stick;
            if (mode == StickMode.Translation)
            {
                // T 形手柄：握住整体平移。
                var handle = Frame(root, "Handle", Vector3.zero, Quaternion.identity);
                Primitive(handle, "Grip", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.032f, 0.05f, 0.032f), "Panel White");
                var bar = Primitive(handle, "T Bar", PrimitiveType.Cylinder, new Vector3(0f, 0.125f, 0f), new Vector3(0.026f, 0.045f, 0.026f), "Guide Teal");
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                stick = root.gameObject.AddComponent<CockpitStick>();
                stick.Configure(mode, handle, 0.05f, 30f);
                Finish(stick, bar);
            }
            else
            {
                // 姿态球：绕球心转动，与手腕转动一一对应。
                Primitive(root, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.018f, 0.025f, 0.018f), "Station Shell");
                var center = Frame(root, "Ball Center", new Vector3(0f, 0.08f, 0f), Quaternion.identity);
                var ball = Primitive(center, "Ball", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.07f, "Guide Teal");
                Primitive(center, "Ball Pointer", PrimitiveType.Cube, new Vector3(0f, 0f, 0.04f), new Vector3(0.012f, 0.012f, 0.03f), "Safety Amber");
                Primitive(center, "Ball Top Mark", PrimitiveType.Cube, new Vector3(0f, 0.037f, 0f), new Vector3(0.03f, 0.006f, 0.008f), "Panel White");
                stick = root.gameObject.AddComponent<CockpitStick>();
                stick.Configure(mode, center, 0.05f, 30f);
                Finish(stick, ball);
            }
            return stick;
        }

        // 操纵件根物体：保持不动并承载碰撞体，可见部件在其下移动。
        private static Transform Control(Transform parent, string name, Vector3 position, Vector3? colliderSize, Vector3 colliderCenter)
        {
            var root = Frame(parent, name, position, Quaternion.identity);
            if (colliderSize.HasValue)
            {
                var box = root.gameObject.AddComponent<BoxCollider>();
                box.size = colliderSize.Value;
                box.center = colliderCenter;
            }
            return root;
        }

        private static void Finish(CockpitControl control, GameObject highlighted)
        {
            control.interactionLayers = cockpitLayer;
            control.ConfigureHighlight(new[] { highlighted.GetComponent<Renderer>() });
            EditorUtility.SetDirty(control);
        }

        private static CockpitLamp Lamp(Transform parent, string name, Vector3 position)
        {
            var bulb = Primitive(parent, name, PrimitiveType.Sphere, position + new Vector3(0f, 0.006f, 0f), Vector3.one * 0.016f, "Cockpit Lamp");
            var lamp = bulb.AddComponent<CockpitLamp>();
            lamp.Configure(bulb.GetComponent<Renderer>());
            return lamp;
        }

        private static void Label(Transform parent, string name, string key, Vector3 position, Quaternion rotation, Vector2 size, float maxFont)
        {
            var label = ui.WorldLabel(name, key, Vector3.zero, size, maxFont, Quaternion.identity);
            label.transform.SetParent(parent, false);
            label.transform.SetLocalPositionAndRotation(position, rotation);
            // 三种语言长度差别很大，自动缩小字号，保证不溢出说明牌。
            var text = label.GetComponent<TextMeshPro>();
            text.enableAutoSizing = true;
            text.fontSizeMin = maxFont * 0.35f;
            text.fontSizeMax = maxFont;
            text.color = new Color(0.85f, 0.95f, 1f);
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, string material)
            => Primitive(parent, name, PrimitiveType.Cube, position, size, material);

        // 装饰几何体不带碰撞：射线可以穿过驾驶台指向前方面板，只有操纵件本身可被选中。
        private static GameObject Primitive(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, string material)
        {
            var o = GameObject.CreatePrimitive(shape);
            o.name = name;
            o.transform.SetParent(parent, false);
            o.transform.SetLocalPositionAndRotation(position, Quaternion.identity);
            o.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
            o.GetComponent<Renderer>().sharedMaterial = LoadMaterial(material);
            return o;
        }

        private static Material LoadMaterial(string name)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            // 驾驶台专用材质：红色按钮与可发光的指示灯。
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            if (name == "Cockpit Lamp")
            {
                material.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.08f));
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", Color.black);
            }
            else material.SetColor("_BaseColor", new Color(0.78f, 0.1f, 0.08f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
