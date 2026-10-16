using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    // 可重复安装的返航尾声；只扩展场景11，保留旧教学关卡的对接结算。
    public static class InstallEarthReturnScene
    {
        private const string Art = "Assets/_LunarEscape/Art/EarthReturn";
        private static Material white, dark, blue, glow, orange, trail;
        [MenuItem("Lunar Escape/Install Earth Return Sequence (Life Support Scene)")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs/LayoutRevisionBackup");
            var backup = "Logs/LayoutRevisionBackup/11-before-earth-return.unity";
            if (!File.Exists(backup)) File.Copy(BuildLifeSupportScene.ScenePath, backup);
            BuildRepairScene.RefreshFont();
            EditorSceneManager.OpenScene(BuildLifeSupportScene.ScenePath);
            ConfigureCurrent(Object.FindAnyObjectByType<StationMissionSession>());
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Debug.Log("EARTH_RETURN_SEQUENCE_INSTALLED");
        }
        public static void ConfigureCurrent(StationMissionSession session)
        {
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            var old = session.gameObject.scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Earth Return Experience");
            if (old != null) Object.DestroyImmediate(old);
            white = Material("Pearl shell", new Color(.65f,.73f,.77f)); dark = Material("Graphite",new Color(.025f,.04f,.055f));
            blue = Material("Seat fabric",new Color(.06f,.13f,.21f)); glow = Material("Cyan light",new Color(.25f,.85f,1),true);
            orange = Material("Rescue orange",new Color(.85f,.12f,.035f)); trail = Material("Entry streak",Color.white,true);
            trail.shader=Shader.Find("LunarEscape/Return Plasma");EditorUtility.SetDirty(trail);
            var mission = Component<EarthReturnMission>(session.gameObject); mission.Configure(session.GetComponent<AscentMission>(),session.GetComponent<CrewMission>());
            var presentation = session.GetComponent<FlightScenePresenter>();
            var root = new GameObject("Earth Return Experience").transform;
            var cabin = Child(root,"Orbital Return Cabin",new Vector3(250,0,0));
            var seat = Child(cabin,"Return Player Station",new Vector3(0,-.20f,-.86f));
            var shell=BuildCabin(cabin);
            var commander = Crew(cabin,"Returned Commander",new Vector3(-.96f,0,-.45f));
            var pilot = Crew(cabin,"Orbital Pilot",new Vector3(.96f,0,-.45f));
            pilot.localRotation = Quaternion.identity;
            var hatch=Child(cabin,"Top hatch hinge",new Vector3(.65f,3.13f,0));
            foreach(var part in cabin.GetComponentsInChildren<Transform>().Where(t=>t.name=="Top transfer hatch" || t.name.StartsWith("Hatch handrail")).ToArray())
            {part.SetParent(hatch,false);part.localPosition=new Vector3(-.65f,-3.13f,0);}
            var lander=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BuildApolloExterior.PrefabPath));
            PrefabUtility.UnpackPrefabInstance(lander,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            lander.name="Separated Ascent Stage";lander.transform.SetParent(cabin,false);
            foreach(var part in lander.GetComponentsInChildren<MeshRenderer>().ToArray())
                if(part.name!="Ascentstructure" && part.name!="CabinPanels" && part.name!="Hardware")Object.DestroyImmediate(part.gameObject);
            var globeImporter=(TextureImporter)AssetImporter.GetAtPath(Art+"/NASA Earth Equirectangular.jpg");
            globeImporter.maxTextureSize=4096;globeImporter.wrapModeU=TextureWrapMode.Repeat;globeImporter.wrapModeV=TextureWrapMode.Clamp;globeImporter.SaveAndReimport();
            var earthMat=Material("Rotating Earth",Color.white);earthMat.shader=Shader.Find("LunarEscape/Return Earth Globe");
            earthMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/NASA Earth Equirectangular.jpg"));EditorUtility.SetDirty(earthMat);
            var earth=Primitive(cabin,"Rotating Earth Globe",PrimitiveType.Sphere,new Vector3(2500,1300,5000),Vector3.one*600,earthMat);
            earth.GetComponent<MeshFilter>().sharedMesh=BuildGlobeMesh();
            var moon = Primitive(cabin,"Departing Moon",PrimitiveType.Sphere,new Vector3(-100,-50,280),Vector3.one*180,Material("Moon",new Color(.42f,.43f,.44f)));
            var moonSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/_LunarEscape/Materials/Orbital Moon.mat");
            if (moonSource != null) moon.GetComponent<Renderer>().sharedMaterial.SetTexture("_BaseMap",moonSource.GetTexture("_BaseMap"));
            var service = BuildReturnAssembly.ServiceModule(cabin);
            BuildReturnAssembly.UpdateDockingTarget(presentation);
            var seaMat = Material("Pacific Ocean",new Color(.025f,.22f,.32f)); seaMat.shader=Shader.Find("LunarEscape/Return Ocean");EditorUtility.SetDirty(seaMat);
            var ocean = Primitive(cabin,"Pacific Ocean",PrimitiveType.Plane,new Vector3(0,-250,0),Vector3.one*800,seaMat);
            var chutes = BuildParachutes(cabin, mission);
            var streaks = BuildPlasma(cabin);
            var shock=BuildShockLayer(cabin);
            var heatLight=Child(cabin,"Entry glow",new Vector3(0,1.8f,1.55f)).gameObject.AddComponent<Light>();
            heatLight.type=LightType.Point;heatLight.range=4;heatLight.color=new Color(1,.22f,.035f);heatLight.intensity=0;
            var language = Object.FindAnyObjectByType<LocalizationService>();
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_LunarEscape/Fonts/Station Multilingual SDF.asset");
            var ui=new LocalizedPanelBuilder(font,language);
            var transfer = ui.Panel("Confirm Orbital Transfer",new Vector3(99.95f,1.6f,1.30f),new Vector2(1250,790),.0012f,Quaternion.identity);
            transfer.SetParent(root,true);
            ui.Label(transfer,"Transfer title","return.phase.AwaitingTransfer",new Vector2(0,295),new Vector2(1130,100),44,new Color(.4f,.9f,1));
            var transferText=ui.Label(transfer,"Transfer details","return.transfer.detail",new Vector2(0,75),new Vector2(1110,275),35,Color.white);
            var transferButton=ui.Button(transfer,"Transfer to orbiter","return.action.transfer",new Vector2(0,-220),new Vector2(1000,130),39);
            UnityEventTools.AddPersistentListener(transferButton.onClick,mission.Confirm);
            var panel=ui.Panel("Earth Return Dashboard",cabin.TransformPoint(new Vector3(0,2.20f,.815f)),new Vector2(1480,800),.00115f,Quaternion.Euler(-25,0,0));
            panel.SetParent(root,true);
            ui.Label(panel,"Return header","return.title",new Vector2(0,350),new Vector2(1380,50),28,new Color(.4f,.9f,1));
            var phase=ui.Label(panel,"Return phase","return.phase.CrewTransfer",new Vector2(0,275),new Vector2(1370,80),32,Color.white);
            var detail=ui.Label(panel,"Return instruction","return.detail.CrewTransfer",new Vector2(0,-80),new Vector2(430,195),23,new Color(.7f,.87f,.92f));
            var crew=ui.Label(panel,"Return crew","return.crew.all",new Vector2(0,-260),new Vector2(430,110),21,new Color(.42f,.95f,.71f));
            ui.Rectangle(panel,"Progress background",new Vector2(0,-330),new Vector2(1350,7),new Color(.12f,.22f,.26f));
            var progress=ui.Rectangle(panel,"Stage progress",new Vector2(0,-330),new Vector2(1350,7),new Color(.3f,.82f,1));
            progress.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");progress.type=Image.Type.Filled;progress.fillMethod=Image.FillMethod.Horizontal;progress.fillOrigin=0;
            var action=ui.Button(panel,"Confirm Return Stage","return.action.separate",new Vector2(-480,-95),new Vector2(420,100),25);
            var pause=ui.Button(panel,"Pause Return Animation","return.pause",new Vector2(-480,-205),new Vector2(360,70),25);
            UnityEventTools.AddPersistentListener(action.onClick,mission.Confirm);UnityEventTools.AddPersistentListener(pause.onClick,mission.TogglePause);
            ui.Label(panel,"Compressed journey","return.compressed",new Vector2(0,-371),new Vector2(1350,35),18,new Color(.45f,.62f,.7f));
            var guidance=Child(panel,"Entry Guidance",Vector3.zero);
            var telemetry=ui.Label(guidance,"Entry telemetry","return.telemetry",new Vector2(0,128),new Vector2(430,202),25,Color.white);
            var down=ui.Button(guidance,"Shallower angle","return.shallower",new Vector2(355,65),new Vector2(195,70),22);
            var up=ui.Button(guidance,"Steeper angle","return.steeper",new Vector2(575,65),new Vector2(195,70),22);
            var corridor=Child(guidance,"Entry corridor gauge",Vector3.zero);
            ui.Rectangle(corridor,"Danger range",new Vector2(465,-78),new Vector2(400,14),new Color(.6f,.13f,.055f));
            ui.Rectangle(corridor,"Safe entry range",new Vector2(465,-78),new Vector2(400*2/11f,14),new Color(.12f,.8f,.42f));
            var cursor=ui.Rectangle(corridor,"Selected angle",new Vector2(465,-78),new Vector2(7,26),Color.white).rectTransform;
            UnityEventTools.AddPersistentListener(down.onClick,mission.Shallower);UnityEventTools.AddPersistentListener(up.onClick,mission.Steeper);
            var throttleText=ui.Label(panel,"Throttle setting","return.throttle",new Vector2(-480,170),new Vector2(410,70),30,Color.white);
            ui.Rectangle(panel,"Throttle background",new Vector2(-480,103),new Vector2(390,15),new Color(.10f,.16f,.2f));
            var throttleBar=ui.Rectangle(panel,"Throttle value",new Vector2(-480,103),new Vector2(390,15),new Color(.3f,.85f,1));
            throttleBar.sprite=progress.sprite;throttleBar.type=Image.Type.Filled;throttleBar.fillMethod=Image.FillMethod.Horizontal;throttleBar.fillOrigin=0;
            var less=ui.Button(panel,"Throttle down","return.throttle.down",new Vector2(-590,25),new Vector2(190,72),25);
            var more=ui.Button(panel,"Throttle up","return.throttle.up",new Vector2(-370,25),new Vector2(190,72),25);
            var direction=ui.Label(panel,"Entry direction","return.direction",new Vector2(465,170),new Vector2(420,90),25,Color.white);
            var left=ui.Button(panel,"Yaw left","return.turn.left",new Vector2(355,-15),new Vector2(195,60),22);
            var right=ui.Button(panel,"Yaw right","return.turn.right",new Vector2(575,-15),new Vector2(195,60),22);
            var deploy=ui.Button(panel,"Deploy parachutes","return.deploy",new Vector2(465,-168),new Vector2(420,95),26);
            var release=ui.Button(panel,"Release heat shield","return.release",new Vector2(465,-275),new Vector2(420,85),26);
            UnityEventTools.AddPersistentListener(less.onClick,mission.DecreaseThrottle);UnityEventTools.AddPersistentListener(more.onClick,mission.IncreaseThrottle);
            UnityEventTools.AddPersistentListener(left.onClick,mission.TurnLeft);UnityEventTools.AddPersistentListener(right.onClick,mission.TurnRight);
            UnityEventTools.AddPersistentListener(deploy.onClick,mission.DeployParachutes);UnityEventTools.AddPersistentListener(release.onClick,mission.ReleaseHeatShield);
            var throttleGroup = Child(panel, "Flight throttle controls", Vector3.zero);
            foreach (var item in new[] { throttleText.transform, panel.Find("Throttle background"), throttleBar.transform, less.transform, more.transform }) item.SetParent(throttleGroup, false);
            var altitudePanel = Child(panel, "Continuous descent altitude", Vector3.zero);
            ui.Label(altitudePanel, "Altitude gauge title", "return.altitude.title", new Vector2(-480, 190), new Vector2(430, 60), 27, Color.white);
            ui.Rectangle(altitudePanel, "Altitude scale", new Vector2(-650, -55), new Vector2(28, 360), new Color(.12f, .20f, .25f));
            ui.Rectangle(altitudePanel, "Parachute altitude window", new Vector2(-650, -235 + 2.5f / 4.2f * 360), new Vector2(42, 1.4f / 4.2f * 360), new Color(.15f, .8f, .35f));
            ui.Rectangle(altitudePanel, "Shield altitude window", new Vector2(-650, -235 + .7f / 4.2f * 360), new Vector2(42, .6f / 4.2f * 360), new Color(.15f, .72f, 1));
            ui.Label(altitudePanel, "Parachute window label", "return.altitude.chute_range", new Vector2(-465, -12), new Vector2(300, 95), 26, new Color(.4f, 1, .55f));
            ui.Label(altitudePanel, "Shield window label", "return.altitude.shield_range", new Vector2(-465, -173), new Vector2(300, 95), 26, new Color(.4f, .85f, 1));
            var marker = ui.Rectangle(altitudePanel, "Current altitude marker", new Vector2(-650, 125), new Vector2(65, 7), Color.white).rectTransform;
            var altitudeStatus = ui.Label(altitudePanel, "Altitude action prompt", "return.altitude.chute_next", new Vector2(-480, -281), new Vector2(430, 70), 23, Color.white);
            var monitor=ui.Panel("Exterior Tracking Monitor",cabin.TransformPoint(new Vector3(1.38f,1.45f,.40f)),new Vector2(760,560),.0009f,Quaternion.Euler(0,42,0));monitor.SetParent(root,true);
            ui.Label(monitor,"External view title","return.monitor",new Vector2(0,242),new Vector2(720,55),27,new Color(.45f,.9f,1));
            var imageObject=new GameObject("Live exterior image",typeof(RectTransform),typeof(RawImage));imageObject.transform.SetParent(monitor,false);
            var image=imageObject.GetComponent<RawImage>();image.raycastTarget=false;image.rectTransform.sizeDelta=new Vector2(728,410);image.rectTransform.anchoredPosition=new Vector2(0,2);
            var stage=ui.Label(monitor,"External view phase","return.phase.CrewTransfer",new Vector2(0,-242),new Vector2(720,65),23,Color.white);
            Component<EarthReturnExteriorMonitor>(session.gameObject).Configure(mission,cabin,monitor.gameObject,image,stage);
            foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))foreach(var child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            var view=Component<EarthReturnPresenter>(session.gameObject);
            view.Configure(mission,presentation,session.GetComponent<FlightSeatLock>(),cabin.gameObject,transfer.gameObject,panel.gameObject,
                new[]{seat,commander,pilot,hatch,lander.transform,moon,service,ocean,chutes,earth},streaks,
                new[]{transferText,phase,detail,crew,action.GetComponentInChildren<LocalizedText>(),pause.GetComponentInChildren<LocalizedText>()},
                new[]{transferButton,action,pause},progress);
            view.ConfigureGuidance(guidance.gameObject,telemetry,down,up,corridor.gameObject,cursor);
            view.ConfigureShock(shock,heatLight);
            view.ConfigureStructure(shell);
            view.ConfigureControls(new[]{less,more,left,right,deploy,release},throttleText,direction,throttleBar,shell.Find("Thermal shield"));
            view.ConfigureAltitude(altitudePanel.gameObject, throttleGroup.gameObject, marker, altitudeStatus);
            session.ConfigureReturn(mission);session.GetComponent<FlightSeatLock>().ConfigureReturnControls(root.gameObject);
            session.GetComponent<MissionScorePresenter>().ConfigureReturn(mission);
            foreach(var button in root.GetComponentsInChildren<Button>(true))Component<ButtonPressFeedback>(button.gameObject);
            const string photoPath=Art+"/NASA Blue Marble East.jpg";
            var photoImporter=(TextureImporter)AssetImporter.GetAtPath(photoPath);
            photoImporter.maxTextureSize=2048;photoImporter.wrapMode=TextureWrapMode.Clamp;photoImporter.SaveAndReimport();
            var sky=Object.FindAnyObjectByType<EarthSkyView>();sky.ConfigureReturn(mission,view,AssetDatabase.LoadAssetAtPath<Texture2D>(photoPath));EditorUtility.SetDirty(sky);
            foreach(var item in session.GetComponents<MonoBehaviour>())EditorUtility.SetDirty(item);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        }
        private static Transform BuildCabin(Transform root)
        {
            var prefab=ImportReturnCapsule.Import();
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);model.transform.SetParent(root,false);
            foreach(float side in new[]{-1f,1f})
            {
                var light=Child(root,"Cabin diffuse light",new Vector3(side*.85f,2.35f,0)).gameObject.AddComponent<Light>();
                light.type=LightType.Point;light.range=4;light.intensity=2.5f;light.color=new Color(.9f,.95f,1);
            }
            var sunlight=Child(root,"Return sunlight",Vector3.zero).gameObject.AddComponent<Light>();
            sunlight.type=LightType.Directional;sunlight.intensity=1.1f;sunlight.transform.rotation=Quaternion.Euler(28,145,0);
            return model.transform;
        }
        private static Transform Crew(Transform parent,string name,Vector3 position)
        {
            var source=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BuildCrewSuit.PrefabPath));
            var model=Child(parent,name,position);
            foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh();skin.BakeMesh(mesh);var vertices=mesh.vertices;var normals=mesh.normals;
                for(int i=0;i<vertices.Length;i++)
                {
                    var v=source.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
                    var n=skin.transform.TransformDirection(normals[i]);float y=v.y;
                    if(y>=.9f){v.y=.60f+(y-.9f)*.91f;v.z-=(y-.9f)*.41f;n=Quaternion.Euler(-24,0,0)*n;}
                    else if(y>=.45f){v.y=.60f-(.9f-y)*.15f;v.z+=.9f-y;n=Quaternion.Euler(-81,0,0)*n;}
                    else{v.y=.5325f-(.45f-y);v.z+=.45f;}
                    vertices[i]=v;normals[i]=n;
                }
                mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();
                string path=Art+"/Seated "+skin.name+".asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(stored==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);mesh=stored;EditorUtility.SetDirty(mesh);}
                var part=Child(model,skin.name,Vector3.zero);part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
            }
            Object.DestroyImmediate(source);return model;
        }
        private static LineRenderer[] BuildPlasma(Transform root)
        {
            return System.Array.Empty<LineRenderer>();
        }
        private static Mesh BuildGlobeMesh()
        {
            const int width=128,height=64;var vertices=new Vector3[(width+1)*(height+1)];var normals=new Vector3[vertices.Length];var indices=new System.Collections.Generic.List<int>();
            for(int y=0;y<=height;y++)for(int x=0;x<=width;x++)
            {float a=x*2*Mathf.PI/width,b=y*Mathf.PI/height;int i=y*(width+1)+x;normals[i]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));vertices[i]=normals[i]*.5f;}
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {int a=y*(width+1)+x,b=a+width+1;indices.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            return SaveMesh(new Mesh{name="Return Earth sphere",vertices=vertices,normals=normals,triangles=indices.ToArray()},"Earth globe");
        }
        private static Renderer BuildShockLayer(Transform root)
        {
            const int width=128,height=12;var vertices=new Vector3[(width+1)*(height+1)];var uv=new Vector2[vertices.Length];var indices=new System.Collections.Generic.List<int>();
            for(int y=0;y<=height;y++)for(int x=0;x<=width;x++)
            {float t=y/(float)height,a=x*2*Mathf.PI/width,r=2.34f+1.3f*t*t;int i=y*(width+1)+x;vertices[i]=new Vector3(Mathf.Sin(a)*r,-.20f+5.5f*t,Mathf.Cos(a)*r);uv[i]=new Vector2(x/(float)width,t);}
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {int a=y*(width+1)+x,b=a+width+1;indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var mesh=SaveMesh(new Mesh{name="Curved plasma envelope",vertices=vertices,uv=uv,triangles=indices.ToArray()},"Plasma envelope");
            var obj=Child(root,"Atmospheric shock layer",Vector3.zero);obj.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.gameObject.AddComponent<MeshRenderer>();var mat=Material("Shock layer",Color.white);mat.shader=Shader.Find("LunarEscape/Return Shock Layer");EditorUtility.SetDirty(mat);renderer.sharedMaterial=mat;return renderer;
        }
        private static Mesh SaveMesh(Mesh mesh,string name)
        {
            mesh.RecalculateBounds();string path=Art+"/"+name+".asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(stored==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);mesh=stored;EditorUtility.SetDirty(mesh);}return mesh;
        }
        private static Transform BuildParachutes(Transform parent, EarthReturnMission mission)
        {
            var root=Child(parent,"Three Main Parachutes",Vector3.zero);
            const int segments=32,rings=8;var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new[]{new System.Collections.Generic.List<int>(),new System.Collections.Generic.List<int>()};
            for(int j=0;j<=rings;j++)for(int i=0;i<=segments;i++)
            {float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*.5f/rings;vertices.Add(new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b)*.48f,Mathf.Sin(b)*Mathf.Sin(a)));}
            for(int j=0;j<rings;j++)for(int i=0;i<segments;i++)
            {int a=j*(segments+1)+i,b=a+segments+1;triangles[i/4%2].AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var mesh=new Mesh{name="Return striped canopy",vertices=vertices.ToArray(),subMeshCount=2};mesh.SetTriangles(triangles[0],0);mesh.SetTriangles(triangles[1],1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Art+"/Canopy.asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(stored==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,stored);Object.DestroyImmediate(mesh);mesh=stored;EditorUtility.SetDirty(mesh);}
            var canopyWhite=Material("Canopy white",new Color(.78f,.76f,.69f));
            var canopyOrange=Material("Canopy orange",new Color(.82f,.15f,.035f));
            canopyWhite.shader=canopyOrange.shader=Shader.Find("Universal Render Pipeline/Lit");
            canopyWhite.SetFloat("_Cull",0);canopyOrange.SetFloat("_Cull",0);
            var canopies = new Transform[3]; var anchors = new Transform[3]; var covers = new Transform[3];
            var lines = new System.Collections.Generic.List<LineRenderer>();
            for(int k=0;k<3;k++)
            {
                float bearing = (-90 + k * 120) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Sin(bearing), 0, Mathf.Cos(bearing));
                var mount = radial * 1.65f + Vector3.up * 2.8f;
                anchors[k] = Child(root, "External parachute riser " + k, mount);
                var pack = Child(root, "External parachute pack " + k, mount); pack.localRotation = Quaternion.LookRotation(radial);
                Box(pack, "Pack housing", new Vector3(0, -.08f, 0), new Vector3(.48f, .30f, .22f), white);
                covers[k] = Child(pack, "Pack lid hinge", new Vector3(0, .07f, .10f));
                Box(covers[k], "External pack lid", new Vector3(0, -.14f, .015f), new Vector3(.48f, .29f, .035f), dark);
                var canopy=canopies[k]=Child(root,"Main canopy "+k,Vector3.zero);
                canopy.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;canopy.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{canopyOrange,canopyWhite};
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4;var rope=Child(root,"Suspension line",Vector3.zero).gameObject.AddComponent<LineRenderer>();
                    rope.sharedMaterial=canopyWhite;rope.useWorldSpace=false;rope.positionCount=2;rope.widthMultiplier=.016f;
                    rope.SetPositions(new[]{mount,mount}); lines.Add(rope);
                }
            }
            root.gameObject.AddComponent<EarthReturnParachutes>().Configure(mission, canopies, anchors, covers, lines.ToArray());
            return root;
        }
        private static Material Material(string name,Color colour,bool unlit=false)
        {
            string path=Art+"/"+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null){result=new Material(Shader.Find(unlit?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(result,path);}
            result.SetColor("_BaseColor",colour);result.SetFloat("_Smoothness",.28f);EditorUtility.SetDirty(result);return result;
        }
        private static Transform Child(Transform parent,string name,Vector3 position)
        {var result=new GameObject(name).transform;result.SetParent(parent,false);result.localPosition=position;return result;}
        private static Transform Box(Transform p,string name,Vector3 pos,Vector3 scale,Material mat)=>Primitive(p,name,PrimitiveType.Cube,pos,scale,mat);
        private static Transform Primitive(Transform p,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat)
        {var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(p,false);o.transform.localPosition=pos;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(o.GetComponent<Collider>());return o.transform;}
        private static T Component<T>(GameObject obj) where T:UnityEngine.Component
        {var value=obj.GetComponent<T>();return value!=null?value:obj.AddComponent<T>();}
    }
}
