using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class InstallStationExpansion
    {
        [MenuItem("Lunar Escape/Randomize Expanded Station Door Latches")]
        public static void InstallRandomDoorLatches()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs/LayoutRevisionBackup");
            string backup = "Logs/LayoutRevisionBackup/11-before-random-door-latches.unity";
            if (!File.Exists(backup)) File.Copy(BuildLifeSupportScene.ScenePath, backup);
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            session = Object.FindAnyObjectByType<StationMissionSession>();
            life = session.GetComponent<LifeSupportMission>();
            ConfigureRandomDoorLatches();
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("RANDOM_DOOR_LATCHES_INSTALLED: 8 positions / 3 bolts");
        }

        private static void ConfigureRandomDoorLatches()
        {
            var door = session.GetComponent<MissionEnvironment>().Door.transform;
            var skin = door.Find("Pressure Door Appearance");
            var holder = door.Find("Airlock Latch Bolts");
            var anchors = holder.Find("Eight Latch Positions");
            if (anchors == null) anchors = Frame(holder, "Eight Latch Positions", Vector3.zero);
            var positions = new Transform[8];
            for (int side = 0; side < 2; side++)
                for (int row = 0; row < 4; row++)
                {
                    int index = side * 4 + row;
                    string name = "Latch Position " + (index + 1);
                    var point = anchors.Find(name) ?? Frame(anchors, name, Vector3.zero);
                    // Pressure Door Appearance has metre-sized coordinates and
                    // the existing eight latch pads are spaced .54 m vertically.
                    point.SetPositionAndRotation(skin.TransformPoint(new Vector3(-.16f, -.82f + row * .54f, side == 0 ? -.63f : .63f)),
                        skin.rotation * Quaternion.Euler(0, -90, 0));
                    positions[index] = point;
                    EditorUtility.SetDirty(point);
                }
            foreach (var fault in life.AirlockRepair.Faults.OfType<LatchFault>())
            {
                fault.ConfigureRandomPositions(positions);
                EditorUtility.SetDirty(fault);
            }
        }
    }
}
