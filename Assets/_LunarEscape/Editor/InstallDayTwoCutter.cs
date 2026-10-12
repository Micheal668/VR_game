using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LunarEscape.Editor.LunarToolBuilder;

namespace LunarEscape.Editor
{
    // 第二天（场景 11）：事故中坍塌的三根钢梁堵住了出口通道外端，必须用激光切割器沿红色切割线切断两端。
    // 切割器挂在通道南墙的支架上（泄压开门后才进得去）。同时在开场黑幕上叠加“第二天”标题。
    // 须在 Install Voice Hints and Decompression 之后执行；可重复执行。
    public static class InstallDayTwoCutter
    {
        private const string BlockadeName = "Exit Beam Blockade";
        private const string CutterRackName = "Laser Cutter Bracket";

        [MenuItem("Lunar Escape/Install Day Two: Exit Beams and Laser Cutter (Life Support Scene)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("DAY_TWO_CUTTER_INSTALLED");
        }

        [MenuItem("Lunar Escape/Clear Exit Beams (Play Mode)")]
        public static void ClearInPlayMode() { var b = UnityEngine.Object.FindAnyObjectByType<BeamBlockade>(); if (b != null) b.ClearInstantly(); }
        [MenuItem("Lunar Escape/Clear Exit Beams (Play Mode)", true)]
        private static bool CanClear() => Application.isPlaying && UnityEngine.Object.FindAnyObjectByType<BeamBlockade>() != null;

        public static void ApplyToCurrentScene()
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            var life = session.GetComponent<LifeSupportMission>();
            if (life == null) throw new InvalidOperationException("请先执行 Install Life Support and Cockpit。");
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            foreach (var name in new[] { BlockadeName, CutterRackName })
            {
                var old = ground.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            var (south, north) = MeasurePassage();
            var cutter = BuildCutterRack(ground, south);
            var blockade = BuildBeams(ground, south, north, session, cutter);
            var director = session.GetComponentInChildren<MissionVoiceDirector>(true);
            if (director != null) { director.ConfigureBlockade(blockade); EditorUtility.SetDirty(director); }
            BuildDayTitle(session);
            RussianByDefault();
            AddDesktopTrigger();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // 出口通道（x 4–8.1，沿 z 方向两侧墙）的内墙面位置：从通道中线向两侧打射线。
        private static (float south, float north) MeasurePassage()
        {
            Physics.SyncTransforms();
            float Cast(Vector3 direction, float fallback)
            {
                var origin = new Vector3(7.4f, 1.2f, 1.7f);
                return Physics.Raycast(origin, direction, out var hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.z : fallback;
            }
            float south = Cast(Vector3.back, 0.7f), north = Cast(Vector3.forward, 2.7f);
            Debug.Log($"DAY_TWO_PASSAGE south={south:0.000} north={north:0.000}");
            return (south, north);
        }

        private static LaserCutter BuildCutterRack(Transform ground, float southWall)
        {
            var rack = Frame(ground, CutterRackName, new Vector3(5.2f, 1.1f, southWall + 0.01f));
            Prim(rack, "Bracket Back", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.006f), new Vector3(0.42f, 0.3f, 0.012f), "Station Graphite");
            Prim(rack, "Bracket Shelf", PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.07f), new Vector3(0.42f, 0.015f, 0.13f), "Station Shell", collider: true);
            Prim(rack, "Bracket Lip", PrimitiveType.Cube, new Vector3(0f, -0.04f, 0.135f), new Vector3(0.42f, 0.03f, 0.008f), "Safety Amber");
            var label = Text(rack, "Cutter Label", new Vector3(0f, 0.19f, 0.014f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.4f, 0.06f), 0.32f, new Color(1f, 0.8f, 0.4f));
            var localized = label.gameObject.AddComponent<LocalizedText>();
            localized.Configure(UnityEngine.Object.FindAnyObjectByType<LocalizationService>(FindObjectsInactive.Include), "day2.cutter.label");
            var lamp = Lamp(rack, "Bracket Lamp", new Vector3(0.18f, 0.2f, 0.014f));
            lamp.State = LampState.Next;
            var cutter = BuildCutter(ground, Vector3.zero, Quaternion.identity);
            // 切割器横放在支架上，喷口朝出口方向（+X）。
            BuildHolster(rack, "Cutter Holster", new Vector3(0f, 0.01f, 0.07f), Quaternion.LookRotation(Vector3.right, Vector3.up), cutter);
            return cutter;
        }

        private static BeamBlockade BuildBeams(Transform ground, float south, float north, StationMissionSession session, LaserCutter cutter)
        {
            var root = Frame(ground, BlockadeName, new Vector3(7.75f, 0f, (south + north) * 0.5f));
            float s = south - 0.08f, n = north + 0.08f; // 两端插进墙里
            var beams = new[]
            {
                Beam(root, "Fallen Beam Low", new Vector3(7.72f, 0.55f, s), new Vector3(7.78f, 0.62f, n)),
                Beam(root, "Fallen Beam Diagonal", new Vector3(7.6f, 1.95f, s), new Vector3(7.62f, 0.85f, n)),
                Beam(root, "Fallen Beam High", new Vector3(7.88f, 1.42f, s), new Vector3(7.86f, 1.55f, n))
            };
            var blockade = root.gameObject.AddComponent<BeamBlockade>();
            blockade.Configure(beams, session.Mission, cutter, session.Exit.PlayerBody);
            foreach (var beam in beams) EditorUtility.SetDirty(beam);
            EditorUtility.SetDirty(blockade);
            return blockade;
        }

        // 一根工字钢：朝玩家（-X）的翼缘上，靠两端各画一条横跨梁高的红色切割线。
        private static CutBeam Beam(Transform parent, string name, Vector3 a, Vector3 b)
        {
            Vector3 along = (b - a).normalized, facing = Vector3.left;
            var up = Vector3.Cross(facing, along);
            var rotation = Quaternion.LookRotation(facing, up);
            if (Vector3.Dot(rotation * Vector3.right, along) < 0f) rotation = Quaternion.LookRotation(facing, -up);
            float length = Vector3.Distance(a, b);
            var beam = new GameObject(name).transform;
            beam.SetParent(parent, true);
            beam.SetPositionAndRotation((a + b) * 0.5f, rotation);
            Prim(beam, "Beam Web", PrimitiveType.Cube, Vector3.zero, new Vector3(length, 0.16f, 0.03f), "Station Graphite");
            Prim(beam, "Beam Front Flange", PrimitiveType.Cube, new Vector3(0f, 0f, 0.055f), new Vector3(length, 0.17f, 0.012f), "Station Shell");
            Prim(beam, "Beam Back Flange", PrimitiveType.Cube, new Vector3(0f, 0f, -0.055f), new Vector3(length, 0.17f, 0.012f), "Station Shell");
            // 不加警示条纹：红色应急灯下黄色条纹也发红，会和切割线混淆。只在梁面上留几颗深色螺栓。
            for (float x = -length * 0.5f + 0.55f; x < length * 0.5f - 0.5f; x += 0.4f)
                Prim(beam, "Beam Rivet", PrimitiveType.Cylinder, new Vector3(x, 0.05f, 0.063f), new Vector3(0.025f, 0.004f, 0.025f), "Station Graphite", Quaternion.Euler(90f, 0f, 0f));
            var collider = beam.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(length, 0.17f, 0.12f);
            var body = beam.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.mass = 40f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var cuts = new[] { Cut(beam, "Cut Line A", -length * 0.5f + 0.3f), Cut(beam, "Cut Line B", length * 0.5f - 0.3f) };
            var steel = beam.gameObject.AddComponent<CutBeam>();
            steel.Configure(cuts);
            return steel;
        }

        private static CutLine Cut(Transform beam, string name, float x)
        {
            var line = Frame(beam, name, new Vector3(x, 0f, 0.0625f));
            const int count = 6; const float height = 0.16f;
            var parts = new Renderer[count];
            for (int i = 0; i < count; i++)
            {
                float y = -height * 0.5f + height * (i + 0.5f) / count;
                parts[i] = Prim(line, "Cut Mark " + i, PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(0.03f, height / count * 0.8f, 0.003f), "Cockpit Lamp").GetComponent<Renderer>();
            }
            var cut = line.gameObject.AddComponent<CutLine>();
            cut.Configure(new Vector3(0f, -height * 0.5f, 0f), new Vector3(0f, height * 0.5f, 0f), parts);
            return cut;
        }

        // 俄语是主语言：场景默认俄语（玩家仍可在屏幕上切换中文与英文）。
        public static void RussianByDefault()
        {
            foreach (var language in UnityEngine.Object.FindObjectsByType<LocalizationService>(FindObjectsInactive.Include))
            {
                var serialized = new SerializedObject(language);
                serialized.FindProperty("currentLanguage").enumValueIndex = (int)GameLanguage.Russian;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 键鼠模拟器上挂扳机力度调节（- / = 键），在电脑上也能试半按扳机。
        public static void AddDesktopTrigger()
        {
            foreach (var simulator in UnityEngine.Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsInactive.Include))
                if (simulator.GetComponent<DesktopTriggerLevel>() == null) simulator.gameObject.AddComponent<DesktopTriggerLevel>();
        }

        // 第二天的标题：叠在剧情“睁眼”黑幕之上（黑幕本身由 StationExpansionMission 控制）。
        private static void BuildDayTitle(StationMissionSession session)
        {
            var camera = session.Player.Camera.transform;
            var canvas = camera.Find("Station Eye Effects");
            if (canvas == null) { Debug.LogWarning("没有找到 Station Eye Effects，跳过第二天标题。"); return; }
            foreach (var n in new[] { "Day Title", "Day Subtitle" }) { var old = canvas.Find(n); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject); }
            var fade = canvas.GetComponent<ScreenFade>();
            if (fade == null) fade = canvas.gameObject.AddComponent<ScreenFade>();
            var (title, subtitle) = TitleTexts(canvas);
            fade.Configure(null, title, subtitle, "day.title.2", "day.title.sub.2", false);
            EditorUtility.SetDirty(fade);
        }

        // 标题文字：头前 0.18 m 的画布上，字号按 1 单位 = 1 米换算。
        public static (TMP_Text title, TMP_Text subtitle) TitleTexts(Transform canvas)
        {
            TMP_Text Make(string name, float y, float size, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                go.layer = canvas.gameObject.layer;
                var text = go.AddComponent<TextMeshProUGUI>();
                text.font = StationFont; text.fontSize = size; text.color = color;
                text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
                text.rectTransform.sizeDelta = new Vector2(0.5f, 0.08f);
                text.rectTransform.anchoredPosition = new Vector2(0f, y);
                text.alpha = 0f; text.raycastTarget = false;
                return text;
            }
            return (Make("Day Title", 0.015f, 0.03f, new Color(0.92f, 0.96f, 1f)), Make("Day Subtitle", -0.02f, 0.011f, new Color(0.6f, 0.85f, 0.95f)));
        }
    }
}
