using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LunarEscape.Editor
{
    public static class BuildDockingScene
    {
        private const string Root="Assets/_LunarEscape";
        public const string ScenePath=Root+"/Scenes/10_LunarStation_Docking.unity";
        private static LocalizedPanelBuilder ui;
        [MenuItem("Lunar Escape/Open Rendezvous and Docking Scene")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){EditorSceneManager.OpenScene(ScenePath);Debug.Log("LUNAR_DOCKING_SCENE_OPENED");}}
        public static void Create(){if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)throw new InvalidOperationException("场景已存在，请使用开发重建入口。");Build();}
        public static void RebuildDevelopment()=>Build();
        private static void Build()
        {
            AssetDatabase.Refresh();BuildRepairScene.RefreshFont();
            // 修复各教学场景中圆底胶囊碰撞让氧气瓶自行倾倒的问题，保留实际抓放物理。
            foreach(string name in new[]{"07_LunarStation_CargoBoarding","08_LunarStation_Ascent","09_LunarStation_Orbit"})
            {EditorSceneManager.OpenScene(Root+"/Scenes/"+name+".unity");FixCargoColliders();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());}
            EditorSceneManager.OpenScene(BuildOrbitalScene.ScenePath);
            var session=UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();var flight=session.GetComponent<AscentMission>();var scene=session.GetComponent<FlightScenePresenter>();var layout=session.GetComponent<LunarViewLayout>();
            var language=UnityEngine.Object.FindAnyObjectByType<LocalizationService>();ui=new LocalizedPanelBuilder(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/Station Multilingual SDF.asset"),language);
            var settings=UnityEngine.Object.Instantiate(flight.Config);settings.name="Rendezvous Flight Config";settings.ConfigureOxygenCapacity(600);settings.ConfigureVitals(420,100,100);
            settings=SaveAsset(settings,Root+"/Settings/Rendezvous Flight Config.asset");flight.Configure(settings,session.Mission,session.GetComponent<CargoInventory>());
            var config=AssetDatabase.LoadAssetAtPath<DockingConfig>(Root+"/Settings/Docking Config.asset");
            if(config==null){config=ScriptableObject.CreateInstance<DockingConfig>();AssetDatabase.CreateAsset(config,Root+"/Settings/Docking Config.asset");}
            var docking=session.gameObject.AddComponent<DockingMission>();docking.Configure(flight,config);flight.ConfigureDocking(docking);
            ReplaceMoon(scene,layout,flight,session);
            BuildControls(session,scene,flight,docking);
            var target=BuildOrbiter(scene.FlightWorld.transform,out Renderer lamp);
            var blast=GameObject.CreatePrimitive(PrimitiveType.Quad);blast.name="Docking Collision Flash";blast.transform.SetParent(scene.FlightWorld.transform,false);UnityEngine.Object.DestroyImmediate(blast.GetComponent<Collider>());
            blast.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Distant Blast.mat");blast.SetActive(false);
            scene.FlightWorld.AddComponent<OrbiterView>().Configure(flight,docking,target,blast.transform,lamp);
            scene.GroundRoot.SetActive(true);scene.FlightWorld.SetActive(false);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();Debug.Log("LUNAR_DOCKING_SCENE_READY");
        }
        private static void FixCargoColliders()
        {
            // 外部连廊原来伸入系统舱，直接与架上左侧三件物资重叠并把它们弹飞。
            foreach(var connector in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name=="Station Connector"))
            {var position=connector.localPosition;position.z=Mathf.Sign(position.z)*5.375f;connector.localPosition=position;var scale=connector.localScale;scale.z=2.55f;connector.localScale=scale;}
            foreach(var item in UnityEngine.Object.FindObjectsByType<CargoItem>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var old=item.GetComponent<CapsuleCollider>();if(old==null||item.Kind!=CargoKind.Oxygen)continue;
                UnityEngine.Object.DestroyImmediate(old);var collider=item.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=item.GetComponent<MeshFilter>().sharedMesh;collider.convex=true;
            }
        }
        private static void ReplaceMoon(FlightScenePresenter scene,LunarViewLayout layout,AscentMission flight,StationMissionSession session)
        {
            var old=scene.FlightWorld.GetComponent<OrbitalMoonView>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var oldSphere=scene.FlightWorld.transform.Find("Orbital Moon");if(oldSphere!=null)UnityEngine.Object.DestroyImmediate(oldSphere.gameObject);
            var material=BuildContinuousMoon.BuildMaterial();var mesh=BuildContinuousMoon.BuildMesh();
            var terrain=session.GetComponent<LunarTerrainLayout>();
            // 与原有真实近景使用同一种坐标、纹理和光照，全球网格从远景的方形边界接起。
            var groundTerrain=scene.GroundRoot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Cratered Lunar Surface");
            var flightTerrain=layout.OutsideWorld.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Flight Cratered Surface");
            foreach(var root in new[]{groundTerrain,flightTerrain})
            {
                var globe=BuildLunarTerrain.MeshObject(root,"Continuous Global Surface",mesh,material);
                foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            }
            scene.FlightWorld.AddComponent<ContinuousMoonView>().Configure(flight,layout.OutsideWorld,flightTerrain.GetComponentsInChildren<Renderer>(true));
            scene.FlightWorld.GetComponentInChildren<LunarWindowAnimation>(true).ConfigureContinuousSurface(true);
            session.Player.Camera.farClipPlane=80000;
        }
        private static void BuildControls(StationMissionSession session,FlightScenePresenter scene,AscentMission flight,DockingMission docking)
        {
            var world=scene.FlightWorld.transform;
            foreach(var rail in world.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Crew Safety Rail"&&t.localPosition.x<0).ToArray())UnityEngine.Object.DestroyImmediate(rail.gameObject);
            // 平缓上升时基地会位于脚下左方，向下观察窗保留真实基地视线，避免为看爆炸强行转弯。
            var floor=world.Find("Cabin Floor");if(floor!=null)UnityEngine.Object.DestroyImmediate(floor.GetComponent<Renderer>());
            Box(world,"Cabin Main Floor",new Vector3(.45f,-.1f,0),new Vector3(3.5f,.2f,4.8f),"Station Graphite",false);
            Box(world,"Observation Window Outer Floor Rail",new Vector3(-2.1f,-.1f,0),new Vector3(.2f,.2f,4.8f),"Station Graphite",false);
            Box(world,"Observation Window Inner Border",new Vector3(-1.3f,.025f,0),new Vector3(.07f,.05f,4.7f),"Safety Amber",false);
            foreach(var lower in world.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==world && t.name=="Side Lower" && t.localPosition.x<0).ToArray())UnityEngine.Object.DestroyImmediate(lower.gameObject);
            foreach(var border in world.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==world&&t.name=="Window Border"&&t.localPosition.x<0&&t.localPosition.y<1).ToArray())
            {var position=border.localPosition;position.y=.04f;border.localPosition=position;}
            var wall=world.Find("Console Back");if(wall!=null)UnityEngine.Object.DestroyImmediate(wall.gameObject);
            Box(world,"Forward Window Lower",new Vector3(0,.36f,2.3f),new Vector3(4.4f,.72f,.14f),"Station Graphite");
            Box(world,"Forward Window Upper",new Vector3(0,2.91f,2.3f),new Vector3(4.4f,.5f,.14f),"Station Shell");
            foreach(float x in new[]{-2.18f,2.18f})Box(world,"Forward Window Frame",new Vector3(x,1.68f,2.3f),new Vector3(.13f,2.05f,.14f),"Safety Amber");
            var oldPanel=scene.Panel;var wrapper=new GameObject("Flight Controls");wrapper.transform.SetParent(world,false);oldPanel.transform.SetParent(wrapper.transform,true);
            var failure=session.GetComponent<MissionFailurePresenter>();var seatLock=session.GetComponent<FlightSeatLock>();
            seatLock.Configure(flight,session.Player,failure.Root,wrapper);
            var overlay=session.Player.Camera.GetComponentsInChildren<Renderer>(true).Single(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="LunarEscape/Flight Fade");
            scene.Configure(flight,seatLock,scene.GroundRoot,scene.FlightWorld,wrapper,overlay,scene.Seat);
            var orbitalTelemetry=oldPanel.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Orbital Telemetry").GetComponent<RectTransform>();orbitalTelemetry.anchoredPosition=new Vector2(0,-155);orbitalTelemetry.sizeDelta=new Vector2(1490,100);
            var guidance=oldPanel.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Orbital Guidance").GetComponent<RectTransform>();guidance.anchoredPosition=new Vector2(0,-245);
            var panel=ui.Panel("Rendezvous Console",world.TransformPoint(new Vector3(-.82f,.67f,1.4f)),new Vector2(2100,1100),.00105f,Quaternion.Euler(15,0,0));panel.SetParent(wrapper.transform,true);
            var instruments=ui.Label(panel,"Docking Instruments","dock.telemetry",new Vector2(0,425),new Vector2(1990,190),37,new Color(.65f,.95f,1));
            var status=ui.Label(panel,"Docking Status","dock.status.manual",new Vector2(0,280),new Vector2(1990,85),32,new Color(.5f,1,.8f));
            var controls=new GameObject("RCS Controls",typeof(RectTransform)).transform;controls.SetParent(panel,false);
            var buttons=new List<Button>();
            Button Control(DockCommand command,Vector2 position,Vector2? size=null)
            {
                var button=ui.Button(controls,command.ToString(),"dock.control."+command,position,size??new Vector2(285,100),28);
                button.gameObject.AddComponent<DockingThrustButton>().Configure(docking,command);buttons.Add(button);return button;
            }
            Control(DockCommand.Forward,new Vector2(-775,135));Control(DockCommand.Backward,new Vector2(-775,15));
            Control(DockCommand.Left,new Vector2(-915,-105),new Vector2(245,100));Control(DockCommand.Right,new Vector2(-600,-105),new Vector2(245,100));
            Control(DockCommand.Up,new Vector2(-415,135));Control(DockCommand.Down,new Vector2(-415,15));
            Control(DockCommand.PitchUp,new Vector2(405,135));Control(DockCommand.PitchDown,new Vector2(405,15));
            Control(DockCommand.YawLeft,new Vector2(600,-105));Control(DockCommand.YawRight,new Vector2(915,-105),new Vector2(245,100));
            Control(DockCommand.RollLeft,new Vector2(780,135));Control(DockCommand.RollRight,new Vector2(780,15));
            Control(DockCommand.Brake,new Vector2(0,-100),new Vector2(380,125));Control(DockCommand.MainBoost,new Vector2(-665,-265),new Vector2(540,95));
            var assist=ui.Button(controls,"Assist Toggle","dock.assist.on",new Vector2(0,-265),new Vector2(510,95),27);UnityEventTools.AddPersistentListener(assist.onClick,docking.ToggleAssistance);
            var help=ui.Label(panel,"Docking Help","dock.help",new Vector2(0,-475),new Vector2(1980,130),26,Color.white);
            var retry=ui.Button(panel,"Docking Retry","mission.retry",new Vector2(0,-200),new Vector2(620,110),38);UnityEventTools.AddPersistentListener(retry.onClick,session.RetryMission);
            // 舱内仍可使用剩余补给，按钮复用规则，不另建物资清单。
            var axes=ui.Label(controls,"Docking Axis Errors","dock.alignment",new Vector2(665,-250),new Vector2(630,120),25,Color.white);
            var kinds=new[]{CargoKind.Oxygen,CargoKind.RepairKit,CargoKind.MedicalKit,CargoKind.Battery};var supplies=new Button[4];
            for(int i=0;i<4;i++)supplies[i]=ui.Button(controls,"Docking Supply "+kinds[i],"flight.use."+kinds[i],new Vector2(-720+i*480,-365),new Vector2(435,72),23);
            UnityEventTools.AddPersistentListener(supplies[0].onClick,flight.UseOxygen);UnityEventTools.AddPersistentListener(supplies[1].onClick,flight.UseRepair);
            UnityEventTools.AddPersistentListener(supplies[2].onClick,flight.UseMedical);UnityEventTools.AddPersistentListener(supplies[3].onClick,flight.UseBattery);
            session.gameObject.AddComponent<DockingPanelPresenter>().Configure(flight,docking,oldPanel,panel.gameObject,instruments,status,help,assist.GetComponentInChildren<LocalizedText>(),buttons.ToArray(),assist,retry,controls.gameObject,axes,supplies);
        }
        private static Transform BuildOrbiter(Transform world,out Renderer lamp)
        {
            var root=new GameObject("Waiting Lunar Orbiter").transform;root.SetParent(world,false);
            // 接口在局部原点，船体向 +Z 延伸，玩家沿 +Z 接近其朝 -Z 的接口。
            Primitive(root,"Orbiter Service Module",PrimitiveType.Cylinder,new Vector3(0,0,5.3f),new Vector3(3.6f,2.3f,3.6f),Quaternion.Euler(90,0,0),"Station Shell");
            Primitive(root,"Crew Pressure Module",PrimitiveType.Sphere,new Vector3(0,0,2.4f),new Vector3(3.6f,3.6f,4.2f),Quaternion.identity,"Panel White");
            Primitive(root,"Docking Tunnel",PrimitiveType.Cylinder,new Vector3(0,0,.6f),new Vector3(1.45f,.65f,1.45f),Quaternion.Euler(90,0,0),"Station Graphite");
            Primitive(root,"Docking Face",PrimitiveType.Cylinder,new Vector3(0,0,.03f),new Vector3(1.55f,.07f,1.55f),Quaternion.Euler(90,0,0),"Safety Amber");
            Primitive(root,"Docking Aperture",PrimitiveType.Cylinder,new Vector3(0,0,-.045f),new Vector3(1.16f,.025f,1.16f),Quaternion.Euler(90,0,0),"Station Graphite");
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6;
                Primitive(root,"Docking Guide Light",PrimitiveType.Sphere,new Vector3(Mathf.Cos(angle)*.66f,Mathf.Sin(angle)*.66f,-.1f),Vector3.one*.09f,Quaternion.identity,"Guide Teal");
            }
            lamp=Primitive(root,"Capture Indicator",PrimitiveType.Cube,new Vector3(0,.88f,-.1f),new Vector3(.55f,.1f,.08f),Quaternion.identity,"Guide Teal").GetComponent<Renderer>();
            foreach(float side in new[]{-1,1})
            {
                Box(root,"Solar Boom",new Vector3(side*4,0,5.2f),new Vector3(6,.13f,.13f),"Station Shell",false);
                for(int i=0;i<4;i++)
                {
                    Box(root,"Solar Panel Frame",new Vector3(side*(3.15f+i*1.35f),0,5.2f),new Vector3(1.3f,3.6f,.08f),"Panel White",false);
                    Box(root,"Solar Cell Panel",new Vector3(side*(3.15f+i*1.35f),0,5.145f),new Vector3(1.22f,3.48f,.025f),"Station Graphite",false);
                    for(int j=0;j<6;j++)Box(root,"Solar Cell Grid",new Vector3(side*(3.15f+i*1.35f),-1.47f+j*.58f,5.125f),new Vector3(1.22f,.015f,.012f),"Guide Teal",false);
                }
                Primitive(root,"RCS Cluster",PrimitiveType.Cylinder,new Vector3(side*1.9f,0,3.7f),new Vector3(.32f,.35f,.32f),Quaternion.Euler(0,0,90),"Station Graphite");
                Primitive(root,"Navigation Beacon",PrimitiveType.Sphere,new Vector3(side*1.65f,1.05f,1.8f),Vector3.one*.14f,Quaternion.identity,side<0?"Guide Teal":"Safety Amber");
            }
            Primitive(root,"Main Engine Nozzle",PrimitiveType.Cylinder,new Vector3(0,0,8),new Vector3(1.8f,.55f,1.8f),Quaternion.Euler(90,0,0),"Station Graphite");
            Box(root,"Antenna Mast",new Vector3(0,2,5.3f),new Vector3(.08f,1.5f,.08f),"Station Shell",false);
            Primitive(root,"Communications Dish",PrimitiveType.Sphere,new Vector3(0,2.75f,5.3f),new Vector3(1.3f,.15f,1.3f),Quaternion.Euler(25,0,0),"Panel White");
            root.gameObject.SetActive(false);return root;
        }
        private static GameObject Primitive(Transform parent,string name,PrimitiveType shape,Vector3 position,Vector3 scale,Quaternion rotation,string material)
        {var o=GameObject.CreatePrimitive(shape);o.name=name;o.transform.SetParent(parent,false);o.transform.SetLocalPositionAndRotation(position,rotation);o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+material+".mat");UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());return o;}
        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,string material,bool collision=true)
        {var o=Primitive(parent,name,PrimitiveType.Cube,position,scale,Quaternion.identity,material);if(collision)o.AddComponent<BoxCollider>();return o;}
        private static T SaveAsset<T>(T value,string path)where T:UnityEngine.Object
        {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null)AssetDatabase.CreateAsset(value,path);else{EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);value=old;EditorUtility.SetDirty(value);}return value;}
    }
}
