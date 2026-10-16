using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static LunarEscape.Editor.LunarToolBuilder;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    // 只给最新版基地补充共用工作区，不重建其他系统；第一天始终由该场景复制。
    public static class InstallStationPreparation
    {
        private const string RootName = "Shared Station Preparation";
        [MenuItem("Lunar Escape/Install Shared Preparation (Both Days)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildRepairScene.RefreshFont();
            InstallScene();
            BuildDayOneScene.Build();
            Debug.Log("STATION_PREPARATION_INSTALLED");
        }
        public static void EnsureInstalled()
        {
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            if (Object.FindAnyObjectByType<StationPreparation>(FindObjectsInactive.Include) == null) InstallScene();
        }
        public static void InstallScene()
        {
            var scene = EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            var session = Object.FindAnyObjectByType<StationMissionSession>();
            var life = session.GetComponent<LifeSupportMission>();
            var random = session.GetComponent<StationRandomSupplies>();
            var inventory = life.Inventory;
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            var previous = ground.Find(RootName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            InstallStationUsability.Apply(session);
            // 月壤只能来自第一天钻取与研究；移除原来无来源的随机陨石。
            foreach (var group in random.Groups.Where(g => g.name == "Meteorite"))
                foreach (var item in group.items) if (item != null) Object.DestroyImmediate(item.gameObject);
            var groups = random.Groups.Where(g => g.name != "Meteorite").ToArray();
            var items = groups.SelectMany(g => g.items).ToArray();
            var drop = (Transform)new SerializedObject(inventory).FindProperty("dropPoint").objectReferenceValue;
            inventory.Configure(session.Mission, inventory.Config, items, drop);
            random.Configure(life, groups);
            var shared = Frame(ground, RootName, Vector3.zero);
            var owner = shared.gameObject.AddComponent<StationPreparation>();
            var oldSampleLabel = ground.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Sample Labels");
            if (oldSampleLabel != null) oldSampleLabel.gameObject.SetActive(false);
            Physics.SyncTransforms();
            var work = BuildDayOneScene.BuildSharedWork(shared, session.GetComponent<MissionEnvironment>().Door.transform);
            var blowerSettings = new SerializedObject(work.blower);
            blowerSettings.FindProperty("maxRange").floatValue = 5f;
            blowerSettings.FindProperty("maxConeDegrees").floatValue = 24f;
            blowerSettings.FindProperty("fullTankSeconds").floatValue = 180f;
            blowerSettings.ApplyModifiedPropertiesWithoutUndo();
            var prepared = new List<PreparedCargo>();
            for (int g = 0; g < groups.Length; g++)
                for (int i = 0; i < groups[g].items.Length; i++)
                {
                    var item = groups[g].items[i];
                    var old = item.transform.Find("Preparation Label"); if (old != null) Object.DestroyImmediate(old.gameObject);
                    var entry = item.GetComponent<PreparedCargo>() ?? item.gameObject.AddComponent<PreparedCargo>();
                    var bounds = VisualBounds(item);
                    var label = Text(item.transform, "Preparation Label", Vector3.zero, Quaternion.Euler(0, 180, 0), new Vector2(.34f, .12f), .29f, Color.white);
                    label.transform.localScale = new Vector3(1 / item.transform.lossyScale.x, 1 / item.transform.lossyScale.y, 1 / item.transform.lossyScale.z);
                    label.transform.position = new Vector3(bounds.center.x, bounds.max.y + .09f, bounds.center.z);
                    label.enableAutoSizing = true; label.fontSizeMin = .16f; label.fontSizeMax = .29f;
                    entry.Configure(groups[g].name + "/" + i, label); prepared.Add(entry);
                }
            var ports = new List<SupplyChargingDock>();
            BuildChargingBench(shared, owner, prepared.Where(c => c.Item.Kind == CargoKind.Oxygen).ToArray(), CargoKind.Oxygen,
                new Vector3(-14.75f, .98f, .43f), true, ports);
            BuildChargingBench(shared, owner, prepared.Where(c => c.Item.Kind == CargoKind.Battery).ToArray(), CargoKind.Battery,
                new Vector3(-14.75f, .98f, 2.95f), false, ports);
            var research = BuildResearch(shared, owner, work.rack, work.sites.Select(s => s.Core).ToArray(), prepared.ToArray());
            var tools = ground.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true)
                .Where(t => t.name == "Maintenance Tool" || t.GetComponent<TriggerTool>() != null).Select(t => t.transform).Distinct().ToArray();
            owner.Configure(life, Object.FindAnyObjectByType<HabitatBreaker>(), groups, prepared.ToArray(), ports.ToArray(), research,
                work.panels, work.sites, work.blower, work.drill, tools);
            random.ConfigurePreparation(owner);
            EditorUtility.SetDirty(inventory); EditorUtility.SetDirty(random);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static Bounds VisualBounds(CargoItem item)
        {
            var renderers = item.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.GetComponent<TMP_Text>() == null).ToArray();
            var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); return bounds;
        }
        private static void Place(CargoItem item, Vector3 floor)
        {
            item.transform.rotation = Quaternion.identity;
            var b = VisualBounds(item);
            item.transform.position += new Vector3(floor.x - b.center.x, floor.y - b.min.y + .012f, floor.z - b.center.z);
        }
        private static void BuildChargingBench(Transform parent, StationPreparation owner, PreparedCargo[] items, CargoKind kind,
            Vector3 top, bool front, List<SupplyChargingDock> ports)
        {
            string name = kind == CargoKind.Oxygen ? "Oxygen Filling Bench" : "Battery Charging Bench";
            var bench = Frame(parent, name, Vector3.zero);
            Prim(bench, "Worktop", PrimitiveType.Cube, top - Vector3.up * .04f, new Vector3(1.55f, .08f, .96f), "EX_White", collider: true);
            foreach (float x in new[] { -.64f, .64f })
                foreach (float z in new[] { -.35f, .35f })
                    Prim(bench, "Bench Leg", PrimitiveType.Cube, new Vector3(top.x + x, top.y * .5f, top.z + z), new Vector3(.055f, top.y, .055f), "EX_Trim", collider: true);
            float direction = front ? 1 : -1;
            var face = Quaternion.Euler(0, front ? 180 : 0, 0);
            Vector3 header = top + new Vector3(0, .70f, -.44f * direction);
            Prim(bench, "Station Sign", PrimitiveType.Cube, header, new Vector3(1.5f, .36f, .035f), "EX_Blue");
            var title = Text(bench, "Station Title", header + new Vector3(0, .075f, .021f * direction), face, new Vector2(1.42f, .17f), .60f, Color.white);
            title.enableAutoSizing = true; title.fontSizeMin = .40f; title.fontSizeMax = .80f;
            title.gameObject.AddComponent<LocalizedText>().Configure(Object.FindAnyObjectByType<LocalizationService>(), kind == CargoKind.Oxygen ? "prep.oxygen.title" : "prep.battery.title");
            var instruction = Text(bench, "Preparation Instruction", header + new Vector3(0, -.10f, .021f * direction), face, new Vector2(1.42f, .13f), .26f, new Color(.65f, 1, .85f));
            instruction.textWrappingMode = TextWrappingModes.Normal; instruction.enableAutoSizing = true; instruction.fontSizeMin = .18f; instruction.fontSizeMax = .26f;
            instruction.gameObject.AddComponent<LocalizedText>().Configure(Object.FindAnyObjectByType<LocalizationService>(), "prep.charge.instruction");
            for (int i = 0; i < items.Length; i++)
            {
                float x = top.x - .49f + i * .49f;
                var item = items[i].Item;
                Place(item, new Vector3(x, top.y, top.z + .26f * direction));
                var rootPose = item.transform.position + Vector3.back * .53f * direction;
                var socketRoot = Frame(bench, name + " Port " + (i + 1), rootPose);
                var trigger = socketRoot.gameObject.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = .18f;
                var socket = socketRoot.gameObject.AddComponent<XRSocketInteractor>();
                socket.showInteractableHoverMeshes = false;
                var attach = Frame(socketRoot, "Dock Attach", Vector3.zero);
                if (item.Grab.attachTransform != null)
                    attach.SetPositionAndRotation(rootPose + item.Grab.attachTransform.position - item.transform.position, item.Grab.attachTransform.rotation);
                socket.attachTransform = attach;
                Prim(bench, "Dock Base", PrimitiveType.Cylinder, new Vector3(x, top.y + .018f, top.z - .27f * direction), new Vector3(.28f, .018f, .28f), "EX_Trim");
                Prim(bench, "Port Display Housing", PrimitiveType.Cube, new Vector3(x, top.y - .09f, top.z + .485f * direction), new Vector3(.44f, .18f, .02f), "EX_Trim");
                var label = Text(bench, "Port Status " + (i + 1), new Vector3(x, top.y - .09f, top.z + .50f * direction), face, new Vector2(.42f, .16f), .36f, Color.white);
                label.textWrappingMode = TextWrappingModes.Normal; label.enableAutoSizing = true; label.fontSizeMin = .18f; label.fontSizeMax = .28f;
                var lamp = Lamp(bench, "Port Lamp", new Vector3(x, top.y + .05f, top.z - .07f * direction), .03f);
                var port = socketRoot.gameObject.AddComponent<SupplyChargingDock>(); port.Configure(owner, kind, kind + "/" + i, label, lamp); ports.Add(port);
                if (kind == CargoKind.Oxygen)
                {
                    Prim(bench, "Oxygen Port Hose", PrimitiveType.Cylinder, new Vector3(x, top.y + .055f, top.z - .35f), new Vector3(.025f, .08f, .025f), "EX_Blue", Quaternion.Euler(90, 0, 0));
                    Prim(bench, "Port Coupler", PrimitiveType.Cylinder, new Vector3(x, top.y + .05f, top.z - .27f), new Vector3(.065f, .018f, .065f), "Safety Amber");
                }
                else
                    foreach (float dx in new[] { -.05f, .05f })
                        Prim(bench, "Charging Contact", PrimitiveType.Cube, new Vector3(x + dx, top.y + .042f, top.z + .27f), new Vector3(.025f, .012f, .10f), "EX_Copper");
            }
            if (kind == CargoKind.Oxygen)
            {
                Prim(bench, "Oxygen Manifold", PrimitiveType.Cylinder, new Vector3(-14.45f, top.y + .055f, top.z - .43f), new Vector3(.035f, .96f, .035f), "EX_Blue", Quaternion.Euler(0, 0, 90));
                Prim(bench, "Generator Connection", PrimitiveType.Cylinder, new Vector3(-13.5f, top.y + .055f, -.1f), new Vector3(.035f, .1f, .035f), "EX_Blue", Quaternion.Euler(90, 0, 0));
            }
            else
            {
                // 充电台面向工程舱中央，标牌靠窗，留出背后电路板的操作空间。
                var turn = Quaternion.Euler(0, -90, 0);
                bench.RotateAround(top, Vector3.up, -90);
                foreach (var entry in items)
                    entry.transform.SetPositionAndRotation(top + turn * (entry.transform.position - top), turn * entry.transform.rotation);
            }
        }
        private static LunarResearchStation BuildResearch(Transform parent, StationPreparation owner, SampleRack rack, CoreSample[] cores, PreparedCargo[] items)
        {
            var data = items.Where(c => c.Item.Kind == CargoKind.DataCore).ToArray();
            var soil = items.Where(c => c.Item.Kind == CargoKind.LunarSample).ToArray();
            for (int i = 0; i < data.Length; i++)
            {
                float x = -6.75f + i * .65f;
                foreach (float y in new[] { .82f, 1.40f })
                {
                    Prim(parent, "Research Output Tray", PrimitiveType.Cube, new Vector3(x, y - .025f, -4.12f), new Vector3(.52f, .05f, .47f), "EX_White", collider: true);
                    Prim(parent, "Tray Wall Bracket", PrimitiveType.Cube, new Vector3(x, y + .08f, -4.36f), new Vector3(.52f, .22f, .045f), "EX_Blue");
                }
                Place(data[i].Item, new Vector3(x, 1.40f, -4.07f)); Place(soil[i].Item, new Vector3(x, .82f, -4.07f));
            }
            var root = Frame(parent, "Lunar Sample Analyzer", new Vector3(-7.8f, 1.86f, -4.30f));
            Prim(root, "Analyzer Display Housing", PrimitiveType.Cube, Vector3.zero, new Vector3(1.48f, .60f, .075f), "EX_Trim");
            var text = Text(root, "Research Progress", new Vector3(0, 0, .042f), Quaternion.Euler(0, 180, 0), new Vector2(1.40f, .55f), .36f, new Color(.65f, 1f, .86f));
            text.enableAutoSizing = true; text.fontSizeMin = .38f; text.fontSizeMax = .65f; text.textWrappingMode = TextWrappingModes.Normal;
            var research = root.gameObject.AddComponent<LunarResearchStation>(); research.Configure(owner, rack, cores, data, soil, text);
            return research;
        }
    }
}
