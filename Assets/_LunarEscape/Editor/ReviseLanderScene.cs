using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape.Editor
{
    // 仅升级当前第五步的登月器、月面路线与窗外表现；前四个教学场景不改动。
    public static class ReviseLanderScene
    {
        private const string Root="Assets/_LunarEscape";
        public static readonly Vector3 PadPosition=new Vector3(48,0,1.7f);
        public static readonly Vector3 Approach=new Vector3(48,1.2f,-4.3f);
        private static LocalizedPanelBuilder ui;
        public static void Upgrade()
        {
            AssetDatabase.Refresh();BuildRepairScene.RefreshFont();
            var scene=EditorSceneManager.OpenScene(BuildAscentScene.ScenePath);
            Install();TuneView();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("LUNAR_LANDER_REVISION_READY");
        }
        public static void Install()
        {
            if(Find("Lander Revision")!=null) return;
            var session=UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();var flight=session.GetComponent<AscentMission>();
            var inventory=session.GetComponent<CargoInventory>();var presenter=session.GetComponent<FlightScenePresenter>();
            var ground=presenter.GroundRoot.transform;
            var language=UnityEngine.Object.FindAnyObjectByType<LocalizationService>();
            ui=new LocalizedPanelBuilder(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/Station Multilingual SDF.asset"),language);
            var revision=new GameObject("Lander Revision");revision.transform.SetParent(ground,false);
            // 沿用原来的身体区域引用，避免留下一份可从旧船舱触发的入口。
            var zone=session.Exit;zone.transform.SetParent(revision.transform,true);zone.name="Ladder Boarding Approach";
            zone.transform.position=Approach;zone.Volume.size=new Vector3(2.2f,2.4f,1.8f);
            var oldRoute=session.GetComponent<LunarRouteAccess>();if(oldRoute!=null)UnityEngine.Object.DestroyImmediate(oldRoute);
            Remove(BuildLunarSurface.CabinRootName);Remove("Cabin Manifest");Remove("Cabin Boarding Hatch");Remove(BuildLunarSurface.SurfaceRootName);
            BuildSurface(revision.transform,out var areas);
            session.gameObject.AddComponent<LanderRouteAccess>().Configure(session.Mission,areas);
            var lander=(GameObject)PrefabUtility.InstantiatePrefab(ImportApolloModel.Import());lander.name="Apollo Lander Exterior";lander.transform.SetParent(revision.transform,false);lander.transform.position=PadPosition;
            // 原模型带有梯子和四条支腿。碰撞只用少量简单形状，避免 10 万面模型参与动态碰撞。
            ColliderBox(lander.transform,"Closed Descent Stage",new Vector3(0,1.55f,0),new Vector3(4.3f,2.7f,4.3f));
            ColliderBox(lander.transform,"Closed Ascent Cabin",new Vector3(0,4.6f,0),new Vector3(3.6f,3.4f,3.6f));
            ColliderBox(lander.transform,"Ladder - boarding by hatch only",new Vector3(0,1.5f,-3.25f),new Vector3(.9f,2.9f,.5f));
            foreach(var foot in new[]{new Vector3(4.4f,.35f,0),new Vector3(-4.4f,.35f,0),new Vector3(0,.35f,4.4f),new Vector3(0,.35f,-4.4f)})
                ColliderBox(lander.transform,"Landing Foot Collision",foot,new Vector3(1.0f,.65f,1.0f));
            var hatch=BuildHatch(lander.transform,session,inventory);
            var baseExterior=BuildBaseExterior(ground);
            var world=presenter.FlightWorld.transform;
            var outside=new GameObject("Exterior in Lunar Coordinates").transform;outside.SetParent(world,false);
            var baseProxy=new GameObject("Distant Base");baseProxy.transform.SetParent(outside,false);
            foreach(var source in baseExterior.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(source.GetComponent<TMP_Text>()!=null || !source.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh==null)continue;
                var obj=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(baseProxy.transform,false);
                obj.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                // 此时父节点尚无位移；复制米制坐标，随后统一由飞船坐标变换驱动。
                obj.transform.localPosition=source.transform.position;
                obj.transform.localRotation=source.transform.rotation;obj.transform.localScale=source.transform.lossyScale;
                obj.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;obj.GetComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
            }
            var animation=world.GetComponent<LunarWindowAnimation>();
            var oldBase=world.Find("Distant Base");if(oldBase!=null)UnityEngine.Object.DestroyImmediate(oldBase.gameObject);
            var landscape=world.Find("Animated Lunar Panorama");landscape.SetParent(outside,false);landscape.localPosition=new Vector3(0,-300,0);landscape.localScale=Vector3.one*100;
            // 近处真实地面承接基地与着陆器，远月面仍是原有贴图曲面。
            Box(outside,"Flight Lunar Ground",new Vector3(0,-.2f,0),new Vector3(500,.3f,500),"Lunar Soil",false);
            var blast=world.Find("Distant Blast");blast.SetParent(outside,false);blast.localPosition=new Vector3(0,2.2f,0);
            var debris=world.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Distant Debris ")).ToArray();
            foreach(var fragment in debris){fragment.SetParent(outside,false);fragment.localScale=Vector3.one*.3f;}
            Vector3 launch=PadPosition+Vector3.up*3.6f;
            animation.ConfigureWorld(outside,launch,baseProxy.transform,blast,debris);
            outside.localPosition=-launch;
            var sunlight=new GameObject("Flight Sunlight").AddComponent<Light>();sunlight.transform.SetParent(outside,false);sunlight.type=LightType.Directional;sunlight.intensity=1.35f;sunlight.transform.localRotation=Quaternion.Euler(38,-35,0);
            world.gameObject.AddComponent<FlightImpactMotion>().Configure(flight);
            session.gameObject.AddComponent<LunarViewLayout>().Configure(lander.transform,baseExterior,baseProxy,outside,hatch,launch);
            foreach(var p in ground.GetComponentsInChildren<MissionPresenter>(true))p.ConfigureInstructionPrefix("lander.");
            var simulator=UnityEngine.Object.FindAnyObjectByType<CollisionAwareSimulator>();simulator.bodyTranslateMultiplier=12;
            // 为下一次 Play 保存正式 Briefing 初始状态，而不是预览状态。
            ground.gameObject.SetActive(true);world.gameObject.SetActive(false);
            TuneView();
        }
        private static void TuneView()
        {
            var session=UnityEngine.Object.FindAnyObjectByType<StationMissionSession>();
            session.Player.Camera.farClipPlane=2000;
            var world=session.GetComponent<FlightScenePresenter>().FlightWorld;
            var panorama=AssetImporter.GetAtPath(Root+"/Art/Flight/LunarPanorama.png") as TextureImporter;
            if(panorama!=null && panorama.wrapMode!=TextureWrapMode.Clamp) {panorama.wrapMode=TextureWrapMode.Clamp;panorama.SaveAndReimport();}
            // 下窗沿略低于常见眼高，让短暂垂直离地时仍能向下观察基地。
            foreach(Transform part in world.GetComponentsInChildren<Transform>(true))
            {
                if(part.name=="Side Lower") {var at=part.localPosition;at.y=.375f;part.localPosition=at;var size=part.localScale;size.y=.75f;part.localScale=size;}
                if(part.name=="Window Border" && part.localPosition.y<1.1f) {var at=part.localPosition;at.y=.79f;part.localPosition=at;}
                if(part.name=="Window Frame") {var at=part.localPosition;at.y=1.72f;part.localPosition=at;var size=part.localScale;size.y=1.95f;part.localScale=size;}
            }
        }
        private static void BuildSurface(Transform root,out TeleportationArea[] areas)
        {
            Box(root,"Lunar Ground Slab",new Vector3(25,-.22f,0),new Vector3(112,.4f,72),"Lunar Soil");
            var segments=new[]{(new Vector3(23,-.035f,1.7f),new Vector3(30,.07f,3)),(new Vector3(38,-.035f,-1.3f),new Vector3(3,.07f,9)),(new Vector3(43,-.035f,-4.3f),new Vector3(13,.07f,3))};
            areas=new TeleportationArea[segments.Length];
            for(int i=0;i<segments.Length;i++)
            {
                var s=segments[i];Box(root,"Lunar Escape Route "+i,s.Item1,s.Item2,"Lunar Route Soil");
                var surface=Box(root,"Lander Route Teleport "+i,s.Item1+Vector3.up*.04f,new Vector3(s.Item2.x-.6f,.01f,s.Item2.z-.6f),"Lunar Soil");surface.GetComponent<Renderer>().enabled=false;
                areas[i]=surface.AddComponent<TeleportationArea>();areas[i].interactionLayers=1<<31;areas[i].enabled=false;
            }
            for(int i=0;i<11;i++)
            {
                float x=9+i*2.7f;
                foreach(float z in new[]{.2f,3.2f})Beacon(root,new Vector3(x,0,z));
            }
            foreach(float z in new[]{-1.2f,-3.9f})Beacon(root,new Vector3(36.5f,0,z));
            foreach(float x in new[]{40f,43f,46f})foreach(float z in new[]{-5.8f,-2.8f})Beacon(root,new Vector3(x,0,z));
            for(int i=0;i<22;i++)
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name="Moon Boulder "+i;obj.transform.SetParent(root,false);
                obj.transform.localPosition=new Vector3(10+(i%11)*5,-.08f,(i<11 ? 9 : -12)+(i%3)*2);
                obj.transform.localScale=new Vector3(1.5f+(i%3)*.5f,.6f+(i%2)*.4f,1.2f);obj.GetComponent<Renderer>().sharedMaterial=Mat("Lunar Rock");
            }
            foreach(var edge in new[]{(new Vector3(-31,2,0),new Vector3(.4f,4,72)),(new Vector3(81,2,0),new Vector3(.4f,4,72)),(new Vector3(25,2,-36),new Vector3(112,4,.4f)),(new Vector3(25,2,36),new Vector3(112,4,.4f))})
            {
                var wall=Box(root,"Moon Perimeter",edge.Item1,edge.Item2,"Lunar Soil");wall.GetComponent<Renderer>().enabled=false;
            }
            var sign=ui.WorldLabel("Ladder Approach Sign","lander.route",new Vector3(37,1.75f,-3.9f),new Vector2(2.3f,.8f),.5f,Quaternion.Euler(0,90,0));sign.transform.SetParent(root,true);
        }
        private static Transform BuildHatch(Transform lander,StationMissionSession session,CargoInventory cargo)
        {
            var hatch=new GameObject("Boarding Hatch");hatch.transform.SetParent(lander,false);hatch.transform.localPosition=new Vector3(.04f,4.09f,-1.94f);
            var collider=hatch.AddComponent<BoxCollider>();collider.size=new Vector3(1.02f,1.04f,.18f);
            var select=hatch.AddComponent<XRSimpleInteractable>();select.interactionLayers=1;
            var panel=ui.Panel("Hatch Control",hatch.transform.position+Vector3.back*.12f,new Vector2(800,800),.00105f,Quaternion.identity);panel.SetParent(hatch.transform,true);
            var button=ui.Button(panel,"Enter Through Hatch","lander.hatch.enter",new Vector2(0,100),new Vector2(730,390),75);
            var hint=ui.Label(panel,"Hatch Hint","lander.hatch.approach",new Vector2(0,-230),new Vector2(740,210),42,Color.white);
            var control=hatch.AddComponent<HatchBoardingController>();control.Configure(session,cargo,hatch.transform,button,select,hint);
            UnityEventTools.AddPersistentListener(button.onClick,control.RequestBoarding);UnityEventTools.AddPersistentListener(select.selectEntered,control.OnSelected);
            return hatch.transform;
        }
        private static GameObject BuildBaseExterior(Transform ground)
        {
            var root=new GameObject("Station Exterior - shared scale");root.transform.SetParent(ground,false);
            var room=Find("Station Room - editable blockout");room.transform.SetParent(root.transform,true);
            // 外壳补齐科研站的尺度参照；可玩区域仍然是原来的系统舱。
            foreach(float z in new[]{-8.5f,8.5f})
            {
                var module=GameObject.CreatePrimitive(PrimitiveType.Cylinder);module.name="Station Habitat Shell";module.transform.SetParent(root.transform,false);module.transform.localPosition=new Vector3(-3.5f,1.9f,z);module.transform.localRotation=Quaternion.Euler(0,0,90);module.transform.localScale=new Vector3(3.7f,4.5f,3.7f);module.GetComponent<Renderer>().sharedMaterial=Mat("Panel White");
                Box(root.transform,"Station Connector",new Vector3(-2,1.5f,Mathf.Sign(z)*5.375f),new Vector3(2.6f,2.8f,2.55f),"Station Shell");
                foreach(float x in new[]{-7.5f,.5f})Box(root.transform,"Habitat Structural Band",new Vector3(x,1.9f,z),new Vector3(.12f,3.8f,3.8f),"Station Graphite");
                Box(root.transform,"Solar Array",new Vector3(-10,1.3f,z),new Vector3(5,.15f,5.5f),"Station Graphite");
                for(int i=0;i<5;i++)Box(root.transform,"Solar Cell Stripe",new Vector3(-12+i,1.39f,z),new Vector3(.025f,.02f,5.4f),"Guide Teal",false);
            }
            Box(root.transform,"Antenna Mast",new Vector3(-7,3.2f,0),new Vector3(.18f,6.4f,.18f),"Panel White");
            var dish=GameObject.CreatePrimitive(PrimitiveType.Sphere);dish.name="Station Communications Dish";dish.transform.SetParent(root.transform,false);dish.transform.localPosition=new Vector3(-7,6,0);dish.transform.localScale=new Vector3(2,.25f,2);dish.transform.localRotation=Quaternion.Euler(35,0,0);dish.GetComponent<Renderer>().sharedMaterial=Mat("Panel White");
            return root;
        }
        private static void Beacon(Transform root,Vector3 at)
        {
            Box(root,"Route Marker",at+Vector3.up*.23f,new Vector3(.08f,.46f,.08f),"Station Graphite");Box(root,"Route Beacon",at+Vector3.up*.5f,new Vector3(.14f,.08f,.14f),"Guide Teal",false);
        }
        private static void ColliderBox(Transform parent,string name,Vector3 position,Vector3 size)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.AddComponent<BoxCollider>().size=size;
        }
        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,string material,bool collision=true)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=Mat(material);
            if(!collision)UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
        private static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");
        private static GameObject Find(string name)=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name==name)?.gameObject;
        private static void Remove(string name){var obj=Find(name);if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);}
    }
}
