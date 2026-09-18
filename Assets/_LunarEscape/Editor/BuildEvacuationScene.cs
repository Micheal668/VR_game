using System;
using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape.Editor
{
    // 第三课沿用维修场景，只增加任务流程与短撤离通道；结果仍是可编辑的场景资源。
    public static class BuildEvacuationScene
    {
        public const string ScenePath = "Assets/_LunarEscape/Scenes/06_LunarStation_Evacuation.unity";
        private const string Root = "Assets/_LunarEscape";
        private static LocalizedPanelBuilder ui;
        private static LocalizationService localization;
        private static Transform corridor;

        [MenuItem("Lunar Escape/Open Evacuation Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("撤离场景已存在，拒绝覆盖手动编辑内容。");
            AssetDatabase.Refresh();
            BuildRepairScene.RefreshFont();
            var scene = EditorSceneManager.OpenScene(BuildRepairScene.ScenePath);
            localization = UnityEngine.Object.FindAnyObjectByType<LocalizationService>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset");
            ui = new LocalizedPanelBuilder(font, localization);
            var player = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            var task = UnityEngine.Object.FindAnyObjectByType<TimedRepairTask>();
            var contact = task.GetComponent<RepairContact>();
            var tool = UnityEngine.Object.FindAnyObjectByType<ReturnFallenTool>();
            UnityEngine.Object.DestroyImmediate(task.GetComponent<RepairPresenter>());
            UnityEngine.Object.DestroyImmediate(GameObject.Find("Repair Panel"));

            var flow = new GameObject("Mission Flow");
            var mission = flow.AddComponent<StationMission>();
            mission.Configure(LoadConfig(), task);
            var session = flow.AddComponent<StationMissionSession>();
            var spawn = new GameObject("Player Start").transform;
            spawn.SetParent(flow.transform);
            spawn.SetPositionAndRotation(player.Origin.transform.position, player.Origin.transform.rotation);
            var zone = BuildCorridor(player.GetComponent<CharacterController>(), out var door, out var teleport,
                out var exitIndicator, out var exitLabel);
            session.Configure(mission, contact, zone, player, spawn, tool);
            var alarm = BuildAlarm(flow.transform, out var lights);
            flow.AddComponent<MissionEnvironment>().Configure(mission, door, teleport, lights,
                exitIndicator, exitLabel, alarm);

            BuildMainPanel(mission, session, task);
            BuildExitPanel(mission, session);
            ConfigureMissionFailure.Install(session);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(BuildRepairScene.ScenePath, true),
                new EditorBuildSettingsScene(BuildFoundation.ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_EVACUATION_CREATED " + ScenePath);
        }

        private static StationMissionConfig LoadConfig()
        {
            const string path = Root + "/Settings/Station Mission.asset";
            var config = AssetDatabase.LoadAssetAtPath<StationMissionConfig>(path);
            if (config != null) return config;
            if (!AssetDatabase.IsValidFolder(Root + "/Settings")) AssetDatabase.CreateFolder(Root, "Settings");
            config = ScriptableObject.CreateInstance<StationMissionConfig>();
            config.Configure(90, 4, 60, 30);
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        private static EvacuationZone BuildCorridor(CharacterController playerBody, out GameObject door,
            out TeleportationArea teleport, out Renderer indicator, out LocalizedText exitLabel)
        {
            var room = GameObject.Find("Station Room - editable blockout").transform;
            UnityEngine.Object.DestroyImmediate(GameObject.Find("Wall Right"));
            UnityEngine.Object.DestroyImmediate(GameObject.Find("Reserved Airlock"));
            foreach (var child in room.GetComponentsInChildren<Transform>())
                if (child.name == "Right Rib" && child.position.z > 0.8f && child.position.z < 2.6f)
                    UnityEngine.Object.DestroyImmediate(child.gameObject);

            // 真正切出门洞，而不是把门贴在原来完整的墙面上。
            Box("Wall Right Front", room, new Vector3(4, 1.65f, -1.6f), new Vector3(0.2f, 3.3f, 4.8f), "Station Shell");
            Box("Wall Right Rear", room, new Vector3(4, 1.65f, 3.3f), new Vector3(0.2f, 3.3f, 1.4f), "Station Shell");
            Box("Airlock Lintel", room, new Vector3(4, 2.9f, 1.7f), new Vector3(0.2f, 0.8f, 1.8f), "Station Shell");
            door = Box("Evacuation Door", room, new Vector3(4, 1.25f, 1.7f), new Vector3(0.18f, 2.5f, 1.8f), "Station Graphite");
            Box("Door Sign Backing", room, new Vector3(3.86f, 2.85f, 1.7f), new Vector3(0.04f, 0.48f, 1.9f), "Station Graphite");
            exitLabel = ui.WorldLabel("Exit Door Status", "mission.exit.locked", new Vector3(3.83f, 2.85f, 1.7f),
                new Vector2(1.75f, 0.42f), 0.52f, Quaternion.Euler(0, 90, 0));
            indicator = Box("Exit Beacon", room, new Vector3(3.82f, 2.55f, 1.7f), new Vector3(0.045f, 0.06f, 1.6f), "Safety Amber", false).GetComponent<Renderer>();

            corridor = new GameObject("Evacuation Corridor").transform;
            Box("Corridor Floor", corridor, new Vector3(6, -0.1f, 1.7f), new Vector3(4.2f, 0.2f, 2.2f), "Station Graphite");
            Box("Corridor Wall South", corridor, new Vector3(6, 1.65f, 0.7f), new Vector3(4.2f, 3.3f, 0.2f), "Station Shell");
            Box("Corridor Wall North", corridor, new Vector3(6, 1.65f, 2.7f), new Vector3(4.2f, 3.3f, 0.2f), "Station Shell");
            Box("Corridor End", corridor, new Vector3(8.1f, 1.65f, 1.7f), new Vector3(0.2f, 3.3f, 2.2f), "Station Shell");
            Box("Corridor Ceiling", corridor, new Vector3(6, 3.4f, 1.7f), new Vector3(4.2f, 0.2f, 2.2f), "Station Shell");
            Box("Corridor Light Strip", corridor, new Vector3(6, 3.27f, 1.7f), new Vector3(3.8f, 0.035f, 0.12f), "Panel White", false);
            foreach (float z in new[] { 1.02f, 2.38f })
                Box("Corridor Guidance", corridor, new Vector3(6, 0.012f, z), new Vector3(3.8f, 0.015f, 0.045f), "Guide Teal", false);
            Box("Exit Direction", room, new Vector3(2.8f, 0.012f, 1.7f), new Vector3(1.4f, 0.015f, 0.055f), "Guide Teal", false);
            foreach (int side in new[] { -1, 1 })
            {
                var wing = Box("Exit Arrow", room, new Vector3(3.25f, 0.014f, 1.7f + side * 0.13f),
                    new Vector3(0.42f, 0.015f, 0.055f), "Guide Teal", false);
                wing.transform.rotation = Quaternion.Euler(0, side * 45, 0);
            }

            // 可传送区域向内收边，避免传送目标落进墙或关闭的门内。
            var floor = GameObject.Find("Floor - teleport surface");
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<TeleportationArea>());
            TeleportSurface("Room Teleport Surface", room, Vector3.zero, new Vector2(7.2f, 7.2f));
            teleport = TeleportSurface("Corridor Teleport Surface", corridor, new Vector3(6, 0, 1.7f), new Vector2(3.4f, 1.2f));

            Box("Assembly Floor Marker", corridor, new Vector3(7.1f, 0.014f, 1.7f), new Vector3(0.9f, 0.015f, 1.5f), "Guide Teal", false);
            var zoneObject = new GameObject("Evacuation Assembly Zone");
            zoneObject.transform.SetParent(corridor);
            zoneObject.transform.position = new Vector3(7.1f, 1.25f, 1.7f);
            var volume = zoneObject.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(0.9f, 2.5f, 1.6f);
            var zone = zoneObject.AddComponent<EvacuationZone>();
            zone.Configure(volume, playerBody);
            return zone;
        }

        private static TeleportationArea TeleportSurface(string name, Transform parent, Vector3 center, Vector2 size)
        {
            // 2 毫米薄的不可见实体面与地板连续，独立控制传送而不取消地板物理碰撞。
            var surface = Box(name, parent, center + Vector3.up * 0.001f,
                new Vector3(size.x, 0.002f, size.y), "Station Graphite");
            surface.GetComponent<Renderer>().enabled = false;
            var area = surface.AddComponent<TeleportationArea>();
            area.interactionLayers = 1 << 31;
            return area;
        }

        private static AudioSource BuildAlarm(Transform parent, out Light[] lights)
        {
            lights = new Light[2];
            for (int i = 0; i < lights.Length; i++)
            {
                var lamp = new GameObject("Alarm Light " + (i + 1)).AddComponent<Light>();
                lamp.transform.SetParent(parent);
                lamp.transform.position = i == 0 ? new Vector3(2.7f, 2.75f, 1.7f) : new Vector3(6, 2.75f, 1.7f);
                lamp.type = LightType.Point;
                lamp.range = 6;
                lamp.intensity = 2.5f;
                lamp.color = new Color(1f, 0.12f, 0.055f);
                lamp.shadows = LightShadows.None;
                lights[i] = lamp;
            }
            var audio = new GameObject("Evacuation Alarm").AddComponent<AudioSource>();
            audio.transform.SetParent(parent);
            audio.transform.position = new Vector3(3, 2.6f, 1.7f);
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Evacuation Alarm.wav");
            if (audio.clip == null) throw new InvalidOperationException("缺少撤离提示音。");
            audio.playOnAwake = false;
            audio.loop = true;
            audio.volume = 0.12f;
            audio.spatialBlend = 1f;
            audio.dopplerLevel = 0f;
            audio.minDistance = 2f;
            audio.maxDistance = 16f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            return audio;
        }

        private static void BuildMainPanel(StationMission mission, StationMissionSession session, TimedRepairTask task)
        {
            var panel = ui.Panel("Mission Panel", new Vector3(0, 2.18f, 1.3f), new Vector2(1400, 940), 0.00185f, Quaternion.identity);
            ui.Label(panel, "Station Title", "station.title", new Vector2(0, 390), new Vector2(1280, 80), 27, new Color(0.4f, 0.95f, 0.87f));
            var title = ui.Label(panel, "Mission Title", "mission.briefing.title", new Vector2(0, 290), new Vector2(1260, 80), 42, Color.white);
            var instruction = ui.Label(panel, "Mission Instructions", "mission.briefing.instructions", new Vector2(0, 183), new Vector2(1260, 126), 29, new Color(0.8f, 0.88f, 0.92f));
            var clock = ui.Label(panel, "Mission Clock", "mission.clock.ready", new Vector2(0, 79), new Vector2(1260, 60), 38, new Color(1f, 0.7f, 0.3f));
            var summary = ui.Label(panel, "Mission Repair Summary", "mission.summary.pending", new Vector2(0, 13), new Vector2(1240, 52), 26, Color.white);
            var details = new GameObject("Repair Details", typeof(RectTransform)).GetComponent<RectTransform>();
            details.SetParent(panel, false);
            details.sizeDelta = Vector2.zero;
            var status = ui.Label(details, "Repair Status", "repair.ready", new Vector2(0, -71), new Vector2(1240, 100), 28, Color.white);
            var track = ui.Rectangle(details, "Progress Track", new Vector2(0, -138), new Vector2(1160, 14), new Color(0.16f, 0.23f, 0.29f));
            var fill = ui.Rectangle(track.transform, "Progress Fill", Vector2.zero, new Vector2(1160, 14), Color.white);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0;
            var percentage = ui.Label(details, "Repair Percentage", "repair.progress", new Vector2(0, -174), new Vector2(1240, 40), 24, Color.white);
            var begin = ui.Button(panel, "Begin Mission", "mission.start", new Vector2(0, -255), new Vector2(430, 70));
            var retry = ui.Button(panel, "Retry Mission", "mission.retry", new Vector2(0, -255), new Vector2(430, 70));
            UnityEventTools.AddPersistentListener(begin.onClick, session.BeginMission);
            UnityEventTools.AddPersistentListener(retry.onClick, session.RetryMission);
            AddLanguages(panel, -368, 285, 260, 28);
            panel.gameObject.AddComponent<MissionPresenter>().Configure(mission, title, instruction, clock, summary, begin, retry, details.gameObject);
            task.gameObject.AddComponent<RepairPresenter>().Configure(task, status, percentage, fill,
                GameObject.Find("Repair Point").GetComponent<Renderer>());
        }

        private static void BuildExitPanel(StationMission mission, StationMissionSession session)
        {
            var panel = ui.Panel("Exit Mission Panel", new Vector3(7.98f, 1.85f, 1.7f),
                new Vector2(1000, 820), 0.00165f, Quaternion.Euler(0, 90, 0));
            ui.Label(panel, "Exit Destination", "mission.exit", new Vector2(0, 330), new Vector2(920, 80), 32, new Color(0.4f, 0.95f, 0.87f));
            var title = ui.Label(panel, "Exit Mission Title", "mission.briefing.title", new Vector2(0, 228), new Vector2(920, 85), 38, Color.white);
            var instruction = ui.Label(panel, "Exit Mission Instructions", "mission.briefing.instructions", new Vector2(0, 92), new Vector2(920, 150), 26, Color.white);
            var clock = ui.Label(panel, "Exit Mission Clock", "mission.clock.ready", new Vector2(0, -40), new Vector2(920, 65), 34, new Color(1f, 0.7f, 0.3f));
            var summary = ui.Label(panel, "Exit Repair Summary", "mission.summary.pending", new Vector2(0, -116), new Vector2(920, 65), 23, Color.white);
            var retry = ui.Button(panel, "Retry At Exit", "mission.retry", new Vector2(0, -224), new Vector2(430, 70));
            UnityEventTools.AddPersistentListener(retry.onClick, session.RetryMission);
            AddLanguages(panel, -335, 280, 255, 26);
            panel.gameObject.AddComponent<MissionPresenter>().Configure(mission, title, instruction, clock, summary, null, retry);
        }

        private static void AddLanguages(Transform parent, float y, float spacing, float width, float fontSize)
        {
            var keys = new[] { "language.zh", "language.en", "language.ru" };
            for (int i = 0; i < keys.Length; i++)
            {
                var button = ui.Button(parent, parent.name + " Language " + i, keys[i],
                    new Vector2((i - 1) * spacing, y), new Vector2(width, 64), fontSize);
                UnityEventTools.AddIntPersistentListener(button.onClick, localization.SetLanguageIndex, i);
            }
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, string material, bool collide = true)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + material + ".mat");
            if (!collide) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }
    }
}
