using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class InstallStationExpansion
    {
        private const string Art = ImportStationExpansion.Root;
        private static Transform root, ground;
        private static StationMissionSession session;
        private static LifeSupportMission life;
        private static TMP_FontAsset font;
        private static LocalizedPanelBuilder ui;
        private static LocalizationService language;
        private static Dictionary<string, Material> materials;

        [MenuItem("Lunar Escape/Install Expanded Station Story")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs/LayoutRevisionBackup");
            string backup = "Logs/LayoutRevisionBackup/11-before-station-expansion.unity";
            if (!File.Exists(backup)) File.Copy(BuildLifeSupportScene.ScenePath, backup);
            ImportStationExpansion.Import();
            BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            session = Object.FindAnyObjectByType<StationMissionSession>(); life = session.GetComponent<LifeSupportMission>();
            ground = session.GetComponent<FlightScenePresenter>().GroundRoot.transform;
            language = Object.FindAnyObjectByType<LocalizationService>();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_LunarEscape/Fonts/Station Multilingual SDF.asset");
            ui = new LocalizedPanelBuilder(font, language);
            materials = ImportStationExpansion.Read().materials.ToDictionary(m => m.name,
                m => AssetDatabase.LoadAssetAtPath<Material>(Art + "/Materials/" + m.name + ".mat"));
            var previous = ground.Find("Expanded Station Story"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            root = Frame(ground, "Expanded Station Story", Vector3.zero);
            ReplaceArchitecture();
            ConfigureSettings();
            MoveRepairAndTools();
            BuildSupplies();
            BuildStory();
            ConfigureUsability();
            RefreshVoiceClips();
            UpdateExteriorProxy();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("EXPANDED_STATION_STORY_INSTALLED");
        }

        private static void RefreshVoiceClips()
        {
            var voice = Object.FindAnyObjectByType<MissionVoice>();
            if (voice == null) return;
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_LunarEscape/Audio/Voice" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(p => new VoiceLine
                {
                    id = Path.GetFileNameWithoutExtension(p), clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p),
                    speaker = Path.GetFileName(p).StartsWith("cup_") ? VoiceSpeaker.MissionControl : VoiceSpeaker.Commander
                }).ToArray();
            voice.Configure(clips, session.Player.Camera.transform, session.GetComponent<CrewMission>().Commander);
            EditorUtility.SetDirty(voice);
        }

        private static void ReplaceArchitecture()
        {
            var layout = session.GetComponent<LunarViewLayout>();
            var baseDoor = session.GetComponent<MissionEnvironment>().Door.transform;
            // Preserve the live airlock, screens, tools and session. Replace only static architecture.
            foreach (string name in new[] { "Station Realistic Exterior", "Station Realistic Interior" })
            {
                var old = Find(name); if (old != null) old.gameObject.SetActive(false);
            }
            var blockout = Find("Station Room - editable blockout");
            foreach (var collider in blockout.GetComponentsInChildren<Collider>(true))
            {
                if (collider.transform == baseDoor || collider.transform.IsChildOf(baseDoor)
                    || collider.name.Contains("Teleport") || collider.name == "Floor - teleport surface") continue;
                collider.enabled = false;
            }
            foreach (var renderer in blockout.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(baseDoor) && renderer.transform != baseDoor) renderer.enabled = false;
            RestoreBaseDoor();
            // Old exterior proxy colliders include a mast and sealed decorative pods in the new rooms.
            foreach (var collider in layout.BaseExterior.GetComponentsInChildren<Collider>(true))
                if (!collider.transform.IsChildOf(blockout)) collider.enabled = false;
            foreach (var name in new[] { "Supply Shelf", "Supply Shelf Leg", "Workbench Safety Edge", "Tool Placement Mat" })
                foreach (var t in ground.GetComponentsInChildren<Transform>(true).Where(t => t.name == name))
                { foreach (var r in t.GetComponents<Renderer>()) r.enabled = false; foreach (var c in t.GetComponents<Collider>()) c.enabled = false; }
            var art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ImportStationExpansion.PrefabPath));
            art.transform.SetParent(root, false);
            var physics = Frame(root, "Architecture Collisions", Vector3.zero);
            foreach (var spec in ImportStationExpansion.Read().colliders)
            {
                var t = Frame(physics, spec.name, ImportStationExpansion.Vector(spec.position));
                var collider = t.gameObject.AddComponent<BoxCollider>(); collider.size = ImportStationExpansion.Vector(spec.size);
                if (spec.floor) t.gameObject.AddComponent<TeleportationArea>();
            }
            var table = Primitive(root, "Solid Round Supply Table", PrimitiveType.Cylinder,
                new Vector3(0, .935f, 0), new Vector3(3, .065f, 3), "EX_White");
            table.GetComponent<Renderer>().enabled = false;
            var cylinder = table.AddComponent<MeshCollider>(); cylinder.sharedMesh = table.GetComponent<MeshFilter>().sharedMesh; cylinder.convex = true;
            var pedestal = Frame(physics, "Table Pedestal", new Vector3(0, .42f, 0));
            var pole = pedestal.gameObject.AddComponent<CapsuleCollider>(); pole.radius = .56f; pole.height = .86f;
            // Pane is physically closed and visually transparent; the arrays remain outside.
            var glass = Primitive(root, "Engineering Observation Window", PrimitiveType.Cube, new Vector3(-15.985f, 1.97f, 1.725f), new Vector3(.025f, 1.88f, 3.5f), "EX_Screen");
            glass.AddComponent<BoxCollider>(); glass.GetComponent<Renderer>().sharedMaterial = GlassMaterial();
            var volumes = new List<BoxCollider>();
            foreach (var spec in ImportStationExpansion.Read().rooms.Where(s => s.name != "Main lobby" && s.name != "Exit airlock"))
            {
                var volume = Frame(root, spec.name + " Air Volume", ImportStationExpansion.Vector(spec.position)).gameObject.AddComponent<BoxCollider>();
                volume.size = ImportStationExpansion.Vector(spec.size); volume.isTrigger = true; volumes.Add(volume);
            }
            life.ConfigureExpansion(volumes.ToArray());
            FlattenFoundation();
        }

        private static void ConfigureSettings()
        {
            var settings = new SerializedObject(life.Config);
            settings.FindProperty("baseOxygenRange").vector2Value = new Vector2(50, 80);
            settings.FindProperty("basePowerRange").vector2Value = new Vector2(50, 80);
            settings.FindProperty("baseOxygenSeconds").floatValue = 600;
            settings.FindProperty("basePowerSeconds").floatValue = 720;
            settings.FindProperty("suffocationSeconds").floatValue = 5;
            settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(life.Config);
            session.Mission.Config.Configure(420, 4, 120, 30); EditorUtility.SetDirty(session.Mission.Config);
            foreach (var light in session.GetComponent<LifeSupportEnvironment>().ShoulderLights)
            { light.intensity = 8; light.range = 10; EditorUtility.SetDirty(light); }
            foreach (var valve in life.AirlockRepair.Faults.OfType<ValveFault>())
            {
                var mount = valve.Valve.transform.parent;
                foreach(var oldHint in mount.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="Random Valve Direction").ToArray())
                    Object.DestroyImmediate(oldHint.gameObject);
                var hint = Label(mount, "Random Valve Direction", "", new Vector3(0, -.32f, .14f), new Vector2(.56f, .17f), .34f, new Vector3(0, 180, 0));
                valve.ConfigureRandomization(hint); EditorUtility.SetDirty(valve);
            }
            foreach (var fault in life.AirlockRepair.Faults.OfType<LatchFault>()) foreach (var bolt in fault.Bolts)
            { bolt.ConfigureAssistance(); EditorUtility.SetDirty(bolt); }
            ConfigureRandomDoorLatches();
        }

        private static void MoveRepairAndTools()
        {
            var task = session.Mission.RepairTask; task.Configure(4);
            var contact = task.GetComponent<RepairContact>();
            contact.RepairPoint.SetPositionAndRotation(new Vector3(-13.1f, 1.28f, .24f), Quaternion.Euler(0, 180, 0));
            contact.Configure(contact.RepairPoint, Object.FindObjectsByType<RepairTool>(FindObjectsInactive.Include, FindObjectsSortMode.None), "maintenance", .20f);
            var unit = Find("Oxygen Unit"); if (unit != null) unit.gameObject.SetActive(false);
            var label = Find("Repair Point Label");
            if (label != null) label.SetPositionAndRotation(new Vector3(-13.1f, 1.0f, .29f), Quaternion.Euler(0, 180, 0));
            var tool = Object.FindObjectsByType<RepairTool>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
            tool.transform.SetPositionAndRotation(new Vector3(-1.1f, 1.05f, -3.30f), Quaternion.Euler(0, 90, 0));
            var body = tool.GetComponent<Rigidbody>(); body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            tool.GetComponent<ReturnFallenTool>().ConfigureFloorRecovery(-.08f);
            var toolLabel = Find("Repair Tool Label"); if (toolLabel != null)
                toolLabel.SetPositionAndRotation(new Vector3(-1.1f, 1.25f, -3.72f), Quaternion.Euler(0, 180, 0));
            EditorUtility.SetDirty(contact); EditorUtility.SetDirty(task); EditorUtility.SetDirty(tool);
        }

        private static Transform Find(string name) => ground.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        private static Transform Frame(Transform parent, string name, Vector3 position)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }
        private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, string material)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            Object.DestroyImmediate(obj.GetComponent<Collider>()); obj.GetComponent<Renderer>().sharedMaterial = materials[material]; return obj;
        }
        private static TMP_Text Label(Transform parent, string name, string text, Vector3 position, Vector2 size, float fontSize, Vector3 angles = default)
        {
            var label = Frame(parent, name, position).gameObject.AddComponent<TextMeshPro>();
            label.transform.localRotation = Quaternion.Euler(angles); label.font = font; label.text = text; label.fontSize = fontSize;
            label.rectTransform.sizeDelta = size; label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.Normal; return label;
        }
        private static Material GlassMaterial()
        {
            const string path = Art + "/Materials/EX_Window.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", new Color(.12f, .3f, .4f, .10f)); material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0); material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0); material.SetFloat("_Smoothness", .7f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000; EditorUtility.SetDirty(material); return material;
        }
        private static void FlattenFoundation()
        {
            // The moon surface is shared by older scenes: bake a separate foundation mesh for this scene.
            foreach (var filter in ground.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.name.Contains("terrain", StringComparison.OrdinalIgnoreCase)) continue;
                if (!filter.sharedMesh.isReadable) continue;
                var vertices = filter.sharedMesh.vertices; bool changed = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = filter.transform.TransformPoint(vertices[i]);
                    bool inStation = p.x >= -27 && p.x <= 4.2f && p.z >= -5.3f && p.z <= 8.4f;
                    bool inAirlock = p.x >= 3.8f && p.x <= 8.4f && p.z >= .45f && p.z <= 2.95f;
                    if ((!inStation && !inAirlock) || p.y <= -.10f) continue;
                    p.y = -.10f; vertices[i] = filter.transform.InverseTransformPoint(p); changed = true;
                }
                if (!changed) continue;
                var mesh = Object.Instantiate(filter.sharedMesh); mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                mesh = ImportStationGeometry.SaveMesh(mesh, Art + "/Meshes/Expanded Foundation Terrain.asset"); filter.sharedMesh = mesh;
                if (filter.TryGetComponent<MeshCollider>(out var collider)) collider.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
            }
            foreach (var rock in ground.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Breccia")))
                if (rock.position.x > -17 && rock.position.x < -4 && rock.position.z > -5.3f && rock.position.z < 8.4f) rock.gameObject.SetActive(false);
        }
        private static void UpdateExteriorProxy()
        {
            var proxy = session.GetComponent<LunarViewLayout>().BaseProxy.transform;
            foreach (Transform child in proxy.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach (var source in root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Lobby__") || r.name.StartsWith("Corridor__")
                || r.name.StartsWith("Bedroom__") || r.name.StartsWith("Laboratory__") || r.name.StartsWith("Utilities__") || r.name.StartsWith("SolarArray__") || r.name.StartsWith("ExitAirlock__")))
            {
                var copy = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer)); copy.transform.SetParent(proxy, false);
                copy.transform.localPosition = source.transform.position; copy.transform.localRotation = source.transform.rotation; copy.transform.localScale = source.transform.lossyScale;
                copy.GetComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh; copy.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            }
        }
    }
}
