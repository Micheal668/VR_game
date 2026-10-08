using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace LunarEscape.Editor
{
    // 在已有主场景安装一个可反复生成的救援情境，保留教学场景和既有美术资产。
    public static class BuildCrewRescue
    {
        private const string Root = "Assets/_LunarEscape";
        [MenuItem("Lunar Escape/Install Commander Rescue and Scoring")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BuildDockingScene.ScenePath);
            BuildRepairScene.RefreshFont();
            ApplyToCurrentScene();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("CREW_RESCUE_SCORING_INSTALLED");
        }
        [MenuItem("Lunar Escape/View Commander Rescue Version")]
        public static void Open()
        {
            BuildDockingScene.Open();
            if (SceneManager.GetActiveScene().path != BuildDockingScene.ScenePath) return;
            var controller = Object.FindAnyObjectByType<GroundCrewController>();
            Selection.activeGameObject = controller != null ? controller.CommanderTransform.gameObject : null;
            EditorApplication.delayCall += () =>
            {
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                view.LookAt(new Vector3(-1.7f, 1.1f, -.5f), Quaternion.Euler(10, -25, 0), 4, false); view.Focus();
            };
            Debug.Log("CREW_RESCUE_SCORING_SCENE_OPENED");
        }
        public static void ApplyToCurrentScene()
        {
            var session = Object.FindAnyObjectByType<StationMissionSession>();
            if (session == null || session.GetComponent<DockingMission>() == null)
                throw new InvalidOperationException("乘员救援版本需要完整的月面到对接主场景。");
            var flight = session.GetComponent<AscentMission>(); var inventory = session.GetComponent<CargoInventory>();
            var presentation = session.GetComponent<FlightScenePresenter>();
            var cabinLayout = presentation.FlightWorld.GetComponent<CrewCabinLayout>();
            var localization = Object.FindAnyObjectByType<LocalizationService>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Fonts/Station Multilingual SDF.asset");
            var ui = new LocalizedPanelBuilder(font, localization);
            RemoveNamed(presentation.GroundRoot.transform, "Commander Rescue Area");
            RemoveNamed(presentation.GroundRoot.transform, "Commander Boarding Status");
            RemoveNamed(presentation.Panel.transform, "Commander Care Console");
            RemoveNamed(presentation.FlightWorld.transform, "Commander Status");

            var area = new GameObject("Commander Rescue Area").transform;
            area.SetParent(presentation.GroundRoot.transform, false);
            var crew = GetOrAdd<CrewMission>(session.gameObject);
            var controller = GetOrAdd<GroundCrewController>(session.gameObject);
            var score = GetOrAdd<MissionScore>(session.gameObject);
            var config = AssetDatabase.LoadAssetAtPath<CrewConfig>(Root + "/Settings/Crew Config.asset");
            if (config == null) { config = ScriptableObject.CreateInstance<CrewConfig>(); AssetDatabase.CreateAsset(config, Root + "/Settings/Crew Config.asset"); }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(BuildCrewSuit.Import());
            model.name = "Commander - Ground Rescue"; model.transform.SetParent(area, false);
            model.transform.SetPositionAndRotation(new Vector3(-2.05f, 0, -.55f), Quaternion.Euler(0, 180, 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            var port = Marker(model.transform, "Commander Medical Port", new Vector3(0, 1.15f, .36f), true);
            var rescueAnchor = Marker(area, "Commander Rescue Anchor", new Vector3(-1.6f, 1.02f, -.9f));
            var routePositions = new[] { new Vector3(-2.05f,0,-.55f),new Vector3(-2.05f,0,.9f),new Vector3(0,0,1.7f),
                new Vector3(3.4f,0,1.7f),new Vector3(8.4f,0,1.7f),new Vector3(38,0,1.7f),new Vector3(38,0,-4.3f),
                new Vector3(45.6f,0,-4.3f),new Vector3(45.6f,0,-6),new Vector3(47.5f,0,-6) };
            var route = routePositions.Select((position, index) => Marker(area, "Commander Route " + index.ToString("00"), position)).ToArray();
            route[0].rotation = model.transform.rotation;
            var boardAnchor = Marker(area, "Commander Boarding Anchor", new Vector3(48, 0, -6));
            var restraint = new GameObject("Jammed Safety Restraint").transform; restraint.SetParent(area, false);
            Box(restraint, "Restraint Crossbar", new Vector3(-2.05f, .9f, -.92f), new Vector3(1.05f, .08f, .08f), "Safety Amber", false);
            foreach (float x in new[] { -2.62f, -1.48f })
                Box(restraint, "Restraint Support", new Vector3(x, .48f, -.92f), new Vector3(.065f, .96f, .065f), "Station Graphite", false);
            var handle = Box(restraint, "Commander Rescue Handle", rescueAnchor.position, new Vector3(.24f, .19f, .12f), "Safety Amber", true);
            var interactable = handle.AddComponent<XRSimpleInteractable>();
            var physicalHandle = handle.AddComponent<CrewRescueHandle>(); physicalHandle.Configure(controller, interactable);
            Box(port, "Medical Port Marker", Vector3.zero, new Vector3(.12f,.10f,.035f), "Station Teal", false, true);
            crew.Configure(config, session.Mission, flight, inventory, session.Exit.PlayerBody, rescueAnchor, model.transform, boardAnchor);
            controller.Configure(crew, model.transform, port, route, restraint.gameObject, cabinLayout.Companion, physicalHandle);
            score.Configure(session.Mission, flight, inventory, crew);

            var groundPanel = ui.Panel("Commander Rescue Instructions", new Vector3(-.55f, 1.7f, -.78f), new Vector2(1250, 610), .0011f, Quaternion.identity);
            groundPanel.SetParent(area, true);
            ui.Label(groundPanel,"Commander Rescue Title","crew.title",new Vector2(0,245),new Vector2(1160,76),43,Color.white);
            var status = ui.Label(groundPanel,"Commander Ground Status","crew.telemetry",new Vector2(0,142),new Vector2(1160,120),30,new Color(.75f,.96f,1));
            var help = ui.Label(groundPanel,"Commander Rescue Help","crew.help.ready",new Vector2(0,15),new Vector2(1160,125),28,Color.white);
            var rescueButton = ui.Button(groundPanel,"Hold Commander Rescue","crew.rescue.hold",new Vector2(-285,-175),new Vector2(535,105),28);
            rescueButton.gameObject.AddComponent<CrewRescueHandle>().Configure(controller);
            var medicalButton = ui.Button(groundPanel,"Treat Held Medical","crew.medical.ground",new Vector2(285,-175),new Vector2(535,105),27);
            // Unity 持久事件需要命名的无返回值方法，绑定公共桥接包装。
            UnityEventTools.AddPersistentListener(medicalButton.onClick, controller.TreatHeldMedical);
            var boardingPanel = ui.Panel("Commander Boarding Status", new Vector3(46.4f, 1.75f, -4.5f), new Vector2(1180, 340), .0011f, Quaternion.Euler(0,-35,0));
            boardingPanel.SetParent(presentation.GroundRoot.transform, true);
            var boardingText = ui.Label(boardingPanel,"Commander Boarding Readout","crew.board.alone",Vector2.zero,new Vector2(1100,280),33,Color.white);
            var cabinPanel = ui.Panel("Commander Care Console", presentation.FlightWorld.transform.TransformPoint(new Vector3(1.05f,1.25f,1.48f)),new Vector2(1040,530),.00085f,Quaternion.Euler(0,-15,0));
            cabinPanel.SetParent(presentation.Panel.transform,true);
            ui.Label(cabinPanel,"Commander Care Title","crew.cabin.title",new Vector2(0,200),new Vector2(970,80),37,Color.white);
            var cabinText = ui.Label(cabinPanel,"Commander Cabin Readout","crew.cabin.status",new Vector2(0,42),new Vector2(970,215),29,Color.white);
            var cabinMedical = ui.Button(cabinPanel,"Use Medical On Commander","crew.medical.cabin",new Vector2(0,-178),new Vector2(930,98),28);
            UnityEventTools.AddPersistentListener(cabinMedical.onClick, controller.UseLoadedMedicalOnCommander);
            GetOrAdd<CrewPanelPresenter>(session.gameObject).Configure(crew,localization,controller,groundPanel.gameObject,boardingPanel.gameObject,cabinPanel.gameObject,status,help,boardingText,cabinText,rescueButton,medicalButton,cabinMedical);
            BuildScorePanel(session,score,localization,ui);
            session.GetComponent<DockingPanelPresenter>().ConfigureScore(score);
            foreach (var component in session.GetComponents<MonoBehaviour>()) EditorUtility.SetDirty(component);
            presentation.GroundRoot.SetActive(true); presentation.FlightWorld.SetActive(false);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
        }
        private static void BuildScorePanel(StationMissionSession session, MissionScore score, LocalizationService localization, LocalizedPanelBuilder ui)
        {
            var legacy = session.GetComponent<MissionFailurePresenter>();
            var panel = legacy.Root.transform; var restart = legacy.RestartButton;
            foreach (Transform child in panel.Cast<Transform>().ToArray()) if (child != restart.transform) Object.DestroyImmediate(child.gameObject);
            var rect = (RectTransform)panel; rect.sizeDelta = new Vector2(1440, 1400); rect.localScale = Vector3.one * .0011f;
            var background = ui.Rectangle(panel,"Score Background",Vector2.zero,rect.sizeDelta,new Color(.022f,.038f,.054f,.98f));
            background.raycastTarget = true; background.transform.SetAsFirstSibling();
            ui.Label(panel,"Stage Score Title","score.title",new Vector2(0,610),new Vector2(1330,85),48,Color.white);
            var outcome = ui.Label(panel,"Stage Score Outcome","score.outcome.docked",new Vector2(0,502),new Vector2(1320,105),32,new Color(.72f,.92f,1));
            var total = ui.Label(panel,"Stage Score Total","score.total",new Vector2(0,377),new Vector2(1320,110),52,Color.white);
            var members = ui.Label(panel,"Stage Score Crew","score.crew",new Vector2(0,245),new Vector2(1320,150),29,Color.white);
            var lines = new LocalizedText[6];
            var categories = (MissionScoreCategory[])Enum.GetValues(typeof(MissionScoreCategory));
            for (int i = 0; i < lines.Length; i++)
                lines[i] = ui.Label(panel,"Score " + categories[i],"score.line." + categories[i],new Vector2(0,94-i*63),new Vector2(1290,58),31,i%2==0?new Color(.82f,.94f,.97f):Color.white);
            var science = ui.Label(panel,"Stage Recovered Science","score.science",new Vector2(0,-330),new Vector2(1300,80),28,new Color(.76f,.88f,.92f));
            ui.Label(panel,"Stage Score Scope","score.scope",new Vector2(0,-423),new Vector2(1300,80),24,new Color(.63f,.73f,.8f));
            var restartRect = restart.GetComponent<RectTransform>(); restartRect.anchoredPosition = new Vector2(0,-570);restartRect.sizeDelta = new Vector2(640,104);
            var uiMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Failure Summary UI.mat");
            var textMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Failure Summary Text.mat");
            foreach(var image in panel.GetComponentsInChildren<Image>(true)) image.material=uiMaterial;
            foreach(var label in panel.GetComponentsInChildren<TMP_Text>(true)) label.fontSharedMaterial=textMaterial;
            legacy.Configure(session.Mission,session.Player.Camera,panel.gameObject,restart,members);
            legacy.ConfigureFlight(session.GetComponent<AscentMission>(),outcome);
            legacy.enabled=false;
            GetOrAdd<MissionScorePresenter>(session.gameObject).Configure(score,localization,session.Player.Camera,panel.gameObject,outcome,total,members,science,lines,restart);
            panel.gameObject.SetActive(false);
        }
        private static T GetOrAdd<T>(GameObject owner) where T : Component => owner.GetComponent<T>() ?? owner.AddComponent<T>();
        private static Transform Marker(Transform parent,string name,Vector3 position,bool local=false)
        {var result=new GameObject(name).transform;result.SetParent(parent,false);if(local)result.localPosition=position;else result.position=position;return result;}
        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,string material,bool collider,bool local=false)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);
            if(local)box.transform.localPosition=position;else box.transform.position=position;box.transform.localScale=size;
            box.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+material+".mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Station Graphite.mat");
            if(!collider)Object.DestroyImmediate(box.GetComponent<Collider>());return box;
        }
        private static void RemoveNamed(Transform root,string name)
        {foreach(var child in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name).ToArray())Object.DestroyImmediate(child.gameObject);}
    }
}
