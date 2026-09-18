using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape.Editor
{
    // 仅由 07 场景生成器调用。月面、舱体和远山均保存为可编辑场景对象。
    // 任务状态与传送权限由调用方绑定；本类不修改相机位姿、时间或重力。
    public static class BuildLunarSurface
    {
        private const string AssetsRoot = "Assets/_LunarEscape";
        public const string SurfaceRootName = "Lunar Surface - bounded blockout";
        public const string CabinRootName = "Ascent Cabin - interactive blockout";
        public const string BoardingZoneName = "Ascent Boarding Zone";

        // 舱内东墙上的可用面板位置，朝向从西门进入的玩家。
        public static Vector3 CabinPanelPosition => new Vector3(25.82f, 1.75f, 1.7f);
        public static Quaternion CabinPanelRotation => Quaternion.Euler(0, 90, 0);
        public static Vector3 BoardingCenter => new Vector3(24, 1.2f, 1.7f);

        public static EvacuationZone Build(CharacterController body, LocalizedPanelBuilder ui,
            out TeleportationArea[] routeAreas)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (GameObject.Find(SurfaceRootName) != null || GameObject.Find(CabinRootName) != null)
                throw new InvalidOperationException("月面或上升器已经存在，拒绝重复生成。");

            Remove("Corridor End");
            Remove("Exit Mission Panel");
            Remove("Assembly Floor Marker");
            Remove("Evacuation Assembly Zone");

            var shell = ExistingMaterial("Station Shell");
            var dark = ExistingMaterial("Station Graphite");
            var teal = ExistingMaterial("Guide Teal");
            var amber = ExistingMaterial("Safety Amber");
            var white = ExistingMaterial("Panel White");
            var soil = CachedMaterial("Lunar Soil", new Color(0.37f, 0.38f, 0.36f));
            var route = CachedMaterial("Lunar Route Soil", new Color(0.43f, 0.44f, 0.42f));
            var rock = CachedMaterial("Lunar Rock", new Color(0.26f, 0.28f, 0.28f));
            var horizon = CachedMaterial("Lunar Horizon", new Color(0.3f, 0.32f, 0.33f));

            var surface = new GameObject(SurfaceRootName).transform;
            var cabin = new GameObject(CabinRootName).transform;
            BuildGround(surface, soil, rock, horizon);
            BuildRoute(surface, ui, route, dark, teal, amber);
            BuildCabin(cabin, ui, shell, dark, teal, white);
            BuildDarkSky();

            routeAreas = new[]
            {
                TeleportSurface("Lunar Route Teleport Surface", surface,
                    new Vector3(14, 0, 1.7f), new Vector2(11.2f, 1.2f), dark),
                TeleportSurface("Cabin Teleport Surface", cabin,
                    new Vector3(23, 0, 1.7f), new Vector2(5.1f, 3.6f), dark)
            };

            Box("Boarding Floor Marker", cabin, new Vector3(24, 0.014f, 1.7f),
                new Vector3(1.4f, 0.015f, 2.8f), teal, false);
            var zoneObject = new GameObject(BoardingZoneName);
            zoneObject.transform.SetParent(cabin);
            zoneObject.transform.position = BoardingCenter;
            var volume = zoneObject.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(1.4f, 2.4f, 2.8f);
            var zone = zoneObject.AddComponent<EvacuationZone>();
            zone.Configure(volume, body);
            return zone;
        }

        private static void BuildGround(Transform root, Material soil, Material rock, Material horizon)
        {
            // 只有这块约 30 x 30 m 的地面参与物理；远山只提供舷窗和室外背景。
            Box("Lunar Ground Slab", root, new Vector3(17, -0.22f, 1.7f),
                new Vector3(30, 0.4f, 30), soil);
            MeshObject("Distant Lunar Horizon", root, new Vector3(17, 0, 1.7f), Vector3.one,
                CachedHorizonMesh(), horizon, false);

            var positions = new[]
            {
                new Vector3(9.3f, -0.02f, -1.2f), new Vector3(11.2f, -0.02f, 4.7f),
                new Vector3(13.4f, -0.02f, -2.6f), new Vector3(15.4f, -0.02f, 5.2f),
                new Vector3(17.7f, -0.02f, -1.7f), new Vector3(19, -0.02f, 4.9f),
                new Vector3(22, -0.02f, -4.4f), new Vector3(25.7f, -0.02f, 7.3f),
                new Vector3(27.8f, -0.02f, -2.2f), new Vector3(29.2f, -0.02f, 3.8f),
                new Vector3(6.8f, -0.02f, -6), new Vector3(9, -0.02f, 9),
                new Vector3(16.8f, -0.02f, 11), new Vector3(20.5f, -0.02f, -9)
            };
            var mesh = CachedRockMesh();
            for (int i = 0; i < positions.Length; i++)
            {
                float size = 0.7f + (i % 4) * 0.36f;
                var obj = MeshObject("Lunar Rock " + (i + 1), root, positions[i],
                    new Vector3(size * 1.4f, size, size), mesh, rock, true);
                obj.transform.rotation = Quaternion.Euler(0, (i * 73) % 360, 0);
            }
        }

        private static void BuildRoute(Transform root, LocalizedPanelBuilder ui, Material route,
            Material dark, Material teal, Material amber)
        {
            Box("Lunar Route Floor", root, new Vector3(14, -0.08f, 1.7f),
                new Vector3(12, 0.16f, 2.2f), route);
            foreach (float z in new[] { 0.65f, 2.75f })
            {
                Box("Lunar Route Safety Rail", root, new Vector3(14, 1.08f, z),
                    new Vector3(12, 0.09f, 0.09f), dark);
                Box("Lunar Route Kerb", root, new Vector3(14, 0.09f, z),
                    new Vector3(12, 0.18f, 0.14f), dark);
                Box("Lunar Route Guidance", root, new Vector3(14, 0.195f, z),
                    new Vector3(12, 0.022f, 0.06f), teal, false);
                foreach (float x in new[] { 8.25f, 10.5f, 12.8f, 15.1f, 17.4f, 19.7f })
                {
                    Box("Lunar Route Rail Post", root, new Vector3(x, 0.57f, z),
                        new Vector3(0.09f, 1.14f, 0.09f), dark);
                    Box("Lunar Route Post Beacon", root, new Vector3(x, 1.18f, z),
                        new Vector3(0.12f, 0.09f, 0.12f), amber, false);
                }
            }

            // 视线可越过护栏看见月面，身体不能离开切片范围绕到舱体背后。
            foreach (float z in new[] { 0.5f, 2.9f })
            {
                var barrier = Box("Lunar Route Boundary", root, new Vector3(14, 2, z),
                    new Vector3(12.2f, 4, 0.16f), dark);
                barrier.GetComponent<Renderer>().enabled = false;
            }
            for (int i = 0; i < 3; i++)
                FloorArrow(root, new Vector3(10.5f + 3 * i, 0.012f, 1.7f), teal);

            Box("Lunar Route Sign Backing", root, new Vector3(8.4f, 2.85f, 1.7f),
                new Vector3(0.08f, 0.48f, 1.95f), dark, false);
            Label(ui, root, "Lunar Route Sign", "cargo.route", new Vector3(8.34f, 2.85f, 1.7f),
                new Vector2(1.85f, 0.42f), 0.48f, Quaternion.Euler(0, 90, 0));
        }

        private static void BuildCabin(Transform root, LocalizedPanelBuilder ui, Material shell,
            Material dark, Material teal, Material white)
        {
            Box("Ascent Cabin Floor", root, new Vector3(23, -0.1f, 1.7f),
                new Vector3(6.2f, 0.2f, 4.8f), dark);
            Box("Ascent Cabin Roof", root, new Vector3(23, 3.4f, 1.7f),
                new Vector3(6.2f, 0.2f, 4.8f), shell);
            Box("Ascent Cabin End Wall", root, new Vector3(26, 1.65f, 1.7f),
                new Vector3(0.2f, 3.3f, 4.8f), shell);

            // 西侧只在 z=0.8..2.6、y=0..2.5 留下真实门洞。
            Box("Ascent Cabin Entry South", root, new Vector3(20, 1.65f, 0.1f),
                new Vector3(0.2f, 3.3f, 1.4f), shell);
            Box("Ascent Cabin Entry North", root, new Vector3(20, 1.65f, 3.3f),
                new Vector3(0.2f, 3.3f, 1.4f), shell);
            Box("Ascent Cabin Entry Lintel", root, new Vector3(20, 2.9f, 1.7f),
                new Vector3(0.2f, 0.8f, 1.8f), shell);
            Box("Ascent Cabin Entry Beacon", root, new Vector3(19.88f, 2.57f, 1.7f),
                new Vector3(0.05f, 0.055f, 1.8f), teal, false);
            Label(ui, root, "Ascent Boarding Sign", "cargo.boarding", new Vector3(19.86f, 2.92f, 1.7f),
                new Vector2(1.72f, 0.44f), 0.46f, Quaternion.Euler(0, 90, 0));

            BuildWindowWall(root, "South", -0.6f, shell, dark);
            BuildWindowWall(root, "North", 4, shell, dark);
            Label(ui, root, "Cabin Window Sign", "cargo.window", new Vector3(23.5f, 2.92f, 3.86f),
                new Vector2(2.8f, 0.3f), 0.43f, Quaternion.identity);

            foreach (float z in new[] { 0.05f, 3.35f })
                Box("Cabin Ceiling Light Strip", root, new Vector3(23, 3.27f, z),
                    new Vector3(5.5f, 0.035f, 0.12f), white, false);
            var light = new GameObject("Cabin Interior Light").AddComponent<Light>();
            light.transform.SetParent(root);
            light.transform.position = new Vector3(23, 2.85f, 1.7f);
            light.type = LightType.Point;
            light.range = 7;
            light.intensity = 4;
            light.color = new Color(0.76f, 0.89f, 1);
            light.shadows = LightShadows.None;

            foreach (float z in new[] { 0.93f, 2.47f })
                Box("Cabin Floor Guidance", root, new Vector3(22, 0.012f, z),
                    new Vector3(3.8f, 0.015f, 0.045f), teal, false);
        }

        private static void BuildWindowWall(Transform root, string side, float z,
            Material shell, Material dark)
        {
            // 窗洞：x=22..25，y=1.05..2.55。窗片只保留碰撞，保证真实视线穿透。
            Box("Cabin " + side + " Lower Wall", root, new Vector3(23, 0.525f, z),
                new Vector3(6, 1.05f, 0.2f), shell);
            Box("Cabin " + side + " Upper Wall", root, new Vector3(23, 2.925f, z),
                new Vector3(6, 0.75f, 0.2f), shell);
            Box("Cabin " + side + " Entry Pillar", root, new Vector3(21, 1.8f, z),
                new Vector3(2, 1.5f, 0.2f), shell);
            Box("Cabin " + side + " End Pillar", root, new Vector3(25.5f, 1.8f, z),
                new Vector3(1, 1.5f, 0.2f), shell);
            foreach (float y in new[] { 1.05f, 2.55f })
                Box("Cabin " + side + " Window Horizontal Frame", root, new Vector3(23.5f, y, z),
                    new Vector3(3.12f, 0.09f, 0.28f), dark);
            foreach (float x in new[] { 22, 25 })
                Box("Cabin " + side + " Window Vertical Frame", root, new Vector3(x, 1.8f, z),
                    new Vector3(0.09f, 1.58f, 0.28f), dark);
            var pane = Box("Cabin " + side + " Clear Window Pane", root, new Vector3(23.5f, 1.8f, z),
                new Vector3(3, 1.5f, 0.06f), dark);
            pane.GetComponent<Renderer>().enabled = false;
        }

        private static TeleportationArea TeleportSurface(string name, Transform parent,
            Vector3 center, Vector2 size, Material material)
        {
            var surface = Box(name, parent, center + Vector3.up * 0.001f,
                new Vector3(size.x, 0.002f, size.y), material);
            surface.GetComponent<Renderer>().enabled = false;
            var area = surface.AddComponent<TeleportationArea>();
            area.interactionLayers = 1 << 31;
            area.enabled = false; // 调用方只能在 Evacuation 阶段打开，失败后再次关闭。
            return area;
        }

        private static void FloorArrow(Transform parent, Vector3 center, Material material)
        {
            Box("Lunar Route Arrow Stem", parent, center, new Vector3(0.75f, 0.015f, 0.05f), material, false);
            foreach (int side in new[] { -1, 1 })
            {
                var wing = Box("Lunar Route Arrow Wing", parent,
                    center + new Vector3(0.24f, 0, side * 0.12f), new Vector3(0.4f, 0.015f, 0.05f), material, false);
                wing.transform.rotation = Quaternion.Euler(0, side * 45, 0);
            }
        }

        private static void Label(LocalizedPanelBuilder ui, Transform parent, string name, string key,
            Vector3 position, Vector2 size, float fontSize, Quaternion rotation)
        {
            var label = ui.WorldLabel(name, key, position, size, fontSize, rotation);
            label.transform.SetParent(parent, true);
        }

        private static void BuildDarkSky()
        {
            const string path = AssetsRoot + "/Materials/Lunar Dark Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader == null) throw new InvalidOperationException("Skybox/Procedural shader is missing.");
                sky = new Material(shader) { name = "Lunar Dark Sky" };
                sky.SetFloat("_SunDisk", 0);
                sky.SetFloat("_AtmosphereThickness", 0);
                sky.SetFloat("_Exposure", 0.25f);
                sky.SetColor("_SkyTint", Color.black);
                sky.SetColor("_GroundColor", Color.black);
                AssetDatabase.CreateAsset(sky, path);
            }
            RenderSettings.skybox = sky;
        }

        private static Material ExistingMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "/Materials/" + name + ".mat");
            if (material == null) throw new InvalidOperationException("缺少共用材质：" + name);
            return material;
        }

        private static Material CachedMaterial(string name, Color color)
        {
            string path = AssetsRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Mesh CachedRockMesh()
        {
            const string path = AssetsRoot + "/Art/Lunar Blockout Rock.asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached != null) return cached;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var ring = new Vector3[6];
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = i * Mathf.PI / 3;
                float radius = i % 2 == 0 ? 0.65f : 0.53f;
                ring[i] = new Vector3(Mathf.Cos(angle) * radius, 0.15f, Mathf.Sin(angle) * radius);
            }
            for (int i = 0; i < ring.Length; i++)
            {
                int next = (i + 1) % ring.Length;
                Triangle(vertices, indices, new Vector3(0.08f, 0.85f, -0.06f), ring[next], ring[i]);
                Triangle(vertices, indices, new Vector3(0, -0.08f, 0), ring[i], ring[next]);
            }
            return SaveMesh("Lunar Blockout Rock", path, vertices, indices);
        }

        private static Mesh CachedHorizonMesh()
        {
            const string path = AssetsRoot + "/Art/Lunar Blockout Horizon.asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached != null) return cached;
            const int segments = 24;
            var rings = new Vector3[4, segments];
            var radii = new[] { 12f, 20f, 29f, 35f };
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < segments; i++)
                {
                    float angle = 2 * Mathf.PI * i / segments;
                    float y = ring == 0 ? -0.12f : ring == 1 ? -0.02f :
                        ring == 2 ? 2.5f + (i * 7 % 5) * 0.8f : -0.2f;
                    rings[ring, i] = new Vector3(Mathf.Cos(angle) * radii[ring], y,
                        Mathf.Sin(angle) * radii[ring]);
                }
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    Triangle(vertices, indices, rings[ring, i], rings[ring, next], rings[ring + 1, i]);
                    Triangle(vertices, indices, rings[ring, next], rings[ring + 1, next], rings[ring + 1, i]);
                }
            return SaveMesh("Lunar Blockout Horizon", path, vertices, indices);
        }

        private static void Triangle(List<Vector3> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
        }

        private static Mesh SaveMesh(string name, string path, List<Vector3> vertices, List<int> indices)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static GameObject MeshObject(string name, Transform parent, Vector3 position,
            Vector3 scale, Mesh mesh, Material material, bool collide)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent);
            obj.transform.SetPositionAndRotation(position, Quaternion.identity);
            obj.transform.localScale = scale;
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = collide ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collide) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            return obj;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position,
            Vector3 size, Material material, bool collide = true)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collide) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static void Remove(string name)
        {
            var obj = GameObject.Find(name);
            if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
