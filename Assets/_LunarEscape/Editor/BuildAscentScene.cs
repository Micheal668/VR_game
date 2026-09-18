using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace LunarEscape.Editor
{
    public static class BuildAscentScene
    {
        public const string ScenePath = "Assets/_LunarEscape/Scenes/08_LunarStation_Ascent.unity";
        private const string Root = "Assets/_LunarEscape";
        private static LocalizedPanelBuilder ui;
        [MenuItem("Lunar Escape/Open Ascent Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Lunar Escape/Preview Late Launch with Supplies (Play Mode)")]
        public static void Preview()
        {
            var flight = UnityEngine.Object.FindAnyObjectByType<AscentMission>();
            if (!EditorApplication.isPlaying || flight == null) throw new InvalidOperationException("先运行第五步场景。");
            var session = flight.GetComponent<StationMissionSession>();
            session.RetryMission(); session.BeginMission(); session.Advance(session.Mission.Config.RepairWindowSeconds);
            foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.RepairKit, CargoKind.MedicalKit })
                flight.Inventory.TryHold(flight.Inventory.Items.First(i => i.Kind == kind));
            var destination=session.Exit.Volume.bounds.center;
            session.Player.MoveCameraToWorldLocation(new Vector3(destination.x,session.Player.CameraInOriginSpaceHeight,destination.z));
            Physics.SyncTransforms();
            flight.Inventory.LoadAllCarriedIntoShip(); session.ConfirmExit();
            session.Advance(session.Mission.RemainingSeconds - 18);
        }
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("第五步场景已存在，拒绝覆盖手动修改。");
            AssetDatabase.Refresh(); BuildRepairScene.RefreshFont();
            var scene = EditorSceneManager.OpenScene(BuildCargoBoardingScene.ScenePath);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            var inventory = session.GetComponent<CargoInventory>();
            var localization = UnityEngine.Object.FindAnyObjectByType<LocalizationService>();
            ui = new LocalizedPanelBuilder(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset"), localization);
            var config = AssetDatabase.LoadAssetAtPath<AscentConfig>(Root + "/Settings/Ascent Config.asset");
            if (config == null) { config = ScriptableObject.CreateInstance<AscentConfig>(); AssetDatabase.CreateAsset(config, Root + "/Settings/Ascent Config.asset"); }
            var flight = session.gameObject.AddComponent<AscentMission>(); flight.Configure(config, session.Mission, inventory); session.ConfigureAscent(flight);
            var failure = session.GetComponent<MissionFailurePresenter>();
            failure.ConfigureFlight(flight, failure.Root.transform.Find("Failure Reason").GetComponent<LocalizedText>());
            // 保留输入、任务与语言服务；地面物体仅在黑屏时统一切换。
            var ground = new GameObject("Ground Environment");
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == ground || root == session.gameObject || root.GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>(true) != null
                    || root.GetComponentInChildren<LocalizationService>(true) != null || root.GetComponentInChildren<CollisionAwareSimulator>(true) != null
                    || root.GetComponentInChildren<EventSystem>(true) != null || root.GetComponentInChildren<XRInteractionManager>(true) != null) continue;
                root.transform.SetParent(ground.transform, true);
            }
            var world = new GameObject("Flight Cabin World"); world.transform.position = new Vector3(100, 0, 0);
            BuildCabin(world.transform);
            var seat = new GameObject("Flight Seat").transform;
            seat.SetParent(world.transform, false); seat.localPosition = new Vector3(0, 0, -0.5f);
            var panel = BuildPanel(world.transform, flight, session, localization);
            var seatLock = session.gameObject.AddComponent<FlightSeatLock>(); seatLock.Configure(flight, session.Player, failure.Root, panel.gameObject);
            var overlay = GameObject.CreatePrimitive(PrimitiveType.Sphere); overlay.name = "Blink Fade Overlay";
            UnityEngine.Object.DestroyImmediate(overlay.GetComponent<Collider>());
            overlay.transform.SetParent(session.Player.Camera.transform, false); overlay.transform.localScale = Vector3.one * 0.7f;
            var renderer = overlay.GetComponent<Renderer>(); renderer.sharedMaterial = CreateMaterial("Flight Fade", "LunarEscape/Flight Fade", Color.black);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            session.gameObject.AddComponent<FlightScenePresenter>().Configure(flight, seatLock, ground, world, panel.gameObject, renderer, seat);
            ReviseLanderScene.Install();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_ASCENT_CREATED " + ScenePath);
        }
        private static void BuildCabin(Transform world)
        {
            Box(world, "Cabin Floor", new Vector3(0, -.1f, 0), new Vector3(3.5f, .2f, 4), "Station Graphite");
            Box(world, "Cabin Ceiling", new Vector3(0, 3.15f, 0), new Vector3(3.5f, .15f, 4), "Station Shell");
            Box(world, "Cabin Back", new Vector3(0, 1.5f, -1.95f), new Vector3(3.5f, 3, .12f), "Station Shell");
            Box(world, "Console Back", new Vector3(0, 1.55f, 1.7f), new Vector3(3.5f, 3.1f, .12f), "Station Graphite");
            foreach (float side in new[] { -1.72f, 1.72f })
            {
                Box(world, "Side Lower", new Vector3(side, .48f, 0), new Vector3(.12f, .96f, 4), "Station Shell");
                Box(world, "Side Upper", new Vector3(side, 2.91f, 0), new Vector3(.12f, .5f, 4), "Station Shell");
                foreach (float z in new[] { -1.88f, 1.88f })
                    Box(world, "Window Frame", new Vector3(side, 1.82f, z), new Vector3(.18f, 1.8f, .24f), "Safety Amber");
                foreach (float y in new[] { 1f, 2.65f })
                    Box(world, "Window Border", new Vector3(side, y, 0), new Vector3(.18f, .09f, 3.75f), "Safety Amber");
            }
            var lightObj = new GameObject("Cabin Light"); lightObj.transform.SetParent(world, false); lightObj.transform.localPosition = new Vector3(0, 2.7f, -.3f);
            var light = lightObj.AddComponent<Light>(); light.type = LightType.Point; light.range = 8; light.intensity = 2; light.color = new Color(.7f,.83f,1);
            var terrain = BuildBackdrop(world);
            var station = new GameObject("Distant Base").transform; station.SetParent(world, false); station.localPosition = new Vector3(-9, -.2f, 1);
            for (int i = 0; i < 5; i++) Box(station, "Far Module " + i, new Vector3((i % 2) * .75f, .22f, (i / 2 - 1) * .65f), new Vector3(.6f,.4f,.48f), "Panel White", false);
            var blast = GameObject.CreatePrimitive(PrimitiveType.Quad); blast.name = "Distant Blast"; UnityEngine.Object.DestroyImmediate(blast.GetComponent<Collider>());
            blast.transform.SetParent(world, false); blast.transform.localPosition = new Vector3(-9, .3f, 1); blast.transform.localRotation=Quaternion.Euler(0,90,0);
            blast.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Distant Blast", "LunarEscape/Distant Blast", new Color(1,.55f,.16f));
            var debris = new Transform[12];
            for (int i = 0; i < debris.Length; i++) debris[i] = Box(world, "Distant Debris " + i, Vector3.zero, Vector3.one * .07f, "Station Graphite", false).transform;
            var sound = world.gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 0; sound.volume = .25f;
            sound.clip = BuildHullSound();
            var flight = UnityEngine.Object.FindAnyObjectByType<AscentMission>();
            world.gameObject.AddComponent<LunarWindowAnimation>().Configure(flight, terrain, station, blast.transform, debris, sound);
        }
        private static Transform BuildBackdrop(Transform world)
        {
            var obj = new GameObject("Animated Lunar Panorama", typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(world, false);
            obj.transform.localPosition = new Vector3(0,-1,0);
            const int segments = 96;
            var vertices = new Vector3[(segments+1)*2]; var uv = new Vector2[vertices.Length]; var triangles = new int[segments*6];
            for (int i=0; i<=segments; i++)
            {
                float angle = (-140 + 280f*i/segments) * Mathf.Deg2Rad;
                for (int j=0;j<2;j++) { vertices[i*2+j]=new Vector3(Mathf.Sin(angle)*12, j==0 ? -6 : 9, Mathf.Cos(angle)*12); uv[i*2+j]=new Vector2((float)i/segments,j); }
                if(i==segments) continue;
                int b=i*6, v=i*2; triangles[b]=v; triangles[b+1]=v+1; triangles[b+2]=v+2; triangles[b+3]=v+1; triangles[b+4]=v+3; triangles[b+5]=v+2;
            }
            var mesh=new Mesh { name="Lunar Panorama Arc", vertices=vertices, uv=uv, triangles=triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshPath=Root+"/Art/Flight/Lunar Panorama Arc.asset";
            var oldMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(oldMesh==null) AssetDatabase.CreateAsset(mesh,meshPath);
            else { EditorUtility.CopySerialized(mesh,oldMesh); UnityEngine.Object.DestroyImmediate(mesh); mesh=oldMesh; EditorUtility.SetDirty(mesh); }
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            var mat=CreateMaterial("Lunar Panorama", "Universal Render Pipeline/Unlit", Color.white);
            mat.SetFloat("_Cull",0); mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Flight/LunarPanorama.png"));
            obj.GetComponent<MeshRenderer>().sharedMaterial=mat; EditorUtility.SetDirty(mat);
            return obj.transform;
        }
        private static Transform BuildPanel(Transform world, AscentMission flight, StationMissionSession session, LocalizationService localization)
        {
            var panel=ui.Panel("Flight Controls", new Vector3(100,1.53f,1.55f),new Vector2(1600,1130),.00175f,Quaternion.identity); panel.SetParent(world,true);
            ui.Label(panel,"Flight Title","flight.title",new Vector2(0,495),new Vector2(1510,65),42,new Color(.4f,.95f,.87f));
            var phase=ui.Label(panel,"Flight Phase","flight.phase.Startup",new Vector2(0,425),new Vector2(1490,70),31,Color.white);
            var vitals=ui.Label(panel,"Flight Vitals","flight.vitals",new Vector2(0,350),new Vector2(1490,65),33,new Color(.5f,.92f,1));
            var danger=ui.Label(panel,"Flight Countdown","flight.countdown",new Vector2(0,280),new Vector2(1490,62),33,new Color(1,.66f,.25f));
            var condition=ui.Label(panel,"Flight Condition","flight.condition",new Vector2(0,215),new Vector2(1490,55),26,Color.white);
            var startup=new Button[4];
            string[] startKeys={"power","navigation","engine","ignite"};
            for(int i=0;i<4;i++) startup[i]=ui.Button(panel,"Flight Start "+i,"flight.start."+startKeys[i],new Vector2((i-1.5f)*380,130),new Vector2(360,83),27);
            UnityEventTools.AddPersistentListener(startup[0].onClick,flight.PowerOn); UnityEventTools.AddPersistentListener(startup[1].onClick,flight.StartNavigation);
            UnityEventTools.AddPersistentListener(startup[2].onClick,flight.PrepareEngine); UnityEventTools.AddPersistentListener(startup[3].onClick,flight.Ignite);
            var feedback=ui.Label(panel,"Flight Feedback","flight.feedback.ready",new Vector2(0,40),new Vector2(1490,80),27,new Color(1,.8f,.42f));
            var supplies=new Button[4]; var labels=new LocalizedText[4]; var kinds=new[]{CargoKind.Oxygen,CargoKind.RepairKit,CargoKind.Battery,CargoKind.MedicalKit};
            for(int i=0;i<4;i++) { supplies[i]=ui.Button(panel,"Flight Use "+kinds[i],"flight.use."+kinds[i],new Vector2((i-1.5f)*380,-55),new Vector2(360,88),28); labels[i]=supplies[i].GetComponentInChildren<LocalizedText>(); }
            UnityEventTools.AddPersistentListener(supplies[0].onClick,flight.UseOxygen); UnityEventTools.AddPersistentListener(supplies[1].onClick,flight.UseRepair);
            UnityEventTools.AddPersistentListener(supplies[2].onClick,flight.UseBattery); UnityEventTools.AddPersistentListener(supplies[3].onClick,flight.UseMedical);
            ui.Label(panel,"Flight Supplies Hint","flight.supplyhint",new Vector2(0,-151),new Vector2(1490,86),25,new Color(.7f,.8f,.88f));
            var result=ui.Label(panel,"Flight Result","flight.result",new Vector2(0,-268),new Vector2(1490,126),28,new Color(.5f,1,.78f));
            var retry=ui.Button(panel,"Flight Replay","mission.retry",new Vector2(0,-390),new Vector2(460,72),29); UnityEventTools.AddPersistentListener(retry.onClick,session.RetryMission);
            for(int i=0;i<3;i++) { var button=ui.Button(panel,"Flight Language "+i,new[]{"language.zh","language.en","language.ru"}[i],new Vector2((i-1)*390,-492),new Vector2(330,58),26); UnityEventTools.AddIntPersistentListener(button.onClick,localization.SetLanguageIndex,i); }
            panel.gameObject.AddComponent<FlightPanelPresenter>().Configure(flight,localization,new[]{phase,vitals,danger,condition,feedback,result},startup,supplies,labels,retry);
            return panel;
        }
        private static Material CreateMaterial(string name,string shader,Color color)
        {
            string path=Root+"/Materials/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(mat,path); }
            mat.shader=Shader.Find(shader);
            if(mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",color); EditorUtility.SetDirty(mat); return mat;
        }
        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,string material,bool collision=true)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localScale=size;
            obj.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+material+".mat");
            if(!collision) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>()); return obj;
        }
        private static AudioClip BuildHullSound()
        {
            const string path=Root+"/Art/Flight/HullImpact.wav";
            const int rate=22050, samples=rate*2;
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+samples*2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate*2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples*2); var random=new System.Random(17);
                for(int i=0;i<samples;i++) { double t=(double)i/rate, fade=Math.Exp(-t*3)*(1-Math.Exp(-t*80)); double wave=Math.Sin(t*2*Math.PI*47)*.6+(random.NextDouble()*2-1)*.2; writer.Write((short)(wave*fade*22000)); }
            }
            AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
