using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape.Editor
{
    // 第四步：保留上一课，生成独立的资源选择、月面通道和登舱场景。
    public static class BuildCargoBoardingScene
    {
        public const string ScenePath = "Assets/_LunarEscape/Scenes/07_LunarStation_CargoBoarding.unity";
        private const string Root = "Assets/_LunarEscape";
        private static LocalizedPanelBuilder ui;
        private static LocalizationService localization;

        [MenuItem("Lunar Escape/Open Cargo and Boarding Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Lunar Escape/Preview Cargo Evacuation (Play Mode)")]
        public static void PreviewEvacuation()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            if (!EditorApplication.isPlaying || UnityEngine.Object.FindAnyObjectByType<CargoInventory>() == null)
                throw new InvalidOperationException("请先运行第四步场景。");
            session.RetryMission();
            session.BeginMission();
            session.Advance(session.Mission.Config.RepairWindowSeconds);
        }
        [MenuItem("Lunar Escape/Preview Cargo Evacuation (Play Mode)", true)]
        private static bool CanPreviewEvacuation() => EditorApplication.isPlaying
            && UnityEngine.Object.FindAnyObjectByType<CargoInventory>() != null;

        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("第四步场景已存在，拒绝覆盖手动修改。");
            AssetDatabase.Refresh();
            BuildRepairScene.RefreshFont();
            var scene = EditorSceneManager.OpenScene(BuildEvacuationScene.ScenePath);
            EditorSceneManager.SaveScene(scene, ScenePath);
            localization = UnityEngine.Object.FindAnyObjectByType<LocalizationService>();
            ui = new LocalizedPanelBuilder(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset"), localization);
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            var mission = session.Mission;
            session.Player.Camera.clearFlags = CameraClearFlags.Skybox;
            var tool = UnityEngine.Object.FindAnyObjectByType<ReturnFallenTool>();
            var zone = BuildLunarSurface.Build(session.Exit.PlayerBody, ui, out var routeAreas);
            session.Configure(mission, mission.RepairTask.GetComponent<RepairContact>(), zone,
                session.Player, session.SpawnPoint, tool);
            session.ConfigureExitConfirmation(true);
            foreach (var presenter in UnityEngine.Object.FindObjectsByType<MissionPresenter>())
                presenter.ConfigureInstructionPrefix("cargo.");

            var hatch = Solid("Cabin Boarding Hatch", new Vector3(20, 1.25f, 1.7f),
                new Vector3(0.12f, 2.52f, 1.82f), "Station Graphite");
            session.gameObject.AddComponent<LunarRouteAccess>().Configure(mission, routeAreas, hatch, routeAreas[1]);

            var config = LoadConfig();
            var inventory = session.gameObject.AddComponent<CargoInventory>();
            var pack = new GameObject("Player Cargo Pouch");
            pack.AddComponent<CargoPackFollower>().Configure(session.Player);
            var dropPoint = new GameObject("Cargo Drop Point").transform;
            dropPoint.SetParent(pack.transform, false);
            dropPoint.localPosition = new Vector3(0.15f, 0.35f, 0.25f);
            var items = BuildRack(inventory, config);
            inventory.Configure(mission, config, items, dropPoint);
            inventory.ConfigureShipZone(zone);
            var volume = pack.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(0.65f, 0.45f, 0.55f);
            pack.AddComponent<CargoPackZone>().Configure(inventory, volume);
            // 只有框架可见，没有阻挡手或玩家的实体包壳。
            foreach (float x in new[] { -0.3f, 0.3f })
                LocalBox("Pouch Side", pack.transform, new Vector3(x, -0.06f, 0), new Vector3(0.025f, 0.25f, 0.5f), "Guide Teal");
            LocalBox("Pouch Base", pack.transform, new Vector3(0, -0.2f, 0), new Vector3(0.65f, 0.02f, 0.55f), "Station Graphite");
            var label = ui.WorldLabel("Pouch Label", "cargo.pack", Vector3.zero, new Vector2(0.58f, 0.25f), 0.35f, Quaternion.identity);
            label.transform.SetParent(pack.transform, false);
            label.transform.localPosition = new Vector3(0, -0.02f, 0.28f);
            label.transform.localRotation = Quaternion.Euler(65, 0, 0);

            BuildPocketPanel(pack.transform, inventory);

            BuildInventoryPanel("Supply Manifest", new Vector3(0, 2.48f, 3.72f), Quaternion.identity, inventory, session, false);
            BuildInventoryPanel("Cabin Manifest", new Vector3(25.82f, 1.78f, 1.7f), Quaternion.Euler(0, 90, 0), inventory, session, true);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_CARGO_BOARDING_CREATED " + ScenePath);
        }

        private static CargoConfig LoadConfig()
        {
            const string path = Root + "/Settings/Evacuation Cargo.asset";
            var config = AssetDatabase.LoadAssetAtPath<CargoConfig>(path);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<CargoConfig>();
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        private static CargoItem[] BuildRack(CargoInventory inventory, CargoConfig config)
        {
            var rack = new GameObject("Evacuation Supply Rack").transform;
            var table = Solid("Supply Shelf", new Vector3(0, 0.85f, 3.1f), new Vector3(6.5f, 0.14f, 0.8f), "Station Graphite");
            table.transform.SetParent(rack);
            var kinds = new[] { CargoKind.Oxygen, CargoKind.Oxygen, CargoKind.RepairKit,
                CargoKind.Battery, CargoKind.MedicalKit, CargoKind.DataCore, CargoKind.LunarSample };
            var items = new CargoItem[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                var obj = GameObject.CreatePrimitive(kind == CargoKind.Oxygen ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                obj.name = "Cargo " + kind + " " + i;
                obj.transform.SetParent(rack);
                obj.transform.position = new Vector3(-2.7f + i * 0.9f, 1.14f, 3.1f);
                obj.transform.localScale = kind == CargoKind.Oxygen ? new Vector3(0.24f, 0.2f, 0.24f) : new Vector3(0.34f, 0.34f, 0.28f);
                if(kind==CargoKind.Oxygen)
                {
                    UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
                    var cylinder=obj.AddComponent<MeshCollider>();cylinder.sharedMesh=obj.GetComponent<MeshFilter>().sharedMesh;cylinder.convex=true;
                }
                obj.GetComponent<Renderer>().sharedMaterial = Material(i < 2 ? "Panel White"
                    : i < 4 ? "Safety Amber" : i == 4 ? "Guide Teal" : "Station Shell");
                var body = obj.AddComponent<Rigidbody>();
                body.mass = config.GetWeightKg(kind);
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var grab = obj.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                grab.useDynamicAttach = true;
                grab.throwVelocityScale = 1f;
                grab.interactionLayers = 1;
                obj.AddComponent<ReturnFallenTool>();
                items[i] = obj.AddComponent<CargoItem>();
                items[i].Configure(inventory, kind);
                var itemLabel = ui.WorldLabel("Cargo Shelf Label " + i, "cargo.kind." + kind,
                    new Vector3(-2.7f + i * 0.9f, 0.66f, 2.66f), new Vector2(0.85f, 0.35f), 0.35f, Quaternion.identity);
                itemLabel.transform.SetParent(rack);
                var info = ui.WorldLabel("Cargo Weight Label " + i, "cargo.item", new Vector3(-2.7f + i * 0.9f, 1.5f, 3.1f),
                    new Vector2(0.86f, 0.3f), 0.25f, Quaternion.identity);
                info.gameObject.AddComponent<CargoItemLabel>().Configure(localization, info, kind, config);
                info.transform.SetParent(rack);
            }
            return items;
        }

        private static void BuildInventoryPanel(string name, Vector3 position, Quaternion rotation,
            CargoInventory inventory, StationMissionSession session, bool cabin)
        {
            float scale = cabin ? 0.00185f : 0.0019f;
            var panel = ui.Panel(name, position, new Vector2(1660, 840), scale, rotation);
            ui.Label(panel, name + " Header", "cargo.title", new Vector2(0, 350), new Vector2(1550, 60), 36, new Color(0.4f, 0.95f, 0.87f));
            var capacity = ui.Label(panel, name + " Capacity", "cargo.capacity", new Vector2(0, 280), new Vector2(1550, 60), 32, Color.white);
            var help = ui.Label(panel, name + " Help", cabin ? "cargo.await" : "cargo.help", new Vector2(0, 182), new Vector2(1550, 122), 28, Color.white);
            var presenter = panel.gameObject.AddComponent<CargoPresenter>();
            var rows = new LocalizedText[6];
            var buttons = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                float column = i < 3 ? -395 : 395;
                float y = 60 - i % 3 * 66;
                rows[i] = ui.Label(panel, name + " Row " + i, "cargo.row", new Vector2(column - 80, y), new Vector2(550, 60), 26, Color.white);
                buttons[i] = ui.Button(panel, name + " Discard " + i, "cargo.discard", new Vector2(column + 276, y), new Vector2(190, 50), 23);
                UnityEventTools.AddIntPersistentListener(buttons[i].onClick, presenter.DiscardIndex, i);
            }
            var status = ui.Label(panel, name + " Status", "cargo.status.ready", new Vector2(0, -152), new Vector2(1540, 70), 26, new Color(1, 0.72f, 0.32f));
            presenter.Configure(inventory, session.Mission, localization, capacity, status, rows, buttons);
            if (cabin)
            {
                var confirm = ui.Button(panel, "Confirm Boarding", "cargo.confirm", new Vector2(-260, -251), new Vector2(835, 65), 28);
                var retry = ui.Button(panel, "Retry At Cabin", "mission.retry", new Vector2(480, -251), new Vector2(450, 65), 25);
                UnityEventTools.AddPersistentListener(retry.onClick, session.RetryMission);
                var control = panel.gameObject.AddComponent<CargoBoardingController>();
                control.Configure(session, inventory, help, confirm);
                UnityEventTools.AddPersistentListener(confirm.onClick, control.ConfirmBoarding);
            }
            else ui.Label(panel, name + " Empty Hint", "cargo.empty", new Vector2(0, -251), new Vector2(1540, 72), 26, Color.white);
            for (int i = 0; i < 3; i++)
            {
                var button = ui.Button(panel, name + " Language " + i, new[] { "language.zh", "language.en", "language.ru" }[i],
                    new Vector2((i - 1) * 350, -351), new Vector2(310, 58), 26);
                UnityEventTools.AddIntPersistentListener(button.onClick, localization.SetLanguageIndex, i);
            }
        }

        private static void BuildPocketPanel(Transform pack, CargoInventory inventory)
        {
            var panel = ui.Panel("Pouch Manifest", Vector3.zero, new Vector2(1000, 350), 0.00085f, Quaternion.identity);
            panel.SetParent(pack, false);
            panel.localPosition = new Vector3(0, 0.25f, 0.32f);
            panel.localRotation = Quaternion.Euler(65, 0, 0);
            var capacity = ui.Label(panel, "Pouch Capacity", "cargo.capacity", new Vector2(0, 125), new Vector2(930, 50), 31, Color.white);
            var presenter = panel.gameObject.AddComponent<CargoPocketPresenter>();
            var rows = new LocalizedText[3];
            var buttons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                rows[i] = ui.Label(panel, "Pouch Row " + i, "cargo.row", new Vector2(-130, 48 - i * 75), new Vector2(650, 65), 29, Color.white);
                buttons[i] = ui.Button(panel, "Pouch Discard " + i, "cargo.discard", new Vector2(345, 48 - i * 75), new Vector2(230, 60), 26);
                UnityEventTools.AddIntPersistentListener(buttons[i].onClick, presenter.DiscardSlot, i);
            }
            presenter.Configure(inventory, localization, capacity, rows, buttons);
        }

        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
        private static GameObject Solid(string name, Vector3 position, Vector3 size, string material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.position = position; obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = Material(material);
            return obj;
        }
        private static void LocalBox(string name, Transform parent, Vector3 position, Vector3 size, string material)
        {
            var obj = Solid(name, Vector3.zero, size, material);
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
        }
    }
}
