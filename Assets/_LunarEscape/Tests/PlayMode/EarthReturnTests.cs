using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LunarEscape.Tests
{
    public sealed class EarthReturnTests
    {
        private StationMissionSession session;
        private AscentMission flight;
        private EarthReturnMission journey;
        private EarthReturnPresenter view;
        private DockingConfig dockingConfig;
        private DockingConfig originalDockingConfig;
        private float oldDelta;
        [UnitySetUp] public IEnumerator Load()
        {
            oldDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/30;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            foreach(var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None))simulator.gameObject.SetActive(false);
            session=Object.FindAnyObjectByType<StationMissionSession>();session.enabled=false;
            flight=session.GetComponent<AscentMission>();journey=session.GetComponent<EarthReturnMission>();view=session.GetComponent<EarthReturnPresenter>();
            originalDockingConfig=session.GetComponent<DockingMission>().Config;
            Assert.That(journey,Is.Not.Null);yield return ExpansionTestSteps.Wake(session);
        }
        [TearDown] public void Cleanup()
        {Time.captureDeltaTime=oldDelta;if(dockingConfig!=null)Object.Destroy(dockingConfig);}
        private IEnumerator Dock(bool rescue=false, bool captureApproach=false)
        {
            var docking=session.GetComponent<DockingMission>();
            dockingConfig=Object.Instantiate(originalDockingConfig);dockingConfig.ConfigureStart(new Vector3(0,0,captureApproach ? -10 : -.6f),Vector3.forward*.08f,Vector3.zero);docking.Configure(flight,dockingConfig);
            var steps=new LifeSupportSteps(session);
            try
            {
                // 保留第二天的钢梁阻挡规则，完成清障后才执行原来的登舱步骤。
                steps.Don();
                var blockade = Object.FindAnyObjectByType<BeamBlockade>();
                Assert.That(blockade, Is.Not.Null); Assert.That(blockade.Beams.Length, Is.EqualTo(3));
                Assert.That(Object.FindAnyObjectByType<LaserCutter>(), Is.Not.Null);
                blockade.ClearInstantly(); Assert.That(blockade.Cleared, Is.True);
                if(!rescue)steps.Board(flight);
                else
                {
                    steps.Don();
                    session.Mission.Tick(session.Mission.RepairTask.RemainingSeconds,true,false);session.Advance(session.Mission.Config.StabilizedSeconds);
                    yield return ExpansionTestSteps.Rescue(session);
                    var life=session.GetComponent<LifeSupportMission>();life.AirlockRepair.SkipRepair();
                    steps.MoveBody(life.DoorControl.position+Vector3.left*.8f);Assert.That(life.TryOpenDoor(),Is.True);
                    var ground=session.GetComponent<GroundCrewController>();float distance=0;
                    for(int w=1;w<ground.Waypoints.Count;w++)
                    {
                        var waypoint=ground.Waypoints[w];distance+=Vector3.Distance(ground.Waypoints[w-1].position,waypoint.position);steps.MoveBody(waypoint.position);
                        for(int i=0;i<240&&!ground.ReachedBoarding;i++)
                        {if(ground.RouteProgress>=distance-.86f&&waypoint!=ground.Waypoints.Last())break;session.Advance(.25f);}
                    }
                    Assert.That(ground.ReachedBoarding,Is.True);Object.FindAnyObjectByType<HatchBoardingController>().RequestBoarding();session.Advance(0);flight.Tick(.951f);
                    Assert.That(session.GetComponent<CrewMission>().CommanderBoarded,Is.True);
                }
            }
            finally{steps.Dispose();}
            flight.PowerOn();flight.StartNavigation();flight.Tick(flight.Config.NavigationSeconds);flight.PrepareEngine();flight.Tick(flight.Config.EngineSeconds);
            flight.Ignite();flight.Tick(flight.Config.IgnitionSeconds);flight.Tick(flight.OrbitAscentSeconds-flight.AirborneSeconds);
            flight.Circularize();flight.Tick(flight.Config.CircularizationSeconds);
            if (captureApproach) yield return CaptureDockingApproach();
            for (int i=0;i<400 && flight.Phase!=AscentPhase.Docked && flight.Phase!=AscentPhase.Failed;i++) flight.Tick(.25f);
            yield return null;
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Docked),flight.Failure.ToString());
            Assert.That(journey.Phase,Is.EqualTo(EarthReturnPhase.AwaitingTransfer));
        }
        private void Next() { Assert.That(journey.Duration,Is.GreaterThan(0));journey.Tick(journey.Duration-journey.Elapsed+.0001f); }
        private IEnumerator Click(Button button)
        {
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(),Is.True,button.name);
            var raycasters=button.GetComponentInParent<Canvas>().GetComponents<BaseRaycaster>();
            Assert.That(raycasters.All(r=>r.enabled),Is.True,"Return UI must remain on the flight interaction whitelist.");
            var camera=session.Player.Camera;var rect=button.GetComponent<RectTransform>();
            var rotation=camera.transform.rotation;camera.transform.rotation=Quaternion.LookRotation(rect.position-camera.transform.position);yield return null;
            Canvas.ForceUpdateCanvases();var point=RectTransformUtility.WorldToScreenPoint(camera,rect.position);
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button),string.Join(",",hits.Select(h=>h.gameObject.name)));
            // 使用事件系统派发实际 UI 点击，确认监听和射线遮挡均正确。
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            camera.transform.rotation=rotation;yield return null;
        }
        private void AdvanceUntil(Func<bool> condition, float step = .05f)
        {
            for (int i = 0; i < 10000 && !condition() && !journey.IsFailed && !journey.IsComplete; i++) journey.Tick(step);
            Assert.That(condition(), Is.True, journey.Phase + " " + journey.Failure);
        }
        private void EnterOrbitHold()
        {
            journey.Confirm(); Next(); Next(); journey.Confirm(); Next(); journey.Confirm();
            AdvanceUntil(() => journey.Phase == EarthReturnPhase.OrbitHold);
        }
        private void EnterEntry(float angle = 6.5f)
        {
            EnterOrbitHold();
            for (int i = 0; i < Mathf.RoundToInt((angle - 3) * 2); i++) journey.Steeper();
            journey.Confirm(); Next(); Next(); Assert.That(journey.Phase, Is.EqualTo(EarthReturnPhase.Entry));
        }
        private void AutomaticLanding(float step = .05f)
        {
            for (int i = 0; i < 10000 && !journey.IsComplete && !journey.IsFailed; i++)
            {
                if (journey.CanDeployParachutes && journey.AltitudeKm <= 3) journey.DeployParachutes();
                if (journey.CanReleaseHeatShield && journey.AltitudeKm <= .8f) journey.ReleaseHeatShield();
                journey.Tick(step);
            }
        }
        [UnityTest] public IEnumerator LatestDayTwoDockingContinuesIntoCompleteAssemblyAndSixtySecondReturn()
        {
            yield return Dock(true, true);
            var score=session.GetComponent<MissionScore>();var snapshot=score.Result;var summary=session.GetComponent<MissionScorePresenter>();
            Assert.That(summary.Panel.activeSelf,Is.False);
            var target=session.GetComponent<FlightScenePresenter>().FlightWorld.GetComponent<OrbiterView>().Target;
            var assembly=target.Find("Complete Orbital Return Assembly");
            var returnShell=view.HeatShield.parent;
            var targetShell=assembly.Find(returnShell.name);
            Assert.That(targetShell,Is.Not.Null);
            foreach(var root in new[]{assembly,view.World.transform})
            {
                Assert.That(root.Find("Orbital Transfer Module"),Is.Null,"不保留重复的灰色指令舱");
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh.name.StartsWith("Orbital ")),Is.False);
            }
            var dockingRing=targetShell.Find("Docking capture ring");
            var ringCentre=target.InverseTransformPoint(dockingRing.TransformPoint(dockingRing.GetComponent<MeshFilter>().sharedMesh.bounds.center));
            Assert.That(ringCentre.x,Is.EqualTo(0).Within(.001f));Assert.That(ringCentre.y,Is.EqualTo(0).Within(.001f));
            Assert.That(ringCentre.z,Is.InRange(0,.15f),"返回舱原有对接环贴合对接坐标原点");
            var targetService=assembly.Find("Service and Propulsion Module");
            foreach(var mesh in view.ServiceModule.GetComponentsInChildren<MeshFilter>(true))
                Assert.That(targetService.Find(mesh.name).GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh.sharedMesh),"两个视角使用同一个服务舱模型");
            yield return Click(view.TransferButton);Next();Next();
            Assert.That(journey.CommanderTransferred,Is.True);
            Assert.That(view.ServiceModule.gameObject.activeInHierarchy,Is.True);
            var service=view.ServiceModule.localPosition;var lander=view.Lander.localPosition;
            var returnRing=returnShell.Find("Docking capture ring");
            var portPosition=returnRing.position;
            yield return Capture("return-main-complete-assembly");
            yield return Click(view.ActionButton);journey.Tick(2);
            Assert.That(returnRing.position,Is.EqualTo(portPosition));Assert.That(view.ServiceModule.localPosition,Is.EqualTo(service));
            Assert.That(Vector3.Distance(lander,view.Lander.localPosition),Is.GreaterThan(5));
            yield return Capture("return-main-ascent-separation");Next();
            yield return Click(view.ActionButton);AdvanceUntil(()=>journey.Phase==EarthReturnPhase.OrbitHold);
            Assert.That(returnRing.position,Is.EqualTo(portPosition));Assert.That(view.ServiceModule.localPosition,Is.EqualTo(service));
            for(int i=0;i<7;i++)yield return Click(view.SteeperButton);
            yield return Click(view.ActionButton);Next();journey.Tick(1);
            Assert.That(returnRing.position,Is.EqualTo(portPosition));
            Assert.That(Vector3.Distance(view.ServiceModule.localPosition,service),Is.GreaterThan(2));
            yield return Capture("return-main-orbital-separation");Next();journey.Tick(8);
            yield return Capture("return-main-entry");
            AdvanceUntil(()=>journey.CanDeployParachutes);float height=journey.AltitudeKm;
            yield return Capture("return-main-altitude-window");
            yield return Click(view.DeployChutesButton);Assert.That(journey.AltitudeKm,Is.EqualTo(height));
            journey.Tick(.4f);yield return Capture("return-main-parachute-extraction");journey.Tick(1.7f);
            yield return Capture("return-main-parachutes");
            AdvanceUntil(()=>journey.CanReleaseHeatShield);height=journey.AltitudeKm;
            yield return Click(view.ReleaseShieldButton);Assert.That(journey.AltitudeKm,Is.EqualTo(height));
            journey.Tick(1);Assert.That(journey.AltitudeKm,Is.LessThan(height));yield return Capture("return-main-heat-shield");
            AutomaticLanding();yield return Capture("return-main-splashdown");
            Assert.That(journey.IsComplete,Is.True);Assert.That(journey.FlightElapsed,Is.EqualTo(60).Within(.06f));
            Assert.That(summary.Panel.activeInHierarchy,Is.True);Assert.That(score.Result,Is.SameAs(snapshot));
            yield return Click(summary.RetryButton);Assert.That(journey.Phase,Is.EqualTo(EarthReturnPhase.Inactive));
            Assert.That(Object.FindAnyObjectByType<BeamBlockade>().Cleared,Is.False,"第二天重开仍恢复钢梁");
        }
        [UnityTest] public IEnumerator EarthSurfaceAndAltitudeRemainContinuousAcrossEveryDescentAction()
        {
            yield return Dock();EnterEntry();
            float height=journey.AltitudeKm,distance=view.EarthSurfaceDistance,atmosphere=view.Atmosphere;
            var globe=view.Earth.localPosition;var radius=view.Earth.localScale;
            var monitor=session.GetComponent<EarthReturnExteriorMonitor>();yield return null;
            for(int i=0;i<2000&&!journey.IsComplete;i++)
            {
                if(journey.CanDeployParachutes && journey.AltitudeKm<3 || journey.CanReleaseHeatShield && journey.AltitudeKm<.8f)
                {
                    yield return null;
                    float before=journey.AltitudeKm;var position=monitor.TrackingCamera.transform.position;
                    if(journey.CanDeployParachutes)journey.DeployParachutes();else journey.ReleaseHeatShield();
                    yield return null;
                    Assert.That(journey.AltitudeKm,Is.EqualTo(before));Assert.That(Vector3.Distance(position,monitor.TrackingCamera.transform.position),Is.LessThan(.001f));
                }
                journey.Tick(.025f);
                Assert.That(journey.IsFailed,Is.False);Assert.That(journey.AltitudeKm,Is.LessThan(height));
                Assert.That(view.EarthSurfaceDistance,Is.LessThan(distance+.005f));Assert.That(view.Atmosphere,Is.GreaterThanOrEqualTo(atmosphere-.00001f));
                Assert.That(Vector3.Distance(globe,view.Earth.localPosition),Is.LessThan(25),"不能切换或瞬移到地面背景");
                Assert.That(view.Earth.localScale,Is.EqualTo(radius));Assert.That(view.Earth.gameObject.activeInHierarchy,Is.True);
                height=journey.AltitudeKm;distance=view.EarthSurfaceDistance;atmosphere=view.Atmosphere;globe=view.Earth.localPosition;
            }
            Assert.That(journey.IsComplete,Is.True);Assert.That(view.Ocean.localPosition.y,Is.EqualTo(.08f).Within(.001f));
        }
        [UnityTest] public IEnumerator MissingEitherAltitudeWindowKeepsFallingAndEndsInFailure()
        {
            foreach(bool missShield in new[]{false,true})
            {
                yield return Dock();EnterEntry();
                if(missShield){AdvanceUntil(()=>journey.CanDeployParachutes);journey.DeployParachutes();}
                AdvanceUntil(()=>journey.Phase==EarthReturnPhase.HardLanding);
                Assert.That(journey.Failure,Is.EqualTo(missShield?EarthReturnFailure.MissedHeatShield:EarthReturnFailure.MissedParachutes));
                float h=journey.AltitudeKm,v=journey.SpeedKmS;journey.Tick(.6f);
                Assert.That(journey.AltitudeKm,Is.LessThan(h));Assert.That(journey.SpeedKmS,Is.GreaterThan(v));
                journey.DeployParachutes();journey.ReleaseHeatShield();journey.Tick(20);
                Assert.That(journey.IsFailed,Is.True);Assert.That(journey.IsComplete,Is.False);Assert.That(journey.AltitudeKm,Is.Zero);
                var labels=session.GetComponent<MissionScorePresenter>().Panel.GetComponentsInChildren<LocalizedText>();
                Assert.That(labels.Any(l=>l.Key==journey.FailureKey),Is.True);
                session.RetryMission();yield return ExpansionTestSteps.Wake(session);
            }
        }
        [UnityTest] public IEnumerator WindowEdgesAllowActionsButOutsideWindowsDoNot()
        {
            foreach(bool lowerEdge in new[]{false,true})
            {
                yield return Dock();EnterEntry();Next();
                journey.DeployParachutes();journey.ReleaseHeatShield();Assert.That(journey.ParachutesDeployed||journey.HeatShieldReleased,Is.False);
                journey.Tick((lowerEdge?9:3)-journey.DescentElapsed);
                Assert.That(journey.CanDeployParachutes,Is.True,journey.AltitudeKm.ToString("F5"));
                journey.DeployParachutes();Assert.That(journey.ParachutesDeployed,Is.True);
                journey.ReleaseHeatShield();Assert.That(journey.HeatShieldReleased,Is.False);
                journey.Tick((lowerEdge?18:12)-journey.DescentElapsed);
                Assert.That(journey.CanReleaseHeatShield,Is.True,journey.AltitudeKm.ToString("F5"));
                journey.ReleaseHeatShield();AutomaticLanding();Assert.That(journey.IsComplete,Is.True);
                session.RetryMission();yield return ExpansionTestSteps.Wake(session);
            }
        }
        [UnityTest] public IEnumerator CanopiesAndRopesStartOutsideThePressureShellAndNeverScaleThroughIt()
        {
            yield return Dock();EnterEntry();AdvanceUntil(()=>journey.CanDeployParachutes);journey.DeployParachutes();
            var rig=view.World.GetComponentInChildren<EarthReturnParachutes>(true);Assert.That(rig,Is.Not.Null);
            foreach(float age in new[]{0f,.1f,.3f,.65f,1.2f,2f})
            {
                journey.Tick(Mathf.Max(0,age-journey.ParachuteAge));
                Assert.That(rig.transform.localScale,Is.EqualTo(Vector3.one));
                foreach(var canopy in rig.Canopies)
                    foreach(var v in canopy.GetComponent<MeshFilter>().sharedMesh.vertices)
                    {
                        var local=view.World.transform.InverseTransformPoint(canopy.TransformPoint(v));
                        Assert.That(local.y>4.1f || new Vector2(local.x,local.z).magnitude>1.55f,Is.True,"伞衣不得出现在舱内");
                    }
                foreach(var rope in rig.Lines)
                {
                    var start=rope.GetPosition(0);Assert.That(start.y,Is.EqualTo(2.8f).Within(.001f));
                    for(int i=0;i<=10;i++)
                    {var point=Vector3.Lerp(start,rope.GetPosition(1),i/10f);Assert.That(point.y>4.1f||new Vector2(point.x,point.z).magnitude>1.55f,Is.True,"伞绳不得穿壳");}
                }
            }
            for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)
                Assert.That(Vector3.Distance(rig.Canopies[i].localPosition,rig.Canopies[j].localPosition),Is.GreaterThan(6.8f));
        }
        [UnityTest] public IEnumerator ThrottleSteeringPauseAndLiveCameraRespectTheSharedClock()
        {
            yield return Dock();journey.Confirm();Next();Next();journey.Confirm();Next();
            for(int i=0;i<5;i++)yield return Click(view.ThrottleDownButton);
            journey.Confirm();journey.Tick(3);Assert.That(journey.Elapsed,Is.Zero);
            for(int i=0;i<10;i++)yield return Click(view.ThrottleUpButton);
            journey.Tick(1.5f);Assert.That(journey.Elapsed,Is.EqualTo(3).Within(.001f));AdvanceUntil(()=>journey.Phase==EarthReturnPhase.OrbitHold);
            var earth=view.Earth.localPosition;yield return Click(view.TurnRightButton);Assert.That(Vector3.Distance(earth,view.Earth.localPosition),Is.GreaterThan(10));
            var monitor=session.GetComponent<EarthReturnExteriorMonitor>();yield return null;
            Assert.That(monitor.Feed.IsCreated(),Is.True);Assert.That(monitor.UsesStereo,Is.False);Assert.That(monitor.TrackingCamera.cullingMask&(1<<5),Is.Zero);
            foreach(var module in new[]{view.HeatShield.parent,view.ServiceModule})
            {
                var bounds=module.GetComponentsInChildren<Renderer>().First().bounds;
                foreach(var renderer in module.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);
                var point=monitor.TrackingCamera.WorldToViewportPoint(bounds.center);
                Assert.That(point.z,Is.GreaterThan(0));Assert.That(point.x,Is.InRange(.1f,.9f));Assert.That(point.y,Is.InRange(.1f,.9f));
            }
            for(int i=0;i<7;i++)journey.Steeper();journey.Confirm();AdvanceUntil(()=>journey.CanDeployParachutes);
            yield return Capture("return-main-side-monitor",new Vector3(.15f,1.5f,-.9f),Quaternion.Euler(0,42,0));
            yield return Click(view.DeployChutesButton);journey.Tick(.8f);yield return null;
            var origin=session.Player.Origin.transform;var position=origin.position;var head=session.Player.Camera.transform;
            var oldHead=head.localPosition;var cameraPosition=monitor.TrackingCamera.transform.position;float height=journey.AltitudeKm;
            yield return Click(view.PauseButton);journey.Tick(20);head.localPosition+=new Vector3(.1f,.03f,0);yield return null;
            Assert.That(journey.AltitudeKm,Is.EqualTo(height));Assert.That(monitor.TrackingCamera.transform.position,Is.EqualTo(cameraPosition));
            Assert.That(origin.position,Is.EqualTo(position));Assert.That(head.localPosition,Is.EqualTo(oldHead+new Vector3(.1f,.03f,0)));
            journey.TogglePause();AutomaticLanding();Assert.That(journey.IsComplete,Is.True);
            session.RetryMission();yield return null;
            Assert.That(monitor.Feed,Is.Null);Assert.That(monitor.TrackingCamera,Is.Null);Assert.That(journey.Throttle,Is.EqualTo(.5f));Assert.That(journey.HeadingDegrees,Is.Zero);
            Assert.That(view.HeatShield.localPosition,Is.EqualTo(Vector3.zero));head.localPosition=oldHead;
        }
        [UnityTest] public IEnumerator EntryCorridorEdgesAndDifferentFrameStepsAgree()
        {
            foreach(float step in new[]{.013f,.37f})foreach(float angle in new[]{5f,5.5f,7.5f,8f})
            {
                yield return Dock();EnterEntry(angle);AutomaticLanding(step);
                bool safe=angle>=5.5f&&angle<=7.5f;
                Assert.That(journey.IsComplete,Is.EqualTo(safe),angle+" / "+step);
                Assert.That(journey.IsFailed,Is.EqualTo(!safe));
                if(safe)Assert.That(journey.FlightElapsed,Is.EqualTo(60).Within(.08f));
                else Assert.That(journey.Failure,Is.EqualTo(angle>7.5f?EarthReturnFailure.TooSteep:EarthReturnFailure.TooShallow));
                session.RetryMission();yield return ExpansionTestSteps.Wake(session);
            }
        }
        private void StepToward(EarthReturnPhase target)
        {
            if(journey.Phase==EarthReturnPhase.OrbitHold)
            {
                int clicks=target==EarthReturnPhase.Breakup?18:target==EarthReturnPhase.SkipOut?0:7;
                for(int i=0;i<clicks;i++)journey.Steeper();journey.Confirm();return;
            }
            if(!journey.IsAnimating){journey.Confirm();return;}
            if(target!=EarthReturnPhase.HardLanding && target!=EarthReturnPhase.Failed)
            {
                if(journey.CanDeployParachutes){journey.DeployParachutes();return;}
                if(journey.CanReleaseHeatShield){journey.ReleaseHeatShield();return;}
            }
            journey.Tick(.1f);
        }
        [UnityTest] public IEnumerator RetryAtEveryStageRestoresTwoDaySceneAndReturnState()
        {
            foreach(var target in Enum.GetValues(typeof(EarthReturnPhase)).Cast<EarthReturnPhase>().Where(p=>p!=EarthReturnPhase.Inactive))
            {
                yield return Dock();
                for(int i=0;i<2500&&journey.Phase!=target;i++)StepToward(target);
                Assert.That(journey.Phase,Is.EqualTo(target));session.RetryMission();yield return null;
                Assert.That(journey.Phase,Is.EqualTo(EarthReturnPhase.Inactive));Assert.That(journey.FlightElapsed,Is.Zero);Assert.That(journey.DescentElapsed,Is.Zero);
                Assert.That(journey.ParachutesDeployed||journey.HeatShieldReleased||journey.Paused,Is.False);Assert.That(journey.Failure,Is.EqualTo(EarthReturnFailure.None));
                Assert.That(view.World.activeSelf||view.Dashboard.activeSelf,Is.False);Assert.That(view.Atmosphere,Is.Zero);
                var presentation=session.GetComponent<FlightScenePresenter>();Assert.That(presentation.GroundRoot.activeSelf,Is.True);Assert.That(presentation.FadeAlpha,Is.Zero);
                Assert.That(session.GetComponent<FlightSeatLock>().IsLocked,Is.False);Assert.That(Object.FindAnyObjectByType<BeamBlockade>().Cleared,Is.False);
                Object.Destroy(dockingConfig);dockingConfig=null;yield return ExpansionTestSteps.Wake(session);
            }
        }
        [UnityTest] public IEnumerator AllThreeLanguagesFitEveryReturnStageAndOnlyOneClockAdvances()
        {
            yield return Dock();journey.Confirm();Next();journey.Tick(2);
            float elapsed=journey.Elapsed;session.enabled=true;yield return null;session.enabled=false;
            Assert.That(journey.Elapsed,Is.EqualTo(elapsed+Time.deltaTime).Within(.002f));
            journey.Tick(float.NaN);journey.Tick(float.PositiveInfinity);journey.Tick(-2);Assert.That(float.IsFinite(journey.Elapsed),Is.True);
            var language=Object.FindAnyObjectByType<LocalizationService>();var last=EarthReturnPhase.Inactive;
            for(int i=0;i<2500&&!journey.IsComplete;i++)
            {
                if(journey.Phase!=last)
                {
                    foreach(var current in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian})
                    {
                        language.SetLanguage(current);Canvas.ForceUpdateCanvases();
                        foreach(var label in view.Dashboard.GetComponentsInChildren<TMP_Text>())
                        {
                            label.ForceMeshUpdate();Assert.That(label.isTextOverflowing,Is.False,current+" "+journey.Phase+" "+label.name+": "+label.text);
                            Assert.That(label.text.Contains("return."),Is.False,label.text);
                            foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c)))Assert.That(label.font.HasCharacter(c,true),Is.True,"Missing glyph "+c);
                        }
                    }
                    last=journey.Phase;
                }
                StepToward(EarthReturnPhase.Complete);
            }
            Assert.That(journey.IsComplete,Is.True);
        }
        private IEnumerator CaptureDockingApproach()
        {
            for(int i=0;i<3;i++)yield return null;
            var optics=Object.FindAnyObjectByType<DockingCameraDisplay>();
            Assert.That(optics,Is.Not.Null);Assert.That(optics.Feed,Is.Not.Null);
            var previous=RenderTexture.active;var pixels=new Texture2D(optics.Feed.width,optics.Feed.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=optics.Feed;pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0);pixels.Apply();
                PreviewEvidence.Write("Docs/Previews/return-connected-docking-feed.png",pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;Object.Destroy(pixels);}
        }
        private IEnumerator Capture(string name,Vector3? position=null,Quaternion? rotation=null)
        {
            for(int i=0;i<3;i++)yield return null;
            var obj=new GameObject("Return evidence camera");var camera=obj.AddComponent<Camera>();camera.CopyFrom(session.Player.Camera);camera.enabled=false;
            camera.transform.SetPositionAndRotation(session.Player.Camera.transform.position,Quaternion.Euler(-22,0,0));camera.fieldOfView=76;camera.farClipPlane=150000;
            if(position.HasValue)camera.transform.SetPositionAndRotation(view.World.transform.TransformPoint(position.Value),rotation.Value);
            if(name=="return-three-seats")camera.cullingMask |= 1<<LayerMask.NameToLayer("Player Head");
            var rt=new RenderTexture(1600,1100,24);var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                rt.Create();camera.targetTexture=rt;camera.enabled=true;for(int i=0;i<3;i++)yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,1100),0,0);pixels.Apply();
                Directory.CreateDirectory("Docs/Previews");PreviewEvidence.Write("Docs/Previews/"+name+".png",pixels.EncodeToPNG());
                var monitor=session.GetComponent<EarthReturnExteriorMonitor>();
                if(monitor.Feed!=null)
                {
                    var frame=new Texture2D(768,432,TextureFormat.RGB24,false);RenderTexture.active=monitor.Feed;frame.ReadPixels(new Rect(0,0,768,432),0,0);frame.Apply();
                    PreviewEvidence.Write("Docs/Previews/"+name+"-feed.png",frame.EncodeToPNG());Object.Destroy(frame);
                }
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(pixels);Object.Destroy(rt);Object.Destroy(obj);}
        }
    }
}
