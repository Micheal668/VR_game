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
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    public sealed class LifeSupportTests
    {
        private StationMissionSession session;
        private LifeSupportMission life;
        private LifeSupportSceneLayout layout;
        private CargoInventory cargo;
        private AscentMission flight;
        private DockingMission docking;
        private XRRayInteractor hand;
        private XRInteractionManager manager;
        private readonly List<Object> temporary=new();
        private float previousDelta;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousFocus;
        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/30;
            previousBackground=InputSystem.settings.backgroundBehavior;previousFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            // Fixtures control the view explicitly, independent of the user's saved simulator toggle.
            foreach(var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None))simulator.gameObject.SetActive(false);
            yield return null;
            session=Object.FindAnyObjectByType<StationMissionSession>();session.enabled=false;
            life=session.GetComponent<LifeSupportMission>();layout=session.GetComponent<LifeSupportSceneLayout>();cargo=session.GetComponent<CargoInventory>();
            flight=session.GetComponent<AscentMission>();docking=session.GetComponent<DockingMission>();
            Assert.That(life.IsConfigured,Is.True);Assert.That(layout,Is.Not.Null);
            manager=Object.FindAnyObjectByType<XRInteractionManager>();var obj=new GameObject("Life support verification hand");temporary.Add(obj);
            hand=obj.AddComponent<XRRayInteractor>();hand.interactionManager=manager;
            hand.selectInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            hand.activateInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            yield return null;
        }
        [TearDown] public void Restore()
        {
            Time.captureDeltaTime=previousDelta;InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousFocus;
            foreach(var obj in temporary)if(obj!=null)Object.Destroy(obj);temporary.Clear();
        }
        [UnityTest] public IEnumerator EveryRetryRandomizesWithinBoundsAndStartsInCasualClothes()
        {
            var wardrobe=session.GetComponent<CrewWardrobe>();var hud=session.GetComponent<SuitHudPresenter>();
            var values=new HashSet<float>();var day=new HashSet<bool>();life.SetRandomSeed(72);
            for(int i=0;i<24;i++)
            {
                session.RetryMission();values.Add(life.BaseOxygen);day.Add(life.IsDaylight);
                Assert.That(life.BaseOxygen,Is.InRange(30,55));Assert.That(life.BasePower,Is.InRange(35,65));
                Assert.That(life.SuitOxygen,Is.InRange(20,50));Assert.That(life.SuitPower,Is.InRange(20,50));
                Assert.That(life.SuitWorn||life.DoorOpen||hud.Hud.activeSelf,Is.False);
                Assert.That(wardrobe.CasualRenderers.All(r=>r.enabled),Is.True);Assert.That(wardrobe.SuitedRenderers.Any(r=>r.enabled),Is.False);
                Assert.That(wardrobe.HangingSuit.activeSelf,Is.True);Assert.That(session.GetComponent<CrewPanelPresenter>().GroundPanel.activeSelf,Is.False);
                Assert.That(wardrobe.CommanderHangingSuit.activeSelf,Is.True);
                float oxygen=life.BaseOxygen;session.Advance(100);Assert.That(life.BaseOxygen,Is.EqualTo(oxygen));
            }
            Assert.That(values.Count,Is.EqualTo(24));Assert.That(day.Count,Is.EqualTo(2));
            yield return Languages(layout.HabitatConsole,"life-habitat");
            yield return Capture("life-base-overview",new Vector3(-1.7f,1.7f,-3.3f),new Vector3(1.4f,1.5f,1.5f));
            yield return Capture("life-suit-rack",new Vector3(1.1f,1.5f,-2),new Vector3(3.6f,1.3f,-1.6f));
        }
        [UnityTest] public IEnumerator UnsuitedHatchVentsImmediatelyAndSuffocatesAtSixSeconds()
        {
            session.BeginMission();RepairDoor();MoveBody(life.DoorControl.position+Vector3.left*.8f);
            Assert.That(life.TryOpenDoor(),Is.True);Assert.That(life.BaseOxygen,Is.Zero);
            session.Advance(5.99f);Assert.That(session.Mission.IsTerminal,Is.False);Assert.That(Mathf.Abs(life.BaseTemperature-22),Is.GreaterThan(40));
            session.Advance(.02f);Assert.That(session.Mission.FailureReason,Is.EqualTo(StationMissionFailure.Suffocation));
            Assert.That(life.ElapsedSeconds,Is.EqualTo(life.Config.DoorRepairSeconds+6).Within(.001f));
            float elapsed=life.ElapsedSeconds;session.Advance(100);Assert.That(life.ElapsedSeconds,Is.EqualTo(elapsed));
            yield return Languages(session.GetComponent<MissionScorePresenter>().Panel,"life-suffocation");
            session.RetryMission();Assert.That(life.DoorOpen,Is.False);Assert.That(life.BaseOxygen,Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator EmptySealedHabitatKillsAfterGracePeriodWithCoarseAndFineSteps()
        {
            Configure("{\"baseOxygenSeconds\":10,\"baseOxygenRange\":{\"x\":30,\"y\":30}}");
            session.BeginMission();session.Advance(8.99f);Assert.That(life.BaseOxygen,Is.Zero);Assert.That(session.Mission.IsTerminal,Is.False);
            session.Advance(.02f);Assert.That(session.Mission.FailureReason,Is.EqualTo(StationMissionFailure.Suffocation));float coarse=life.ElapsedSeconds;
            session.RetryMission();session.BeginMission();for(int i=0;i<100&&!session.Mission.IsTerminal;i++)session.Advance(.1f);
            Assert.That(life.ElapsedSeconds,Is.EqualTo(coarse).Within(.002f));Assert.That(coarse,Is.EqualTo(9).Within(.002f));yield return null;
        }
        [UnityTest] public IEnumerator RealSuitGripRequiresContactAndFourSecondsAndSynchronizesBothCrew()
        {
            session.BeginMission();life.SetDonHeld(true);session.Advance(2);Assert.That(life.DonProgressSeconds,Is.Zero);
            MoveBody(life.SuitRack.position+Vector3.left*.75f);
            Select(layout.SuitHandle.Physical,layout.SuitHandle.transform.position);session.Advance(2);Release(layout.SuitHandle.Physical);
            session.Advance(1);Assert.That(life.DonProgressSeconds,Is.EqualTo(2).Within(.001f));
            Select(layout.SuitHandle.Physical,layout.SuitHandle.transform.position);MoveBody(new Vector3(0,0,0));session.Advance(1);
            Assert.That(life.DonProgressSeconds,Is.EqualTo(2).Within(.001f));Release(layout.SuitHandle.Physical);
            MoveBody(life.SuitRack.position+Vector3.left*.75f);Select(layout.SuitHandle.Physical,layout.SuitHandle.transform.position);
            session.Advance(1.99f);Assert.That(life.SuitWorn,Is.False);session.Advance(.01f);Release(layout.SuitHandle.Physical);
            Assert.That(life.SuitWorn,Is.True);var wardrobe=session.GetComponent<CrewWardrobe>();
            Assert.That(wardrobe.SuitedRenderers.All(r=>r.enabled),Is.True);Assert.That(wardrobe.CasualRenderers.Any(r=>r.enabled),Is.False);
            Assert.That(wardrobe.HangingSuit.activeSelf,Is.False);Assert.That(session.GetComponent<SuitHudPresenter>().Hud.activeSelf,Is.True);
            Assert.That(wardrobe.CommanderHangingSuit.activeSelf,Is.False);
            yield return null;yield return Languages(session.GetComponent<SuitHudPresenter>().Hud);
            yield return Capture("life-suit-hud",session.Player.Camera.transform.position,session.Player.Camera.transform.position+session.Player.Camera.transform.forward);
        }
        [UnityTest] public IEnumerator RealOxygenActivationAndPackedBatteryAreAtomicAndRetrySafe()
        {
            Don();var oxygen=cargo.Items.First(i=>i.Kind==CargoKind.Oxygen);float before=life.SuitOxygen;
            Assert.That(life.TryUseSupply(oxygen),Is.False);Assert.That(cargo.TryHold(oxygen),Is.True);
            Assert.That(life.TryUseSupply(oxygen),Is.False,"Ledger Held without XR contact must not consume");cargo.DropHeld(oxygen);
            Select(oxygen.Grab,oxygen.transform.position);hand.activateInput.QueueManualState(true,1,true,false);
            for(int i=0;i<4;i++)yield return null;
            hand.activateInput.QueueManualState(false,0,false,true);
            Assert.That(oxygen.State,Is.EqualTo(CargoState.Consumed));Assert.That(life.SuitOxygen,Is.EqualTo(Mathf.Min(100,before+60)).Within(.001f));
            Assert.That(life.SuppliesUsed,Is.EqualTo(1));Assert.That(life.TryUseSupply(oxygen),Is.False);
            var battery=Pack(CargoKind.Battery);float power=life.SuitPower;Assert.That(life.TryUseSupply(battery),Is.True);
            Assert.That(life.SuitPower,Is.EqualTo(Mathf.Min(100,power+60)).Within(.001f));Assert.That(cargo.GetCount(CargoKind.Battery),Is.Zero);
            session.RetryMission();Assert.That(life.SuppliesUsed,Is.Zero);Assert.That(oxygen.State,Is.EqualTo(CargoState.World));
            Don();var again=Pack(CargoKind.Oxygen);bool reset=false;
            void RetryDuringConsume(){if(!reset&&again.State==CargoState.Consumed){reset=true;session.RetryMission();}}
            cargo.Changed+=RetryDuringConsume;Assert.That(life.TryUseSupply(again),Is.False);cargo.Changed-=RetryDuringConsume;
            Assert.That(reset,Is.True);Assert.That(again.State,Is.EqualTo(CargoState.World));Assert.That(life.SuppliesUsed,Is.Zero);Assert.That(life.SuitWorn,Is.False);
        }
        [UnityTest] public IEnumerator PowerLossBlacksOutHudAcceleratesOxygenAndWristBatteryRestoresIt()
        {
            Configure("{\"suitPowerSeconds\":20,\"suitPowerRange\":{\"x\":20,\"y\":20},\"suitOxygenSeconds\":10000}");
            Don();Pack(CargoKind.Battery);session.Advance(4);var hud=session.GetComponent<SuitHudPresenter>();
            Assert.That(life.SuitPower,Is.Zero);Assert.That(hud.Hud.activeSelf,Is.False);Assert.That(hud.RouteLine.enabled,Is.False);
            Assert.That(hud.RouteGuide.Visible,Is.False);
            Assert.That(hud.WristControls.activeSelf,Is.True);Assert.That(life.SuitOxygenRate,Is.EqualTo(life.Config.SuitOxygenRate*2.5f));
            RepairDoor();MoveBody(life.DoorControl.position+Vector3.left*.8f);life.OpenDoor();float oxygen=life.SuitOxygen;session.Advance(2);
            Assert.That(life.SuitOxygen,Is.EqualTo(oxygen-life.Config.SuitOxygenRate*5).Within(.001f));
            var button=hud.WristControls.GetComponentsInChildren<Button>().Single(b=>b.name=="Wrist Battery");button.onClick.Invoke();
            Assert.That(hud.Hud.activeSelf,Is.True);Assert.That(life.SuitPower,Is.EqualTo(60).Within(.001f));
            Assert.That(life.SuitOxygenRate,Is.EqualTo(life.Config.SuitOxygenRate));yield return null;
        }
        [UnityTest] public IEnumerator UnpoweredSuitDiesFromHeatByDayAndColdByNight()
        {
            Configure("{\"suitPowerSeconds\":20,\"suitPowerRange\":{\"x\":20,\"y\":20},\"suitOxygenSeconds\":10000}");
            foreach(bool daylight in new[]{true,false})
            {
                life.SetRandomSeed(123);for(int attempt=0;attempt<50;attempt++){session.RetryMission();if(life.IsDaylight==daylight)break;}
                Assert.That(life.IsDaylight,Is.EqualTo(daylight));Don();session.Advance(4);
                RepairDoor();MoveBody(life.DoorControl.position+Vector3.left*.8f);life.OpenDoor();session.Advance(9.08f);Assert.That(session.Mission.IsTerminal,Is.False);
                session.Advance(.03f);Assert.That(session.Mission.FailureReason,Is.EqualTo(daylight?StationMissionFailure.Hyperthermia:StationMissionFailure.Hypothermia));
                Assert.That(life.SuitOxygen,Is.GreaterThan(0));yield return null;
            }
        }
        [UnityTest] public IEnumerator EmptySuitOxygenKillsEvenWithPowerAndDoesNotUseSealedHabitatAir()
        {
            Configure("{\"suitOxygenSeconds\":20,\"suitOxygenRange\":{\"x\":20,\"y\":20}}");
            Don();session.Advance(4);Assert.That(life.SuitOxygen,Is.Zero);Assert.That(life.BaseOxygen,Is.GreaterThan(0));
            session.Advance(5.99f);Assert.That(session.Mission.IsTerminal,Is.False);session.Advance(.02f);
            Assert.That(session.Mission.FailureReason,Is.EqualTo(StationMissionFailure.Suffocation));yield return null;
        }
        [UnityTest] public IEnumerator MinimumReservesWithSuppliesCanRescueWalkBoardAndOperateIntegratedCockpit()
        {
            Configure("{\"baseOxygenRange\":{\"x\":30,\"y\":30},\"basePowerRange\":{\"x\":35,\"y\":35},\"suitOxygenRange\":{\"x\":20,\"y\":20},\"suitPowerRange\":{\"x\":20,\"y\":20}}");
            Don();Pack(CargoKind.Oxygen);Pack(CargoKind.Battery);life.UseOxygen();life.UseBattery();
            session.Mission.Tick(session.Mission.RepairTask.RemainingSeconds,true,false);session.Advance(session.Mission.Config.StabilizedSeconds);
            var crew=session.GetComponent<CrewMission>();var ground=session.GetComponent<GroundCrewController>();
            MoveBody(crew.Commander.position+Vector3.right*.9f);Select(ground.RescueHandle.GetComponent<XRSimpleInteractable>(),ground.RescueHandle.transform.position);
            session.Advance(crew.Config.RescueSeconds);Release(ground.RescueHandle.GetComponent<XRSimpleInteractable>());
            Assert.That(crew.CommanderRescued,Is.True);RepairDoor();MoveBody(life.DoorControl.position+Vector3.left*.8f);Assert.That(life.TryOpenDoor(),Is.True);
            // The commander can already approach the hatch during lock repair; continue forward from the spawn waypoint.
            float routeDistance=0;
            for(int waypointIndex=1;waypointIndex<ground.Waypoints.Count;waypointIndex++)
            {
                var waypoint=ground.Waypoints[waypointIndex];
                routeDistance+=Vector3.Distance(ground.Waypoints[waypointIndex-1].position,waypoint.position);
                MoveBody(waypoint.position);
                for(int i=0;i<240&&!ground.ReachedBoarding;i++)
                {if(ground.RouteProgress>=routeDistance-.86f&&waypoint!=ground.Waypoints.Last())break;session.Advance(.25f);}
                Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Evacuation),session.Mission.FailureReason+" at "+waypoint.name);
            }
            Assert.That(ground.ReachedBoarding,Is.True);Assert.That(life.SuitOxygen,Is.GreaterThan(20));Assert.That(life.SuitPower,Is.GreaterThan(20));
            var hatch=Object.FindAnyObjectByType<HatchBoardingController>();Assert.That(hatch.CanBoard,Is.True);hatch.RequestBoarding();session.Advance(0);
            flight.Tick(.951f);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Startup));yield return null;
            Assert.That(crew.CommanderBoarded,Is.True);var screen=session.GetComponent<CockpitScreenPresenter>();
            float groundOxygen=life.SuitOxygen;session.Advance(20);Assert.That(life.SuitOxygen,Is.EqualTo(groundOxygen));
            yield return Languages(layout.OperationsScreen.transform.parent.gameObject);
            yield return Capture("life-cockpit-startup",session.Player.Camera.transform.position,new Vector3(99.55f,1.5f,1.38f),70);
            yield return Click(screen.StartupButtons[0]);Assert.That(flight.Powered,Is.True);
            yield return Click(screen.StartupButtons[1]);flight.Tick(flight.Config.NavigationSeconds);
            yield return Click(screen.StartupButtons[2]);flight.Tick(flight.Config.EngineSeconds);
            yield return Click(screen.StartupButtons[3]);flight.Tick(flight.Config.IgnitionSeconds);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Ascent));flight.Tick(flight.OrbitAscentSeconds-flight.AirborneSeconds);
            yield return Click(screen.CircularizeButton);flight.Tick(flight.Config.CircularizationSeconds);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Rendezvous));
            yield return null;var feed=layout.NavigationScreen.GetComponent<DockingCameraDisplay>();Assert.That(feed.Feed,Is.Not.Null);Assert.That(feed.Feed.IsCreated(),Is.True);
            var reticle=layout.NavigationScreen.GetComponentInChildren<DockingReticle>();Assert.That(reticle.TargetVisible,Is.True);
            Vector2 before=reticle.TargetViewport;
            var right=screen.ThrustButtons.Single(b=>b.GetComponent<DockingThrustButton>().Command==DockCommand.Right);
            yield return HoldMouse(right,10);Assert.That(docking.ActiveCommandCount,Is.Zero);yield return null;
            Assert.That(Vector2.Distance(before,reticle.TargetViewport),Is.GreaterThan(.0001f));
            yield return Languages(layout.OperationsScreen.transform.parent.gameObject);
            yield return Capture("life-cockpit-docking",session.Player.Camera.transform.position,new Vector3(99.55f,1.5f,1.38f),70);
            yield return Capture("life-cockpit-window",session.Player.Camera.transform.position,new Vector3(96,1.6f,1));
            Assert.That(screen.Oxygen.DisplayedPercent,Is.EqualTo(flight.Oxygen/flight.Config.OxygenCapacity*100).Within(.001f));
            // 与玩家相同的六自由度推力输入完成默认偏移，验证实体屏工作时仍可完整对接。
            for(int i=0;i<4000&&!flight.IsTerminal;i++)
            {
                docking.ReleaseControls();
                if(docking.CanAssist||docking.State==DockingState.Capturing){flight.Tick(.05f);continue;}
                Vector3 desired=new(Mathf.Clamp(-docking.Position.x*.3f,-.15f,.15f),Mathf.Clamp(-docking.Position.y*.3f,-.15f,.15f),docking.Distance>10?.55f:.2f);
                Vector3 correction=Quaternion.Inverse(docking.Attitude)*(desired-docking.Velocity);
                Axis(correction.x,.025f,DockCommand.Right,DockCommand.Left);Axis(correction.y,.025f,DockCommand.Up,DockCommand.Down);
                Axis(correction.z,.025f,DockCommand.Forward,DockCommand.Backward);
                var euler=docking.Attitude.eulerAngles;
                var desiredAngular=new Vector3(Mathf.Clamp(-Mathf.DeltaAngle(0,euler.x)*.5f,-1,1),Mathf.Clamp(-Mathf.DeltaAngle(0,euler.y)*.5f,-1,1),Mathf.Clamp(-Mathf.DeltaAngle(0,euler.z)*.5f,-1,1));
                var angular=desiredAngular-docking.AngularVelocity;
                Axis(angular.x,.12f,DockCommand.PitchDown,DockCommand.PitchUp);Axis(angular.y,.12f,DockCommand.YawRight,DockCommand.YawLeft);
                Axis(angular.z,.12f,DockCommand.RollLeft,DockCommand.RollRight);flight.Tick(.05f);
            }
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Docked),flight.Failure.ToString());
            Assert.That(session.GetComponent<MissionScore>().HasResult,Is.True);
            session.RetryMission();yield return null;Assert.That(life.SuitWorn,Is.False);Assert.That(feed.Optics.enabled,Is.False);
        }
        [UnityTest] public IEnumerator DayNightLampsBeaconPulsesAndThreeLanguageWallPanelsRemainReadable()
        {
            life.SetRandomSeed(82);for(int i=0;i<30;i++){session.RetryMission();if(!life.IsDaylight)break;}
            Assert.That(life.IsDaylight,Is.False);Assert.That(session.GetComponent<LifeSupportEnvironment>().Sun.intensity,Is.LessThan(.05f));
            Assert.That(Object.FindObjectsByType<LocalizedText>(FindObjectsSortMode.None).Any(t=>t.GetComponent<TMP_Text>().text=="警报后开启"),Is.False);
            yield return Languages(layout.AirlockConsole,"life-airlock");yield return Languages(layout.SupplyConsole);
            var suitPanel=layout.SuitRack.transform.Find("Suit Service Console");yield return Languages(suitPanel.gameObject);
            yield return Capture("life-base-night",new Vector3(-1.7f,1.7f,-3.3f),new Vector3(1.4f,1.5f,1.5f));
            var beacon=Object.FindAnyObjectByType<LanderNavigationBeacon>();var lit=new HashSet<bool>();
            for(int i=0;i<20;i++){yield return new WaitForSecondsRealtime(.09f);lit.Add(beacon.Lit);}Assert.That(lit.Count,Is.EqualTo(2));
            while(Mathf.Repeat(Time.unscaledTime,1.4f)>.08f)yield return null;
            yield return Capture("life-lander-night",new Vector3(36,3,-8),new Vector3(48,3.2f,1.7f));
            Don();RepairDoor();MoveBody(life.DoorControl.position+Vector3.left*.8f);life.OpenDoor();yield return null;
            Assert.That(session.GetComponent<SuitHudPresenter>().RouteLine.positionCount,Is.GreaterThan(2));
            Assert.That(session.GetComponent<SuitHudPresenter>().RouteLine.enabled,Is.False);
            Assert.That(session.GetComponent<SuitHudPresenter>().RouteGuide.ArrowCount,Is.GreaterThan(2));
            Assert.That(session.GetComponent<LifeSupportEnvironment>().ShoulderLights.All(l=>l.enabled),Is.True);
        }
        [UnityTest] public IEnumerator RevisedEquipmentLayoutHasClearConsoleAndTwoSeparateSuitRacks()
        {
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Any(t=>t.name=="Station Electrical Service Panel"),Is.False);
            var wardrobe=session.GetComponent<CrewWardrobe>();
            Assert.That(Vector3.Distance(wardrobe.HangingSuit.transform.position,wardrobe.CommanderHangingSuit.transform.position),Is.GreaterThan(1.2f));
            Assert.That(layout.SupplyConsole.transform.position.x-layout.HabitatConsole.transform.position.x,Is.EqualTo(2.1f).Within(.01f));
            Assert.That(layout.SupplyConsole.transform.position.y,Is.EqualTo(layout.HabitatConsole.transform.position.y).Within(.01f));
            Assert.That(layout.SupplyConsole.transform.position.z,Is.EqualTo(layout.HabitatConsole.transform.position.z).Within(.01f));
            Assert.That(Quaternion.Angle(layout.SupplyConsole.transform.rotation,layout.HabitatConsole.transform.rotation),Is.LessThan(.1f));
            yield return Languages(layout.SupplyConsole);
            yield return Languages(wardrobe.CommanderHangingSuit.transform.parent.gameObject);
            yield return Capture("life-revised-supply",new Vector3(.65f,1.9f,-.5f),new Vector3(.65f,2.1f,3.69f),60);
            yield return Capture("life-revised-suits",new Vector3(.9f,1.65f,-1.25f),new Vector3(3.6f,1.3f,-1.25f),65);
        }
        [UnityTest] public IEnumerator ForwardViewHasNoWristBoxAndVisorRemainsReadableAcrossHands()
        {
            Don();var hud=session.GetComponent<SuitHudPresenter>();var eye=session.Player.Camera;
            RepairDoor();MoveBody(life.DoorControl.position+Vector3.left*.8f);life.OpenDoor();
            foreach(var modality in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>(FindObjectsSortMode.None))modality.enabled=false;
            for(var parent=hud.WristControls.transform.parent;parent!=session.Player.transform;parent=parent.parent)parent.gameObject.SetActive(true);
            MoveBody(new Vector3(16,0,1.7f));eye.transform.rotation=Quaternion.LookRotation(Vector3.right);
            yield return null;yield return null;
            var wrist=hud.WristControls.GetComponent<LookDownDisplay>();
            Assert.That(wrist.Visible,Is.False);Assert.That(hud.WristControls.GetComponent<Canvas>().enabled,Is.False);
            var pocket=Object.FindAnyObjectByType<CargoPocketPresenter>();Assert.That(pocket.GetComponent<Canvas>().enabled,Is.False);
            foreach(var text in hud.Hud.GetComponentsInChildren<TMP_Text>())
                Assert.That(text.fontSharedMaterial.shader.name,Is.EqualTo("TextMeshPro/Mobile/Distance Field Overlay"));
            var wristTransform=hud.WristControls.transform;var controller=wristTransform.parent;
            controller.position=eye.transform.position+new Vector3(.35f,-.5f,0);
            eye.transform.rotation=Quaternion.LookRotation(wristTransform.position-eye.transform.position);
            for(int i=0;i<3;i++)yield return null;
            Assert.That(wrist.Visible,Is.True,"A deliberate downward look must expose the manual supply keys");
            Pack(CargoKind.Battery);float before=life.SuitPower;
            hud.WristControls.GetComponentsInChildren<Button>().Single(b=>b.name=="Wrist Battery").onClick.Invoke();
            Assert.That(life.SuitPower,Is.GreaterThan(before));
            eye.transform.rotation=Quaternion.LookRotation(Vector3.right);yield return null;
            yield return Capture("life-revised-hud",eye.transform.position,eye.transform.position+Vector3.right*10,65);
        }
        [UnityTest] public IEnumerator ChevronNavigationFollowsTurnsAndTurnsOffWithSuitPower()
        {
            Don();RepairDoor();MoveBody(life.DoorControl.position+Vector3.left*.8f);life.OpenDoor();MoveBody(new Vector3(16,0,1.7f));
            var hud=session.GetComponent<SuitHudPresenter>();yield return null;
            Assert.That(hud.RouteGuide.Visible,Is.True);Assert.That(hud.RouteGuide.ArrowCount,Is.InRange(3,10));
            Assert.That(hud.RouteLine.enabled,Is.False);
            yield return Capture("life-revised-route",new Vector3(16,1.65f,1.7f),new Vector3(34,.5f,1.7f),65);
            MoveBody(new Vector3(38,0,-3));yield return null;
            var vertices=hud.RouteGuide.GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.That(vertices.Any(v=>v.x>40),Is.True,"Arrows must turn toward the landing ladder");
            session.RetryMission();yield return null;Assert.That(hud.RouteGuide.Visible,Is.False);
        }
        [UnityTest] public IEnumerator OpeningRepairedHatchStartsEvacuationAndSuitedCommanderFollowsWithoutOldRescueGate()
        {
            Don();Pack(CargoKind.Oxygen);Pack(CargoKind.Battery);life.UseOxygen();life.UseBattery();RepairDoor();
            Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Repair));
            yield return Languages(layout.HabitatConsole);
            var crew=session.GetComponent<CrewMission>();var ground=session.GetComponent<GroundCrewController>();
            Assert.That(crew.CommanderState,Is.EqualTo(CrewState.Ready));
            Assert.That(life.TryOpenDoor(),Is.True);
            Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(crew.CommanderState,Is.EqualTo(CrewState.Following));
            Assert.That(crew.CommanderRescued,Is.True);Assert.That(session.Mission.RepairRestored,Is.False);
            yield return Languages(layout.HabitatConsole);
            Vector3 before=crew.Commander.position;MoveBody(new Vector3(8.4f,0,1.7f));session.Advance(2);
            Assert.That(ground.RouteProgress,Is.GreaterThan(2));Assert.That(Vector3.Distance(before,crew.Commander.position),Is.GreaterThan(1));
            float routeDistance=0;
            for(int index=1;index<ground.Waypoints.Count;index++)
            {
                var waypoint=ground.Waypoints[index];routeDistance+=Vector3.Distance(ground.Waypoints[index-1].position,waypoint.position);
                MoveBody(waypoint.position);
                for(int step=0;step<240&&!ground.ReachedBoarding;step++)
                {if(ground.RouteProgress>=routeDistance-.86f&&index<ground.Waypoints.Count-1)break;session.Advance(.25f);}
                Assert.That(session.Mission.IsTerminal,Is.False,session.Mission.FailureReason.ToString());
            }
            Assert.That(ground.ReachedBoarding,Is.True);Assert.That(crew.IsCommanderAtBoardingPoint,Is.True);
            var hatch=Object.FindAnyObjectByType<HatchBoardingController>();
            MoveBody(new Vector3(49.4f,0,-4.5f));yield return null;
            yield return Capture("life-following-at-ship",new Vector3(42,2.1f,-8.8f),new Vector3(48,1.8f,-3.5f),65);
            yield return Click(hatch.AccessibleButton);session.Advance(0);flight.Tick(.951f);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Startup));Assert.That(crew.CommanderBoarded,Is.True);
            Assert.That(session.GetComponent<LifeSupportEnvironment>().ShoulderLights.All(l=>!l.enabled),Is.True);
            session.RetryMission();Assert.That(crew.CommanderRescued,Is.False);Assert.That(ground.RouteProgress,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator ShipSideButtonAcceptsRealMouseClickFromBoardingApproach()
        {
            Don();RepairDoor();life.OpenDoor();
            Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Evacuation));
            var hatch=Object.FindAnyObjectByType<HatchBoardingController>();
            MoveBody(new Vector3(49.4f,0,-4.5f));yield return null;
            Assert.That(session.Exit.ContainsPlayer,Is.True);Assert.That(hatch.CanBoard,Is.True);
            Assert.That(hatch.AccessibleButton.interactable,Is.True);
            yield return Click(hatch.AccessibleButton);session.Advance(0);
            Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Completed));
            flight.Tick(.951f);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Startup));
        }
        [UnityTest] public IEnumerator ShipSideButtonAcceptsTrackedControllerRayAfterOpeningAirlock()
        {
            Don();RepairDoor();life.OpenDoor();
            var hatch=Object.FindAnyObjectByType<HatchBoardingController>();
            Assert.That(hatch.CanBoard,Is.False,"A ray from the base cannot remotely board");
            MoveBody(new Vector3(49.4f,0,-4.5f));
            hand.enableUIInteraction=true;hand.uiPressInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            hand.transform.SetPositionAndRotation(session.Player.Camera.transform.position,
                Quaternion.LookRotation(hatch.AccessibleButton.transform.position-session.Player.Camera.transform.position));
            for(int i=0;i<5;i++)yield return null;
            Assert.That(hatch.CanBoard,Is.True);
            Assert.That(hand.TryGetCurrentUIRaycastResult(out var hit),Is.True);
            Assert.That(hit.gameObject==hatch.AccessibleButton.gameObject||hit.gameObject.transform.IsChildOf(hatch.AccessibleButton.transform),Is.True,hit.gameObject.name);
            hand.uiPressInput.manualValue=1;hand.uiPressInput.manualPerformed=true;for(int i=0;i<3;i++)yield return null;
            hand.uiPressInput.manualValue=0;hand.uiPressInput.manualPerformed=false;for(int i=0;i<3;i++)yield return null;
            session.Advance(0);Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Completed));
        }
        [UnityTest] public IEnumerator ShoulderLampsStayAtBothShouldersAndFaceForwardWhenLookingDown()
        {
            Configure("{\"suitPowerSeconds\":20,\"suitPowerRange\":{\"x\":20,\"y\":20},\"suitOxygenSeconds\":10000}");
            var environment=session.GetComponent<LifeSupportEnvironment>();var lamps=environment.ShoulderLights;
            Assert.That(lamps.Count,Is.EqualTo(2));Assert.That(lamps.All(l=>!l.enabled),Is.True);
            Assert.That(session.Player.Camera.transform.Find("Suit Helmet Lamp"),Is.Null);
            Don();MoveBody(Vector3.zero);var eye=session.Player.Camera.transform;
            eye.rotation=Quaternion.Euler(0,90,0);yield return null;
            var suit=lamps[0].transform.parent.parent.parent;
            Assert.That(suit.name,Is.Not.EqualTo("Player - Station Uniform"));
            var left=suit.InverseTransformPoint(lamps[0].transform.position);var right=suit.InverseTransformPoint(lamps[1].transform.position);
            Assert.That(left.x,Is.LessThan(-.2f));Assert.That(right.x,Is.GreaterThan(.2f));Assert.That(left.y,Is.InRange(1.35f,1.5f));
            Assert.That(right.y,Is.EqualTo(left.y).Within(.001f));Assert.That(lamps.All(l=>l.enabled&&l.gameObject.activeInHierarchy),Is.True);
            var before=lamps.Select(l=>l.transform.position).ToArray();
            eye.rotation=Quaternion.Euler(60,90,25);yield return null;
            for(int i=0;i<lamps.Count;i++)
            {
                Assert.That(Vector3.Distance(before[i],lamps[i].transform.position),Is.LessThan(.01f));
                Assert.That(Vector3.Dot(lamps[i].transform.forward,Vector3.right),Is.GreaterThan(.99f));
            }
            eye.rotation=Quaternion.Euler(0,90,0);yield return null;
            yield return Capture("life-shoulder-lamps",eye.position+Vector3.right*2.3f+Vector3.back*.8f,eye.position+Vector3.down*.2f,55,true,true);
            life.SetRandomSeed(82);for(int i=0;i<30;i++){session.RetryMission();if(!life.IsDaylight)break;}
            Assert.That(life.IsDaylight,Is.False);Don();MoveBody(new Vector3(16,0,1.7f));
            eye.rotation=Quaternion.Euler(8,90,0);yield return null;
            Color32[] litPixels=null,darkPixels=null;
            yield return Capture("life-shoulder-beams-night",eye.position,eye.position+eye.forward*8,65,true,false,p=>litPixels=p);
            session.Advance(4);Assert.That(lamps.All(l=>!l.enabled),Is.True);
            yield return Capture("life-shoulder-beams-unpowered",eye.position,eye.position+eye.forward*8,65,true,false,p=>darkPixels=p);
            // Compare the actual ground render, so a shader that ignores local lights cannot pass.
            float illuminationGain=0;int samples=0;
            for(int y=220;y<550;y++)for(int x=450;x<1350;x++)
            {int pixel=y*1800+x;illuminationGain+=(litPixels[pixel].r+litPixels[pixel].g+litPixels[pixel].b-darkPixels[pixel].r-darkPixels[pixel].g-darkPixels[pixel].b)/765f;samples++;}
            Assert.That(illuminationGain/samples,Is.GreaterThan(.025f),"The shoulder lamps must visibly illuminate the lunar ground");
            Pack(CargoKind.Battery);life.UseBattery();Assert.That(lamps.All(l=>l.enabled),Is.True);
            session.RetryMission();Assert.That(lamps.All(l=>!l.enabled&&!l.gameObject.activeInHierarchy),Is.True);
        }
        private void Axis(float value,float deadzone,DockCommand positive,DockCommand negative)
        {if(value>deadzone)docking.SetCommand(positive,true);else if(value<-deadzone)docking.SetCommand(negative,true);}
        // 门锁维修已换成气闸 A 抢修（三处故障 + 拉杆），真实手部操作见 AirlockRepairTests；
        // 这里只验证它与生命保障规则的衔接：未放行不能开门、穿航天服也不能绕过、重开重新上锁。
        [UnityTest] public IEnumerator HatchRequiresAirlockRepairEvenWhenSuitedAndRetryRelocksIt()
        {
            var panel=session.GetComponent<HabitatLifePanel>();var airlock=life.AirlockRepair;
            Assert.That(airlock,Is.Not.Null,"请先执行 Lunar Escape → Install Airlock Repair (Life Support Scene)");
            Assert.That(life.DoorRepairContact,Is.Null,"旧的门锁工具维修已移除");
            session.BeginMission();
            Assert.That(life.TryOpenDoor(),Is.False);Assert.That(panel.HatchButton.interactable,Is.False);
            panel.HatchButton.onClick.Invoke();Assert.That(life.DoorOpen,Is.False);
            Don();MoveBody(life.DoorControl.position+Vector3.left*.8f);
            Assert.That(life.TryOpenDoor(),Is.False,"Wearing a suit cannot bypass repair");
            session.Mission.Tick(session.Mission.RepairTask.RemainingSeconds,true,false);
            Assert.That(session.Mission.RepairRestored,Is.True);Assert.That(life.TryOpenDoor(),Is.False,"Oxygen repair is a separate task");
            airlock.SkipRepair();yield return null;
            Assert.That(life.DoorRepaired,Is.True);Assert.That(life.DoorOpen,Is.False,"放行只解除门锁，不会自己开门");Assert.That(life.BaseOxygen,Is.GreaterThan(0));
            Assert.That(panel.HatchButton.interactable,Is.True);yield return Languages(layout.AirlockConsole);
            yield return Click(panel.HatchButton);Assert.That(life.DoorOpen,Is.True);Assert.That(life.BaseOxygen,Is.Zero);
            session.Advance(6.1f);Assert.That(session.Mission.IsTerminal,Is.False,"A supplied suit protects after opening");
            session.RetryMission();yield return null;Assert.That(life.DoorRepaired,Is.False);Assert.That(airlock.IsReleased,Is.False);
            session.BeginMission();MoveBody(life.DoorControl.position+Vector3.left*.8f);Assert.That(life.TryOpenDoor(),Is.False);
        }
        [UnityTest] public IEnumerator DeathStopsAirlockRepair()
        {
            Configure("{\"baseOxygenSeconds\":1,\"baseOxygenRange\":{\"x\":100,\"y\":100},\"suffocationSeconds\":4}");
            session.BeginMission();session.Advance(5.1f);
            Assert.That(session.Mission.FailureReason,Is.EqualTo(StationMissionFailure.Suffocation));
            Assert.That(life.AirlockRepair.Active,Is.False,"死亡后不能继续抢修");
            Assert.That(life.DoorRepaired,Is.False);Assert.That(life.TryOpenDoor(),Is.False);
            yield return null;
        }
        private void Configure(string json)
        {
            var settings=Object.Instantiate(life.Config);temporary.Add(settings);JsonUtility.FromJsonOverwrite(json,settings);
            var habitat=layout.HabitatConsole.transform.parent.Find("Pressurized Habitat Volume").GetComponent<BoxCollider>();
            life.Configure(settings,session,cargo,life.SuitRack,life.DoorControl,habitat);session.RetryMission();
        }
        // 门锁维修的时间消耗保持原测试口径；修理本身由气闸抢修的捷径完成（真实操作见 AirlockRepairTests）。
        private void RepairDoor()
        {
            MoveBody(life.DoorControl.position+Vector3.left*.8f);
            session.Advance(life.Config.DoorRepairSeconds);life.AirlockRepair.SkipRepair();
            Assert.That(life.DoorRepaired,Is.True);Assert.That(life.DoorOpen,Is.False);
        }
        private void Don()
        {
            session.BeginMission();MoveBody(life.SuitRack.position+Vector3.left*.75f);
            Select(layout.SuitHandle.Physical,layout.SuitHandle.transform.position);session.Advance(life.Config.DonSeconds);Release(layout.SuitHandle.Physical);
            Assert.That(life.SuitWorn,Is.True);
        }
        private void MoveBody(Vector3 destination)
        {
            var body=session.Exit.PlayerBody;var center=body.transform.TransformPoint(body.center);body.enabled=false;
            body.transform.position+=new Vector3(destination.x-center.x,0,destination.z-center.z);body.enabled=true;Physics.SyncTransforms();
        }
        private void Select(XRBaseInteractable target,Vector3 position)
        {
            hand.transform.position=position;hand.selectInput.manualPerformed=true;hand.selectInput.manualValue=1;
            manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)target);Assert.That(target.isSelected,Is.True);
        }
        private void Release(XRBaseInteractable target)
        {
            hand.selectInput.manualPerformed=false;hand.selectInput.manualValue=0;
            if(target.isSelected)manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)target);
        }
        private CargoItem Pack(CargoKind kind)
        {
            var item=cargo.Items.First(i=>i.Kind==kind&&i.State==CargoState.World);Select(item.Grab,item.transform.position);
            var pack=Object.FindAnyObjectByType<CargoPackZone>();var point=pack.Volume.transform.TransformPoint(pack.Volume.center)-item.transform.TransformVector(item.Body.centerOfMass);
            hand.transform.position+=point-item.transform.position;item.Body.position=point;item.transform.position=point;Physics.SyncTransforms();Release(item.Grab);
            Assert.That(item.State,Is.EqualTo(CargoState.Packed));return item;
        }
        private IEnumerator Click(Button button) {yield return HoldMouse(button,3,false);}
        private IEnumerator HoldMouse(Button button,int frames,bool advance=true)
        {
            session.Player.Camera.transform.rotation=Quaternion.LookRotation(button.transform.position-session.Player.Camera.transform.position);
            yield return null;
            var mouse=InputSystem.AddDevice<Mouse>();try
            {
                Canvas.ForceUpdateCanvases();var point=RectTransformUtility.WorldToScreenPoint(session.Player.Camera,button.transform.position);
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                Assert.That(hits.Any(h=>h.gameObject==button.gameObject||h.gameObject.transform.IsChildOf(button.transform)),Is.True,"Button must be reachable: "+button.name);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));
                for(int i=0;i<frames;i++){yield return null;if(advance)flight.Tick(1f/30);}
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
            }
            finally {InputSystem.RemoveDevice(mouse);}
        }
        private IEnumerator Languages(GameObject panel,string screenshot=null)
        {
            var language=Object.FindAnyObjectByType<LocalizationService>();
            var overflows=new List<string>();
            foreach(var value in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian})
            {
                language.SetLanguage(value);yield return null;
                foreach(var label in panel.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();if(label.isTextOverflowing)overflows.Add(value+" "+label.name+" "+label.text);
                    Assert.That(label.text.Contains('{'),Is.False,label.text);Assert.That(label.text.StartsWith("life."),Is.False,label.text);
                    foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c)))Assert.That(label.font.HasCharacter(c,true),Is.True,"Missing glyph "+c);
                }
                if(screenshot!=null)
                {
                    var target=panel.transform;var eye=screenshot.Contains("suffocation")?session.Player.Camera.transform.position:target.position-target.forward*2.6f;
                    yield return Capture(screenshot+"-"+value,eye,target.position,55);
                }
            }
            language.SetLanguage(GameLanguage.Chinese);yield return null;
            Assert.That(overflows,Is.Empty,string.Join("\n",overflows));
        }
        private IEnumerator Capture(string name,Vector3 eye,Vector3 target,float fov=65,bool hideUi=false,bool includePlayerHead=false,System.Action<Color32[]> captured=null)
        {
            if(Vector3.Distance(eye,session.Player.Camera.transform.position)<.1f)
            {session.Player.Camera.transform.rotation=Quaternion.LookRotation(target-eye);for(int i=0;i<4;i++)yield return null;}
            var obj=new GameObject("Life support capture");var camera=obj.AddComponent<Camera>();camera.CopyFrom(session.Player.Camera);camera.enabled=false;
            if(hideUi)camera.cullingMask&=~(1<<5);
            if(includePlayerHead)camera.cullingMask|=1<<LayerMask.NameToLayer("Player Head");
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.fieldOfView=fov;
            var texture=new RenderTexture(1800,1200,24);var pixels=new Texture2D(1800,1200,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                texture.Create();camera.targetTexture=texture;camera.enabled=true;for(int i=0;i<8;i++)yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=texture});
                RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,1800,1200),0,0);pixels.Apply();
                captured?.Invoke(pixels.GetPixels32());
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Previews"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Object.Destroy(texture);Object.Destroy(pixels);Object.Destroy(obj);}
        }
    }
}
