using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LunarEscape.Editor
{
    // Replace presentation only: mission objects, XR components, physical bodies,
    // timers, and the lander remain the same scene objects.
    public static class BuildRealisticStation
    {
        public const string Root = "Assets/_LunarEscape/Art/StationDetails";
        public const string ExteriorName = "Station Realistic Exterior";
        public const string InteriorName = "Station Realistic Interior";
        private const string PropName = "Realistic Prop";

        [MenuItem("Lunar Escape/Install Realistic Lunar Station")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ImportStationGeometry.Import();
            ImportStationProps.Import();
            EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("REALISTIC_STATION_INSTALLED");
        }

        [MenuItem("Lunar Escape/View Realistic Lunar Station")]
        public static void Open()
        {
            BuildDockingScene.Open();
            if (SceneManager.GetActiveScene().path != BuildDockingScene.ScenePath) return;
            var layout = UnityEngine.Object.FindAnyObjectByType<LunarViewLayout>();
            if (layout == null) return;
            Selection.activeGameObject = layout.BaseExterior;
            EditorApplication.delayCall += () =>
            {
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                view.LookAt(new Vector3(-1, 2, 0), Quaternion.Euler(22, -42, 0), 17);
                view.Focus();
            };
            Debug.Log("REALISTIC_STATION_SCENE_OPENED");
        }

        public static void ApplyToCurrentScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ImportStationGeometry.ExteriorPrefabPath) == null) ImportStationGeometry.Import();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ImportStationProps.PrefabPath("industrial_battery")) == null) ImportStationProps.Import();
            Directory.CreateDirectory(Root + "/Meshes");
            AssetDatabase.Refresh();
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            var layout = session.GetComponent<LunarViewLayout>();
            var ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            RemoveNamed(ground, ExteriorName); RemoveNamed(ground, InteriorName);
            var hiddenNames = new HashSet<string>(ImportStationGeometry.HiddenRendererNames);
            // These additional prototypes are fully covered by the new furniture.
            hiddenNames.UnionWith(new[] { "Supply Shelf Leg", "Workbench Safety Edge", "Tool Placement Mat" });
            foreach (var renderer in ground.GetComponentsInChildren<MeshRenderer>(true))
                if (hiddenNames.Contains(renderer.name) && !renderer.transform.IsChildOf(layout.Lander)) renderer.enabled = false;
            var exterior = Instantiate(ImportStationGeometry.ExteriorPrefabPath, layout.BaseExterior.transform, ExteriorName);
            var interior = Instantiate(ImportStationGeometry.InteriorPrefabPath, ground, InteriorName);
            BuildDoor(Find(ground, "Evacuation Door"));
            ReplaceCargo(session.GetComponent<CargoInventory>(), ground);
            ReplaceTool(UnityEngine.Object.FindAnyObjectByType<RepairTool>());
            ReplaceRepairUnit(ground);
            AddWallEquipment(ground, interior.transform);
            AddInteriorLights(interior.transform);
            RebuildFlightProxy(layout, exterior.transform);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        }

        private static void ReplaceCargo(CargoInventory inventory, Transform ground)
        {
            var shelf = Find(ground, "Supply Shelf").GetComponent<Collider>();
            foreach (var item in inventory.Items)
            {
                string id = item.Kind switch
                {
                    CargoKind.Oxygen => "oxygen_cylinder", CargoKind.RepairKit => "metal_toolbox",
                    CargoKind.Battery => "industrial_battery", CargoKind.MedicalKit => "medical_box",
                    CargoKind.DataCore => "circuit_board", CargoKind.LunarSample => "apollo_10021_79",
                    _ => throw new ArgumentOutOfRangeException()
                };
                var target = item.Kind switch
                {
                    CargoKind.Oxygen => new Vector3(.275f, .5f, .25f),
                    CargoKind.MedicalKit => new Vector3(.48f, .16f, .33f),
                    CargoKind.DataCore => new Vector3(.36f, .12f, .36f),
                    CargoKind.LunarSample => new Vector3(.27f, .2f, .27f),
                    _ => new Vector3(.42f, .32f, .28f)
                };
                var visual = AddScaledProp(item.transform, id, PropName, target);
                var bounds = BoundsOf(visual);
                var oldRenderer = item.GetComponent<MeshRenderer>();
                if (oldRenderer != null) oldRenderer.enabled = false;
                FitCargoCollider(item, bounds);
                // Fit the new physical envelope to the existing shelf height.
                var position = item.transform.position;
                position.y = shelf.bounds.max.y + bounds.size.y * .5f + .015f;
                item.transform.position = position;
                EditorUtility.SetDirty(item);
            }
        }

        private static void FitCargoCollider(CargoItem item, Bounds visualBounds)
        {
            var scale = item.transform.lossyScale;
            var localSize = Divide(visualBounds.size, scale);
            if (item.TryGetComponent<BoxCollider>(out var box))
            { box.center = Vector3.zero; box.size = localSize; }
            else if (item.TryGetComponent<MeshCollider>(out var cylinder))
            {
                // Keep a flat-bottom convex cylinder for the oxygen vessel. A
                // capsule rocks on its rounded bottom and falls off the shelf.
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                try
                {
                    var mesh = UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
                    mesh.name = "Portable oxygen collision";
                    var vertices = mesh.vertices;
                    var factor = new Vector3(localSize.x * .86f, localSize.y * .5f, localSize.z);
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i], factor);
                    mesh.vertices = vertices; mesh.RecalculateBounds();
                    cylinder.sharedMesh = SaveMesh(mesh, Root + "/Meshes/OxygenCollision.asset");
                    cylinder.convex = true;
                }
                finally { UnityEngine.Object.DestroyImmediate(primitive); }
            }
        }

        private static void ReplaceTool(RepairTool tool)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(tool))
                PrefabUtility.UnpackPrefabInstance(tool.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // The wrench's working end aligns with the original Repair Tip.
            var visual = AddScaledProp(tool.transform, "combination_wrench", PropName, new Vector3(.08f, .04f, .4f));
            visual.transform.localPosition = new Vector3(0, 0, .05f);
            foreach (var renderer in tool.GetComponentsInChildren<MeshRenderer>(true))
                if (!renderer.transform.IsChildOf(visual.transform)) renderer.enabled = false;
            var grip = tool.transform.Find("Grip");
            var head = tool.transform.Find("Tool Head");
            if (grip != null) grip.localScale = new Vector3(.035f, .035f, .3f);
            if (head != null)
            { head.localPosition = new Vector3(0, 0, .212f); head.localScale = new Vector3(.067f, .035f, .076f); }
        }

        private static void ReplaceRepairUnit(Transform ground)
        {
            var unit = Find(ground, "Oxygen Unit");
            var visual = AddScaledProp(unit, "power_box_01", "Realistic Oxygen Control Unit", new Vector3(.5f, .32f, .34f));
            unit.GetComponent<MeshRenderer>().enabled = false;
            // Keep the existing point renderer and its mission-driven colour.
            // Add a metal collar around it instead of losing repair feedback.
            var target = Find(ground, "Repair Point");
            var old = target.Find("Repair Socket Bezel");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var bezel = new GameObject("Repair Socket Bezel").transform;
            bezel.SetParent(target, false); bezel.localScale = Divide(Vector3.one, target.lossyScale);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                DetailBox(bezel, "Socket rim", new Vector3(Mathf.Cos(angle) * .095f, Mathf.Sin(angle) * .095f, .015f),
                    new Vector3(.035f, .035f, .025f), "LB_BrushedTitanium");
            }
            CombineDetails(bezel, "RepairSocket");
        }

        private static void AddWallEquipment(Transform ground, Transform interior)
        {
            var equipment = new GameObject("Station Electrical Service Panel").transform;
            equipment.SetParent(interior, false);
            equipment.localPosition = new Vector3(3.46f, 1.7f, -1.8f);
            equipment.localRotation = Quaternion.Euler(0, 90, 0);
            AddScaledProp(equipment, "power_box_01", "Electrical distribution cabinet", new Vector3(.65f, .85f, .72f));
        }

        private static void AddInteriorLights(Transform root)
        {
            var workbenchFill = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SingleOrDefault(light => light.name == "Workbench Fill");
            if (workbenchFill != null)
            {
                workbenchFill.intensity = 1.3f;
                workbenchFill.color = new Color(.92f, .96f, 1f);
            }
            // Fixtures have emissive surfaces; a few unshadowed fill lights give
            // their nearby tools legible metal and paint response in VR.
            foreach (var point in new[] { new Vector3(-2.3f, 2.85f, 1.7f), new Vector3(2.3f, 2.85f, 1.7f), new Vector3(0, 2.85f, -2) })
            {
                var light = new GameObject("Habitat ceiling fill").AddComponent<Light>();
                light.transform.SetParent(root, false); light.transform.localPosition = point;
                light.type = LightType.Point; light.range = 5; light.intensity = .45f;
                light.color = new Color(.94f, .97f, 1); light.shadows = LightShadows.None;
            }
        }

        private static void BuildDoor(Transform door)
        {
            var previous = door.Find("Pressure Door Appearance");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            door.GetComponent<MeshRenderer>().enabled = false;
            var skin = new GameObject("Pressure Door Appearance").transform;
            skin.SetParent(door, false); skin.localScale = Divide(Vector3.one, door.lossyScale);
            DetailBox(skin, "Pressure gasket", Vector3.zero, new Vector3(.19f, 2.45f, 1.72f), "LB_SealRubber");
            DetailBox(skin, "Pressure hatch shell", Vector3.zero, new Vector3(.21f, 2.35f, 1.62f), "LB_PaintedAlloy");
            foreach (float x in new[] { -.12f, .12f })
            {
                foreach (float z in new[] { -.63f, .63f })
                {
                    DetailBox(skin, "Hatch edge rail", new Vector3(x, 0, z), new Vector3(.025f, 2.16f, .035f), "LB_BrushedTitanium");
                    for (int j = 0; j < 4; j++)
                        DetailBox(skin, "Pressure latch", new Vector3(x, -.82f + j * .54f, z), new Vector3(.06f, .09f, .15f), "LB_BrushedTitanium");
                }
                foreach (float y in new[] { -.98f, .97f })
                    DetailBox(skin, "Hatch stiffener", new Vector3(x, y, 0), new Vector3(.045f, .075f, 1.43f), "LB_AnodizedGraphite");
                DetailBox(skin, "Window frame", new Vector3(x, .46f, 0), new Vector3(.035f, .46f, .67f), "LB_AnodizedGraphite");
                DetailBox(skin, "Inspection glass", new Vector3(x * 1.2f, .46f, 0), new Vector3(.02f, .36f, .56f), "LB_InstrumentGlass");
                DetailBox(skin, "Hatch handle", new Vector3(x * 1.8f, -.1f, -.48f), new Vector3(.05f, .32f, .045f), "LB_SafetyOchre");
                foreach (float y in new[] { -.25f, .05f })
                    DetailBox(skin, "Handle stand-off", new Vector3(x * 1.45f, y, -.48f), new Vector3(.12f, .045f, .045f), "LB_BrushedTitanium");
            }
            CombineDetails(skin, "PressureDoor");
        }

        private static void RebuildFlightProxy(LunarViewLayout layout, Transform exterior)
        {
            var proxy = layout.BaseProxy.transform;
            foreach (Transform child in proxy.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (var source in exterior.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!source.enabled || !source.TryGetComponent<MeshFilter>(out var filter)) continue;
                var obj = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(proxy, false);
                // OutsideWorld already has a launch-relative offset: source
                // world coordinates belong in the proxy's lunar local frame.
                obj.transform.localPosition = source.transform.position;
                obj.transform.localRotation = source.transform.rotation;
                obj.transform.localScale = source.transform.lossyScale;
                obj.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                obj.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            }
        }

        private static GameObject AddScaledProp(Transform parent, string id, string name, Vector3 target)
        {
            var previous = parent.Find(name);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportStationProps.PrefabPath(id));
            if (prefab == null) throw new FileNotFoundException("Missing station prop: " + id);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var bounds = BoundsOf(obj);
            float factor = Mathf.Min(target.x / bounds.size.x, target.y / bounds.size.y, target.z / bounds.size.z);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Divide(Vector3.one * factor, parent.lossyScale);
            if (id == "power_box_01") obj.transform.localRotation = Quaternion.Euler(0, 180, 0);
            return obj;
        }

        private static GameObject Instantiate(string path, Transform parent, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new FileNotFoundException(path);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            obj.name = name; obj.transform.SetParent(parent, false);
            return obj;
        }
        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static Vector3 Divide(Vector3 value, Vector3 scale) => new(value.x / scale.x, value.y / scale.y, value.z / scale.z);
        private static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<MeshRenderer>(true);
            var result = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }
        private static void RemoveNamed(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        private static void DetailBox(Transform parent, string name, Vector3 position, Vector3 size, string material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ImportStationGeometry.Root + "/Materials/" + material + ".mat");
        }
        private static void CombineDetails(Transform root, string assetName)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            foreach (var group in renderers.GroupBy(r => r.sharedMaterial))
            {
                var mesh = new Mesh { name = assetName + " " + group.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh,
                    transform = root.worldToLocalMatrix * r.transform.localToWorldMatrix }).ToArray());
                var obj = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root, false);
                obj.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, Root + "/Meshes/" + mesh.name + ".asset");
                obj.GetComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
            foreach (var renderer in renderers) UnityEngine.Object.DestroyImmediate(renderer.gameObject);
        }
        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, cached); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(cached); return cached;
        }
    }
}
