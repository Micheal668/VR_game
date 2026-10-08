using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class BuildLifeSupportScene
    {
        // 只更新本次照明，保留使用者对场景、登舱按钮及设备位置的手动调整。
        public static void RefreshEvacuationAndShoulderLights()
        {
            EditorSceneManager.OpenScene(ScenePath);
            BuildRepairScene.RefreshFont();
            var session = Object.FindAnyObjectByType<StationMissionSession>();
            BuildShoulderLights(session);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("LIFE_SUPPORT_EVACUATION_AND_SHOULDER_LIGHTS_UPDATED");
        }

        private static void BuildShoulderLights(StationMissionSession session)
        {
            RemoveNamed(session.Player.Camera.transform, "Suit Helmet Lamp");
            var suit = session.Player.GetComponentsInChildren<TrackedCrewSuit>(true)
                .Single(t => t.name != "Player - Station Uniform").Suit;
            RemoveNamed(suit.transform, "Suit Shoulder Lamps");
            var rig = Child(suit.transform, "Suit Shoulder Lamps");
            var lights = new Light[2];
            for (int i = 0; i < lights.Length; i++)
            {
                var mount = Child(rig, i == 0 ? "Left Shoulder Lamp" : "Right Shoulder Lamp");
                // 绑定服装躯干而非头显或手臂，抬头、低头及举手时仍朝身体前方照明。
                mount.localPosition = new Vector3(i == 0 ? -.26f : .26f, 1.44f, .16f);
                Solid(mount, "Shoulder lamp housing", Vector3.zero, new Vector3(.075f,.075f,.11f), "LS_Graphite");
                Solid(mount, "Shoulder lamp lens", new Vector3(0,0,.058f), new Vector3(.056f,.056f,.008f), "LS_Light");
                var lamp = Child(mount, "Forward illumination").gameObject.AddComponent<Light>();
                lamp.transform.localPosition = new Vector3(0,0,.072f);
                lamp.type = LightType.Spot; lamp.range = 12; lamp.spotAngle = 65; lamp.innerSpotAngle = 38;
                lamp.intensity = 50; lamp.color = new Color(.8f,.91f,1);
                lamp.shadows = LightShadows.Soft; lamp.shadowStrength = .8f;
                lights[i] = lamp;
            }
            var environment = session.GetComponent<LifeSupportEnvironment>();
            environment.ConfigureShoulderLights(rig.gameObject, lights);
            EditorUtility.SetDirty(environment);
        }
    }
}
