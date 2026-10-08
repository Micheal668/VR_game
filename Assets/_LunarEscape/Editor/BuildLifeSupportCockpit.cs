using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LunarEscape.Editor
{
    public static partial class BuildLifeSupportScene
    {
        private static readonly float[] CockpitKeyPositions = { .32f,.64f,.96f,1.28f,-1.55f,-1.18f,1.60f,-.81f };
        public static void RefreshCockpitLayout()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            ImportLifeSupportArt.ImportCabinLayout();
            var layout=UnityEngine.Object.FindAnyObjectByType<LifeSupportSceneLayout>();
            SetCockpitScreenX(layout.OperationsScreen.transform,-1.38f);
            SetCockpitScreenX(layout.NavigationScreen.transform,-.05f);
            SetCockpitScreenX(layout.ResourceScreen.transform,1.23f);
            var controls=layout.OperationsScreen.transform.parent;
            for(int i=0;i<CockpitKeyPositions.Length;i++)
                SetCockpitScreenX(controls.Find("Physical Key "+i),CockpitKeyPositions[i]);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(layout.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("COCKPIT_LEFT_FLIGHT_RIGHT_SUPPLIES_UPDATED");
        }
        private static void SetCockpitScreenX(Transform screen,float x)
        {
            if(screen==null)throw new InvalidOperationException("Cockpit control is missing.");
            if(screen is RectTransform rect)
            { var position=rect.anchoredPosition;position.x=x;rect.anchoredPosition=position; }
            else
            { var position=screen.localPosition;position.x=x;screen.localPosition=position; }
            EditorUtility.SetDirty(screen);
        }
        private static GameObject BuildCockpit(StationMissionSession session,out Transform resources,out Transform navigation,out Transform operations)
        {
            var view=session.GetComponent<FlightScenePresenter>();var world=view.FlightWorld.transform;
            var flight=session.GetComponent<AscentMission>();var docking=session.GetComponent<DockingMission>();
            var crew=session.GetComponent<CrewMission>();var groundCrew=session.GetComponent<GroundCrewController>();
            var orbiter=world.GetComponent<OrbiterView>();
            session.GetComponent<DockingPanelPresenter>().enabled=false;
            // 外层控制节点仍由座椅锁白名单管理；原浮空面板停止显示。
            foreach(Transform child in view.Panel.transform.Cast<Transform>().ToArray())child.gameObject.SetActive(false);
            foreach(Transform child in world)
            {
                if(child.name.StartsWith("Cabin")||child.name.StartsWith("Window")||child.name.StartsWith("Forward Window")
                    ||child.name.StartsWith("Side ")||child.name.StartsWith("Observation Window")||child.name.StartsWith("Crew ")||child.name=="Commander Station Label")
                    foreach(var renderer in child.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            }
            RemoveNamed(world,"Dragon Inspired Cabin");RemoveNamed(world,"Docking Optics");RemoveNamed(view.Panel.transform,"Integrated Cockpit Controls");
            var cabin=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ImportLifeSupportArt.CabinPrefab));
            cabin.transform.SetParent(world,false);
            int cabinLayer=EnsureLayer("Cockpit Interior");
            foreach(var child in cabin.GetComponentsInChildren<Transform>(true))child.gameObject.layer=cabinLayer;
            var controls=Child(view.Panel.transform,"Integrated Cockpit Controls");
            resources=Display("Cockpit Resources Screen",controls,new Vector3(1.23f,1.57f,1.375f),new Vector2(840,920),.001f,Quaternion.identity);
            Text(resources,"Resources Header","life.cabin.resources",0,412,780,55,32,Cyan);
            var oxygen=Gauge(resources,"Cabin Oxygen","life.oxygen",0,240,770,205,Cyan);
            var power=Gauge(resources,"Cabin Power","life.power",0,-10,770,205,Amber);
            var thermal=Gauge(resources,"Thermal Reserve","life.thermal_reserve",0,-260,770,205,Cyan);
            var temperature=Text(resources,"Cabin Temperature","life.cabin.temperature",0,-413,775,60,29);
            navigation=Display("Cockpit Navigation Screen",controls,new Vector3(-.05f,1.57f,1.375f),new Vector2(1430,920),.001f,Quaternion.identity);
            Text(navigation,"Navigation Header","life.dock.title",0,416,1350,55,33,Cyan);
            var video=new GameObject("Live Optical Feed",typeof(RectTransform)).AddComponent<RawImage>();video.transform.SetParent(navigation,false);
            video.rectTransform.anchoredPosition=new Vector2(0,77);video.rectTransform.sizeDelta=new Vector2(1340,586);video.raycastTarget=false;video.color=Color.white;
            var camera=Child(world,"Docking Optics").gameObject.AddComponent<Camera>();
            camera.transform.localPosition=orbiter.PlayerPort+Vector3.forward*.10f;camera.fieldOfView=50;camera.nearClipPlane=.03f;camera.farClipPlane=80000;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.002f,.004f,.009f);camera.enabled=false;
            camera.cullingMask=~((1<<5)|(1<<cabinLayer)|(1<<LayerMask.NameToLayer("Player Head")));
            camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            navigation.gameObject.AddComponent<DockingCameraDisplay>().Configure(camera,video);
            var reticle=new GameObject("Optical Target Reticle",typeof(RectTransform)).AddComponent<DockingReticle>();reticle.transform.SetParent(video.transform,false);
            reticle.rectTransform.sizeDelta=video.rectTransform.sizeDelta;reticle.Configure(camera,orbiter,docking);
            var guidance=Text(navigation,"Optical Guidance","life.flight.checklist",0,328,1300,62,28,Amber);
            var telemetry=Text(navigation,"Dock Telemetry","life.dock.telemetry",0,-270,1350,93,27);
            var alignment=Text(navigation,"Dock Alignment","life.dock.axes",0,-357,1350,80,23,Muted);
            var crewState=Text(navigation,"Cabin Crew State","life.cabin.crew",0,-422,1350,48,25,Muted);
            operations=Display("Cockpit Operations Screen",controls,new Vector3(-1.38f,1.57f,1.375f),new Vector2(1060,920),.001f,Quaternion.identity);
            var phase=Text(operations,"Operations Header","flight.phase.Startup",0,411,990,62,35,Cyan);
            var start=Child(operations,"Startup Checklist");var rcs=Child(operations,"RCS Controls");
            var startup=new Button[4];var names=new[]{"power","navigation","engine","ignite"};
            for(int i=0;i<4;i++)startup[i]=Key(start,"Startup "+names[i],"flight.start."+names[i],i%2==0?-250:250,234-i/2*150,470,118,32);
            UnityEventTools.AddPersistentListener(startup[0].onClick,flight.PowerOn);UnityEventTools.AddPersistentListener(startup[1].onClick,flight.StartNavigation);
            UnityEventTools.AddPersistentListener(startup[2].onClick,flight.PrepareEngine);UnityEventTools.AddPersistentListener(startup[3].onClick,flight.Ignite);
            var circularize=Key(start,"Circularize Orbit","orbit.circularize",0,-111,970,125,36);
            UnityEventTools.AddPersistentListener(circularize.onClick,flight.Circularize);
            var feedback=Text(start,"Flight Checklist Feedback","flight.feedback.ready",0,-292,955,160,29,Muted);
            var commands=new[]{DockCommand.Forward,DockCommand.Backward,DockCommand.Left,DockCommand.Right,DockCommand.Up,DockCommand.Down,
                DockCommand.PitchUp,DockCommand.PitchDown,DockCommand.YawLeft,DockCommand.YawRight,DockCommand.RollLeft,DockCommand.RollRight};
            var thrust=new List<Button>();
            for(int i=0;i<commands.Length;i++)
            {
                var button=Key(rcs,"RCS "+commands[i],"dock.control."+commands[i],i%2==0?-250:250,301-i/2*115,470,95,29);
                button.gameObject.AddComponent<DockingThrustButton>().Configure(docking,commands[i]);thrust.Add(button);
            }
            var assist=Key(rcs,"Toggle Capture Assist","dock.assist.off",0,-392,970,86,29);
            UnityEventTools.AddPersistentListener(assist.onClick,docking.ToggleAssistance);
            var assistance=assist.GetComponentInChildren<LocalizedText>();
            var positions=CockpitKeyPositions;
            var switchKeys=new[]{"life.key.oxygen","life.key.battery","life.key.repair","life.key.medical","life.key.brake","life.key.boost","life.key.crew","life.key.orbit"};
            var switches=new Button[8];
            for(int i=0;i<8;i++)
            {
                var panel=Display("Physical Key "+i,controls,new Vector3(positions[i],.98f,1.153f),new Vector2(620,240),.00033f,Quaternion.identity);
                switches[i]=Key(panel,"Cockpit "+switchKeys[i],switchKeys[i],0,0,605,225,55,i==5);UiLayer(panel);
            }
            UnityEventTools.AddPersistentListener(switches[0].onClick,flight.UseOxygen);UnityEventTools.AddPersistentListener(switches[1].onClick,flight.UseBattery);
            UnityEventTools.AddPersistentListener(switches[2].onClick,flight.UseRepair);UnityEventTools.AddPersistentListener(switches[3].onClick,flight.UseMedical);
            switches[4].gameObject.AddComponent<DockingThrustButton>().Configure(docking,DockCommand.Brake);
            switches[5].gameObject.AddComponent<DockingThrustButton>().Configure(docking,DockCommand.MainBoost);thrust.Add(switches[4]);thrust.Add(switches[5]);
            UnityEventTools.AddPersistentListener(switches[6].onClick,groundCrew.UseLoadedMedicalOnCommander);
            UnityEventTools.AddPersistentListener(switches[7].onClick,flight.Circularize);
            // 重复的实体入轨键与屏幕上的确认键由同一阶段开关更新。
            switches[7].gameObject.AddComponent<CircularizeKeyPresenter>().Configure(flight);
            GetOrAdd<CockpitScreenPresenter>(session.gameObject).Configure(flight,docking,crew,language,oxygen,power,thermal,
                new[]{temperature,phase,telemetry,alignment,guidance,feedback,crewState,assistance},start.gameObject,rcs.gameObject,startup,
                switches.Take(4).ToArray(),new[]{CargoKind.Oxygen,CargoKind.Battery,CargoKind.RepairKit,CargoKind.MedicalKit},thrust.ToArray(),circularize,assist,switches[6]);
            ApplyCockpitReadability(resources,navigation,operations,controls);
            UiLayer(resources);UiLayer(navigation);UiLayer(operations);return cabin;
        }
        private static int EnsureLayer(string name)
        {
            int existing=LayerMask.NameToLayer(name);if(existing>=0)return existing;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=settings.FindProperty("layers");
            for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {layers.GetArrayElementAtIndex(i).stringValue=name;settings.ApplyModifiedProperties();return i;}
            throw new InvalidOperationException("No free layer for cockpit camera isolation.");
        }
    }
}
