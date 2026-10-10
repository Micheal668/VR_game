using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LunarEscape.Editor
{
    public static class InspectStationExpansion
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            var session = Object.FindAnyObjectByType<StationMissionSession>();
            var root = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            var result = new StringBuilder();
            string Path(Transform t) => t == null || t == root ? "" : Path(t.parent) + "/" + t.name;
            var env=session.GetComponent<MissionEnvironment>();
            var layout=session.GetComponent<LunarViewLayout>();
            result.AppendLine("ENV DOOR "+Path(env.Door.transform)+" active="+env.Door.activeInHierarchy+" pos="+env.Door.transform.position);
            result.AppendLine("LAYOUT DOOR "+Path(layout.Door)+" active="+layout.Door.gameObject.activeInHierarchy+" pos="+layout.Door.position);
            foreach(var r in env.Door.GetComponentsInChildren<Renderer>(true))
                result.AppendLine("DOOR RENDER "+Path(r.transform)+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+" bounds="+r.bounds);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Breccia") || t.name.Contains("Rim") || t.name.Contains("Glyph")) continue;
                bool wanted = t.parent == root || t.GetComponent<Collider>() != null || t.GetComponent<Canvas>() != null
                    || t.name.Contains("Realistic") || t.name.Contains("Commander") || t.name.Contains("Oxygen Unit") || t.name.Contains("Repair Point");
                if (!wanted || t.position.x < -30 || t.position.x > 12 || Mathf.Abs(t.position.z) > 15) continue;
                result.AppendLine(Path(t) + " | " + t.position.ToString("F2") + " | " + t.lossyScale.ToString("F2")
                    + " | " + (t.GetComponent<Collider>() != null ? t.GetComponent<Collider>().GetType().Name + " " + t.GetComponent<Collider>().bounds : ""));
            }
            File.WriteAllText("Logs/expansion-scene-inventory.txt", result.ToString());
            Debug.Log("EXPANSION_SCENE_INSPECTED");
        }
    }
}
