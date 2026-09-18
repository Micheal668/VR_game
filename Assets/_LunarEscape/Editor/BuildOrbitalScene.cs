using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LunarEscape.Editor
{
    // 第六步在上一课的完整任务上加入好点的登舱控制、凹凸月面、双人操作位和入轨。
    public static class BuildOrbitalScene
    {
        private const string Root="Assets/_LunarEscape";
        public const string ScenePath=Root+"/Scenes/09_LunarStation_Orbit.unity";
        private static LocalizedPanelBuilder ui;
        [MenuItem("Lunar Escape/Open Orbital Flight Scene")]
        public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){EditorSceneManager.OpenScene(ScenePath);Debug.Log("LUNAR_ORBIT_SCENE_OPENED: "+ScenePath);}}
        [MenuItem("Lunar Escape/Create Orbital Flight Scene")]
        public static void Create(){if(System.IO.File.Exists(ScenePath))throw new InvalidOperationException("第六步场景已经存在，请打开现有场景。");Build();}
        // 自动化开发重建入口：始终从已验证的 08 生成，不对正在运行的场景做叠加修改。
        public static void RebuildDevelopment()=>Build();
        private static void Build()
        {
            AssetDatabase.Refresh();BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildAscentScene.ScenePath);
            var session=UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();var flight=session.GetComponent<AscentMission>();var scene=session.GetComponent<FlightScenePresenter>();var layout=session.GetComponent<LunarViewLayout>();
            var language=UnityEngine.Object.FindAnyObjectByType<LocalizationService>();
            ui=new LocalizedPanelBuilder(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/Station Multilingual SDF.asset"),language);
            var settings=UnityEngine.Object.Instantiate(flight.Config);settings.name="Orbital Flight Config";settings.ConfigureOrbit(true);settings.ConfigureVitals(180,100,100);
            const string configPath=Root+"/Settings/Orbital Flight Config.asset";var cached=AssetDatabase.LoadAssetAtPath<AscentConfig>(configPath);
            if(cached==null)AssetDatabase.CreateAsset(settings,configPath);else{EditorUtility.CopySerialized(settings,cached);UnityEngine.Object.DestroyImmediate(settings);settings=cached;EditorUtility.SetDirty(settings);}
            flight.Configure(settings,session.Mission,session.GetComponent<CargoInventory>());
            ImproveBoarding(session,layout,scene);
            foreach(var t in scene.GroundRoot.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if(t.name=="Lunar Ground Slab" || t.name.StartsWith("Lunar Escape Route "))UnityEngine.Object.DestroyImmediate(t.gameObject);
                else if(t.name.StartsWith("Moon Boulder ")){var p=t.position;p.y=LunarTerrainProfile.Height(p.x,p.z)-.08f;t.position=p;t.GetComponent<Renderer>().sharedMaterial=BuildLunarTerrain.Material("Crater Rock",new Color(.32f,.31f,.3f));}
                else if(t.name=="Moon Perimeter"){var p=t.position;p.y=8;t.position=p;var size=t.localScale;size.y=36;t.localScale=size;}
            }
            var outside=layout.OutsideWorld;
            var oldGround=outside.Find("Flight Lunar Ground");if(oldGround!=null)UnityEngine.Object.DestroyImmediate(oldGround.gameObject);
            var panorama=outside.Find("Animated Lunar Panorama");if(panorama!=null)panorama.gameObject.SetActive(false);
            var terrain=BuildLunarTerrain.Build(scene.GroundRoot.transform,outside,session);
            var moonMat=BuildLunarTerrain.Material("Orbital Moon",new Color(.5f,.49f,.47f));
            var textureImporter=(TextureImporter)AssetImporter.GetAtPath(Root+"/Art/Orbit/lroc_color_poles_4k.tif");textureImporter.maxTextureSize=4096;textureImporter.anisoLevel=4;textureImporter.SaveAndReimport();
            moonMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/Orbit/lroc_color_poles_4k.tif"));moonMat.SetFloat("_UseMap",1);moonMat.SetFloat("_NoiseScale",3500);moonMat.SetFloat("_InvertFade",1);
            moonMat.SetFloat("_SrcBlend",5);moonMat.SetFloat("_DstBlend",10);moonMat.SetFloat("_ZWrite",0);moonMat.renderQueue=3000;
            var moon=BuildLunarTerrain.MeshObject(scene.FlightWorld.transform,"Orbital Moon",BuildLunarTerrain.CreateMoon(),moonMat);moon.SetActive(false);
            moon.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            scene.FlightWorld.AddComponent<OrbitalMoonView>().Configure(flight,moon.transform,terrain);
            RebuildCabin(session,scene,flight);
            session.Player.Camera.farClipPlane=50000;
            scene.GroundRoot.SetActive(true);scene.FlightWorld.SetActive(false);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
            Debug.Log("LUNAR_ORBIT_SCENE_READY");
        }
        public static void PreviewScene()
        {
            EditorSceneManager.OpenScene(ScenePath);var session=UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();var presenter=session.GetComponent<FlightScenePresenter>();
            ImportApolloModel.Capture("orbit-terrain-editor",new Vector3(12,3.2f,-9),new Vector3(34,0,18));
            ImportApolloModel.Capture("orbit-boarding-editor",new Vector3(48,1.65f,-6.7f),new Vector3(49.1f,1.6f,-3.9f));
            presenter.GroundRoot.SetActive(false);presenter.FlightWorld.SetActive(true);
            ImportApolloModel.Capture("orbit-cabin-editor",new Vector3(99.1f,1.65f,-.6f),new Vector3(100.5f,1.5f,.9f));
            ImportApolloModel.Capture("orbit-companion-editor",new Vector3(102,1.4f,1.8f),new Vector3(101,1.0f,0));
        }
        private static void ImproveBoarding(StationMissionSession session,LunarViewLayout layout,FlightScenePresenter scene)
        {
            // 同一登月舱的游戏版略加宽，为两套 VR 操作位预留手部空间；高度仍为 7 m。
            layout.Lander.localScale=new Vector3(1.18f,1,1.18f);
            session.Exit.transform.position=new Vector3(48,1.2f,-5);session.Exit.Volume.size=new Vector3(5.2f,2.4f,5.2f);
            var hatch=layout.Door.GetComponent<HatchBoardingController>();
            foreach(var presenter in scene.GroundRoot.GetComponentsInChildren<MissionPresenter>(true))presenter.ConfigureInstructionPrefix("orbitalground.");
            scene.GroundRoot.GetComponentsInChildren<LocalizedText>(true).Single(t=>t.name=="Ladder Approach Sign").SetKey("boarding.route");
            var panel=ui.Panel("Accessible Boarding Control",new Vector3(49.4f,1.65f,-2.45f),new Vector2(1100,760),.00135f,Quaternion.identity);panel.SetParent(scene.GroundRoot.transform,true);
            var button=ui.Button(panel,"Board at Ladder","boarding.enter",new Vector2(0,110),new Vector2(1030,420),62);
            var hint=ui.Label(panel,"Boarding Position Hint","boarding.approach",new Vector2(0,-225),new Vector2(1040,190),40,Color.white);
            hatch.ConfigureAccessiblePrompt(button,hint,8);
            UnityEventTools.AddPersistentListener(button.onClick,hatch.RequestBoarding);
            Box(scene.GroundRoot.transform,"Boarding Control Post",new Vector3(49.4f,.6f,-2.32f),new Vector3(.12f,1.2f,.12f),"Station Graphite");
            var marker=ui.WorldLabel("Boarding Zone Label","boarding.zone",new Vector3(48,.045f,-6.4f),new Vector2(3,1),.38f,Quaternion.Euler(90,0,0));marker.transform.SetParent(scene.GroundRoot.transform,true);
            foreach(float x in new[]{45.4f,50.6f})Box(scene.GroundRoot.transform,"Boarding Zone Edge",new Vector3(x,.018f,-5),new Vector3(.055f,.014f,5.2f),"Guide Teal",false);
            foreach(float z in new[]{-7.6f,-2.4f})Box(scene.GroundRoot.transform,"Boarding Zone Edge",new Vector3(48,.018f,z),new Vector3(5.2f,.014f,.055f),"Guide Teal",false);
            var hatchCollider=hatch.GetComponent<BoxCollider>();hatchCollider.size=new Vector3(1.4f,1.4f,.22f);
        }
        private static void RebuildCabin(StationMissionSession session,FlightScenePresenter scene,AscentMission flight)
        {
            var world=scene.FlightWorld.transform;
            foreach(Transform t in world.GetComponentsInChildren<Transform>(true).ToArray())
                if(t.parent==world && new[]{"Cabin Floor","Cabin Ceiling","Cabin Back","Console Back","Side Lower","Side Upper","Window Frame","Window Border"}.Contains(t.name))UnityEngine.Object.DestroyImmediate(t.gameObject);
            Box(world,"Cabin Floor",new Vector3(0,-.1f,0),new Vector3(4.4f,.2f,4.8f),"Station Graphite");
            Box(world,"Cabin Ceiling",new Vector3(0,3.15f,0),new Vector3(4.4f,.15f,4.8f),"Station Shell");
            Box(world,"Cabin Back",new Vector3(0,1.5f,-2.35f),new Vector3(4.4f,3,.12f),"Station Shell");
            Box(world,"Console Back",new Vector3(0,1.55f,2.3f),new Vector3(4.4f,3.1f,.12f),"Station Graphite");
            foreach(float side in new[]{-2.18f,2.18f})
            {
                Box(world,"Side Lower",new Vector3(side,.34f,0),new Vector3(.12f,.68f,4.8f),"Station Shell");Box(world,"Side Upper",new Vector3(side,2.91f,0),new Vector3(.12f,.5f,4.8f),"Station Shell");
                foreach(float z in new[]{-2.28f,2.28f})Box(world,"Window Frame",new Vector3(side,1.68f,z),new Vector3(.18f,2.05f,.2f),"Safety Amber");
                foreach(float y in new[]{.72f,2.65f})Box(world,"Window Border",new Vector3(side,y,0),new Vector3(.18f,.09f,4.7f),"Safety Amber");
            }
            scene.Seat.localPosition=new Vector3(-.9f,0,-.6f);scene.Panel.transform.localPosition=new Vector3(-.88f,1.63f,1.48f);scene.Panel.transform.localScale=Vector3.one*.00145f;
            var npcStation=new GameObject("Commander Station").transform;npcStation.SetParent(world,false);npcStation.localPosition=new Vector3(1.02f,0,-.05f);
            var prefab=ImportApolloModel.ImportStatic(Root+"/Art/Crew",Root+"/Art/Crew/Commander Astronaut.prefab","CrewMeshes.bytes",1.8f,"Commander Astronaut");
            var npc=(GameObject)PrefabUtility.InstantiatePrefab(prefab);npc.name="Commander - visible companion";npc.transform.SetParent(npcStation,false);npc.transform.localRotation=Quaternion.Euler(0,180,0);
            PoseCompanion(npc);
            foreach(float x in new[]{-.9f,1.02f})
            {
                Box(world,"Crew Foot Platform",new Vector3(x,.025f,-.25f),new Vector3(1.25f,.05f,1.6f),"Lunar Route Soil");
                Box(world,"Crew Back Support",new Vector3(x,.9f,-1.05f),new Vector3(.8f,1.2f,.12f),"Station Graphite");
                Box(world,"Crew Safety Rail",new Vector3(x,.88f,.45f),new Vector3(1.05f,.055f,.06f),"Safety Amber");
            }
            var npcLabel=ui.WorldLabel("Commander Station Label","crew.commander",world.TransformPoint(new Vector3(1.02f,2.3f,1.55f)),new Vector2(1.8f,.5f),.28f,Quaternion.identity);npcLabel.transform.SetParent(world,true);
            var crewStatus=ui.Panel("Commander Status",world.TransformPoint(new Vector3(1.24f,1.35f,1.55f)),new Vector2(720,520),.0013f,Quaternion.identity);crewStatus.SetParent(world,true);
            ui.Label(crewStatus,"Commander Status Text","crew.status",Vector2.zero,new Vector2(660,450),38,new Color(.55f,1,.88f));
            world.gameObject.AddComponent<CrewCabinLayout>().Configure(scene.Seat,npcStation,npc,new Vector3(4.4f,3.15f,4.8f));
            // 保留原物资和启动面板，下方空位加入连续仪表和一次性入轨修正。
            FindIn(scene.Panel,"Flight Supplies Hint").gameObject.SetActive(false);
            var telemetry=ui.Label(scene.Panel.transform,"Orbital Telemetry","orbit.telemetry",new Vector2(0,-150),new Vector2(1490,65),29,new Color(.6f,.94f,1));
            var guidance=ui.Label(scene.Panel.transform,"Orbital Guidance","orbit.guidance.ascent",new Vector2(0,-225),new Vector2(1490,75),26,Color.white);
            var circularize=ui.Button(scene.Panel.transform,"Circularize Orbit","orbit.circularize",new Vector2(0,-322),new Vector2(840,83),31);
            UnityEventTools.AddPersistentListener(circularize.onClick,flight.Circularize);
            var result=FindIn(scene.Panel,"Flight Result").GetComponent<RectTransform>();result.anchoredPosition=new Vector2(0,-320);result.sizeDelta=new Vector2(1490,95);result.GetComponent<TMP_Text>().fontSize=25;
            scene.Panel.AddComponent<OrbitalPanelPresenter>().Configure(flight,telemetry,guidance,circularize);
        }
        // 原始 NASA 模型为展开双臂的静态展示姿态；烘焙一个自然下垂的舱内版本，原始网格保留。
        private static void PoseCompanion(GameObject npc)
        {
            int index=0;
            foreach(var filter in npc.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;var normals=mesh.normals;
                var toRoot=npc.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var toPart=toRoot.inverse;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=toRoot.MultiplyPoint3x4(vertices[i]);float side=Mathf.Sign(p.x);
                    float blend=Mathf.SmoothStep(0,1,(Mathf.Abs(p.x)-.24f)/.15f)*Mathf.SmoothStep(0,1,(p.y-1.03f)/.13f);
                    var shoulder=new Vector3(side*.27f,1.38f,0);
                    p=Vector3.Lerp(p,shoulder+Quaternion.AngleAxis(-side*64,Vector3.forward)*(p-shoulder),blend);
                    vertices[i]=toPart.MultiplyPoint3x4(p);
                    normals[i]=toPart.MultiplyVector(Quaternion.AngleAxis(-side*64*blend,Vector3.forward)*toRoot.MultiplyVector(normals[i])).normalized;
                }
                mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();mesh.name="Commander relaxed pose "+index;
                string path=Root+"/Art/Crew/Relaxed Mesh "+index+++".asset";var cached=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(cached==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,cached);UnityEngine.Object.DestroyImmediate(mesh);mesh=cached;EditorUtility.SetDirty(mesh);}
                filter.sharedMesh=mesh;
            }
        }
        private static Transform FindIn(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,string mat,bool collision=true)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+mat+".mat");if(!collision)UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
    }
}
