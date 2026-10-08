using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape.Editor
{
    public static partial class BuildLifeSupportScene
    {
        public const string ScenePath = "Assets/_LunarEscape/Scenes/11_LunarStation_LifeSupport.unity";
        private const string Root = "Assets/_LunarEscape";
        [MenuItem("Lunar Escape/Install Life Support and Cockpit")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Prepare(); ImportLifeSupportArt.Import(); BuildRepairScene.RefreshFont(); Apply();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("LIFE_SUPPORT_COCKPIT_INSTALLED");
        }
        public static void RefreshLayout()
        {
            EditorSceneManager.OpenScene(ScenePath);BuildRepairScene.RefreshFont();Apply();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Debug.Log("LIFE_SUPPORT_LAYOUT_REFRESHED");
        }
        public static void RemoveRoadsideLamps()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var ground=Object.FindAnyObjectByType<StationMissionSession>().GetComponent<FlightScenePresenter>().GroundRoot.transform;
            int removed=RemoveRoadsideLamps(ground);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("ROADSIDE_LAMP_PARTS_REMOVED "+removed);
        }
        private static int RemoveRoadsideLamps(Transform ground)
        {
            var parts=ground.GetComponentsInChildren<Transform>(true)
                .Where(t=>t.name is "Route Marker" or "Route Beacon").ToArray();
            foreach(var part in parts)Object.DestroyImmediate(part.gameObject);
            return parts.Length;
        }
        [MenuItem("Lunar Escape/View Life Support Version")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.delayCall += () => {
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                view.LookAt(new Vector3(0,1.8f,1.7f),Quaternion.Euler(9,0,0),5,false); view.Focus();
            };
        }
        private static void Apply()
        {
            var session=Object.FindAnyObjectByType<StationMissionSession>();
            var presentation=session.GetComponent<FlightScenePresenter>();
            RemoveRoadsideLamps(presentation.GroundRoot.transform);
            language=Object.FindAnyObjectByType<LocalizationService>();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/Station Multilingual SDF.asset");
            ui=new LocalizedPanelBuilder(font,language);
            RemoveNamed(presentation.GroundRoot.transform,"Life Support Hardware");
            var hardware=Child(presentation.GroundRoot.transform,"Life Support Hardware");
            foreach(var old in presentation.GroundRoot.GetComponentsInChildren<Transform>(true)
                .Where(t=>t.name=="Station Electrical Service Panel").ToArray()) Object.DestroyImmediate(old.gameObject);
            foreach(var name in new[]{"Mission Panel","Supply Manifest"}) {
                var old=presentation.GroundRoot.transform.Find(name); if(old!=null)old.gameObject.SetActive(false);
            }
            var rack=Child(hardware,"Wall Suit Rack");rack.localPosition=new Vector3(3.65f,0,-1.8f);rack.localRotation=Quaternion.Euler(0,90,0);
            Solid(rack,"Service backing",new Vector3(0,1.25f,.19f),new Vector3(1.07f,2.4f,.10f),"LS_Graphite");
            foreach(float x in new[]{-.46f,.46f})Solid(rack,"Rack stanchion",new Vector3(x,1.25f,.02f),new Vector3(.05f,2.30f,.12f),"LS_BrushedAlloy");
            Solid(rack,"Hanger bar",new Vector3(0,2.22f,-.03f),new Vector3(1.10f,.055f,.08f),"LS_BrushedAlloy");
            var hanging=(GameObject)PrefabUtility.InstantiatePrefab(BuildCrewSuit.Import());hanging.name="Hanging Emergency Suit";
            hanging.transform.SetParent(rack,false);hanging.transform.localPosition=new Vector3(0,.16f,-.12f);hanging.transform.localRotation=Quaternion.Euler(0,180,0);
            var npcRack=Object.Instantiate(rack.gameObject,hardware).transform;
            npcRack.name="Commander Suit Rack";npcRack.localPosition=new Vector3(3.65f,0,-.48f);
            var npcHanging=npcRack.Find("Hanging Emergency Suit").gameObject;npcHanging.name="Hanging Commander Suit";
            var playerLabel=Display("Player Suit Identification",rack,new Vector3(0,2.37f,-.10f),new Vector2(1000,100),.0009f,Quaternion.identity);
            Text(playerLabel,"Player Suit Label","life.suit.player",0,0,950,90,38,Cyan);
            var npcLabel=Display("Commander Suit Identification",npcRack,new Vector3(0,2.37f,-.10f),new Vector2(1000,100),.0009f,Quaternion.identity);
            Text(npcLabel,"Commander Suit Label","life.suit.commander",0,0,950,90,38,Amber);
            var donPoint=Child(rack,"Suit Donning Point");donPoint.localPosition=new Vector3(0,1.05f,-.22f);
            var doorPoint=Child(hardware,"Airlock Control Point");doorPoint.localPosition=new Vector3(3.47f,1.65f,.24f);
            var air=Child(hardware,"Pressurized Habitat Volume").gameObject.AddComponent<BoxCollider>();
            air.isTrigger=true;air.center=new Vector3(0,1.7f,0);air.size=new Vector3(8,3.4f,8);
            var config=AssetDatabase.LoadAssetAtPath<LifeSupportConfig>(Root+"/Settings/Life Support Config.asset");
            if(config==null){config=ScriptableObject.CreateInstance<LifeSupportConfig>();AssetDatabase.CreateAsset(config,Root+"/Settings/Life Support Config.asset");}
            EditorUtility.SetDirty(config);
            var life=GetOrAdd<LifeSupportMission>(session.gameObject);
            life.Configure(config,session,session.GetComponent<CargoInventory>(),donPoint,doorPoint,air);
            var latch=Child(hardware,"Airlock Repair Latch");latch.localPosition=new Vector3(3.40f,1.05f,.24f);latch.localRotation=Quaternion.Euler(0,90,0);
            Solid(latch,"Latch housing",Vector3.zero,new Vector3(.36f,.23f,.12f),"LS_Graphite");
            var socket=Solid(latch,"Tool contact socket",new Vector3(0,0,-.09f),new Vector3(.10f,.10f,.05f),"LS_Amber");
            var contact=latch.gameObject.AddComponent<RepairContact>();
            contact.Configure(socket.transform,Object.FindObjectsByType<RepairTool>(FindObjectsSortMode.None),"maintenance",.14f);
            life.ConfigureDoorRepair(contact);
            var latchLabel=Display("Latch Identification",latch,new Vector3(0,-.19f,-.08f),new Vector2(700,90),.00065f,Quaternion.identity);
            Text(latchLabel,"Latch Label","life.hatch.tool_point",0,0,690,85,31,Amber);
            session.GetComponent<MissionEnvironment>().ConfigureLifeSupport(life);
            var interactions=GetOrAdd<SuitInteractionController>(session.gameObject);interactions.Configure(life);
            var handle=Solid(rack,"Suit Release Grip",new Vector3(-.63f,1.08f,-.26f),new Vector3(.13f,.23f,.11f),"LS_Amber",true);
            var interactable=handle.AddComponent<XRSimpleInteractable>();
            var suitHandle=handle.AddComponent<SuitRackInteractor>();suitHandle.Configure(interactions,interactable);
            var habitat=BuildHabitatDisplay(hardware,session,out var oxygen,out var power,out var temperature,out var condition,out var begin);
            var supply=BuildSupplyDisplay(hardware,session);
            BuildSuitDisplay(rack,interactions,out var suitState,out var suitHelp,out var don,out var progress);
            var hatch=BuildAirlockDisplay(hardware,life,out var hatchState,out var open);
            GetOrAdd<HabitatLifePanel>(session.gameObject).Configure(life,language,oxygen,power,temperature,condition,suitState,suitHelp,hatchState,don,open,progress);
            BuildWardrobe(session,life,hanging);session.GetComponent<CrewWardrobe>().ConfigureCommanderRack(npcHanging);
            BuildHelmetUi(session,life,hardware);BuildLights(session,life,hardware);
            var crewPanel=session.GetComponent<CrewPanelPresenter>();crewPanel.ConfigureGroundVisibility(true);crewPanel.ConfigureCabinVisibility(false);
            // 保留使用者调整的救援面板位置，并让它成为同风格的实体设备。
            crewPanel.GroundPanel.GetComponentInChildren<UnityEngine.UI.Image>(true).color=Ink;
            RemoveNamed(crewPanel.GroundPanel.transform.parent,crewPanel.GroundPanel.name+" Housing");
            RemoveNamed(crewPanel.GroundPanel.transform,crewPanel.GroundPanel.name+" Housing");
            Frame(crewPanel.GroundPanel.transform,((RectTransform)crewPanel.GroundPanel.transform).sizeDelta*crewPanel.GroundPanel.transform.lossyScale.x);
            crewPanel.GroundPanel.transform.parent.Find(crewPanel.GroundPanel.name+" Housing").SetParent(crewPanel.GroundPanel.transform,true);
            var pocket=Object.FindAnyObjectByType<CargoPocketPresenter>(FindObjectsInactive.Include);
            pocket.transform.localScale=Vector3.one*.0005f;pocket.GetComponentInChildren<UnityEngine.UI.Image>(true).color=new Color(.021f,.033f,.043f,.86f);
            pocket.GetComponentInParent<CargoPackFollower>().ConfigureWaistPocket();
            GetOrAdd<LookDownDisplay>(pocket.gameObject).Configure(session.Player.Camera,55,0);
            var cabin=BuildCockpit(session,out var resources,out var navigation,out var operations);
            GetOrAdd<LifeSupportSceneLayout>(session.gameObject).Configure(habitat.gameObject,supply.gameObject,rack.gameObject,hatch.gameObject,cabin,
                resources.gameObject,navigation.gameObject,operations.gameObject,begin,suitHandle);
            foreach(var component in session.GetComponents<MonoBehaviour>())EditorUtility.SetDirty(component);
            presentation.GroundRoot.SetActive(true);presentation.FlightWorld.SetActive(false);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        }
        private static void BuildWardrobe(StationMissionSession session,LifeSupportMission life,GameObject hanging)
        {
            var player=session.Player;RemoveNamed(player.transform,"Player - Station Uniform");
            var suited=player.GetComponentsInChildren<TrackedCrewSuit>(true).Single().Suit;
            var casual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ImportLifeSupportArt.UniformPrefab));
            casual.name="Player - Station Uniform";casual.transform.SetParent(player.transform,false);
            var tracked=suited.GetComponent<TrackedCrewSuit>();var rig=casual.GetComponent<CrewSuitRig>();
            casual.AddComponent<TrackedCrewSuit>().Configure(player,tracked.LeftController,tracked.RightController,rig);
            rig.Helmet.gameObject.layer=LayerMask.NameToLayer("Player Head");
            var commander=session.GetComponent<GroundCrewController>().CommanderTransform;
            RemoveNamed(commander,"Commander - Station Uniform");
            var npc=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ImportLifeSupportArt.UniformPrefab));
            npc.name="Commander - Station Uniform";npc.transform.SetParent(commander,false);
            var npcSuit=commander.GetComponent<CrewSuitRig>();var npcUniform=npc.GetComponent<CrewSuitRig>();
            GetOrAdd<CrewWardrobe>(session.gameObject).Configure(life,new Renderer[]{suited.Body,suited.Helmet,npcSuit.Body,npcSuit.Helmet},
                new Renderer[]{rig.Body,rig.Helmet,npcUniform.Body,npcUniform.Helmet},hanging);
            foreach(var renderer in new Renderer[]{suited.Body,suited.Helmet,npcSuit.Body,npcSuit.Helmet})PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        private static void BuildLights(StationMissionSession session,LifeSupportMission life,Transform hardware)
        {
            RemoveNamed(session.transform,"Life Support Sun");RemoveNamed(session.Player.Camera.transform,"Suit Helmet Lamp");
            var original=Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);
            original.enabled=false;
            var sunlight=Child(session.transform,"Life Support Sun").gameObject.AddComponent<Light>();sunlight.type=LightType.Directional;
            sunlight.shadows=LightShadows.Soft;sunlight.shadowStrength=.8f;RenderSettings.sun=sunlight;
            foreach(float x in new[]{-2f,2f})foreach(float z in new[]{-2f,2f})
            {
                var lamp=Child(hardware,"Habitat interior lamp").gameObject.AddComponent<Light>();lamp.transform.localPosition=new Vector3(x,2.8f,z);
                lamp.type=LightType.Point;lamp.range=5;lamp.intensity=1.1f;lamp.color=new Color(.80f,.91f,1);
            }
            var lamps=session.GetComponent<FlightScenePresenter>().GroundRoot.GetComponentsInChildren<Light>(true)
                .Where(l=>l!=original&&!l.name.StartsWith("Alarm")).ToArray();
            GetOrAdd<LifeSupportEnvironment>(session.gameObject).Configure(life,session.GetComponent<AscentMission>(),sunlight,lamps);
            BuildShoulderLights(session);
            var beaconRoot=Child(hardware,"Lander Locator Beacons");var lenses=new System.Collections.Generic.List<Renderer>();var lights=new System.Collections.Generic.List<Light>();
            foreach(var position in new[]{new Vector3(46.4f,5.0f,-1.50f),new Vector3(49.6f,5.0f,-1.50f),new Vector3(48,7.15f,1.7f)})
            {
                var lens=Solid(beaconRoot,"Beacon lens",position,new Vector3(.22f,.18f,.22f),"LS_Light");lenses.Add(lens.GetComponent<Renderer>());
                var lamp=Child(beaconRoot,"Beacon illumination").gameObject.AddComponent<Light>();lamp.transform.localPosition=position;
                lamp.type=LightType.Point;lamp.range=9;lamp.color=Cyan;lights.Add(lamp);
            }
            beaconRoot.gameObject.AddComponent<LanderNavigationBeacon>().Configure(lenses.ToArray(),lights.ToArray());
        }
        private static T GetOrAdd<T>(GameObject owner) where T:Component=>owner.GetComponent<T>()??owner.AddComponent<T>();
        private static Transform Child(Transform parent,string name){var child=new GameObject(name).transform;child.SetParent(parent,false);return child;}
        private static void RemoveNamed(Transform parent,string name){var child=parent.Find(name);if(child!=null)Object.DestroyImmediate(child.gameObject);}
        public static void Prepare()
        {
            if (!File.Exists(ScenePath))
            {
                var source = EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
                EditorSceneManager.SaveScene(source, ScenePath);
            }
            else EditorSceneManager.OpenScene(ScenePath);
            var output = new StringBuilder();
            var session = Object.FindAnyObjectByType<StationMissionSession>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.GetComponent<Canvas>() != null || t.name.Contains("Anchor_") || t.name.Contains("Light")
                    || t.name is "Supply Shelf" or "Workbench" or "Player Start" or "Evacuation Door" or "Station Room - editable blockout"
                    || t.parent == session.GetComponent<FlightScenePresenter>().FlightWorld.transform))
            {
                output.AppendLine(t.name + " | parent=" + t.parent?.name + " | p=" + t.position.ToString("F3")
                    + " | r=" + t.eulerAngles.ToString("F1") + " | s=" + t.lossyScale.ToString("F4")
                    + " | active=" + t.gameObject.activeSelf);
            }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/life-source-layout.txt", output.ToString());
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets(); Debug.Log("LIFE_SUPPORT_SOURCE_PREPARED");
        }
    }
}
