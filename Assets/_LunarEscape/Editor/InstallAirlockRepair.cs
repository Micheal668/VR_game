using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Editor
{
    // 生命保障主场景（11）的“气闸 A 抢修”，取代“扳手保持在门锁上”的单一维修：
    //   1. 左墙配电盒：掀盖、拔出烧坏的保险丝、从后墙备件架取来备用保险丝插入；
    //   2. 舱门右侧平衡阀：转动手轮，压力表进入绿区（拧过头会泄压）；
    //   3. 舱门锁销：用工作台扳手拧松三颗高低不同的螺栓；
    //   最后拉下原门锁位置的手动开门拉杆并保持：气闸完成循环、解除门锁；开门仍用气闸压力屏（没穿航天服开门致命）。
    // 须在 Install Life Support and Cockpit 之后执行；可重复执行，旧的抢修设备会先删除再重建。
    public static class InstallAirlockRepair
    {
        private const string Root = "Assets/_LunarEscape";
        private const string AirlockName = "Airlock A Repair";
        private static LocalizedPanelBuilder ui;

        // 试玩捷径：运行中直接解除气闸，便于只测试撤离、飞行和对接。
        [MenuItem("Lunar Escape/Skip Airlock Repair (Play Mode)")]
        public static void SkipInPlayMode() { var airlock = UnityEngine.Object.FindAnyObjectByType<AirlockRepair>(); if (airlock != null) airlock.SkipRepair(); }
        [MenuItem("Lunar Escape/Skip Airlock Repair (Play Mode)", true)]
        private static bool CanSkip() => Application.isPlaying && UnityEngine.Object.FindAnyObjectByType<AirlockRepair>() != null;

        [MenuItem("Lunar Escape/Install Airlock Repair (Life Support Scene)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("AIRLOCK_REPAIR_INSTALLED");
        }

        public static void ApplyToCurrentScene()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            var mission = session.Mission;
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            var language = UnityEngine.Object.FindAnyObjectByType<LocalizationService>(FindObjectsInactive.Include);
            ui = new LocalizedPanelBuilder(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset"), language);

            var old = ground.Find(AirlockName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var door = Find(ground, "Evacuation Door");
            var oldBolts = door.Find("Airlock Latch Bolts");
            if (oldBolts != null) UnityEngine.Object.DestroyImmediate(oldBolts.gameObject);
            MeasureWalls(ground);
            var root = new GameObject(AirlockName).transform;
            root.SetParent(ground, false);


            var tools = UnityEngine.Object.FindObjectsByType<RepairTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var airlock = root.gameObject.AddComponent<AirlockRepair>();
            var fuse = BuildFuseBox(root, airlock);
            var valve = BuildValve(root, airlock);
            var latches = BuildLatches(root, door, tools, airlock);
            var lever = BuildLever(root, out var leverLamp);
            airlock.Configure(mission, new AirlockFault[] { fuse, valve, latches }, lever, leverLamp);
            EditorUtility.SetDirty(airlock);

            // 接入生命保障：气闸循环完成即“门已修好”，之后仍由气闸压力屏的开门键开门；原门锁工具维修整体移除。
            var life = session.GetComponent<LifeSupportMission>();
            if (life == null) throw new InvalidOperationException("当前场景没有生命保障：请先执行 Lunar Escape → Install Life Support and Cockpit。");
            RetireToolLatch(ground);
            life.ConfigureAirlockRepair(airlock);
            EditorUtility.SetDirty(life);
            EditorUtility.SetDirty(airlock);

            ConfigurePanels(life, ground, airlock, fuse, valve, latches, root);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // —— 故障一：配电盒（左墙空白墙面）+ 后墙备件架 ——
        private static FuseFault BuildFuseBox(Transform root, AirlockRepair airlock)
        {
            // 局部 +Z 指向舱内；原点在墙面上。
            var box = Frame(root, "Airlock Power Panel", new Vector3(wallLeft, 1.6f, 2.9f), Quaternion.Euler(0f, 90f, 0f));
            Box(box, "Panel Back", new Vector3(0f, 0f, 0.01f), new Vector3(0.38f, 0.48f, 0.02f), "Panel White");
            foreach (float x in new[] { -0.185f, 0.185f })
                Box(box, "Panel Side", new Vector3(x, 0f, 0.055f), new Vector3(0.012f, 0.48f, 0.09f), "Station Shell");
            foreach (float y in new[] { -0.235f, 0.235f })
                Box(box, "Panel Edge", new Vector3(0f, y, 0.055f), new Vector3(0.38f, 0.012f, 0.09f), "Station Shell");
            // 保险丝夹座与几根完好的“邻居”保险丝，让盒内看起来是一排真实回路。
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                Box(box, "Fuse Clip", new Vector3(i * 0.06f, 0.06f, 0.03f), new Vector3(0.035f, 0.012f, 0.03f), "Panel White");
                Box(box, "Fuse Clip", new Vector3(i * 0.06f, -0.06f, 0.03f), new Vector3(0.035f, 0.012f, 0.03f), "Panel White");
                Primitive(box, "Healthy Fuse", PrimitiveType.Cube, new Vector3(i * 0.06f, 0f, 0.045f), new Vector3(0.026f, 0.1f, 0.026f), "Panel White");
            }
            Box(box, "F3 Clip", new Vector3(0f, 0.06f, 0.03f), new Vector3(0.035f, 0.012f, 0.03f), "Safety Amber");
            Box(box, "F3 Clip", new Vector3(0f, -0.06f, 0.03f), new Vector3(0.035f, 0.012f, 0.03f), "Safety Amber");

            var socketObject = Frame(box, "F3 Fuse Socket", new Vector3(0f, 0f, 0.05f), Quaternion.identity);
            var trigger = socketObject.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.05f;
            var attach = Frame(socketObject, "F3 Attach", Vector3.zero, Quaternion.identity);
            var socket = socketObject.gameObject.AddComponent<XRSocketInteractor>();
            socket.attachTransform = attach;

            // 盖子：左侧竖直铰链，向舱内掀开。
            var coverRoot = Frame(box, "Panel Cover", Vector3.zero, Quaternion.identity);
            var hinge = Frame(coverRoot, "Cover Hinge", new Vector3(-0.19f, 0f, 0.105f), Quaternion.identity);
            var lid = Primitive(hinge, "Cover Lid", PrimitiveType.Cube, new Vector3(0.19f, 0f, 0f), new Vector3(0.38f, 0.48f, 0.012f), "Safety Amber");
            lid.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 4f);
            Primitive(hinge, "Cover Handle", PrimitiveType.Cube, new Vector3(0.35f, 0f, 0.02f), new Vector3(0.025f, 0.12f, 0.03f), "Station Graphite");
            var hazard = Primitive(hinge, "Cover Hazard Stripe", PrimitiveType.Cube, new Vector3(0.19f, 0.17f, 0.007f), new Vector3(0.3f, 0.04f, 0.002f), "Safety Amber");
            hazard.name = "Cover Hazard Stripe";
            var cover = coverRoot.gameObject.AddComponent<CockpitSwitch>();
            cover.Configure(hinge, Vector3.zero, new Vector3(0f, -110f, 0f), true);
            cover.ConfigureHighlight(new[] { lid.GetComponent<Renderer>() });
            Label(hinge, "Cover Label", "airlock.box.label", new Vector3(0.19f, -0.05f, 0.0075f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.32f, 0.14f), 0.24f);
            var lamp = Lamp(box, "Power Fault Lamp", new Vector3(0.15f, 0.27f, 0.03f));

            var burnt = BuildFuse(root, "Burnt Fuse F3", false, attach.position, attach.rotation);
            var spareShelf = Frame(root, "Spare Parts Bracket", new Vector3(-2.2f, 1.15f, wallRear), Quaternion.identity);
            Box(spareShelf, "Bracket Shelf", new Vector3(0f, 0f, 0.06f), new Vector3(0.3f, 0.02f, 0.12f), "Station Shell", true);
            Box(spareShelf, "Bracket Back", new Vector3(0f, 0.12f, 0.005f), new Vector3(0.3f, 0.26f, 0.01f), "Station Graphite");
            Box(spareShelf, "Bracket Lip", new Vector3(0f, 0.02f, 0.115f), new Vector3(0.3f, 0.025f, 0.01f), "Safety Amber", true);
            Label(spareShelf, "Spare Label", "airlock.spare.label", new Vector3(0f, 0.19f, 0.012f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.28f, 0.1f), 0.22f);
            var spare = BuildFuse(root, "Spare Fuse F3", true, spareShelf.TransformPoint(new Vector3(0f, 0.076f, 0.06f)), spareShelf.rotation);

            var spareLamp = Lamp(spareShelf, "Spare Lamp", new Vector3(0.12f, 0.2f, 0.02f));
            var fault = root.gameObject.AddComponent<FuseFault>();
            fault.ConfigureOwner(airlock);
            fault.Configure(cover, socket, burnt, spare, lamp);
            fault.ConfigureSpareLamp(spareLamp);
            burnt.Configure(false, fault);
            spare.Configure(true, fault);
            // 开局烧坏的保险丝就插在座里。
            var socketSerialized = new SerializedObject(socket);
            socketSerialized.FindProperty("m_StartingSelectedInteractable").objectReferenceValue = burnt.Grab;
            socketSerialized.ApplyModifiedPropertiesWithoutUndo();
            return fault;
        }

        private static AirlockFuse BuildFuse(Transform parent, string name, bool good, Vector3 position, Quaternion rotation)
        {
            var fuse = new GameObject(name);
            fuse.transform.SetParent(parent, true);
            fuse.transform.SetPositionAndRotation(position, rotation);
            // 方形熔断管：平放不会滚落，竖插进夹座。
            Primitive(fuse.transform, "Fuse Body", PrimitiveType.Cube, Vector3.zero, new Vector3(0.036f, 0.1f, 0.036f), good ? "Panel White" : "Station Graphite");
            foreach (float y in new[] { -0.046f, 0.046f })
                Primitive(fuse.transform, "Fuse Cap", PrimitiveType.Cube, new Vector3(0f, y * 1.25f, 0f), new Vector3(0.04f, 0.016f, 0.04f), "Safety Amber");
            Primitive(fuse.transform, good ? "Fuse Band" : "Scorch Mark", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.038f, 0.03f, 0.038f), good ? "Guide Teal" : "Cockpit Red");
            var collider = fuse.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.04f, 0.13f, 0.04f);
            var body = fuse.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var grab = fuse.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            fuse.AddComponent<GrabFeedback>();
            return fuse.AddComponent<AirlockFuse>();
        }

        // —— 故障二：右后墙平衡阀 ——
        private static ValveFault BuildValve(Transform root, AirlockRepair airlock)
        {
            var mount = Frame(root, "Pressure Equalizer", new Vector3(wallRight, 1.3f, 2.95f), Quaternion.Euler(0f, -90f, 0f));
            var pipe = Primitive(mount, "Equalizer Pipe", PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0.07f), new Vector3(0.09f, 1.65f, 0.09f), "Station Shell");
            pipe.name = "Equalizer Pipe";
            Primitive(mount, "Valve Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0.1f), new Vector3(0.14f, 0.16f, 0.1f), "Station Graphite");
            var stem = Primitive(mount, "Valve Stem", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.17f), new Vector3(0.025f, 0.04f, 0.025f), "Station Shell");
            stem.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var control = Frame(mount, "Equalizer Handwheel", new Vector3(0f, 0f, 0.21f), Quaternion.identity);
            var collider = control.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.36f, 0.36f, 0.07f);
            var wheel = Frame(control, "Handwheel", Vector3.zero, Quaternion.identity);
            var rimRenderers = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                var segment = Primitive(wheel, "Rim", PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.15f, Mathf.Sin(a) * 0.15f, 0f), new Vector3(0.024f, 0.031f, 0.024f), "Cockpit Red");
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                rimRenderers.Add(segment.GetComponent<Renderer>());
            }
            for (int i = 0; i < 3; i++)
            {
                var spoke = Primitive(wheel, "Spoke", PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.018f, 0.014f), "Station Shell");
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
            }
            var hub = Primitive(wheel, "Hub", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.05f, 0.02f, 0.05f), "Station Graphite");
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive(wheel, "Turn Marker", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0.02f), new Vector3(0.03f, 0.03f, 0.012f), "Panel White");
            var handwheel = control.gameObject.AddComponent<ValveWheel>();
            handwheel.Configure(wheel, 1080f);
            handwheel.ConfigureHighlight(rimRenderers.ToArray());

            // 压力表：0（全关）在左下，指针顺时针扫过 240°；绿区与红区按手轮角度画出刻度。
            var gauge = Frame(mount, "Differential Gauge", new Vector3(0f, 0.36f, 0.16f), Quaternion.identity);
            var face = Primitive(gauge, "Gauge Face", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.2f, 0.01f, 0.2f), "Panel White");
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var bezel = Primitive(gauge, "Gauge Bezel", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.006f), new Vector3(0.22f, 0.008f, 0.22f), "Station Graphite");
            bezel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var green = new Vector2(630f, 810f);
            for (float angle = 0f; angle <= 1080f; angle += 45f)
            {
                float phi = (210f - 240f * angle / 1080f) * Mathf.Deg2Rad;
                string material = angle >= green.x && angle <= green.y ? "Guide Teal" : angle > green.y ? "Cockpit Red" : "Station Graphite";
                var tick = Primitive(gauge, "Gauge Tick", PrimitiveType.Cube, new Vector3(Mathf.Cos(phi) * 0.08f, Mathf.Sin(phi) * 0.08f, 0.007f), new Vector3(0.022f, 0.008f, 0.004f), material);
                tick.transform.localRotation = Quaternion.Euler(0f, 0f, phi * Mathf.Rad2Deg);
            }
            for (float angle = green.x; angle <= green.y; angle += 22.5f)
            {
                float phi = (210f - 240f * angle / 1080f) * Mathf.Deg2Rad;
                Primitive(gauge, "Green Band", PrimitiveType.Cube, new Vector3(Mathf.Cos(phi) * 0.068f, Mathf.Sin(phi) * 0.068f, 0.007f), new Vector3(0.016f, 0.016f, 0.003f), "Guide Teal");
            }
            var needle = Frame(gauge, "Gauge Needle", new Vector3(0f, 0f, 0.01f), Quaternion.Euler(0f, 0f, 120f));
            Primitive(needle, "Needle", PrimitiveType.Cube, new Vector3(0f, 0.035f, 0f), new Vector3(0.006f, 0.075f, 0.003f), "Cockpit Red");
            Primitive(needle, "Needle Pin", PrimitiveType.Cube, Vector3.zero, new Vector3(0.012f, 0.012f, 0.005f), "Station Graphite");
            Label(mount, "Equalizer Label", "airlock.valve.label", new Vector3(0f, 0.56f, 0.12f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.3f, 0.1f), 0.24f);
            var lamp = Lamp(mount, "Pressure Lamp", new Vector3(0.14f, 0.36f, 0.13f));

            var fault = root.gameObject.AddComponent<ValveFault>();
            fault.ConfigureOwner(airlock);
            fault.Configure(handwheel, needle, lamp);
            return fault;
        }

        // —— 故障三：舱门内侧三颗锁销螺栓（与门上原有锁扣对齐，门打开时随门一起隐藏）——
        private static LatchFault BuildLatches(Transform root, Transform door, RepairTool[] tools, AirlockRepair airlock)
        {
            var holder = new GameObject("Airlock Latch Bolts").transform;
            holder.SetParent(door, false);
            holder.localScale = new Vector3(1f / door.lossyScale.x, 1f / door.lossyScale.y, 1f / door.lossyScale.z);
            var positions = new[] { new Vector3(3.84f, 1.51f, 2.33f), new Vector3(3.84f, 0.97f, 1.07f), new Vector3(3.84f, 0.43f, 2.33f) };
            var bolts = new LatchBolt[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var bolt = new GameObject("Latch Bolt " + (i + 1)).transform;
                bolt.SetParent(holder, true);
                bolt.SetPositionAndRotation(positions[i], Quaternion.Euler(0f, -90f, 0f));
                bolt.localScale = Vector3.one;
                var ring = Primitive(bolt, "Bolt Ring", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.005f), new Vector3(0.075f, 0.006f, 0.075f), "Panel White");
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var head = Frame(bolt, "Bolt Head", Vector3.zero, Quaternion.identity);
                var hex = Primitive(head, "Hex Head", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.012f), new Vector3(0.045f, 0.014f, 0.045f), "Station Shell");
                hex.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Primitive(head, "Head Slot", PrimitiveType.Cube, new Vector3(0f, 0f, 0.027f), new Vector3(0.036f, 0.007f, 0.004f), "Station Graphite");
                bolts[i] = bolt.gameObject.AddComponent<LatchBolt>();
                bolts[i].Configure(tools, head, ring.GetComponent<Renderer>());
            }
            var fault = root.gameObject.AddComponent<LatchFault>();
            fault.ConfigureOwner(airlock);
            fault.Configure(bolts);
            return fault;
        }

        // —— 手动开门拉杆（原门锁位置，气闸压力屏下方）与诊断屏（压力屏上方）——
        private static ReleaseLever BuildLever(Transform root, out CockpitLamp lamp)
        {
            var mount = Frame(root, "Manual Release", new Vector3(3.42f, 0.92f, 0.24f), Quaternion.Euler(0f, -90f, 0f));
            Box(mount, "Release Plate", new Vector3(0f, 0.08f, 0.01f), new Vector3(0.14f, 0.36f, 0.02f), "Safety Amber");
            Box(mount, "Release Pedestal", new Vector3(0f, -0.47f, -0.01f), new Vector3(0.08f, 0.9f, 0.06f), "Station Graphite");
            Box(mount, "Release Pivot", new Vector3(0f, 0f, 0.035f), new Vector3(0.08f, 0.05f, 0.05f), "Station Graphite");
            var control = Frame(mount, "Release Lever", new Vector3(0f, 0f, 0.06f), Quaternion.identity);
            var arm = Frame(control, "Lever Arm", Vector3.zero, Quaternion.identity);
            Primitive(arm, "Arm", PrimitiveType.Cube, new Vector3(0f, 0.13f, 0f), new Vector3(0.025f, 0.26f, 0.025f), "Station Shell");
            var grip = Primitive(arm, "T Grip", PrimitiveType.Cylinder, new Vector3(0f, 0.27f, 0f), new Vector3(0.035f, 0.07f, 0.035f), "Cockpit Red");
            grip.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var gripCollider = new GameObject("Lever Grip Collider");
            gripCollider.transform.SetParent(arm, false);
            var box = gripCollider.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.18f, 0f);
            box.size = new Vector3(0.16f, 0.22f, 0.08f);
            var lever = control.gameObject.AddComponent<ReleaseLever>();
            lever.Configure(arm, 110f);
            lever.ConfigureHighlight(new[] { grip.GetComponent<Renderer>() });
            lamp = Lamp(mount, "Release Lamp", new Vector3(0.09f, 0.22f, 0.025f));
            Label(mount, "Release Label", "airlock.lever.label", new Vector3(0f, -0.17f, 0.022f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.24f, 0.09f), 0.2f);
            return lever;
        }

        private static void ConfigurePanels(LifeSupportMission life, Transform ground, AirlockRepair airlock,
            FuseFault fuse, ValveFault valve, LatchFault latches, Transform root)
        {
            // 诊断屏挂在气闸压力屏上方：标题 + 航天服 + 三处故障 + 拉杆。
            var panel = ui.Panel("Airlock Diagnostics", new Vector3(3.46f, 2.26f, 0.24f), new Vector2(980, 620), 0.00062f, Quaternion.Euler(0f, 90f, 0f));
            panel.SetParent(root, true);
            ui.Label(panel, "Diagnostics Title", "airlock.panel.title", new Vector2(0, 255), new Vector2(920, 70), 46, new Color(0.4f, 0.95f, 0.87f));
            var rows = new LocalizedText[5];
            string[] keys = { "airlock.row.suit.off", "airlock.row.power.bad", "airlock.row.pressure.bad", "airlock.row.latch.bad", "airlock.row.lever.locked" };
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = ui.Label(panel, "Diagnostics Row " + i, keys[i], new Vector2(0, 160 - i * 95), new Vector2(940, 88), 38, Color.white);
                AutoSize(rows[i], 38);
            }
            var view = root.gameObject.AddComponent<AirlockPresenter>();
            view.Configure(airlock, fuse, valve, latches, rows[1], rows[2], rows[3], rows[4], null, null, null);
            view.ConfigureLifeSupport(life, rows[0]);
            EditorUtility.SetDirty(view);
        }

        // 移除原“扳手保持在门锁上”的门锁与压力屏上的门锁进度；压力屏本身（标题、警告、开门键）保留。
        private static void RetireToolLatch(Transform ground)
        {
            foreach (var latch in ground.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Airlock Repair Latch" || t.name == "Latch Identification").ToArray())
                if (latch != null) UnityEngine.Object.DestroyImmediate(latch.gameObject);
            foreach (var part in ground.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Hatch Repair State" || t.name == "Hatch Repair Track").ToArray())
                part.gameObject.SetActive(false);
        }

        private static float wallRight = 3.9f, wallRear = -3.9f, wallLeft = -3.9f;
        private static void MeasureWalls(Transform ground)
        {
            var temporary = new System.Collections.Generic.List<Collider>();
            foreach (var filter in ground.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null || !filter.TryGetComponent(out MeshRenderer r) || !r.enabled) continue;
                var c = filter.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = filter.sharedMesh; temporary.Add(c);
            }
            Physics.SyncTransforms();
            float Cast(Vector3 origin, Vector3 dir, float fallback)
            {
                float best = fallback;
                foreach (var y in new[] { 1.0f, 1.3f, 1.6f })
                {
                    var o = new Vector3(origin.x, y, origin.z);
                    if (Physics.Raycast(o, dir, out var hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        best = dir.x > 0 ? Mathf.Min(best, hit.point.x) : dir.x < 0 ? Mathf.Max(best, hit.point.x) : Mathf.Max(best, hit.point.z);
                }
                return best;
            }
            wallRight = Cast(new Vector3(2.5f, 0f, -0.5f), Vector3.right, 3.9f) - 0.005f;
            wallRear = Cast(new Vector3(-2.2f, 0f, -2.5f), Vector3.back, -3.9f) + 0.005f;
            wallLeft = Cast(new Vector3(-2.5f, 0f, 2.9f), Vector3.left, -3.9f) + 0.005f;
            foreach (var c in temporary) UnityEngine.Object.DestroyImmediate(c);
            Physics.SyncTransforms();
            Debug.Log($"AIRLOCK_WALLS right={wallRight:0.000} rear={wallRear:0.000} left={wallLeft:0.000}");
        }

        private static Transform Find(Transform root, string name) => root.root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        private static Transform Frame(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var frame = new GameObject(name).transform;
            frame.SetParent(parent, false);
            frame.SetLocalPositionAndRotation(position, rotation);
            return frame;
        }

        private static CockpitLamp Lamp(Transform parent, string name, Vector3 position)
        {
            var bulb = Primitive(parent, name, PrimitiveType.Sphere, position, Vector3.one * 0.022f, "Cockpit Lamp");
            var lamp = bulb.AddComponent<CockpitLamp>();
            lamp.Configure(bulb.GetComponent<Renderer>());
            return lamp;
        }

        private static void Label(Transform parent, string name, string key, Vector3 position, Quaternion rotation, Vector2 size, float maxFont)
        {
            var label = ui.WorldLabel(name, key, Vector3.zero, size, maxFont, Quaternion.identity);
            label.transform.SetParent(parent, false);
            label.transform.SetLocalPositionAndRotation(position, rotation);
            AutoSize(label, maxFont);
            label.GetComponent<TMP_Text>().color = new Color(0.9f, 0.96f, 1f);
        }

        // 三种语言长度差别很大，自动缩小字号，保证不溢出。
        private static void AutoSize(LocalizedText label, float maxFont)
        {
            var text = label.GetComponent<TMP_Text>();
            text.enableAutoSizing = true;
            text.fontSizeMin = maxFont * 0.45f;
            text.fontSizeMax = maxFont;
            EditorUtility.SetDirty(text);
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, string material, bool collision = false)
        {
            var o = Primitive(parent, name, PrimitiveType.Cube, position, size, material);
            if (collision) o.AddComponent<BoxCollider>();
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, string materialName)
        {
            var o = GameObject.CreatePrimitive(shape);
            o.name = name;
            o.transform.SetParent(parent, false);
            o.transform.SetLocalPositionAndRotation(position, Quaternion.identity);
            o.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + materialName + ".mat");
            if (material == null) throw new InvalidOperationException("缺少材质 " + materialName + "：请先执行 Install Pilot Console 生成驾驶台材质。");
            o.GetComponent<Renderer>().sharedMaterial = material;
            return o;
        }
    }
}
