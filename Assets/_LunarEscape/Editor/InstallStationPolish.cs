using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LunarEscape.Editor.LunarToolBuilder;

namespace LunarEscape.Editor
{
    // 基地的视觉打磨（场景 11；第一天场景复制自它，一并继承）：
    //   · 总闸标签加深色背板：从背面看不再是镜像字；
    //   · 舱顶补光下移、减弱、照得更远，天花板上不再有刺眼的白斑；
    //   · 各舱段的生活与工作细节：卧室的杯子、书、平板和洗衣袋，实验室的试管架与样品罐，
    //     工程舱的管线、阀门和电缆槽，大厅的水培架与挂钟；舱外的闪烁信标与任务旗；
    //   · 指挥官走路时有起伏和摆臂。
    // 装饰件不带碰撞体；位置按家具碰撞体的实际表面测量。可重复执行。
    public static class InstallStationPolish
    {
        private const string PropsName = "Station Lived-in Details";
        private const string ExteriorName = "Station Exterior Details";

        [MenuItem("Lunar Escape/Install Station Polish: Props, Lights, Commander Walk (Life Support Scene)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("STATION_POLISH_INSTALLED");
        }

        public static void ApplyToCurrentScene()
        {
            var session = Object.FindAnyObjectByType<StationMissionSession>(FindObjectsInactive.Include);
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            foreach (var name in new[] { PropsName, ExteriorName }) { var old = ground.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); }
            Physics.SyncTransforms();
            BackBreakerLabel(ground);
            SoftenCeilingLights(ground);
            BuildProps(Frame(ground, PropsName, Vector3.zero));
            BuildExterior(Frame(ground, ExteriorName, Vector3.zero));
            var crew = session.GetComponent<CrewMission>();
            if (crew != null && crew.Commander != null) AddWalk(crew.Commander);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static void AddWalk(Transform commander)
        {
            var walk = commander.GetComponent<CrewWalkMotion>();
            if (walk == null) walk = commander.gameObject.AddComponent<CrewWalkMotion>();
            walk.Configure(commander.GetComponentsInChildren<CrewSuitRig>(true));
            EditorUtility.SetDirty(walk);
            if (PrefabUtility.IsPartOfPrefabInstance(commander)) PrefabUtility.RecordPrefabInstancePropertyModifications(walk);
        }

        private static void BackBreakerLabel(Transform ground)
        {
            var label = ground.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Breaker Label");
            if (label == null) return;
            var old = label.parent.Find("Breaker Label Backplate");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var plate = Prim(label.parent, "Breaker Label Backplate", PrimitiveType.Cube, label.localPosition + new Vector3(0f, 0f, -0.012f), new Vector3(0.32f, 0.13f, 0.01f), "Station Graphite");
            plate.transform.localRotation = Quaternion.identity;
        }

        private static void SoftenCeilingLights(Transform ground)
        {
            foreach (var light in ground.GetComponentsInChildren<Light>(true).Where(l => l.name == "Expansion Ceiling Fill"))
            {
                var p = light.transform.position;
                light.transform.position = new Vector3(p.x, 2.35f, p.z);
                light.intensity = 3.4f; light.range = 6.5f;
                EditorUtility.SetDirty(light);
            }
        }

        // 家具表面高度：自上而下打射线，命中家具碰撞体的顶面。
        private static float Top(float x, float z, float fallback)
        {
            return Physics.Raycast(new Vector3(x, 2.4f, z), Vector3.down, out var hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;
        }

        private static void BuildProps(Transform root)
        {
            // —— 卧室 ——
            float c0 = Top(-9.1f, 6.82f, 0.86f), c1 = Top(-6.85f, 6.82f, 0.86f), store = Top(-10.5f, 3.65f, 0.96f);
            Mug(root, "Bedside Mug", new Vector3(-9.12f, c0, 6.8f), "Cockpit Red");
            Books(root, "Bedside Books", new Vector3(-8.72f, c0, 6.92f));
            Mug(root, "Commander Mug", new Vector3(-6.88f, c1, 6.8f), "Guide Teal");
            Prim(root, "Commander Tablet", PrimitiveType.Cube, new Vector3(-6.5f, c1 + 0.006f, 6.86f), new Vector3(0.2f, 0.012f, 0.14f), "EX_Screen", Quaternion.Euler(0f, 15f, 0f));
            Prim(root, "Laundry Bag", PrimitiveType.Sphere, new Vector3(-10.48f, store + 0.12f, 3.62f), new Vector3(0.38f, 0.26f, 0.32f), "EX_Fabric");
            var patch = Frame(root, "Mission Patch Poster", new Vector3(-10.68f, 1.75f, 4.6f), Quaternion.Euler(0f, 90f, 0f));
            Prim(patch, "Poster Frame", PrimitiveType.Cube, Vector3.zero, new Vector3(0.62f, 0.62f, 0.02f), "EX_Trim");
            Prim(patch, "Patch Disc", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.012f), new Vector3(0.5f, 0.004f, 0.5f), "EX_Blue", Quaternion.Euler(90f, 0f, 0f));
            Prim(patch, "Patch Moon", PrimitiveType.Cylinder, new Vector3(0.08f, 0.06f, 0.016f), new Vector3(0.2f, 0.003f, 0.2f), "Panel White", Quaternion.Euler(90f, 0f, 0f));
            Prim(patch, "Patch Ring", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.011f), new Vector3(0.54f, 0.002f, 0.54f), "Safety Amber", Quaternion.Euler(90f, 0f, 0f));
            var name = Text(patch, "Patch Name", new Vector3(0f, -0.17f, 0.02f), Quaternion.identity, new Vector2(0.5f, 0.08f), 0.4f, Color.white);
            name.text = "SELENA-3";
            name.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // —— 实验室 ——
            float bench = Top(-9.0f, -3.85f, 0.92f), shelf = Top(-10.4f, -1.1f, 1.12f);
            var rack = Frame(root, "Test Tube Rack", new Vector3(-9.05f, bench, -3.82f));
            Prim(rack, "Rack Base", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.28f, 0.04f, 0.08f), "Panel White");
            string[] liquids = { "Guide Teal", "Safety Amber", "Cockpit Red", "Guide Teal", "EX_Green" };
            for (int i = 0; i < liquids.Length; i++)
            {
                float x = -0.1f + i * 0.05f;
                Prim(rack, "Tube Glass", PrimitiveType.Cylinder, new Vector3(x, 0.09f, 0f), new Vector3(0.022f, 0.065f, 0.022f), "EX_Window");
                Prim(rack, "Tube Liquid", PrimitiveType.Cylinder, new Vector3(x, 0.065f, 0f), new Vector3(0.018f, 0.04f, 0.018f), liquids[i]);
            }
            Prim(root, "Beaker", PrimitiveType.Cylinder, new Vector3(-8.65f, bench + 0.07f, -3.88f), new Vector3(0.09f, 0.07f, 0.09f), "EX_Window");
            Prim(root, "Beaker Liquid", PrimitiveType.Cylinder, new Vector3(-8.65f, bench + 0.045f, -3.88f), new Vector3(0.082f, 0.045f, 0.082f), "Guide Teal");
            for (int i = 0; i < 4; i++)
                Jar(root, "Sample Jar", new Vector3(-10.4f, shelf, -1.4f + i * 0.2f), i % 2 == 0 ? "EX_Copper" : "Station Graphite");

            // —— 工程舱：南墙两根管线（铜、蓝）带阀门，北墙电缆槽，发电机前的地面警示条 ——
            for (int p = 0; p < 2; p++)
            {
                float y = 2.55f + p * 0.18f;
                Prim(root, "Engineering Pipe", PrimitiveType.Cylinder, new Vector3(-13.6f, y, -1.0f), new Vector3(0.09f - p * 0.02f, 2.25f, 0.09f - p * 0.02f), p == 0 ? "EX_Copper" : "EX_Blue", Quaternion.Euler(0f, 0f, 90f));
                for (float x = -15.4f; x < -11.6f; x += 1.2f)
                    Prim(root, "Pipe Clamp", PrimitiveType.Cube, new Vector3(x, y, -1.02f), new Vector3(0.04f, 0.13f, 0.12f), "EX_Trim");
            }
            foreach (float x in new[] { -14.7f, -12.5f })
            {
                Prim(root, "Valve Stem", PrimitiveType.Cylinder, new Vector3(x, 2.55f, -0.92f), new Vector3(0.025f, 0.05f, 0.025f), "EX_Trim", Quaternion.Euler(90f, 0f, 0f));
                Prim(root, "Valve Wheel", PrimitiveType.Cylinder, new Vector3(x, 2.55f, -0.86f), new Vector3(0.16f, 0.01f, 0.16f), "Cockpit Red", Quaternion.Euler(90f, 0f, 0f));
            }
            Prim(root, "Cable Tray", PrimitiveType.Cube, new Vector3(-13.6f, 2.9f, 4.4f), new Vector3(4.4f, 0.04f, 0.25f), "EX_Trim");
            foreach (var (z, mat) in new[] { (4.36f, "EX_CircuitRED"), (4.42f, "EX_CircuitYELLOW"), (4.48f, "EX_CircuitBLUE") })
                Prim(root, "Tray Cable", PrimitiveType.Cylinder, new Vector3(-13.6f, 2.94f, z), new Vector3(0.025f, 2.15f, 0.025f), mat, Quaternion.Euler(0f, 0f, 90f));
            for (int i = 0; i < 7; i++)
                Prim(root, "Floor Hazard Stripe", PrimitiveType.Cube, new Vector3(-14.6f + i * 0.3f, 0.004f, 0.3f), new Vector3(0.14f, 0.004f, 0.8f), i % 2 == 0 ? "Safety Amber" : "Station Graphite", Quaternion.Euler(0f, 30f, 0f));

            // —— 大厅：水培架与挂钟 ——
            var planter = Frame(root, "Hydroponic Planter", new Vector3(-3.25f, 0f, -3.45f));
            Prim(planter, "Planter Box", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(1.0f, 0.5f, 0.42f), "EX_White");
            Prim(planter, "Soil", PrimitiveType.Cube, new Vector3(0f, 0.505f, 0f), new Vector3(0.92f, 0.01f, 0.36f), "Station Graphite");
            var random = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                float x = -0.38f + i * 0.095f, s = 0.09f + (float)random.NextDouble() * 0.07f;
                Prim(planter, "Lettuce", PrimitiveType.Sphere, new Vector3(x, 0.53f + s * 0.3f, (float)random.NextDouble() * 0.16f - 0.08f), new Vector3(s, s * 0.7f, s), "EX_Green");
            }
            foreach (float x in new[] { -0.45f, 0.45f })
                Prim(planter, "Grow Light Post", PrimitiveType.Cube, new Vector3(x, 0.95f, -0.15f), new Vector3(0.03f, 0.9f, 0.03f), "EX_Trim");
            Prim(planter, "Grow Light", PrimitiveType.Cube, new Vector3(0f, 1.38f, -0.05f), new Vector3(0.96f, 0.03f, 0.12f), "EX_Light");
            var clock = Frame(root, "Wall Clock", new Vector3(3.35f, 2.55f, 3.88f));
            Prim(clock, "Clock Face", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.34f, 0.01f, 0.34f), "Panel White", Quaternion.Euler(90f, 0f, 0f));
            Prim(clock, "Clock Rim", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.006f), new Vector3(0.37f, 0.008f, 0.37f), "EX_Trim", Quaternion.Euler(90f, 0f, 0f));
            Prim(clock, "Hour Hand", PrimitiveType.Cube, new Vector3(0.03f, 0.03f, -0.008f), new Vector3(0.015f, 0.09f, 0.004f), "Station Graphite", Quaternion.Euler(0f, 0f, -45f));
            Prim(clock, "Minute Hand", PrimitiveType.Cube, new Vector3(0f, 0.06f, -0.01f), new Vector3(0.01f, 0.13f, 0.004f), "Station Graphite");
        }

        private static void Mug(Transform root, string name, Vector3 bottom, string material)
        {
            var mug = Frame(root, name, bottom);
            Prim(mug, "Mug Body", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.08f, 0.05f, 0.08f), material);
            Prim(mug, "Mug Handle", PrimitiveType.Cube, new Vector3(0.05f, 0.05f, 0f), new Vector3(0.02f, 0.06f, 0.012f), material);
        }

        private static void Books(Transform root, string name, Vector3 bottom)
        {
            var stack = Frame(root, name, bottom, Quaternion.Euler(0f, 12f, 0f));
            string[] covers = { "EX_Blue", "Cockpit Red", "Safety Amber" };
            for (int i = 0; i < covers.Length; i++)
                Prim(stack, "Book", PrimitiveType.Cube, new Vector3(0f, 0.015f + i * 0.03f, 0f), new Vector3(0.17f - i * 0.015f, 0.028f, 0.12f), covers[i], Quaternion.Euler(0f, i * 8f, 0f));
        }

        private static void Jar(Transform root, string name, Vector3 bottom, string lid)
        {
            var jar = Frame(root, name, bottom);
            Prim(jar, "Jar Glass", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.09f, 0.06f, 0.09f), "EX_Window");
            Prim(jar, "Jar Regolith", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), new Vector3(0.082f, 0.035f, 0.082f), "Lunar Soil");
            Prim(jar, "Jar Lid", PrimitiveType.Cylinder, new Vector3(0f, 0.125f, 0f), new Vector3(0.095f, 0.008f, 0.095f), lid);
        }

        // —— 舱外：四角闪烁信标、出口旁的任务旗 ——
        private static void BuildExterior(Transform root)
        {
            float Ground(float x, float z) => Physics.Raycast(new Vector3(x, 20f, z), Vector3.down, out var hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
            foreach (var p in new[] { new Vector2(8.7f, 0.2f), new Vector2(8.7f, 3.2f), new Vector2(-16.6f, -1.8f), new Vector2(-16.6f, 5.2f) })
            {
                var post = Frame(root, "Perimeter Beacon", new Vector3(p.x, Ground(p.x, p.y), p.y));
                Prim(post, "Beacon Post", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.06f, 0.5f, 0.06f), "EX_Trim");
                var lamp = Lamp(post, "Beacon Lamp", new Vector3(0f, 1.04f, 0f), 0.09f);
                lamp.State = LampState.Next;
            }
            var flagRoot = Frame(root, "Mission Flag", new Vector3(10.3f, Ground(10.3f, 3.9f), 3.9f), Quaternion.Euler(0f, -20f, 0f));
            Prim(flagRoot, "Flag Pole", PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0f), new Vector3(0.035f, 1.1f, 0.035f), "Station Shell");
            Prim(flagRoot, "Flag Crossbar", PrimitiveType.Cylinder, new Vector3(0.45f, 2.15f, 0f), new Vector3(0.02f, 0.45f, 0.02f), "Station Shell", Quaternion.Euler(0f, 0f, 90f));
            Prim(flagRoot, "Flag Cloth", PrimitiveType.Cube, new Vector3(0.45f, 1.85f, 0f), new Vector3(0.9f, 0.58f, 0.006f), "EX_Blue");
            Prim(flagRoot, "Flag Stripe", PrimitiveType.Cube, new Vector3(0.45f, 1.65f, 0f), new Vector3(0.9f, 0.08f, 0.008f), "Safety Amber");
            foreach (var (z, rot) in new[] { (-0.006f, 0f), (0.006f, 180f) })
            {
                var text = Text(flagRoot, "Flag Text", new Vector3(0.45f, 1.92f, z), Quaternion.Euler(0f, rot, 0f), new Vector2(0.85f, 0.2f), 1.1f, Color.white);
                text.text = "SELENA-3";
            }
        }
    }
}
