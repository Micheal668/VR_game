using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static LunarEscape.Editor.LunarToolBuilder;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    // 只更新共用基地的交互和布局；场景 12 仍由场景 11 生成。
    public static class InstallStationUsability
    {
        [MenuItem("Lunar Escape/Install Station Usability (Both Days)")]
        public static void Install()
        {
            ImportStationExpansion.Import();
            InstallStationPreparation.Install();
            Debug.Log("STATION_USABILITY_INSTALLED");
        }
        public static void Apply(StationMissionSession session)
        {
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            var story = ground.Find("Expanded Station Story");
            var collisions = story.Find("Architecture Collisions");
            string[] furniture = { "Science bench", "Bench drawer cabinet", "Lab terminal plinth", "Lab terminal worktop",
                "Sample sorting table", "Sample sorting pedestal", "Wall microscope shelf" };
            foreach (var child in collisions.Cast<Transform>().Where(t => furniture.Contains(t.name)).ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (var box in ImportStationExpansion.Read().colliders.Where(c => furniture.Contains(c.name)))
            {
                var part = Frame(collisions, box.name, ImportStationExpansion.Vector(box.position));
                part.gameObject.AddComponent<BoxCollider>().size = ImportStationExpansion.Vector(box.size);
            }
            var laboratory = session.GetComponent<StationExpansionMission>().Laboratory;
            laboratory.transform.SetPositionAndRotation(new Vector3(-5.075f, 1.80f, -1.55f), Quaternion.Euler(0, -90, 0));
            foreach (var module in laboratory.Modules)
            {
                module.transform.SetPositionAndRotation(new Vector3(-5.4f, 1.30f, -2.13f + module.Identity * .58f), Quaternion.Euler(0, -90, 0));
                var body = module.GetComponent<Rigidbody>(); body.useGravity = true; body.isKinematic = false;
                EditorUtility.SetDirty(module.transform); EditorUtility.SetDirty(body);
            }
            // 原分析桌上的小物品移到现有样品架，移除桌子后不再悬空。
            var details = ground.Find("Station Lived-in Details");
            if (details != null)
            {
                MoveDetail(details, "Test Tube Rack", new Vector3(-10.4f, 1.165f, -1.95f));
                MoveDetail(details, "Beaker", new Vector3(-10.4f, 1.235f, -.35f));
                MoveDetail(details, "Beaker Liquid", new Vector3(-10.4f, 1.210f, -.35f));
            }
            var player = session.Player;
            foreach (var input in player.GetComponentsInChildren<XRBaseInputInteractor>(true))
            {
                var placement = input.GetComponent<RemoteSocketPlacement>() ?? input.gameObject.AddComponent<RemoteSocketPlacement>();
                var old = input.transform.Find("Placement Guide"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var root = Frame(input.transform, "Placement Guide", Vector3.zero);
                var line = root.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.widthMultiplier = .006f;
                line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_LunarEscape/Art/LifeSupport/Materials/LS_RouteChevron.mat");
                line.startColor = line.endColor = new Color(.25f, 1, .6f); line.numCapVertices = 4; line.enabled = false;
                var hint = Text(root, "Placement Hint", Vector3.zero, Quaternion.identity, new Vector2(.65f, .14f), .35f, new Color(.4f, 1, .65f));
                hint.enableAutoSizing = true; hint.fontSizeMin = .22f; hint.fontSizeMax = .35f;
                hint.gameObject.SetActive(false); placement.Configure(line, hint);
            }
            foreach (var panel in player.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Life Support Wrist Controls"))
            {
                var hide = panel.GetComponent<LookDownDisplay>(); if (hide != null) Object.DestroyImmediate(hide);
                panel.localScale = Vector3.one * .00034f;
                (panel.GetComponent<ReadableWristDisplay>() ?? panel.gameObject.AddComponent<ReadableWristDisplay>()).Configure(player.Camera, panel.parent);
                (panel.GetComponent<HelmetHudOverlay>() ?? panel.gameObject.AddComponent<HelmetHudOverlay>()).Configure(
                    Shader.Find("LunarEscape/Visor UI Overlay"), Shader.Find("TextMeshPro/Mobile/Distance Field Overlay"));
            }
            foreach (var fault in ground.GetComponentsInChildren<FuseFault>(true))
            {
                fault.Socket.enabled = true;
                var serialized = new SerializedObject(fault.Socket);
                serialized.FindProperty("m_StartingSelectedInteractable").objectReferenceValue = fault.Burnt.Grab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Physics.SyncTransforms();
        }
        private static void MoveDetail(Transform root, string name, Vector3 position)
        {
            var detail = root.Find(name);
            if (detail != null) detail.position = position;
        }
    }
}
