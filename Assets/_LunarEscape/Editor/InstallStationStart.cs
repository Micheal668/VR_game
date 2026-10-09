using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace LunarEscape.Editor
{
    // 生命保障主场景（11）的开局体验：
    //   · 出生点移到工作台与开始屏之间，面朝开始屏；
    //   · 圆盘按压可向任意方向平移（以视线为前方），移速提高约 25%；
    //   · 本场景单独的任务时长（240 s 维修 / 120 s 撤离），基地空气与电量更充足，航天服满电满氧；
    //   · 开局整站断电：开始屏旁的照明总闸需要拉下才恢复照明；
    //   · 恢复照明、完成气闸抢修、救出指挥官各奖励时间。
    // 须在 Install Life Support and Cockpit 与 Install Airlock Repair 之后执行；可重复执行。
    public static class InstallStationStart
    {
        private const string Root = "Assets/_LunarEscape";
        private const string BreakerName = "Habitat Lighting Breaker";
        private static readonly Vector3 SpawnPosition = new(0f, 0f, 2.2f);
        public const float WalkSpeed = 1.75f;
        private static LocalizedPanelBuilder ui;

        [MenuItem("Lunar Escape/Install Station Start: Lights, Spawn, Movement (Life Support Scene)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("STATION_START_INSTALLED");
        }

        [MenuItem("Lunar Escape/Switch Station Lights On (Play Mode)")]
        public static void SwitchOnInPlayMode() { var b = UnityEngine.Object.FindAnyObjectByType<HabitatBreaker>(); if (b != null) b.SwitchOn(); }
        [MenuItem("Lunar Escape/Switch Station Lights On (Play Mode)", true)]
        private static bool CanSwitchOn() => Application.isPlaying && UnityEngine.Object.FindAnyObjectByType<HabitatBreaker>() != null;

        public static void ApplyToCurrentScene()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            var life = session.GetComponent<LifeSupportMission>();
            if (life == null) throw new InvalidOperationException("当前场景没有生命保障：请先执行 Install Life Support and Cockpit。");
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            var language = UnityEngine.Object.FindAnyObjectByType<LocalizationService>(FindObjectsInactive.Include);
            ui = new LocalizedPanelBuilder(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset"), language);

            ConfigureTiming(session.Mission, life);
            MoveSpawn(session);
            ConfigureLocomotion();
            var breaker = BuildBreaker(ground, session.Mission);
            session.GetComponent<LifeSupportEnvironment>().ConfigureBreaker(breaker);
            EditorUtility.SetDirty(session.GetComponent<LifeSupportEnvironment>());
            BuildBonus(session, life, breaker);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // 时间与资源：单独的任务时长资产，不影响 06–10 教学场景共用的 Station Mission。
        private static void ConfigureTiming(StationMission mission, LifeSupportMission life)
        {
            const string path = Root + "/Settings/Life Support Mission.asset";
            var config = AssetDatabase.LoadAssetAtPath<StationMissionConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<StationMissionConfig>(); AssetDatabase.CreateAsset(config, path); }
            config.Configure(240f, 4f, 120f, 30f);
            EditorUtility.SetDirty(config);
            mission.Configure(config, mission.RepairTask);
            EditorUtility.SetDirty(mission);

            var settings = new SerializedObject(life.Config);
            void Range(string name, float min, float max) => settings.FindProperty(name).vector2Value = new Vector2(min, max);
            Range("baseOxygenRange", 70f, 90f);
            Range("basePowerRange", 70f, 90f);
            Range("suitOxygenRange", 100f, 100f);
            Range("suitPowerRange", 100f, 100f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(life.Config);
        }

        // 出生点：工作台（z≈0.55）与物资架（z≈3.1）之间，面朝开始屏（+Z）。重新开始也回到这里。
        private static void MoveSpawn(StationMissionSession session)
        {
            session.SpawnPoint.SetPositionAndRotation(SpawnPosition, Quaternion.identity);
            EditorUtility.SetDirty(session.SpawnPoint);
            var rig = session.Player.transform;
            rig.SetPositionAndRotation(SpawnPosition, Quaternion.identity);
            EditorUtility.SetDirty(rig);
            if (PrefabUtility.IsPartOfPrefabInstance(rig)) PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
        }

        // 圆盘：去掉“只保留前后”的缩放，左右也生效；移动以视线为前方，速度 1.4 → 1.75 m/s。
        private static void ConfigureLocomotion()
        {
            // 直接替换移动绑定的处理器字符串：其余绑定保持原样（API 只能追加处理器，不能替换）。
            string json = File.ReadAllText(ConfigureViveControls.InputPath);
            string updated = System.Text.RegularExpressions.Regex.Replace(json,
                "\"processors\": \"[^\"]*scaleVector2[(]x=0,y=1[)][^\"]*\"",
                "\"processors\": \"" + ConfigureViveControls.MoveProcessors + "\"");
            if (updated != json) File.WriteAllText(ConfigureViveControls.InputPath, updated);
            AssetDatabase.ImportAsset(ConfigureViveControls.InputPath, ImportAssetOptions.ForceSynchronousImport);
            foreach (var move in UnityEngine.Object.FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                move.moveSpeed = WalkSpeed;
                move.enableStrafe = true;
                EditorUtility.SetDirty(move);
                if (PrefabUtility.IsPartOfPrefabInstance(move)) PrefabUtility.RecordPrefabInstancePropertyModifications(move);
            }
        }

        // 照明总闸：出生点左前方的独立立柱，出生后一眼可见、伸手可及；上方一盏红色应急灯。
        private static HabitatBreaker BuildBreaker(Transform ground, StationMission mission)
        {
            var old = ground.Find(BreakerName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(BreakerName).transform;
            root.SetParent(ground, false);
            // 独立立柱：出生点左前方，正面朝向出生点，拉杆向玩家方向拉下（局部 +Z 朝玩家）。
            var position = new Vector3(-0.95f, 1.15f, 2.75f);
            var facing = SpawnPosition - new Vector3(position.x, 0f, position.z);
            var mount = Frame(root, "Breaker Mount", position, Quaternion.LookRotation(facing.normalized, Vector3.up));
            Box(mount, "Breaker Pedestal", new Vector3(0f, -0.58f, 0f), new Vector3(0.14f, 1.16f, 0.12f), "Station Graphite");
            Box(mount, "Pedestal Foot", new Vector3(0f, -1.14f, 0f), new Vector3(0.34f, 0.03f, 0.3f), "Station Shell");
            Box(mount, "Breaker Cabinet", new Vector3(0f, 0.12f, 0.03f), new Vector3(0.3f, 0.5f, 0.06f), "Station Graphite");
            Box(mount, "Hazard Plate", new Vector3(0f, 0.12f, 0.062f), new Vector3(0.24f, 0.44f, 0.004f), "Safety Amber");
            Box(mount, "Breaker Pivot", new Vector3(0f, 0f, 0.08f), new Vector3(0.1f, 0.06f, 0.05f), "Station Shell");
            var control = Frame(mount, "Breaker Lever", new Vector3(0f, 0f, 0.1f), Quaternion.identity);
            var arm = Frame(control, "Lever Arm", Vector3.zero, Quaternion.identity);
            Primitive(arm, "Arm", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.03f, 0.3f, 0.03f), "Station Shell");
            var grip = Primitive(arm, "Grip", PrimitiveType.Cylinder, new Vector3(0f, 0.31f, 0f), new Vector3(0.045f, 0.09f, 0.045f), "Cockpit Red");
            grip.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var colliderObject = new GameObject("Lever Grip Collider");
            colliderObject.transform.SetParent(arm, false);
            var box = colliderObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.2f, 0f); box.size = new Vector3(0.2f, 0.26f, 0.09f);
            var lever = control.gameObject.AddComponent<ReleaseLever>();
            lever.Configure(arm, 100f);
            lever.ConfigureHighlight(new[] { grip.GetComponent<Renderer>() });
            var lampObject = Primitive(mount, "Breaker Lamp", PrimitiveType.Sphere, new Vector3(0.11f, 0.33f, 0.07f), Vector3.one * 0.03f, "Cockpit Lamp");
            var lamp = lampObject.AddComponent<CockpitLamp>(); lamp.Configure(lampObject.GetComponent<Renderer>());
            var label = ui.WorldLabel("Breaker Label", "life.breaker.label", Vector3.zero, new Vector2(0.3f, 0.12f), 0.3f, Quaternion.identity);
            label.transform.SetParent(mount, false);
            label.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.47f, 0.035f), Quaternion.Euler(0f, 180f, 0f));
            var text = label.GetComponent<TextMeshPro>();
            text.enableAutoSizing = true; text.fontSizeMin = 0.12f; text.fontSizeMax = 0.3f; text.color = new Color(1f, 0.85f, 0.55f);
            // 名称以 Alarm 开头：生命保障的照明收集会排除它，断电时它仍然亮着。
            var emergency = new GameObject("Alarm Emergency Light").AddComponent<Light>();
            emergency.transform.SetParent(mount, false);
            emergency.transform.localPosition = new Vector3(0f, 0.75f, 0.4f);
            emergency.type = LightType.Point; emergency.range = 4.5f; emergency.intensity = 0.9f;
            emergency.color = new Color(1f, 0.18f, 0.12f); emergency.shadows = LightShadows.None;
            var breaker = root.gameObject.AddComponent<HabitatBreaker>();
            breaker.Configure(mission, lever, lamp, emergency);
            EditorUtility.SetDirty(breaker);
            return breaker;
        }

        private static void BuildBonus(StationMissionSession session, LifeSupportMission life, HabitatBreaker breaker)
        {
            var old = session.transform.Find("Time Bonus Popup");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var popup = new GameObject("Time Bonus Popup").AddComponent<TextMeshPro>();
            popup.transform.SetParent(session.transform, false);
            popup.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset");
            popup.fontSize = 1.2f; popup.alignment = TextAlignmentOptions.Center;
            popup.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
            popup.color = new Color(0.35f, 1f, 0.65f);
            popup.text = "+30 s";
            popup.gameObject.SetActive(false);
            var bonus = session.GetComponent<MissionTimeBonus>();
            if (bonus == null) bonus = session.gameObject.AddComponent<MissionTimeBonus>();
            bonus.Configure(session.Mission, life, breaker, life.AirlockRepair, session.GetComponent<CrewMission>(), session.Player.Camera.transform, popup);
            EditorUtility.SetDirty(bonus);
        }

        private static Transform Frame(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var frame = new GameObject(name).transform;
            frame.SetParent(parent, false);
            frame.SetLocalPositionAndRotation(position, rotation);
            return frame;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, string material)
            => Primitive(parent, name, PrimitiveType.Cube, position, size, material);

        private static GameObject Primitive(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, string materialName)
        {
            var o = GameObject.CreatePrimitive(shape);
            o.name = name;
            o.transform.SetParent(parent, false);
            o.transform.SetLocalPositionAndRotation(position, Quaternion.identity);
            o.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
            o.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + materialName + ".mat");
            return o;
        }
    }
}
