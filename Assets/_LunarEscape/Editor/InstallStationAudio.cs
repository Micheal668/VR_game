using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarEscape.Editor
{
    // 场景声音：舱外/真空闷音与航天服呼吸（挂在头显的听者上）、各舱段背景声、按剧情切换的背景音乐。
    // 第二天（场景 11）用本菜单安装；第一天（场景 12）由 Build Day One Scene 调用同一套方法。可重复执行。
    public static class InstallStationAudio
    {
        private const string AmbienceName = "Station Ambience";
        private const string MusicName = "Station Music";

        [MenuItem("Lunar Escape/Install Station Audio: Ambience, Vacuum, Music (Life Support Scene)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            var session = Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            var life = session.GetComponent<LifeSupportMission>();
            var flight = session.GetComponent<AscentMission>();
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            var breaker = Object.FindAnyObjectByType<HabitatBreaker>(FindObjectsInactive.Include);
            var voice = session.GetComponentInChildren<MissionVoice>(true);
            Apply(session.Player.Camera, ground, session.transform, breaker, life, flight, session.Mission, voice, null, null);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("STATION_AUDIO_INSTALLED");
        }

        public static void Apply(Camera head, Transform ambienceParent, Transform musicParent, HabitatBreaker breaker, LifeSupportMission life,
            AscentMission flight, StationMission mission, MissionVoice voice, DayOneMission dayOne, SuitLocker dayOneSuit)
        {
            var space = head.GetComponent<SpaceAudio>();
            if (space == null) space = head.gameObject.AddComponent<SpaceAudio>();
            space.Configure(life, flight, dayOne, dayOneSuit);
            EditorUtility.SetDirty(space);

            var old = ambienceParent.Find(AmbienceName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var ambience = new GameObject(AmbienceName).AddComponent<StationAmbience>();
            ambience.transform.SetParent(ambienceParent, false);
            ambience.Configure(Zones(), breaker, life);
            EditorUtility.SetDirty(ambience);

            old = musicParent.Find(MusicName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var music = new GameObject(MusicName).AddComponent<MusicDirector>();
            music.transform.SetParent(musicParent, false);
            music.Configure(mission, life, flight, voice, dayOne);
            EditorUtility.SetDirty(music);
            EditorSceneManager.MarkSceneDirty(head.gameObject.scene);
        }

        // 各舱段中心与声音（坐标取自扩建基地的房间布局）。
        private static StationAmbience.Zone[] Zones() => new[]
        {
            Zone("Lobby Ventilation", new Vector3(0f, 2.5f, 0f), FeedbackSound.VentLoop, 0.2f, 6.5f),
            Zone("Corridor Ventilation", new Vector3(-7.5f, 2.5f, 1.7f), FeedbackSound.VentLoop, 0.16f, 4.5f),
            Zone("Quarters Air", new Vector3(-7.8f, 2.6f, 5.4f), FeedbackSound.VentLoop, 0.09f, 4f),
            Zone("Laboratory Instruments", new Vector3(-7.8f, 1.3f, -2.6f), FeedbackSound.LabLoop, 0.2f, 4.5f),
            Zone("Engineering Power", new Vector3(-13.6f, 1.4f, 1.7f), FeedbackSound.HumLoop, 0.28f, 5f),
            Zone("Airlock Air", new Vector3(6f, 2.5f, 1.7f), FeedbackSound.VentLoop, 0.1f, 3.5f)
        };

        private static StationAmbience.Zone Zone(string name, Vector3 center, FeedbackSound sound, float volume, float reach)
            => new() { name = name, center = center, sound = sound, volume = volume, reach = reach };
    }
}
