using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    public static partial class InstallStationExpansion
    {
        private static void BuildStory()
        {
            var crew = session.GetComponent<CrewMission>(); var follower = session.GetComponent<GroundCrewController>();
            var breaker = Object.FindAnyObjectByType<HabitatBreaker>();
            var breakerMount = breaker.Lever.transform.parent;
            breakerMount.SetPositionAndRotation(new Vector3(-11.8f, 1.12f, 4.05f), Quaternion.Euler(0, 180, 0));
            var circuit = BuildPuzzle("Backup Circuit Panel", new Vector3(-13.55f, 1.56f, 4.16f), Quaternion.Euler(0, 180, 0), true, null);
            breaker.ConfigureCircuit(circuit); EditorUtility.SetDirty(breaker);
            var laboratory = BuildPuzzle("Laboratory Module Terminal", new Vector3(-5.65f, 1.45f, -1.69f), Quaternion.identity, false, breaker);
            ConfigureLaboratory(laboratory);
            var bedroom = root.GetComponentsInChildren<BoxCollider>().Single(c => c.name == "Crew quarters Air Volume");
            var door = Frame(root, "Bedroom Sliding Door", new Vector3(-7.5f, 1.32f, 2.9f));
            var doorCollider = door.gameObject.AddComponent<BoxCollider>(); doorCollider.size = new Vector3(1.67f, 2.64f, .20f);
            Primitive(door, "Quarters pressure door", PrimitiveType.Cube, Vector3.zero, new Vector3(1.64f, 2.61f, .18f), "EX_White");
            Primitive(door, "Door safety inlay", PrimitiveType.Cube, new Vector3(0, -.75f, -.10f), new Vector3(1.6f, .09f, .018f), "EX_Copper");
            Primitive(door, "Door lower panel", PrimitiveType.Cube, new Vector3(0, -.36f, -.10f), new Vector3(1.20f, 1.30f, .028f), "EX_Blue");
            Primitive(door, "Door observation glass", PrimitiveType.Cube, new Vector3(0, .66f, -.11f), new Vector3(.70f, .31f, .025f), "EX_Screen");
            Label(root, "Bedroom Nameplate", "01  CREW QUARTERS", new Vector3(-7.5f, 2.89f, 2.76f), new Vector2(1.7f, .22f), .44f);
            Label(root, "Laboratory Nameplate", "02  RESEARCH LAB", new Vector3(-7.5f, 2.89f, .64f), new Vector2(1.7f, .22f), .44f, new Vector3(0, 180, 0));
            Label(root, "Engineering Nameplate", "03  LIFE SUPPORT", new Vector3(-10.96f, 2.89f, 1.7f), new Vector2(1.7f, .22f), .44f, new Vector3(0, -90, 0));
            Label(root, "Generator Label", "O2  GENERATOR", new Vector3(-13.1f, 2.06f, -.215f), new Vector2(1.7f, .21f), .52f, new Vector3(0, 180, 0));
            var oxygenReadout=Label(root,"Oxygen Generator Repair Readout","",new Vector3(-13.1f,2.43f,-.20f),new Vector2(2.4f,.22f),.42f,new Vector3(0,180,0));
            oxygenReadout.gameObject.AddComponent<ExpansionResourcePanel>().Configure(life,oxygenReadout,true);
            var powerReadout=Label(root,"Shared Base Power Readout","",new Vector3(-13.55f,2.32f,4.15f),new Vector2(2.5f,.40f),.43f);
            powerReadout.gameObject.AddComponent<ExpansionResourcePanel>().Configure(life,powerReadout,false);
            Label(root, "Engineering Window Label", "SOLAR ARRAY · BACKUP POWER", new Vector3(-15.78f, .85f, 1.7f), new Vector2(2.5f, .18f), .35f, new Vector3(0, -90, 0));
            Label(root, "Sample Labels", "METEORITE     MOON SOIL     DATA", new Vector3(-7.9f, 1.03f, -2.26f), new Vector2(2.15f, .14f), .30f, new Vector3(70, 180, 0));
            BuildPhotoSlots();
            // Mount central confirmation on the corridor wall, left of the bedroom door.
            var oldPanel = Find("Commander Rescue Instructions");
            var controlPosition = BedroomControlPosition; var controlRotation = Quaternion.identity; oldPanel.gameObject.SetActive(false);
            var panel = ui.Panel("Bedroom Central Control", controlPosition, new Vector2(1000, 660), .00105f, controlRotation);
            panel.SetParent(root, true);
            PlaceBedroomControl(panel);
            var title = ui.Label(panel, "Control Heading", "expansion.control.title", new Vector2(0, 235), new Vector2(930, 120), 58, Color.white);
            var status = ui.Label(panel, "Control Status", "expansion.control.wait", new Vector2(0, 54), new Vector2(930, 210), 42, Color.white).GetComponent<TMP_Text>();
            // The story owns this changing status instead of a static localization binding.
            Object.DestroyImmediate(status.GetComponent<LocalizedText>());
            var button = ui.Button(panel, "Unlock Bedroom Door", "expansion.control.unlock", new Vector2(0, -185), new Vector2(880, 146), 54);
            button.gameObject.AddComponent<ButtonPressFeedback>();
            var expansion = session.GetComponent<StationExpansionMission>() ?? session.gameObject.AddComponent<StationExpansionMission>();
            UnityEventTools.AddPersistentListener(button.onClick, expansion.UnlockBedroom);

            var bedPose = Frame(root, "Commander Sleeping Pose", new Vector3(-5.57f, .84f, 5.38f)); bedPose.rotation = Quaternion.Euler(90, 0, 0);
            Vector3[] toRackPoints = { new(-6.7f,0,5.2f), new(-7.5f,0,3.55f), new(-7.5f,0,1.7f), new(-4.5f,0,1.7f),
                new(-2.2f,0,1.85f), new(2.55f,0,1.85f), new(2.55f,0,-.48f) };
            var rackRoute = toRackPoints.Select((p, i) => Frame(root, "Crew Dressing Route " + i, p)).ToArray();
            // Start the existing lunar route beside the suit rack, clear of the central table.
            var oldRoute = follower.Waypoints.ToArray();
            var flightCommander = new SerializedObject(follower).FindProperty("flightCommander").objectReferenceValue as GameObject;
            var route = new List<Transform> { Frame(root, "Dressed Crew Route Start", toRackPoints[^1]), Frame(root, "Dressed Crew Aisle", new Vector3(2.55f,0,1.85f)) };
            route.AddRange(oldRoute.Where(t => t != null && t.position.x >= 3.3f));
            follower.Configure(crew, crew.Commander, follower.MedicalPort, route.ToArray(), null, flightCommander, follower.RescueHandle);
            var movement = session.Player.GetComponentsInChildren<Behaviour>(true).Where(b => b is ContinuousMoveProvider || b is TeleportationProvider
                || b is SnapTurnProvider || b is ContinuousTurnProvider || b is GravityProvider || b is XRBaseInteractor).ToArray();
            var red = BuildLighting();
            var eyes = BuildHeadEffects();
            expansion.Configure(session, crew, follower, breaker, circuit, laboratory, bedroom, door, bedPose, panel, rackRoute, movement, red, eyes, status);
            session.GetComponent<CrewWardrobe>().ConfigureIndependentCrew(expansion, crew.Commander);
            session.SpawnPoint.SetPositionAndRotation(new Vector3(-8.9f,0,5.48f), Quaternion.Euler(0,180,0));
            session.Player.transform.SetPositionAndRotation(session.SpawnPoint.position, session.SpawnPoint.rotation);
            if (PrefabUtility.IsPartOfPrefabInstance(session.Player)) PrefabUtility.RecordPrefabInstancePropertyModifications(session.Player.transform);
            EditorUtility.SetDirty(expansion); EditorUtility.SetDirty(follower); EditorUtility.SetDirty(session.GetComponent<CrewWardrobe>());
            foreach (var t in new[] { session.SpawnPoint, session.Player.transform, breakerMount }) EditorUtility.SetDirty(t);
        }

        private static Light[] BuildLighting()
        {
            var normal = new List<Light>(); var red = new List<Light>();
            Vector3[] positions = { new(-2,2.7f,1.8f), new(2,2.7f,-2), new(-6,2.7f,1.7f), new(-9.7f,2.7f,1.7f),
                new(-7.7f,2.7f,5.2f), new(-7.7f,2.7f,-2), new(-13.2f,2.7f,1.6f) };
            foreach (var p in positions)
            {
                var light = Frame(root, "Expansion Ceiling Fill", p).gameObject.AddComponent<Light>();
                light.type = LightType.Point; light.intensity = 5f; light.range = 5.5f; light.color = new Color(.82f,.93f,1); light.shadows = LightShadows.None; normal.Add(light);
                var housing = Primitive(root, "Emergency Lamp Housing", PrimitiveType.Cube, p + Vector3.up * .36f, new Vector3(.32f,.10f,.16f), "EX_Trim");
                Primitive(root, "Emergency Lens", PrimitiveType.Cube, p + Vector3.up * .30f, new Vector3(.22f,.025f,.11f), "EX_Red");
                var emergency = Frame(root, "Alarm Expansion Emergency Light", p).gameObject.AddComponent<Light>();
                emergency.type = LightType.Point; emergency.color = new Color(1,.07f,.035f); emergency.intensity = .65f; emergency.range = 4.7f; emergency.shadows = LightShadows.None; red.Add(emergency);
            }
            var environment = session.GetComponent<LifeSupportEnvironment>();
            var oldLights = ground.GetComponentsInChildren<Light>(true).Where(l => l.type != LightType.Directional && !l.name.StartsWith("Alarm")
                && !l.name.Contains("Beacon") && !l.transform.IsChildOf(root)).ToArray();
            environment.Configure(life, session.GetComponent<AscentMission>(), environment.Sun, normal.Concat(oldLights).ToArray());
            var glows = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterials.Any(m => m != null && (m.name == "EX_Light" || m.name == "EX_Red"))).Cast<Renderer>().ToArray();
            environment.ConfigureBlackout(glows, glows.Select(r => r.sharedMaterial.name == "EX_Red" ? .25f : 0f).ToArray());
            // Configure previews the unpowered state in the editor. Persist authored
            // intensities, so runtime Awake caches working lights rather than zeroes.
            foreach(var light in normal) light.intensity=5f;
            foreach(var light in oldLights) light.intensity=1.5f;
            EditorUtility.SetDirty(environment); return red.ToArray();
        }

        private static Image BuildHeadEffects()
        {
            var camera = session.Player.Camera;
            var previous = camera.transform.Find("Station Eye Effects"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var canvas = new GameObject("Station Eye Effects", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(camera.transform, false); canvas.transform.localPosition = new Vector3(0,0,.18f);
            var uiCanvas = canvas.GetComponent<Canvas>(); uiCanvas.renderMode = RenderMode.WorldSpace; uiCanvas.worldCamera = camera; uiCanvas.sortingOrder = 32000;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(2,2);
            var border = ui.Rectangle(canvas.transform, "Breathing Red Edge", Vector2.zero, new Vector2(.55f,.45f), Color.clear);
            border.sprite = VignetteSprite();
            var hypoxia = session.GetComponent<HypoxiaPresentation>() ?? session.gameObject.AddComponent<HypoxiaPresentation>(); hypoxia.Configure(life, border);
            var eyes = ui.Rectangle(canvas.transform, "Closed Eyes", Vector2.zero, new Vector2(2,2), Color.clear);
            foreach (Transform child in canvas.transform) child.gameObject.layer = LayerMask.NameToLayer("UI");
            canvas.layer = LayerMask.NameToLayer("UI"); EditorUtility.SetDirty(hypoxia); return eyes;
        }
        private static Sprite VignetteSprite()
        {
            const string path = Art + "/HypoxiaBorder.png";
            var texture = new Texture2D(256,256,TextureFormat.RGBA32,false); var pixels = new Color[256*256];
            for (int y=0;y<256;y++) for (int x=0;x<256;x++)
            {
                float r = new Vector2((x-127.5f)/127.5f,(y-127.5f)/127.5f).magnitude;
                pixels[y*256+x] = new Color(1,1,1,Mathf.SmoothStep(0,1,(r-.50f)/.45f));
            }
            texture.SetPixels(pixels);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void BuildPhotoSlots()
        {
            // Named, ordinary mesh surfaces allow later photo and school-logo replacement without rebuilding rooms.
            foreach (var p in new[] { new Vector3(-8.93f,1.09f,6.833f), new Vector3(-6.67f,1.09f,6.833f),
                new Vector3(-10.03f,2.2f,7.401f), new Vector3(-5.57f,2.2f,7.401f) })
            {
                bool wall = p.y > 2;
                Primitive(root, wall ? "Replaceable Wall Photo" : "Replaceable Bedside Photo", PrimitiveType.Cube, p,
                    wall ? new Vector3(.99f,.65f,.005f) : new Vector3(.25f,.28f,.005f), "EX_Blue");
            }
        }
    }
}
