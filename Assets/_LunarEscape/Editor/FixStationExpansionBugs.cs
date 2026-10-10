using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class InstallStationExpansion
    {
        private static readonly Vector3 BedroomControlPosition = new(-9.09f, 1.63f, 2.785f);

        [MenuItem("Lunar Escape/Fix Expanded Station Interactions")]
        public static void FixInteractions()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs/LayoutRevisionBackup");
            string backup = "Logs/LayoutRevisionBackup/11-before-expansion-interaction-fixes.unity";
            if (!File.Exists(backup)) File.Copy(BuildLifeSupportScene.ScenePath, backup);
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            session = Object.FindAnyObjectByType<StationMissionSession>(); life = session.GetComponent<LifeSupportMission>();
            ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            root = ground.Find("Expanded Station Story");
            materials = ImportStationExpansion.Read().materials.ToDictionary(m => m.name,
                m => AssetDatabase.LoadAssetAtPath<Material>(Art + "/Materials/" + m.name + ".mat"));
            RestoreBaseDoor();
            PlaceBedroomControl(root.Find("Bedroom Central Control"));
            ConfigureLaboratory(session.GetComponent<StationExpansionMission>().Laboratory);
            FlattenFoundation();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene); AssetDatabase.SaveAssets();
            Debug.Log("STATION_EXPANSION_INTERACTION_FIXES_INSTALLED");
        }

        private static void RestoreBaseDoor()
        {
            var door = session.GetComponent<MissionEnvironment>().Door;
            // LunarViewLayout.Door is the lander's hatch. This is the base pressure
            // door whose skin, bolts and colliders follow LifeSupport.DoorOpen.
            foreach (var renderer in door.GetComponentsInChildren<Renderer>(true))
            { renderer.enabled = renderer.gameObject != door; EditorUtility.SetDirty(renderer); }
            foreach (var collider in door.GetComponentsInChildren<Collider>(true))
            { collider.enabled = true; EditorUtility.SetDirty(collider); }
            door.SetActive(true); EditorUtility.SetDirty(door);
        }

        private static void PlaceBedroomControl(Transform panel)
        {
            panel.SetPositionAndRotation(BedroomControlPosition, Quaternion.identity);
            // World-space canvases serialize their XY position through the
            // RectTransform anchor. Update it explicitly so reload keeps the move.
            ((RectTransform)panel).anchoredPosition3D = panel.parent.InverseTransformPoint(BedroomControlPosition);
            panel.localScale = Vector3.one * .00098f;
            var old = root.Find("Bedroom Control Backplate"); if (old != null) Object.DestroyImmediate(old.gameObject);
            Primitive(root, "Bedroom Control Backplate", PrimitiveType.Cube,
                BedroomControlPosition + Vector3.forward * .033f, new Vector3(1.04f, .75f, .055f), "EX_Trim");
            EditorUtility.SetDirty(panel);
        }

        private static void ConfigureLaboratory(StationPatchPuzzle puzzle)
        {
            puzzle.transform.SetPositionAndRotation(new Vector3(-5.65f, 1.80f, -1.82f), Quaternion.identity);
            foreach (var module in puzzle.Modules)
            {
                // Table top is y=1.22. Put every loose module above it, clear of
                // socket triggers, then let ordinary gravity settle it onto the desk.
                module.transform.localPosition = new Vector3((module.Identity-1)*.48f, -.50f, .65f);
                module.transform.localRotation = Quaternion.identity;
                var body = module.GetComponent<Rigidbody>(); body.useGravity = true; body.isKinematic = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var shape = module.transform.Find("Module Body");
                shape.localScale = module.Identity == 2 ? new Vector3(.135f,.0675f,.12f) : new Vector3(.135f,.135f,.12f);
                EditorUtility.SetDirty(body); EditorUtility.SetDirty(module.transform); EditorUtility.SetDirty(shape);
            }
            foreach (var socket in puzzle.Sockets)
            {
                foreach (string name in new[] { "Socket Trim", "Socket Recess" })
                { var old=socket.transform.Find(name); if(old!=null)Object.DestroyImmediate(old.gameObject); }
                // Dark recesses remain visibly empty after a retry; the movable
                // copper modules are the only raised, filled shapes.
                var shape = Shape(socket.Identity);
                float height = socket.Identity == 2 ? .13f : .22f;
                Primitive(socket.transform,"Socket Trim",shape,Vector3.zero,new Vector3(.24f,height,.030f),"EX_White");
                Primitive(socket.transform,"Socket Recess",shape,new Vector3(0,0,.014f),new Vector3(.19f,height*.77f,.032f),"EX_Screen");
            }
            EditorUtility.SetDirty(puzzle.transform);
        }
    }
}
