using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LunarEscape.Tests
{
    public sealed class DockingSceneTests
    {
        private StationMissionSession session;private AscentMission flight;private DockingMission docking;private FlightScenePresenter scene;
        private CargoInventory cargo;private HatchBoardingController hatch;private DockingPanelPresenter panel;private OrbiterView view;
        private float previousDelta;private InputSettings.BackgroundBehavior previousBackground;private InputSettings.EditorInputBehaviorInPlayMode previousFocus;
        private readonly List<Object> temporary=new();
        [UnitySetUp]public IEnumerator Load()
        {
            previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/30;
            previousBackground=InputSystem.settings.backgroundBehavior;previousFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("10_LunarStation_Docking");yield return null;
            session=Object.FindAnyObjectByType<StationMissionSession>();session.enabled=false;flight=session.GetComponent<AscentMission>();docking=session.GetComponent<DockingMission>();scene=session.GetComponent<FlightScenePresenter>();cargo=session.GetComponent<CargoInventory>();hatch=Object.FindAnyObjectByType<HatchBoardingController>();panel=session.GetComponent<DockingPanelPresenter>();view=scene.FlightWorld.GetComponent<OrbiterView>();
            while(Time.time<1.05f)yield return null;
        }
        [TearDown]public void Restore()
        {Time.captureDeltaTime=previousDelta;InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousFocus;foreach(var o in temporary)if(o!=null)Object.Destroy(o);temporary.Clear();}
        private void Board(float remaining=60,params CargoKind[] supplies)
        {
            session.BeginMission();session.Advance(session.Mission.Config.RepairWindowSeconds);session.Advance(session.Mission.RemainingSeconds-remaining);
            foreach(var kind in supplies)Assert.That(cargo.TryHold(cargo.Items.First(i=>i.Kind==kind)),Is.True);
            var body=session.Exit.PlayerBody;var center=body.transform.TransformPoint(body.center);body.enabled=false;body.transform.position+=new Vector3(48-center.x,0,-6-center.z);body.enabled=true;Physics.SyncTransforms();
            Assert.That(hatch.CanBoard,Is.True);hatch.RequestBoarding();session.Advance(0);flight.Tick(.951f);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Startup));
        }
        private void Launch(){flight.PowerOn();flight.StartNavigation();flight.Tick(2);flight.PrepareEngine();flight.Tick(2);flight.Ignite();flight.Tick(3);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Ascent));}
        private void Orbit(){flight.Tick(flight.OrbitAscentSeconds-flight.AirborneSeconds);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.OrbitalInsertion));flight.Circularize();flight.Tick(6);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Rendezvous));}
        private void StartAt(Vector3 position,Vector3 velocity=default,Vector3 euler=default,string configOverride=null)
        {
            var settings=Object.Instantiate(docking.Config);temporary.Add(settings);settings.ConfigureStart(position,velocity,euler);
            if(configOverride!=null)JsonUtility.FromJsonOverwrite(configOverride,settings);
            docking.Configure(flight,settings);Board();Launch();Orbit();
        }
        private void Drive(DockCommand command,float seconds){docking.SetCommand(command,true);flight.Tick(seconds);docking.SetCommand(command,false);}
        [UnityTest]public IEnumerator RealWasdBindingsMoveBodyAfterStartAndKeepCollision()
        {
            var simulator=Object.FindAnyObjectByType<CollisionAwareSimulator>();
            session.BeginMission();
            var keyboard=InputSystem.AddDevice<Keyboard>("WASD verification keyboard");
            try
            {
                yield return null;yield return null;
                Debug.Log($"WASD_DIAGNOSTIC mode={simulator.currentState.targetedDeviceInput} enabled={simulator.BodyMovementEnabled} x={simulator.translateXInput.inputActionReference?.action?.enabled} z={simulator.translateZInput.inputActionReference?.action?.enabled}");
                foreach(var mode in new[]{UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.TargetedDevices.FPS,UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.TargetedDevices.RightDevice})
                {
#pragma warning disable CS0618
                    simulator.targetedDeviceInput=mode;
#pragma warning restore CS0618
                    yield return null;yield return null;
                    var start=session.Player.Origin.transform.position;
                    InputState.Change(keyboard,new KeyboardState(Key.D));
                    for(int frame=0;frame<10;frame++)yield return null;
                    Assert.That(session.Player.Origin.transform.position.x-start.x,Is.GreaterThan(.3f),mode+"：真实 D 键应移动身体。");
                    InputState.Change(keyboard,new KeyboardState());yield return null;
                }
                InputState.Change(keyboard,new KeyboardState(Key.S));
                for(int frame=0;frame<90;frame++)yield return null;
                var stopped=session.Player.Origin.transform.position;
                for(int frame=0;frame<15;frame++)yield return null;
                Assert.That(Vector3.Distance(stopped,session.Player.Origin.transform.position),Is.LessThan(.08f),"持续后退必须被舱壁阻挡。");
                InputState.Change(keyboard,new KeyboardState());yield return null;
            }
            finally{if(keyboard.added)InputSystem.RemoveDevice(keyboard);}
        }
        [UnityTest]public IEnumerator EarthRemainsVisibleFromSurfaceThroughOrbitAndResetsOnRetry()
        {
            var earth=Object.FindAnyObjectByType<EarthSkyView>();
            Assert.That(earth,Is.Not.Null);yield return null;
            var ground=earth.Direction;var material=RenderSettings.skybox;
            Assert.That(material.shader.name,Is.EqualTo("LunarEscape/Earth Sky"));
            Assert.That(material.GetTexture("_EarthPhoto"),Is.Not.Null);
            Assert.That(material.GetTexture("_StarPhoto"),Is.Not.Null);
            yield return CaptureEarth("surface",new Vector3(36,2,-9),Vector3.forward+Vector3.up*.12f);
            // 头部转动只改变观察方向，不能让地球贴在视野中央。
            var camera=session.Player.Camera;var rotation=camera.transform.rotation;
            camera.transform.rotation=Quaternion.Euler(0,90,0);yield return null;
            Assert.That(Vector3.Angle(ground,earth.Direction),Is.LessThan(.01f));camera.transform.rotation=rotation;
            Board();Launch();
            var outside=scene.FlightWorld.GetComponentInChildren<LunarWindowAnimation>().OutsideWorld;
            foreach(float time in new[]{3f,25,60,85})
            {
                flight.Tick(Mathf.Max(0,time-flight.AirborneSeconds));yield return null;yield return null;
                Assert.That(Vector3.Angle(outside.rotation*ground,earth.Direction),Is.LessThan(.01f));
                Assert.That(RenderSettings.skybox,Is.SameAs(material));
                Assert.That(Vector3.Angle((Vector3)material.GetVector("_SkyForward"),outside.forward),Is.LessThan(.01f));
            }
            Orbit();yield return null;yield return null;
            yield return CaptureEarth("orbit",camera.transform.position,Vector3.forward);
            yield return CaptureEarth("detail",camera.transform.position,earth.Direction,true);
            Drive(DockCommand.YawRight,1);yield return null;yield return null;
            Assert.That(Vector3.Angle(outside.rotation*ground,earth.Direction),Is.LessThan(.01f));
            session.RetryMission();yield return null;yield return null;
            Assert.That(Vector3.Angle(ground,earth.Direction),Is.LessThan(.01f));
            Assert.That(Object.FindObjectsByType<EarthSkyView>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator CrewSuitReplacesNpcAndPlayerWithoutChangingTrackingOrInteraction()
        {
            var avatar=session.Player.GetComponentInChildren<TrackedCrewSuit>(true);
            Assert.That(avatar,Is.Not.Null);var rig=avatar.Suit;
            var crew=scene.FlightWorld.GetComponent<CrewCabinLayout>();
            Assert.That(crew.Companion.GetComponent<CrewSuitRig>(),Is.Not.Null);
            Assert.That(rig.Body.sharedMesh.vertexCount,Is.GreaterThan(1000));
            Assert.That(rig.GetComponentsInChildren<Collider>().Length,Is.Zero,"外观模型不能新增阻碍行走/抓取的碰撞体。");
            Assert.That(session.Player.Camera.cullingMask&(1<<rig.Helmet.gameObject.layer),Is.Zero,"第一人称不能看到自己的头盔内壁。");
            foreach(var t in session.Player.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Left Controller Visual"||t.name=="Right Controller Visual"))
                Assert.That(t.gameObject.activeSelf,Is.False,"旧手柄外观应该被替换，而不是叠加显示。");
            var left=avatar.LeftController;var right=avatar.RightController;
            Assert.That(left.name,Is.EqualTo("Left Controller"));Assert.That(right.name,Is.EqualTo("Right Controller"));
            var originalCameraPosition=session.Player.Camera.transform.position;
            yield return null;yield return null;
            Assert.That(Vector3.Distance(rig.Bones[5].position,left.TransformPoint(new Vector3(0,-.025f,-.035f))),Is.LessThan(.005f));
            Assert.That(Vector3.Distance(rig.Bones[8].position,right.TransformPoint(new Vector3(0,-.025f,-.035f))),Is.LessThan(.005f));
            Assert.That(Vector3.Distance(originalCameraPosition,session.Player.Camera.transform.position),Is.LessThan(.02f));
            Board();yield return null;yield return null;
            var eye=session.Player.Camera.transform.position;
            yield return CaptureEarth("crew-npc",eye,crew.Companion.transform.position+Vector3.up*1.25f-eye);
            yield return CaptureEarth("crew-first-person",eye,Vector3.down*.9f+Vector3.forward*.4f);
            var external=rig.transform.position+Vector3.forward*2.1f+Vector3.up*1.2f;
            yield return CaptureEarth("crew-player",external,rig.transform.position+Vector3.up*.95f-external,false,true);
            Launch();Orbit();yield return null;yield return null;
            Assert.That(rig.gameObject.activeInHierarchy,Is.True);Assert.That(crew.Companion.activeInHierarchy,Is.True);
            Assert.That(Vector3.Distance(rig.Bones[8].position,right.TransformPoint(new Vector3(0,-.025f,-.035f))),Is.LessThan(.005f));
            session.RetryMission();yield return null;yield return null;
            Assert.That(session.Player.GetComponentsInChildren<TrackedCrewSuit>(true).Length,Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<CollisionAwareSimulator>().BodyMovementEnabled,Is.True);
        }
        private static IEnumerator CaptureEarth(string name,Vector3 eye,Vector3 direction,bool skyOnly=false,bool showPlayerHead=false)
        {
            var obj=new GameObject("Earth verification camera");var camera=obj.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;
            camera.fieldOfView=skyOnly?14:70;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(direction));
            if(skyOnly)camera.cullingMask=0;
            if(showPlayerHead)camera.cullingMask|=1<<LayerMask.NameToLayer("Player Head");
            var rt=new RenderTexture(1600,1100,24);var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                // 正常渲染数帧，等待 GPU Resident Drawer 注册新相机和舱体网格，再读回完整画面。
                rt.Create();camera.targetTexture=rt;camera.enabled=true;
                for(int frame=0;frame<8;frame++)yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,1100),0,0);pixels.Apply();
                var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Previews"));Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,"earth-"+name+".png"),pixels.EncodeToPNG());
                if(skyOnly)Assert.That(pixels.GetPixels32().Count(p=>p.b>100&&p.r>80),Is.GreaterThan(1000),"地球天空应实际渲染蓝白色云层。");
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(pixels);Object.Destroy(rt);Object.Destroy(obj);}
        }
        [UnityTest]public IEnumerator ContinuousMoonCoversLocalBoundaryAndIsPresentDuringEveryAscentStage()
        {
            var terrain=session.GetComponent<LunarTerrainLayout>();var global=terrain.GroundVisual.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Continuous Global Surface");
            Assert.That(global.sharedMesh.bounds.size.y,Is.GreaterThan(3400000));
            var vertices=global.sharedMesh.vertices;
            for(int i=0;i<=800;i++){var p=vertices[i];Assert.That(p.y,Is.EqualTo(LunarTerrainProfile.Height(p.x,p.z)).Within(.001f));}
            var surfaces=terrain.FlightVisual.GetComponentsInChildren<Renderer>(true);Assert.That(surfaces.Length,Is.EqualTo(3));Assert.That(surfaces.Select(r=>r.sharedMaterial).Distinct().Count(),Is.EqualTo(1));
            Capture("terrain",new Vector3(12,2,-9),new Vector3(30,0,18));Board();Launch();
            foreach(float time in new[]{0f,3,6,12,25,50,52,54,60,70,85,98})
            {
                flight.Tick(Mathf.Max(0,time-flight.AirborneSeconds));yield return null;
                Assert.That(surfaces.All(r=>r.gameObject.activeInHierarchy),Is.True);
                Capture("surface-"+time,session.Player.Camera.transform.position,session.Player.Camera.transform.position+Vector3.left*20+Vector3.down*6);
                if(time==52 || time==54)
                {
                    var window=scene.FlightWorld.GetComponentInChildren<LunarWindowAnimation>();
                    Capture("blast-window-"+time,session.Player.Camera.transform.position,window.OutsideWorld.TransformPoint(new Vector3(-2,2,0)));
                }
            }
            Assert.That(scene.FlightWorld.GetComponent<OrbitalMoonView>(),Is.Null);
        }
        [Test]public void VerticalAscentDoesNotStopAtThreeSecondsAndTurnRateHasNoJump()
        {
            foreach(float low in new[]{18f,58,88})
            {
                float total=Mathf.Max(80,low+40),dt=.01f;
                var left=OrbitalTrajectory.Sample(3-dt,low,total,100,0);var middle=OrbitalTrajectory.Sample(3,low,total,100,0);var right=OrbitalTrajectory.Sample(3+dt,low,total,100,0);
                Assert.That((middle.Altitude-left.Altitude)/dt,Is.InRange(1.95f,2.05f));Assert.That((right.Altitude-middle.Altitude)/dt,Is.InRange(1.95f,2.05f));
                Assert.That(middle.Pitch,Is.Zero);Assert.That(right.Pitch,Is.LessThan(.001f));
                float previousAltitude=0,previousPitch=0;
                for(float t=.1f;t<total;t+=.1f)
                {var p=OrbitalTrajectory.Sample(t,low,total,100,0);Assert.That(p.Altitude,Is.GreaterThanOrEqualTo(previousAltitude));Assert.That(p.Pitch-previousPitch,Is.InRange(-.001f,.6f));previousAltitude=p.Altitude;previousPitch=p.Pitch;}
                var before=OrbitalTrajectory.Sample(low-.02f,low,total,100,0);var at=OrbitalTrajectory.Sample(low,low,total,100,0);var after=OrbitalTrajectory.Sample(low+.02f,low,total,100,0);
                Assert.That((at.Altitude-before.Altitude)/.02f,Is.GreaterThan(1));Assert.That((after.Altitude-at.Altitude)/.02f,Is.GreaterThan(1));
            }
        }
        [UnityTest]public IEnumerator OrbitSurfaceMovesAtAdjustableRateWithoutMovingPlayer()
        {
            StartAt(new Vector3(0,0,-35));yield return null;var moon=scene.FlightWorld.GetComponent<ContinuousMoonView>();var origin=session.Player.Origin.transform.position;
            moon.SurfaceDegreesPerSecond=.75f;float angle=moon.SurfaceAngle;moon.AdvanceSurface(4);Assert.That(Mathf.DeltaAngle(angle,moon.SurfaceAngle),Is.EqualTo(3).Within(.001f));
            angle=moon.SurfaceAngle;moon.SurfaceDegreesPerSecond=0;moon.AdvanceSurface(10);Assert.That(moon.SurfaceAngle,Is.EqualTo(angle));
            yield return null;
            var properties=new MaterialPropertyBlock();
            var surface=session.GetComponent<LunarTerrainLayout>().FlightVisual.GetComponentsInChildren<Renderer>().Last();surface.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_SurfaceAngle"),Is.EqualTo(moon.SurfaceAngle).Within(.001f));
            Capture("orbit-motion-a",session.Player.Camera.transform.position,session.Player.Camera.transform.position+Vector3.left*20+Vector3.down*6);
            moon.SurfaceDegreesPerSecond=.12f;moon.AdvanceSurface(20);moon.SurfaceDegreesPerSecond=0;yield return null;
            surface.GetPropertyBlock(properties);Assert.That(properties.GetFloat("_SurfaceAngle"),Is.EqualTo(moon.SurfaceAngle).Within(.001f));
            Capture("orbit-motion-b",session.Player.Camera.transform.position,session.Player.Camera.transform.position+Vector3.left*20+Vector3.down*6);
            angle=moon.SurfaceAngle;moon.SurfaceDegreesPerSecond=-.5f;moon.AdvanceSurface(2);Assert.That(Mathf.DeltaAngle(angle,moon.SurfaceAngle),Is.EqualTo(-1).Within(.001f));
            Assert.That(session.Player.Origin.transform.position,Is.EqualTo(origin));moon.SurfaceDegreesPerSecond=.12f;
            Capture("orbit-waiting",session.Player.Camera.transform.position,view.Target.position+Vector3.forward*3);
            Capture("orbit-high-eye",session.Player.Camera.transform.position+Vector3.up*.5f,view.Target.position+Vector3.forward*3);
            session.RetryMission();Board();yield return null;Assert.That(moon.SurfaceAngle,Is.Zero);
        }
        [UnityTest]public IEnumerator MainThrustRcsAndBrakeUseSeparateFuelAndPreserveCoasting()
        {
            StartAt(new Vector3(0,0,-35));float main=flight.MainFuel,rcs=docking.RcsFuel;
            Drive(DockCommand.MainBoost,1);Assert.That(flight.MainFuel,Is.EqualTo(main-2.2f).Within(.01f));Assert.That(docking.RcsFuel,Is.EqualTo(rcs).Within(.001f));
            var velocity=docking.Velocity;flight.Tick(1);Assert.That(docking.Velocity,Is.EqualTo(velocity));
            main=flight.MainFuel;Drive(DockCommand.Left,1);Assert.That(docking.RcsFuel,Is.LessThan(rcs));Assert.That(flight.MainFuel,Is.EqualTo(main));
            Drive(DockCommand.Brake,4);Assert.That(docking.RelativeSpeed,Is.LessThan(.005f));Assert.That(docking.AngularVelocity.magnitude,Is.LessThan(.01f));yield return null;
        }
        [UnityTest]public IEnumerator AssistanceCannotCaptureFarMisalignedOrFastApproaches()
        {
            foreach(var scenario in new[]{(new Vector3(0,0,-9),Vector3.zero,Vector3.zero),(new Vector3(1,0,-5),Vector3.zero,Vector3.zero),(new Vector3(0,0,-5),Vector3.zero,new Vector3(0,10,0)),(new Vector3(0,0,-5),Vector3.forward*.5f,Vector3.zero)})
            {StartAt(scenario.Item1,scenario.Item2,scenario.Item3);Assert.That(docking.CanAssist,Is.False);flight.Tick(.1f);Assert.That(docking.State,Is.EqualTo(DockingState.Approaching));session.RetryMission();yield return null;}
        }
        [UnityTest]public IEnumerator ManualApproachThenLocalAssistanceDocksAndRetryResetsEverything()
        {
            StartAt(new Vector3(0,0,-35));yield return null;var origin=session.Player.Origin.transform.position;
            Drive(DockCommand.Forward,2);Assert.That(docking.RelativeSpeed,Is.InRange(.69f,.71f));
            flight.Tick(35);Assert.That(docking.Position.z,Is.InRange(-11,-8));Drive(DockCommand.Brake,2);
            Drive(DockCommand.Forward,.55f);docking.ReleaseControls();
            for(int i=0;i<1800&&!flight.IsTerminal;i++)flight.Tick(.1f);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Docked),flight.Failure+" "+docking.Position+" v="+docking.Velocity);
            Assert.That(docking.Distance,Is.EqualTo(.22f).Within(.001f));Assert.That(docking.RelativeSpeed,Is.Zero);Assert.That(docking.RcsFuel,Is.GreaterThan(0));
            yield return null;Capture("docked",session.Player.Camera.transform.position,view.Target.position);Capture("docked-panel",session.Player.Camera.transform.position,panel.Panel.transform.position);
            float fuel=docking.RcsFuel,oxygen=flight.Oxygen;flight.Tick(1000);Assert.That(docking.RcsFuel,Is.EqualTo(fuel));Assert.That(flight.Oxygen,Is.EqualTo(oxygen));Assert.That(session.Player.Origin.transform.position,Is.EqualTo(origin));
            yield return Click(panel.RetryButton);yield return null;
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding));Assert.That(docking.State,Is.EqualTo(DockingState.Waiting));Assert.That(docking.ActiveCommandCount,Is.Zero);Assert.That(flight.MainFuel,Is.EqualTo(100));Assert.That(docking.RcsFuel,Is.EqualTo(100));Assert.That(scene.GroundRoot.activeSelf,Is.True);
        }
        [UnityTest]public IEnumerator DefaultOffsetAndAttitudeCanBeManuallyCorrectedWithAvailableFuel()
        {
            Board();Launch();Orbit();yield return null;
            Assert.That(docking.LateralError,Is.GreaterThan(.6f));Assert.That(docking.AlignmentError,Is.GreaterThan(3));
            bool reachedAssist=false;
            // 测试驾驶只发送玩家同样的按下/松开指令；起飞后不直接改位置、姿态或燃料。
            for(int i=0;i<4000&&!flight.IsTerminal;i++)
            {
                docking.ReleaseControls();
                if(docking.CanAssist || docking.State==DockingState.Capturing){reachedAssist=true;flight.Tick(.05f);continue;}
                Vector3 desired=new(Mathf.Clamp(-docking.Position.x*.3f,-.15f,.15f),Mathf.Clamp(-docking.Position.y*.3f,-.15f,.15f),docking.Distance>10?.55f:.2f);
                Vector3 correction=Quaternion.Inverse(docking.Attitude)*(desired-docking.Velocity);
                PilotAxis(correction.x,.025f,DockCommand.Right,DockCommand.Left);
                PilotAxis(correction.y,.025f,DockCommand.Up,DockCommand.Down);
                PilotAxis(correction.z,.025f,DockCommand.Forward,DockCommand.Backward);
                var euler=docking.Attitude.eulerAngles;
                Vector3 desiredAngular=new(Mathf.Clamp(-Mathf.DeltaAngle(0,euler.x)*.5f,-1,1),Mathf.Clamp(-Mathf.DeltaAngle(0,euler.y)*.5f,-1,1),Mathf.Clamp(-Mathf.DeltaAngle(0,euler.z)*.5f,-1,1));
                var angularCorrection=desiredAngular-docking.AngularVelocity;
                PilotAxis(angularCorrection.x,.12f,DockCommand.PitchDown,DockCommand.PitchUp);
                PilotAxis(angularCorrection.y,.12f,DockCommand.YawRight,DockCommand.YawLeft);
                PilotAxis(angularCorrection.z,.12f,DockCommand.RollLeft,DockCommand.RollRight);
                flight.Tick(.05f);
            }
            Assert.That(reachedAssist,Is.True);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Docked),flight.Failure+" position="+docking.Position+" attitude="+docking.AlignmentError);
            Assert.That(docking.RcsFuel,Is.GreaterThan(50));Assert.That(flight.MainFuel,Is.EqualTo(28).Within(.05f));Assert.That(flight.Oxygen,Is.GreaterThan(30));
            yield return null;Capture("default-docked",session.Player.Camera.transform.position,panel.Panel.transform.position);
        }
        private void PilotAxis(float error,float tolerance,DockCommand positive,DockCommand negative)
        {if(Mathf.Abs(error)>tolerance)docking.SetCommand(error>0?positive:negative,true);}
        [UnityTest]public IEnumerator HighSpeedImpactExplodesAndCannotBeRescuedByAssist()
        {
            StartAt(new Vector3(0,0,-1),Vector3.forward*20);flight.Tick(.1f);yield return null;
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Failed));Assert.That(flight.Failure,Is.EqualTo(AscentFailure.DockingCollision));Assert.That(docking.State,Is.EqualTo(DockingState.Failed));Assert.That(view.ExplosionVisible,Is.True);
            Assert.That(session.GetComponent<MissionFailurePresenter>().IsVisible,Is.True);Capture("collision",session.Player.Camera.transform.position,session.Player.Camera.transform.position+Vector3.forward*3);
        }
        [UnityTest]public IEnumerator SlowMisalignedContactAndDistantDriftFailForDistinctReasons()
        {
            StartAt(new Vector3(1.2f,0,-.3f),Vector3.forward*.2f);flight.Tick(1);Assert.That(flight.Failure,Is.EqualTo(AscentFailure.DockingMisaligned));
            session.RetryMission();yield return null;StartAt(new Vector3(0,0,-139.8f),Vector3.back);flight.Tick(1);Assert.That(flight.Failure,Is.EqualTo(AscentFailure.TargetLost));
        }
        [UnityTest]public IEnumerator EmptyRcsAndOxygenRemainRealFailureConditionsInRendezvous()
        {
            StartAt(new Vector3(0,0,-35),configOverride:"{\"initialRcs\":0.005}");Drive(DockCommand.Left,1);Assert.That(flight.Failure,Is.EqualTo(AscentFailure.RcsDepleted));
            session.RetryMission();yield return null;StartAt(new Vector3(0,0,-35));float oxygen=flight.Oxygen;flight.Tick(oxygen+.1f);Assert.That(flight.Failure,Is.EqualTo(AscentFailure.OxygenDepleted));
        }
        [UnityTest]public IEnumerator CloseRangeMainBoostIsDisabledAndOffAxisAssistConsumesFuel()
        {
            StartAt(new Vector3(.55f,-.3f,-5),Vector3.zero,new Vector3(-2,3,0));Assert.That(docking.CanAssist,Is.True);Assert.That(docking.CanBoost,Is.False);
            float main=flight.MainFuel;Drive(DockCommand.MainBoost,1);Assert.That(flight.MainFuel,Is.EqualTo(main));
            float error=docking.LateralError,angle=docking.AlignmentError,fuel=docking.RcsFuel;flight.Tick(3);Assert.That(docking.LateralError,Is.LessThan(error));Assert.That(docking.AlignmentError,Is.LessThan(angle));Assert.That(docking.RcsFuel,Is.LessThan(fuel));
            flight.Tick(100);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Docked),flight.Failure.ToString());yield return null;
        }
        [UnityTest]public IEnumerator RealMouseHoldReleasesThrustAndOnlyOneFrameClockAdvances()
        {
            StartAt(new Vector3(0,0,-35));yield return null;session.Player.MatchOriginUpCameraForward(Vector3.up,Vector3.forward);yield return null;
            var button=panel.Panel.GetComponentsInChildren<DockingThrustButton>().Single(b=>b.Command==DockCommand.Forward).GetComponent<Button>();
            yield return HoldMouse(button,30);Assert.That(docking.RelativeSpeed,Is.GreaterThan(.2f));Assert.That(docking.ActiveCommandCount,Is.Zero);
            float speed=docking.RelativeSpeed,oxygen=flight.Oxygen;session.enabled=true;yield return null;session.enabled=false;
            Assert.That(flight.Oxygen,Is.EqualTo(oxygen-Time.deltaTime*flight.OxygenRate).Within(.002f));Assert.That(docking.RelativeSpeed,Is.EqualTo(speed).Within(.001f));
            Capture("controls",session.Player.Camera.transform.position,panel.Panel.transform.position);
        }
        [UnityTest]public IEnumerator DockingAndFlightControlsFitChineseEnglishRussian()
        {
            var language=Object.FindAnyObjectByType<LocalizationService>();Board();yield return null;
            foreach(var lang in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian}){language.SetLanguage(lang);yield return null;CheckLabels(scene.Panel);}
            Launch();Orbit();yield return null;
            foreach(var lang in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian}){language.SetLanguage(lang);yield return null;CheckLabels(panel.Panel);Capture("controls-"+lang,session.Player.Camera.transform.position,panel.Panel.transform.position);}
        }
        private static void CheckLabels(GameObject root)
        {foreach(var label in root.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();Assert.That(label.isTextOverflowing,Is.False,label.name+" "+label.text);Assert.That(label.text.Contains("{"),Is.False,label.text);foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c)))Assert.That(label.font.HasCharacter(c,true),Is.True,c+" "+label.text);}}
        private IEnumerator Click(Button button){yield return HoldMouse(button,2,false);}
        private IEnumerator HoldMouse(Button button,int frames,bool advance=true)
        {
            Assert.That(button.IsInteractable(),Is.True,button.name);Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();var rect=button.GetComponent<RectTransform>();
            Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center));Assert.That(canvas.worldCamera.pixelRect.Contains(point),Is.True,button.name+" outside view "+point);
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button),string.Join(",",hits.Select(h=>h.gameObject.name)));
            var mouse=InputSystem.AddDevice<Mouse>("Docking verification mouse");
            try
            {var state=new MouseState{position=point};InputState.Change(mouse,state);yield return null;yield return null;InputState.Change(mouse,state.WithButton(MouseButton.Left));for(int i=0;i<frames;i++){yield return null;if(advance)flight.Tick(Time.deltaTime);}InputState.Change(mouse,state.WithButton(MouseButton.Left,false));yield return null;yield return null;}
            finally{if(mouse.added)InputSystem.RemoveDevice(mouse);}
        }
        private static void Capture(string name,Vector3 eye,Vector3 target)
        {
            var obj=new GameObject("Docking verification camera");var camera=obj.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=70;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            var rt=new RenderTexture(1600,1100,24);var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{rt.Create();camera.targetTexture=rt;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,1100),0,0);pixels.Apply();File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../../.development/docking-"+name+".png")),pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(pixels);Object.Destroy(rt);Object.Destroy(obj);}
        }
    }
}
