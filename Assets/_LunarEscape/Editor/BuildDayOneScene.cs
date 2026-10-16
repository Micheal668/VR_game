using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static LunarEscape.Editor.LunarToolBuilder;

namespace LunarEscape.Editor
{
    // 第一天（场景 12）：从事故场景 11 复制同一座基地、月面与玩家，再拆掉全部事故逻辑（倒计时、警报、抢修、飞行），
    // 换成平常的一个班次：卧室醒来 → 大厅穿航天服 → 气闸出舱 → 吹净太阳能板、钻三根岩芯 → 岩芯送实验室 → 回卧室睡觉 → “第二天”。
    // 场景 11 每次重新安装后都要重新执行本菜单（它是复制出来的）。可重复执行：每次都从场景 11 重新复制。
    public static class BuildDayOneScene
    {
        private const string Root = "Assets/_LunarEscape";
        public const string ScenePath = Root + "/Scenes/12_LunarStation_DayOne.unity";
        private static LocalizedPanelBuilder ui;
        private static LocalizationService language;

        [MenuItem("Lunar Escape/Build Day One Scene (copies Life Support Scene)")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InstallStationPreparation.EnsureInstalled();
            BuildRepairScene.RefreshFont();
            // 保存副本保留场景 12 的资源身份，重复安装不会使引用它的资源失效。
            var source = EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            BuildRussianSuit.ApplyToCurrentScene();
            EditorSceneManager.SaveScene(source);
            if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new InvalidOperationException("复制场景 11 失败。");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Apply();
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("DAY_ONE_SCENE_BUILT");
        }

        [MenuItem("Lunar Escape/Day One: Finish All Tasks (Play Mode)")]
        public static void FinishInPlayMode()
        {
            var day = UnityEngine.Object.FindAnyObjectByType<DayOneMission>();
            if (day == null) return;
            day.Suit.Don();
            foreach (var p in day.Panels) p.CleanInstantly();
            foreach (var s in day.Sites) s.CompleteInstantly();
        }
        [MenuItem("Lunar Escape/Day One: Finish All Tasks (Play Mode)", true)]
        private static bool CanFinish() => Application.isPlaying && UnityEngine.Object.FindAnyObjectByType<DayOneMission>() != null;

        // 游戏从第一天开始：场景 12 排在最前，随后是场景 11。
        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != BuildLifeSupportScene.ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(BuildLifeSupportScene.ScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void Apply()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            var life = session.GetComponent<LifeSupportMission>();
            var crew = session.GetComponent<CrewMission>();
            var origin = session.Player;
            var body = session.Exit.PlayerBody;
            var camera = origin.Camera;
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            var preparation = UnityEngine.Object.FindAnyObjectByType<StationPreparation>(FindObjectsInactive.Include);
            var door = session.GetComponent<MissionEnvironment>().Door.transform;
            language = UnityEngine.Object.FindAnyObjectByType<LocalizationService>(FindObjectsInactive.Include);
            ui = new LocalizedPanelBuilder(StationFont, language);

            // —— 先记下要保留的东西，再拆事故逻辑 ——
            var rooms = HabitatRooms(life);
            var commander = crew.Commander;
            commander.SetParent(ground, true);
            var sun = session.transform.Find("Life Support Sun").GetComponent<Light>();
            sun.transform.SetParent(null, true);
            sun.name = "Day One Sun";
            var playerSuit = origin.transform.Find("Player - Crew Escape Suit");
            var playerUniform = origin.transform.Find("Player - Station Uniform");
            var rack = ground.Find("Life Support Hardware/Wall Suit Rack");
            // 服装 HUD 的资源仪表保留，在拆事故会话前复制为第一天的显示控制器。
            var personalDisplay = new GameObject("Day One Suit Display").AddComponent<SuitHudPresenter>();
            EditorUtility.CopySerialized(session.GetComponent<SuitHudPresenter>(), personalDisplay);

            UnityEngine.Object.DestroyImmediate(session.gameObject);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects().Where(r => r.name == "Flight Cabin World" || r.name == "Earth Return Experience").ToArray())
                UnityEngine.Object.DestroyImmediate(root);
            // 只移除事故表现和临时提示。家具、制氧机、工具、物资及维修面板在两天中都存在。
            RemoveNamed(ground, "Repair Point Label", "Repair Tool Label", "Exit Door Status", "Mission Panel",
                "Player Cargo Pouch", "Supply Manifest", "Boarding Zone Label", "Boarding Zone Edge", "Commander Boarding Status",
                "Commander Rescue Area", "Airlock Decompression", "Exit Beam Blockade", "Commander Sleeping Pose", "Alarm Expansion Emergency Light");
            // 第二天的气闸压力屏（含外壳）与登舱按钮：第一天不维修舱门、也不登船。
            foreach (var t in ground.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Crew Dressing Route") || t.name.StartsWith("Dressed Crew")
                         ).ToArray())
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            foreach (var hand in origin.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Life Support Wrist Controls").ToArray())
                UnityEngine.Object.DestroyImmediate(hand.gameObject);
            // 事故用插槽和控制器失效时不能留下无类型过滤的插槽，避免吸走工作物资。
            foreach (var socket in ground.GetComponentsInChildren<XRSocketInteractor>(true))
                if (socket.GetComponent<SupplyChargingDock>() == null && socket.GetComponent<ToolHolster>() == null
                    && socket.GetComponentInParent<SampleRack>() == null && socket.name != "F3 Fuse Socket") socket.enabled = false;
            foreach (var fuse in ground.GetComponentsInChildren<FuseFault>(true))
            {
                fuse.ConfigureDayOne(); fuse.Socket.enabled = true;
                fuse.Burnt.transform.Find("Fuse Body").GetComponent<Renderer>().sharedMaterial = Mat("Panel White");
                fuse.Burnt.transform.Find("Scorch Mark").GetComponent<Renderer>().sharedMaterial = Mat("Guide Teal");
                var start = new SerializedObject(fuse.Socket);
                start.FindProperty("m_StartingSelectedInteractable").objectReferenceValue = fuse.Burnt.Grab;
                start.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var puzzle in ground.GetComponentsInChildren<StationPatchPuzzle>(true))
                foreach (var grab in puzzle.GetComponentsInChildren<XRGrabInteractable>(true))
                { grab.enabled = false; if (grab.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true; }
            foreach (var button in ground.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
            SweepMissionComponents();
            NormalOperationPanels(ground);
            // 气闸通道里旧的传送面会让人绕过两道门，第一天关掉。
            foreach (var teleport in ground.GetComponentsInChildren<TeleportationArea>(true).Where(t => t.name == "Corridor Teleport Surface"))
                teleport.gameObject.SetActive(false);

            Daylight(sun);
            InstallDayTwoCutter.RussianByDefault();
            InstallDayTwoCutter.AddDesktopTrigger();
            var bedroomDoor = ground.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Bedroom Sliding Door");
            if (bedroomDoor != null) bedroomDoor.position += Vector3.up * 2.65f;
            PlaceCommander(commander);
            Physics.SyncTransforms();

            var flow = new GameObject("Day One Flow").transform;
            var suit = BuildSuitLocker(flow, rack, playerSuit, playerUniform);
            var (south, north) = MeasurePassage();
            var airlock = BuildAirlock(flow, door, south, north);
            var blower = preparation.Blower; var drill = preparation.Drill;
            var panels = preparation.Panels; var sites = preparation.Sites;
            var samples = preparation.Research.Rack;
            var bed = BuildBed(flow);
            var boards = BuildTaskBoards();
            var fade = BuildFade(camera.transform);
            var voice = BuildVoice(flow, camera.transform, commander);
            rooms.Add(airlock.Chamber); // 只用于判断“在基地里”；气闸室是否有气由 DayOneAirlock 决定
            var day = flow.gameObject.AddComponent<DayOneMission>();
            day.Configure(body, rooms.ToArray(), suit, airlock, panels, sites, samples, blower, drill, bed, voice, fade, boards);
            preparation.ConfigureDayOne(day);
            day.ConfigurePreparation(preparation);
            day.ConfigureWrist(null); suit.ConfigureDay(day);
            personalDisplay.transform.SetParent(flow, false);
            personalDisplay.ConfigureDayOne(day, null);
            InstallStationAudio.Apply(camera, ground, flow, null, null, null, null, voice, day, suit);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // 基地各舱室的范围（大厅 + 扩建舱室），用于判断玩家是否在舱内。
        private static List<Bounds> HabitatRooms(LifeSupportMission life)
        {
            var rooms = new List<Bounds>();
            void Add(BoxCollider box)
            {
                if (box == null) return;
                var t = box.transform;
                var size = Vector3.Scale(box.size, t.lossyScale);
                rooms.Add(new Bounds(t.TransformPoint(box.center), new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z))));
            }
            Add(life.HabitatVolume);
            var extra = new SerializedObject(life).FindProperty("additionalHabitatVolumes");
            for (int i = 0; extra != null && i < extra.arraySize; i++) Add(extra.GetArrayElementAtIndex(i).objectReferenceValue as BoxCollider);
            return rooms;
        }

        private static void RemoveNamed(Transform root, params string[] names)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToArray())
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        // 场景里剩下的本项目脚本若属于事故流程（引用已删除的任务组件），一律移除；只保留与第一天兼容的通用组件。
        private static readonly HashSet<Type> Keep = new()
        {
            typeof(LocalizationService), typeof(LocalizedText), typeof(CockpitLamp), typeof(CrewSuitRig), typeof(TrackedCrewSuit), typeof(CrewWalkMotion),
            typeof(EarthSkyView), typeof(LanderNavigationBeacon), typeof(CollisionAwareSimulator), typeof(MissionTeleportationProvider),
            typeof(ButtonPressFeedback), typeof(GrabFeedback), typeof(ReturnFallenTool), typeof(SpaceAudio), typeof(ScreenFade), typeof(DesktopTriggerLevel),
            typeof(StationPreparation), typeof(PreparedCargo), typeof(SupplyChargingDock), typeof(LunarResearchStation), typeof(CargoItem),
            typeof(SuitHudPresenter), typeof(HelmetHudOverlay), typeof(ResourceReadout), typeof(RouteChevronGuide), typeof(ReadableWristDisplay),
            typeof(RemoteSocketPlacement), typeof(FuseFault), typeof(AirlockFuse), typeof(CockpitSwitch),
            typeof(NitrogenBlower), typeof(CoreDrill), typeof(LaserCutter), typeof(ToolHolster), typeof(DustyPanel), typeof(DrillSite), typeof(CoreSample), typeof(SampleRack)
        };

        private static void NormalOperationPanels(Transform ground)
        {
            foreach (var name in new[] { "Bedroom Central Control", "Habitat Supply Console", "Suit Service Console", "Airlock Pressure Console" })
            {
                var panel = ground.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
                if (panel == null) continue;
                foreach (Transform child in panel.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                ui.Label(panel, "Normal Shift Status", "prep.station.normal", Vector2.zero, panel.sizeDelta * .88f, 40, new Color(.6f, 1f, .85f));
            }
            foreach (var name in new[] { "Oxygen Generator Repair Readout", "Shared Base Power Readout" })
            {
                var label = ground.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == name);
                if (label == null) continue;
                var binding = label.GetComponent<LocalizedText>() ?? label.gameObject.AddComponent<LocalizedText>();
                binding.Configure(language, "prep.station.normal");
            }
        }

        // 同一份工作区先安装到场景 11，复制第一天时直接复用，避免额外的桌面、工具架或样品台。
        public static (DustyPanel[] panels, DrillSite[] sites, SampleRack rack, NitrogenBlower blower, CoreDrill drill) BuildSharedWork(Transform parent, Transform innerDoor)
        {
            language = UnityEngine.Object.FindAnyObjectByType<LocalizationService>(FindObjectsInactive.Include);
            ui = new LocalizedPanelBuilder(StationFont, language);
            var (south, north) = MeasurePassage();
            var (blower, drill) = BuildToolRack(parent, north);
            var panels = BuildSolarField(parent); var sites = BuildDrillSites(parent); var rack = BuildSampleRack(parent);
            var airlock = BuildAirlock(parent, innerDoor, south, north);
            var outer = parent.Find("Station Outer Hatch");
            outer.position += Vector3.up * 2.7f;
            var panel = parent.Find("Airlock Panel Chamber");
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                button.onClick = new Button.ButtonClickedEvent(); button.interactable = false;
                button.GetComponentInChildren<LocalizedText>().SetKey("prep.airlock.open");
            }
            UnityEngine.Object.DestroyImmediate(airlock.gameObject);
            return (panels, sites, rack, blower, drill);
        }

        private static void SweepMissionComponents()
        {
            var removed = new List<string>();
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || !behaviour.gameObject.scene.IsValid()) continue;
                var type = behaviour.GetType();
                if (type.Assembly != typeof(StationMission).Assembly || Keep.Contains(type)) continue;
                removed.Add(type.Name + "@" + behaviour.name);
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
            Debug.Log("DAY_ONE_SWEEP " + string.Join(", ", removed));
        }

        private static void Daylight(Light sun)
        {
            sun.enabled = true;
            sun.type = LightType.Directional;
            sun.intensity = 1.9f; sun.color = new Color(1f, 0.93f, 0.82f);
            sun.transform.rotation = Quaternion.Euler(42f, 330f, 0f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.21f);
            RenderSettings.reflectionIntensity = 1f;
        }

        // 第一天指挥官穿着站服，在大厅圆桌旁值班。
        private static void PlaceCommander(Transform commander)
        {
            commander.SetPositionAndRotation(new Vector3(1.55f, 0f, -1.75f), Quaternion.LookRotation(new Vector3(-0.8f, 0f, 1f)));
            var rigs = commander.GetComponentsInChildren<CrewSuitRig>(true);
            foreach (var rig in rigs)
            {
                bool uniform = rig.name.Contains("Uniform");
                if (rig.Body != null) rig.Body.enabled = uniform;
                if (rig.Helmet != null) rig.Helmet.enabled = uniform;
                rig.gameObject.SetActive(true);
            }
            foreach (var marker in commander.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Commander Medical Port").ToArray())
                marker.gameObject.SetActive(false);
        }

        private static SuitLocker BuildSuitLocker(Transform flow, Transform rack, Transform playerSuit, Transform playerUniform)
        {
            var grip = rack.Find("Suit Release Grip").GetComponent<XRSimpleInteractable>();
            var hanging = rack.Find("Hanging Emergency Suit").GetComponentsInChildren<Renderer>(true);
            var readout = Text(rack, "Suit Don Progress", rack.InverseTransformPoint(grip.transform.position + Vector3.up * 0.22f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.2f, 0.06f), 0.4f, new Color(0.4f, 1f, 0.7f));
            readout.text = "";
            var locker = flow.gameObject.AddComponent<SuitLocker>();
            var lamps = playerSuit.Find("Suit Shoulder Lamps");
            locker.Configure(grip, hanging, playerSuit.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(lamps)).ToArray(),
                playerUniform.GetComponentsInChildren<Renderer>(true), lamps != null ? lamps.gameObject : null, readout);
            foreach (var light in lamps != null ? lamps.GetComponentsInChildren<Light>(true) : new Light[0]) light.enabled = true;
            // 握把要按住侧键约 2.5 秒；旁边再放一块屏幕按钮，射线或鼠标点一下即可穿上。
            var gripPoint = locker.Handle.transform.position;
            Panel(flow, "Suit Don Button", new Vector3(3.55f, gripPoint.y + 0.42f, gripPoint.z), Quaternion.Euler(0f, 90f, 0f), "day1.suit.toggle", locker.Toggle, new Vector2(520, 150));
            return locker;
        }

        private static (float south, float north) MeasurePassage()
        {
            float Cast(Vector3 direction, float fallback)
                => Physics.Raycast(new Vector3(7.4f, 1.2f, 1.7f), direction, out var hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.z : fallback;
            return (Cast(Vector3.back, 0.7f), Cast(Vector3.forward, 2.7f));
        }

        // —— 气闸：原撤离门作内门；通道外端新装一道外门；基地侧、气闸室内、月面侧各一块按钮面板 ——
        private static DayOneAirlock BuildAirlock(Transform flow, Transform innerDoor, float south, float north)
        {
            float width = north - south, middle = (south + north) * 0.5f;
            var outer = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Station Outer Hatch");
            bool create = outer == null;
            if (create)
            {
            outer = new GameObject("Station Outer Hatch").transform;
            outer.SetParent(flow, false);
            outer.position = new Vector3(8.02f, 1.6f, middle);
            var collider = outer.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.14f, 3.2f, width + 0.1f);
            Prim(outer, "Hatch Panel", PrimitiveType.Cube, Vector3.zero, new Vector3(0.12f, 3.2f, width + 0.1f), "EX_White");
            Prim(outer, "Hatch Stripe", PrimitiveType.Cube, new Vector3(-0.065f, -1.05f, 0f), new Vector3(0.01f, 0.12f, width), "EX_Blue");
            Prim(outer, "Hatch Window", PrimitiveType.Cube, new Vector3(-0.065f, 0.35f, 0f), new Vector3(0.012f, 0.45f, 0.7f), "EX_Window");
            Prim(outer, "Hatch Window Outside", PrimitiveType.Cube, new Vector3(0.065f, 0.35f, 0f), new Vector3(0.012f, 0.45f, 0.7f), "EX_Window");
            Prim(outer, "Hatch Handle", PrimitiveType.Cube, new Vector3(-0.075f, -0.2f, width * 0.32f), new Vector3(0.03f, 0.3f, 0.04f), "Safety Amber");
            ChamberLight(flow, new Vector3(5.2f, 2.9f, middle)); ChamberLight(flow, new Vector3(7.2f, 2.9f, middle));
            }
            outer.position = new Vector3(8.02f, 1.6f, middle);
            var lamps = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.name == "Airlock Status Light").ToArray();
            var chamber = new Bounds(new Vector3(6.05f, 1.65f, middle), new Vector3(3.9f, 3.3f, width));
            var airlock = new GameObject("Day One Airlock").AddComponent<DayOneAirlock>();
            airlock.transform.SetParent(flow, false);
            airlock.Configure(innerDoor, outer, chamber, lamps);
            // 只有气闸室里一个“气闸循环”按钮：在哪一侧开着门，按一下就换到另一侧。
            // 出舱后外门一直开着，回来时走进气闸室再按一次即可（面板朝向：rotation 为观看者视线方向）。
            if (create) Panel(flow, "Airlock Panel Chamber", new Vector3(6.05f, 1.45f, south + 0.02f), Quaternion.Euler(0f, 180f, 0f), "day1.airlock.cycle", airlock.RequestOther);
            else
            {
                var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(b => b.name == "Airlock Panel Chamber Button");
                button.interactable = true; button.GetComponentInChildren<LocalizedText>().SetKey("day1.airlock.cycle");
                button.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(button.onClick, airlock.RequestOther);
            }
            return airlock;
        }

        private static Light ChamberLight(Transform parent, Vector3 position)
        {
            var light = new GameObject("Airlock Status Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = LightType.Point; light.range = 3.2f; light.intensity = 0.9f; light.shadows = LightShadows.None;
            light.color = new Color(0.25f, 1f, 0.45f);
            return light;
        }

        private static void Panel(Transform parent, string name, Vector3 position, Quaternion rotation, string key, UnityEngine.Events.UnityAction action, Vector2? size = null)
        {
            var panel = ui.Panel(name, position, size ?? new Vector2(520, 170), 0.0007f, rotation);
            panel.SetParent(parent, true);
            var button = ui.Button(panel, name + " Button", key, Vector2.zero, new Vector2(480, 130), 44);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            button.gameObject.AddComponent<ButtonPressFeedback>();
        }

        // —— 气闸室北墙的工具架：氮气枪横放在搁板上，取芯钻竖挂 ——
        private static (NitrogenBlower, CoreDrill) BuildToolRack(Transform flow, float north)
        {
            var rack = Frame(flow, "Day One Tool Rack", new Vector3(5.7f, 0f, north - 0.01f), Quaternion.Euler(0f, 180f, 0f));
            Prim(rack, "Rack Backboard", PrimitiveType.Cube, new Vector3(0f, 1.1f, -0.005f), new Vector3(1.1f, 1.3f, 0.02f), "EX_Trim");
            Prim(rack, "Blower Shelf", PrimitiveType.Cube, new Vector3(-0.28f, 1.02f, 0.07f), new Vector3(0.42f, 0.02f, 0.14f), "EX_White", collider: true);
            Prim(rack, "Drill Hook", PrimitiveType.Cube, new Vector3(0.25f, 1.4f, 0.04f), new Vector3(0.16f, 0.03f, 0.08f), "Safety Amber");
            var label = ui.WorldLabel("Tool Rack Label", "day1.rack.tools", Vector3.zero, new Vector2(0.9f, 0.1f), 0.5f, Quaternion.identity);
            label.transform.SetParent(rack, false);
            label.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.68f, 0.012f), Quaternion.Euler(0f, 180f, 0f));
            var blower = BuildBlower(flow, Vector3.zero, Quaternion.identity);
            BuildHolster(rack, "Blower Holster", new Vector3(-0.28f, 1.1f, 0.07f), Quaternion.LookRotation(Vector3.left, Vector3.up), blower);
            var drill = BuildDrill(flow, Vector3.zero, Quaternion.identity);
            BuildHolster(rack, "Drill Holster", new Vector3(0.25f, 1.45f, 0.09f), Quaternion.identity, drill);
            Lamp(rack, "Rack Lamp", new Vector3(0.45f, 1.68f, 0.012f)).State = LampState.Next;
            return (blower, drill);
        }

        private static float Ground(float x, float z)
            => Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out var hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;

        // 后方大阵列：板面相对地面 30°，尘层和碰撞面与美术网格共用同一姿态。
        private static DustyPanel[] BuildSolarField(Transform parent)
        {
            var field = Frame(parent, "Rear Solar Cleaning", Vector3.zero);
            var panels = new List<DustyPanel>();
            foreach (float z in new[] { -1.5f, 4.5f })
                foreach (float x in new[] { -20f, -23.8f })
                {
                    int i = panels.Count;
                    var root = Frame(field, "Solar Panel " + (i + 1), new Vector3(x, 1.55f, z));
                    var rotation = Quaternion.Euler(-60, 0, 0);
                    var normal = rotation * Vector3.forward;
                    var surface = Frame(root, "Panel Surface", normal * .094f, rotation);
                    var dust = Prim(surface, "Dust Layer", PrimitiveType.Quad, new Vector3(0, 0, .004f),
                        new Vector3(3.46f, 4.46f, 1), "EX_White", Quaternion.Euler(0, 180, 0));
                    dust.GetComponent<Renderer>().sharedMaterial = PanelDustMaterial();
                    dust.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var collider = surface.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(3.5f, 4.5f, .06f);
                    var lamp = Lamp(root, "Panel Status Lamp", new Vector3(1.84f, -.70f, 2.3f), .07f);
                    var panel = root.gameObject.AddComponent<DustyPanel>(); panel.Configure(dust.GetComponent<Renderer>(), lamp, 11 + i * 7);
                    panels.Add(panel);
                }
            var sign = Frame(field, "Solar Sign", new Vector3(-17.65f, Ground(-17.65f, -4.3f), -4.3f), Quaternion.Euler(0, 90, 0));
            Prim(sign, "Sign Post", PrimitiveType.Cube, new Vector3(0, .6f, 0), new Vector3(.05f, 1.2f, .05f), "EX_Trim");
            Prim(sign, "Sign Plate", PrimitiveType.Cube, new Vector3(0, 1.25f, 0), new Vector3(.75f, .25f, .02f), "EX_Blue");
            var text = ui.WorldLabel("Sign Text", "day1.solar.sign", Vector3.zero, new Vector2(.70f, .22f), .5f, Quaternion.identity);
            text.transform.SetParent(sign, false); text.transform.SetLocalPositionAndRotation(new Vector3(0, 1.25f, .012f), Quaternion.Euler(0, 180, 0));
            return panels.ToArray();
        }

        // —— 取芯点：橙色定位环、旁边一根带闪灯的小旗；岩芯开始时藏在地下 ——
        private static DrillSite[] BuildDrillSites(Transform parent)
        {
            var sites = new List<DrillSite>();
            var spots = new[] { new Vector2(11.8f, 6.0f), new Vector2(14.7f, 6.9f), new Vector2(17.5f, 5.6f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var p = new Vector3(spots[i].x, Ground(spots[i].x, spots[i].y), spots[i].y);
                var root = Frame(parent, "Core Site " + (i + 1), p);
                // 定位环：16 块橙色砖沿切线排成一圈，每块按自己所在处的地面高度放置，坡地上也不会埋进土里。
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI * 2f / 16f;
                    var offset = new Vector3(Mathf.Cos(a) * 0.24f, 0f, Mathf.Sin(a) * 0.24f);
                    float y = Ground(p.x + offset.x, p.z + offset.z) - p.y;
                    Prim(root, "Site Ring", PrimitiveType.Cube, offset + Vector3.up * (y + 0.015f), new Vector3(0.09f, 0.04f, 0.035f), "Safety Amber", Quaternion.Euler(0f, 90f - a * Mathf.Rad2Deg, 0f));
                }
                Prim(root, "Flag Pole", PrimitiveType.Cylinder, new Vector3(0.38f, 0.45f, 0f), new Vector3(0.025f, 0.45f, 0.025f), "EX_White");
                var lamp = Lamp(root, "Site Lamp", new Vector3(0.38f, 0.93f, 0f), 0.06f);
                var hole = Prim(root, "Bore Hole", PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 0f), new Vector3(0.001f, 0.002f, 0.001f), "Station Graphite").transform;
                var cuttings = Particles(root, "Drill Cuttings", 200, new Vector2(0.4f, 0.9f), new Vector2(0.01f, 0.03f), new Color(0.5f, 0.48f, 0.45f, 0.9f), ParticleSystemSimulationSpace.World);
                var shape = cuttings.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = 0.05f;
                cuttings.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var main = cuttings.main; main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f); main.gravityModifier = 0.6f;
                var core = BuildCore(root, i);
                var site = root.gameObject.AddComponent<DrillSite>();
                site.Configure(hole, lamp, cuttings, core, 0.3f);
                sites.Add(site);
            }
            return sites.ToArray();
        }

        private static CoreSample BuildCore(Transform site, int index)
        {
            var core = new GameObject("Core Sample " + (index + 1));
            core.transform.SetParent(site, false);
            core.transform.localPosition = Vector3.zero;
            string[] layers = { "Lunar Soil", "Lunar Regolith", "Crater Rock" };
            for (int i = 0; i < layers.Length; i++)
                Prim(core.transform, "Core Layer", PrimitiveType.Cylinder, new Vector3(0f, 0.1f - i * 0.1f, 0f), new Vector3(0.05f, 0.05f, 0.05f), layers[i]);
            var collider = core.AddComponent<CapsuleCollider>();
            collider.radius = 0.03f; collider.height = 0.32f;
            var body = core.AddComponent<Rigidbody>();
            body.mass = 0.4f; body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var grab = core.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true; grab.farAttachMode = InteractableFarAttachMode.Near;
            grab.attachTransform = Frame(core.transform, "Core Attach", Vector3.zero);
            grab.colliders.Add(collider);
            core.AddComponent<ReturnFallenTool>();
            core.AddComponent<GrabFeedback>();
            var sample = core.AddComponent<CoreSample>();
            core.SetActive(false);
            return sample;
        }

        // —— 实验室分拣台上的岩芯样品架：三个竖直插槽 ——
        private static SampleRack BuildSampleRack(Transform flow)
        {
            var rack = Frame(flow, "Core Sample Rack", new Vector3(-7.8f, 1.08f, -4.02f));
            Prim(rack, "Rack Base", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(0.55f, 0.03f, 0.16f), "EX_White");
            Prim(rack, "Rack Back", PrimitiveType.Cube, new Vector3(0f, 0.12f, -0.07f), new Vector3(0.55f, 0.22f, 0.015f), "EX_Blue");
            var label = ui.WorldLabel("Sample Rack Label", "day1.rack.samples", Vector3.zero, new Vector2(0.5f, 0.06f), 0.32f, Quaternion.identity);
            label.transform.SetParent(rack, false);
            label.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.27f, -0.06f), Quaternion.Euler(0f, 180f, 0f));
            label.gameObject.SetActive(false); // 分析仪主屏已经提供名称与插入说明，避免文字穿过样品。
            var sockets = new XRSocketInteractor[3];
            var lamps = new CockpitLamp[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -0.17f + i * 0.17f;
                Prim(rack, "Slot Collar", PrimitiveType.Cylinder, new Vector3(x, 0.05f, 0f), new Vector3(0.075f, 0.02f, 0.075f), "EX_Trim");
                var slot = Frame(rack, "Core Slot " + (i + 1), new Vector3(x, 0.2f, 0f));
                var trigger = slot.gameObject.AddComponent<SphereCollider>();
                trigger.isTrigger = true; trigger.radius = 0.1f;
                var socket = slot.gameObject.AddComponent<XRSocketInteractor>();
                socket.attachTransform = slot;
                sockets[i] = socket;
                lamps[i] = Lamp(rack, "Slot Lamp", new Vector3(x, 0.05f, 0.08f), 0.02f);
            }
            var samples = rack.gameObject.AddComponent<SampleRack>();
            samples.Configure(sockets, lamps);
            return samples;
        }

        // —— 卧室里玩家的床：收工后握住床面即可入睡 ——
        private static XRSimpleInteractable BuildBed(Transform flow)
        {
            float top = Physics.Raycast(new Vector3(-10.03f, 2.4f, 6.0f), Vector3.down, out var hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.y : 0.6f;
            var bed = Frame(flow, "Sleep Spot", new Vector3(-10.03f, top + 0.06f, 6.0f));
            var collider = bed.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.85f, 0.12f, 1.6f);
            var interactable = bed.gameObject.AddComponent<XRSimpleInteractable>();
            var label = ui.WorldLabel("Sleep Label", "day1.bed.label", Vector3.zero, new Vector2(0.9f, 0.12f), 0.4f, Quaternion.identity);
            label.transform.SetParent(bed, false);
            label.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.55f, 0.9f), Quaternion.identity);
            label.GetComponent<TMP_Text>().color = new Color(0.75f, 0.9f, 1f);
            return interactable;
        }

        // —— 完整任务清单保留在大厅北墙；当前任务由头盔 HUD 显示 ——
        private static TMP_Text[] BuildTaskBoards()
        {
            var board = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Habitat Environmental Console");
            foreach (Transform child in board.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var lobby = BoardText(board, new Vector2(1680, 740), 48);
            lobby.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            // 与场景 11 一样可在屏幕上切换语言（默认俄语）。
            for (int i = 0; i < 3; i++)
            {
                var button = ui.Button(board, "Language " + i, new[] { "language.zh", "language.en", "language.ru" }[i], new Vector2((i - 1) * 520, -398), new Vector2(460, 100), 44);
                UnityEventTools.AddIntPersistentListener(button.onClick, language.SetLanguageIndex, i);
                button.gameObject.AddComponent<ButtonPressFeedback>();
            }
            return new[] { lobby };
        }

        private static TMP_Text BoardText(Transform panel, Vector2 size, float fontSize)
        {
            var text = new GameObject("Task List", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(panel, false);
            text.rectTransform.sizeDelta = size;
            text.font = StationFont; text.fontSize = fontSize; text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true; text.raycastTarget = false;
            text.text = "";
            return text;
        }

        private static ScreenFade BuildFade(Transform camera)
        {
            var canvas = camera.Find("Station Eye Effects");
            if (canvas == null) throw new InvalidOperationException("缺少 Station Eye Effects 画布。");
            foreach (var n in new[] { "Day Title", "Day Subtitle" }) { var old = canvas.Find(n); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject); }
            var eyes = canvas.Find("Closed Eyes").GetComponent<Image>();
            var fade = canvas.GetComponent<ScreenFade>();
            if (fade == null) fade = canvas.gameObject.AddComponent<ScreenFade>();
            var (title, subtitle) = InstallDayTwoCutter.TitleTexts(canvas);
            fade.Configure(eyes, title, subtitle, "day.title.1", "day.title.sub.1", true);
            return fade;
        }

        private static MissionVoice BuildVoice(Transform flow, Transform head, Transform commander)
        {
            const string folder = Root + "/Audio/Voice";
            var lines = AssetDatabase.FindAssets("t:AudioClip", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder).OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => new VoiceLine
                {
                    id = Path.GetFileNameWithoutExtension(p),
                    speaker = Path.GetFileName(p).StartsWith("cup_") ? VoiceSpeaker.MissionControl : VoiceSpeaker.Commander,
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p)
                }).ToArray();
            var voice = new GameObject("Day One Voice").AddComponent<MissionVoice>();
            voice.transform.SetParent(flow, false);
            voice.Configure(lines, head, commander);
            return voice;
        }
    }
}
