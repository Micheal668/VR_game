using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.UI;

namespace LunarEscape.Tests
{
    public sealed class StationExpansionTests
    {
        private StationMissionSession session;
        private LifeSupportMission life;
        private StationExpansionMission story;
        private XRInteractionManager manager;
        private PilotTestHand hand;
        private float oldDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            oldDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/30;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            foreach(var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None))simulator.gameObject.SetActive(false);
            session=Object.FindAnyObjectByType<StationMissionSession>();session.enabled=false;
            life=session.GetComponent<LifeSupportMission>();story=session.GetComponent<StationExpansionMission>();
            manager=Object.FindAnyObjectByType<XRInteractionManager>();
            hand=new GameObject("Expansion test hand").AddComponent<PilotTestHand>();hand.interactionLayers=-1;
            Assert.That(story,Is.Not.Null);
            for(int i=0;i<160&&!story.IntroComplete;i++)yield return null;
            Assert.That(story.IntroComplete,Is.True,"Alarm and eye-opening sequence must restore player control.");
            Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Repair));
        }
        [TearDown] public void Cleanup(){Time.captureDeltaTime=oldDelta;if(hand!=null)Object.Destroy(hand.gameObject);}

        [UnityTest] public IEnumerator OpeningSequenceAndSharedPowerCircuitUseRealSockets()
        {
            Assert.That(story.Bedroom.bounds.Contains(life.Player.transform.TransformPoint(life.Player.center)),Is.True);
            Assert.That(life.BaseOxygen,Is.InRange(50,80));Assert.That(life.BasePower,Is.InRange(50,80));
            var breaker=Object.FindAnyObjectByType<HabitatBreaker>();
            breaker.SwitchOn();Assert.That(breaker.IsOn,Is.False,"The breaker cannot bypass unconnected circuits.");
            Move(new Vector3(-7.5f,0,1.7f));session.Advance(1.99f);Assert.That(story.BedroomLocked,Is.False);
            session.Advance(.02f);Assert.That(story.BedroomLocked,Is.True);
            Assert.That(session.GetComponent<CrewMission>().CommanderState,Is.EqualTo(CrewState.Trapped));
            Assert.That(story.TryUnlockBedroom(),Is.False);
            var circuit=story.Circuit;
            var wrong=circuit.Sockets.First(s=>s.Identity!=0);
            float power=life.BasePower,time=session.Mission.RemainingSeconds,air=life.BaseOxygen;
            yield return Insert(circuit.Modules[0],wrong);
            Assert.That(wrong.Socket.hasSelection,Is.True);Assert.That(wrong.Correct,Is.False);
            Assert.That(life.BasePower,Is.EqualTo(power-5).Within(.001f));
            Assert.That(session.Mission.RemainingSeconds,Is.EqualTo(time).Within(.001f),"A wiring mistake must not deduct mission time.");
            Assert.That(life.BaseOxygen,Is.EqualTo(air).Within(.001f));
            yield return Insert(circuit.Modules[0],circuit.Sockets.Single(s=>s.Identity==0));
            foreach(var module in circuit.Modules.Where(m=>m.Identity!=0))yield return Insert(module,circuit.Sockets.Single(s=>s.Identity==module.Identity));
            Assert.That(circuit.IsSolved,Is.True);
            yield return PullBreaker(breaker);
            Assert.That(breaker.IsOn,Is.True);
            session.RetryMission();yield return null;
            Assert.That(story.Circuit.IsSolved,Is.False);Assert.That(breaker.IsOn,Is.False);
        }

        [UnityTest] public IEnumerator IndependentCrewRescueDressingAndFullGroundRoute()
        {
            Move(new Vector3(-7.5f,0,1.7f));session.Advance(2.1f);
            var wardrobe=session.GetComponent<CrewWardrobe>();var crew=session.GetComponent<CrewMission>();
            yield return Don();
            Assert.That(life.SuitWorn,Is.True);Assert.That(story.CommanderSuited,Is.False);
            Assert.That(wardrobe.HangingSuit.activeSelf,Is.False);
            Assert.That(wardrobe.CommanderHangingSuit.activeSelf,Is.True);
            Assert.That(wardrobe.SuitedRenderers.Where(r=>r.transform.IsChildOf(crew.Commander)).All(r=>!r.enabled),Is.True);
            foreach(var module in story.Circuit.Modules)yield return Insert(module,story.Circuit.Sockets.Single(s=>s.Identity==module.Identity));
            var breaker=Object.FindAnyObjectByType<HabitatBreaker>();yield return PullBreaker(breaker);
            foreach(var module in story.Laboratory.Modules)yield return Insert(module,story.Laboratory.Sockets.Single(s=>s.Identity==module.Identity));
            Assert.That(story.Laboratory.IsSolved,Is.True);
            Assert.That(story.TryUnlockBedroom(),Is.False,"Module matching alone cannot remotely open the door.");
            Move(story.ControlPoint.position+Vector3.right*.9f);
            var button=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Unlock Bedroom Door");
            var ray=new GameObject("Rescue control ray").AddComponent<XRRayInteractor>();ray.interactionManager=manager;ray.enableUIInteraction=true;
            ray.uiPressInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            ray.transform.SetPositionAndRotation(button.transform.position-button.transform.forward,Quaternion.LookRotation(button.transform.forward));
            for(int i=0;i<5;i++)yield return null;
            Assert.That(ray.TryGetCurrentUIRaycastResult(out var uiHit),Is.True);
            Assert.That(uiHit.gameObject==button.gameObject||uiHit.gameObject.transform.IsChildOf(button.transform),Is.True,uiHit.gameObject.name);
            ray.uiPressInput.manualValue=1;ray.uiPressInput.manualPerformed=true;for(int i=0;i<3;i++)yield return null;
            ray.uiPressInput.manualValue=0;ray.uiPressInput.manualPerformed=false;for(int i=0;i<3;i++)yield return null;
            Object.Destroy(ray.gameObject);Assert.That(story.DoorUnlocked,Is.True,"Controller ray must activate the actual rescue button.");
            Assert.That(story.CommanderSuited,Is.False);Assert.That(crew.CommanderRescued,Is.True);
            session.Advance(25);yield return null;
            Assert.That(story.CommanderSuited,Is.True);Assert.That(crew.CommanderState,Is.EqualTo(CrewState.Following));
            Assert.That(wardrobe.CommanderHangingSuit.activeSelf,Is.False);
            Assert.That(Vector3.Distance(crew.Commander.position,new Vector3(2.55f,0,-.48f)),Is.LessThan(.2f));
            Assert.That(wardrobe.SuitedRenderers.Where(r=>r.transform.IsChildOf(crew.Commander)).All(r=>r.enabled),Is.True);
            life.AirlockRepair.SkipRepair();Move(life.DoorControl.position+Vector3.left*.8f);Assert.That(life.TryOpenDoor(),Is.True);
            var following=session.GetComponent<GroundCrewController>();
            foreach(var point in following.Waypoints.Skip(1)){Move(point.position);session.Advance(12);}
            Assert.That(following.ReachedBoarding,Is.True);
            session.RetryMission();yield return ExpansionTestSteps.Wake(session);
            Assert.That(life.SuitWorn,Is.False);Assert.That(story.CommanderSuited,Is.False);
            Assert.That(wardrobe.HangingSuit.activeSelf,Is.True);
            Assert.That(wardrobe.CommanderHangingSuit.activeSelf,Is.True);
        }

        [UnityTest] public IEnumerator OxygenRepairReallySlowsDrainAndValveOverturnReallyLeaks()
        {
            float before=life.BaseOxygenRate;
            var tool=Object.FindAnyObjectByType<RepairTool>();var grab=tool.GetComponent<XRGrabInteractable>();
            Move(new Vector3(-13.1f,0,1.1f));
            yield return Grab(grab);
            var contact=session.Mission.RepairTask.GetComponent<RepairContact>();
            hand.transform.position+=contact.RepairPoint.position-tool.Tip.position;
            for(int i=0;i<5;i++)yield return null;
            Assert.That(contact.HasValidContact(),Is.True);session.Advance(4.05f);
            yield return Release(grab);
            Assert.That(session.Mission.RepairRestored,Is.True);Assert.That(life.BaseOxygenRate,Is.EqualTo(before*.5f).Within(.0001f));
            Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Repair),"Repairing oxygen must not prematurely start evacuation.");
            var driver=new AirlockTestDriver(life.AirlockRepair);
            var valve=driver.Valve;float target=(valve.GreenZone.x+valve.GreenZone.y)*.5f;
            yield return driver.TurnValve(target*valve.TurnDirection);Assert.That(valve.IsFixed,Is.True);
            yield return driver.TurnValve(160*valve.TurnDirection);Assert.That(valve.OverPressure,Is.True);
            float oxygen=life.BaseOxygen;session.Advance(2);Assert.That(oxygen-life.BaseOxygen,Is.GreaterThan(5));
            driver.Dispose();
        }

        [UnityTest] public IEnumerator BedroomDoorBlocksWalkingAndAssistedBoltsAcceptHeldToolContact()
        {
            Move(new Vector3(-7.5f,0,1.7f));session.Advance(2.1f);
            for(int i=0;i<20;i++)yield return null;
            life.Player.Move(Vector3.forward*2f);Physics.SyncTransforms();
            Assert.That(life.Player.transform.TransformPoint(life.Player.center).z,Is.LessThan(2.7f),"Closed bedroom door has a physical barrier.");
            var wrench=Object.FindAnyObjectByType<RepairTool>();var grab=wrench.GetComponent<XRGrabInteractable>();
            var bolts=Object.FindObjectsByType<LatchBolt>(FindObjectsSortMode.None);
            Move(life.DoorControl.position+Vector3.left*.8f);yield return Grab(grab);
            foreach(var bolt in bolts)
            {
                hand.transform.position+=bolt.transform.position-wrench.Tip.position;
                for(int i=0;i<50&&!bolt.Released;i++)yield return null;
                Assert.That(bolt.Released,Is.True,"Held tool contact must release "+bolt.name);
            }
            yield return Release(grab);
        }

        [UnityTest] public IEnumerator RetriesRandomizeValveAndBasePowerAlsoSuppliesEmergencyLights()
        {
            var valve=life.AirlockRepair.Faults.OfType<ValveFault>().Single();
            var combinations=new HashSet<string>();
            for(int attempt=0;attempt<16;attempt++)
            {
                session.RetryMission();yield return null;
                combinations.Add(valve.TurnDirection+":"+valve.GreenZone.x);
                Assert.That(valve.TurnDirection==1||valve.TurnDirection==-1,Is.True);
                Assert.That(life.BasePower,Is.InRange(50,80));Assert.That(life.BaseOxygen,Is.InRange(50,80));
            }
            Assert.That(combinations.Count,Is.GreaterThan(2));
            yield return ExpansionTestSteps.Wake(session);
            life.DrainBasePower(100);yield return null;
            var redLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.name.Contains("Emergency")).ToArray();
            Assert.That(redLights,Is.Not.Empty);Assert.That(redLights.All(l=>!l.enabled),Is.True,"No separate emergency battery survives an empty base battery.");
        }

        [UnityTest] public IEnumerator DoorBoltsChooseThreeUniquePositionsFromEightAndRetryClearsRepair()
        {
            var latch=life.AirlockRepair.Faults.OfType<LatchFault>().Single();
            var positions=latch.BoltPositions;
            Assert.That(positions.Length,Is.EqualTo(8),"The door must expose eight candidate bolt positions.");
            Assert.That(positions.All(p=>p!=null),Is.True);
            Assert.That(positions.Distinct().Count(),Is.EqualTo(8));
            for(int i=0;i<positions.Length;i++)
                for(int j=i+1;j<positions.Length;j++)
                    Assert.That(Vector3.Distance(positions[i].position,positions[j].position),Is.GreaterThan(.02f),"Candidate positions must be physically distinct.");
            Assert.That(latch.Bolts.Length,Is.EqualTo(3),"Only the three selected bolts require repair.");
            var combinations=new HashSet<string>();
            for(int attempt=0;attempt<16;attempt++)
            {
                if(attempt>0){session.RetryMission();yield return null;}
                var occupied=new HashSet<int>();
                foreach(var bolt in latch.Bolts)
                {
                    var matches=Enumerable.Range(0,positions.Length).Where(i=>Vector3.Distance(bolt.transform.position,positions[i].position)<.001f).ToArray();
                    Assert.That(matches.Length,Is.EqualTo(1),"Each bolt must occupy one of the eight door positions.");
                    Assert.That(occupied.Add(matches[0]),Is.True,"Selected bolts must occupy different positions.");
                    Assert.That(bolt.gameObject.activeInHierarchy,Is.True);
                    Assert.That(bolt.Released,Is.False,"Retry must restore repaired bolts.");
                    Assert.That(bolt.Engaged,Is.False);
                    Assert.That(bolt.Turned,Is.Zero);
                }
                Assert.That(latch.ReleasedCount,Is.Zero);
                Assert.That(latch.IsFixed,Is.False);
                combinations.Add(string.Join(",",occupied.OrderBy(i=>i)));
                if(attempt!=0)continue;
                var wrench=Object.FindAnyObjectByType<RepairTool>();var grab=wrench.GetComponent<XRGrabInteractable>();
                Move(life.DoorControl.position+Vector3.left*.8f);yield return Grab(grab);
                int repaired=0;
                foreach(var bolt in latch.Bolts)
                {
                    hand.transform.position+=bolt.transform.position-wrench.Tip.position;
                    for(int i=0;i<50&&!bolt.Released;i++)yield return null;
                    Assert.That(bolt.Released,Is.True,"Held tool contact must repair the randomly positioned "+bolt.name);
                    Assert.That(latch.ReleasedCount,Is.EqualTo(++repaired));
                    Assert.That(latch.IsFixed,Is.EqualTo(repaired==3),"All three selected bolts, and only those three, complete the latch repair.");
                }
                yield return Release(grab);
            }
            Assert.That(combinations.Count,Is.GreaterThan(1),"Retry must choose new bolt positions instead of retaining a fixed layout.");
        }

        [UnityTest] public IEnumerator UnsuitedOpeningShowsBreathAndFailsWithinFiveSeconds()
        {
            life.AirlockRepair.SkipRepair();Move(life.DoorControl.position+Vector3.left*.8f);
            Assert.That(life.TryOpenDoor(),Is.True);float before=life.BaseOxygen;
            session.Advance(.25f);Assert.That(life.BaseOxygen,Is.LessThan(before));
            var effect=session.GetComponent<HypoxiaPresentation>();yield return null;
            Assert.That(effect.Breathing.isPlaying,Is.True);Assert.That(effect.Pulse,Is.GreaterThan(0));
            var head=session.Player.Camera.transform;head.rotation=Quaternion.LookRotation(new Vector3(0,head.position.y,0)-head.position);
            yield return null;
            yield return Capture("expansion-hypoxia",head.position,head.position+head.forward);
            session.Advance(4.74f);Assert.That(session.Mission.IsTerminal,Is.False);
            session.Advance(.02f);Assert.That(session.Mission.FailureReason,Is.EqualTo(StationMissionFailure.Suffocation));
            Assert.That(life.BaseOxygen,Is.Zero);
        }

        [UnityTest] public IEnumerator SuppliesRecoveryRoomsAndVisualEvidence()
        {
            var supplies=session.GetComponent<StationRandomSupplies>();
            Assert.That(supplies.Groups.Length,Is.EqualTo(7));
            foreach(var group in supplies.Groups)Assert.That(group.items.Count(i=>i.gameObject.activeSelf),Is.InRange(1,3),group.name);
            var tool=Object.FindAnyObjectByType<RepairTool>();
            Assert.That(tool.transform.position.z,Is.LessThan(-2.8f),"Wrench belongs on the lobby wall desk.");
            Assert.That(tool.transform.position.x,Is.InRange(-1.95f,-.3f));
            tool.GetComponent<Rigidbody>().position=new Vector3(-1, -.3f, -3);
            yield return new WaitForFixedUpdate();yield return null;
            Assert.That(tool.transform.position.y,Is.GreaterThan(.9f));
            foreach(var p in new[]{new Vector3(-7.5f,0,1.7f),new Vector3(-7.5f,0,4f),new Vector3(-7.5f,0,-.5f),new Vector3(-12.2f,0,1.7f)})
            { Move(p);Assert.That(life.IsInsideHabitat,Is.True,p.ToString());Assert.That(Physics.Raycast(p+Vector3.up*.6f,Vector3.down,out var hit,1,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),Is.True);Assert.That(hit.point.y,Is.EqualTo(0).Within(.06f)); }
            yield return Capture("expansion-emergency-lobby",new Vector3(1.8f,1.7f,-2.8f),new Vector3(-1.4f,1.35f,1.7f));
            foreach(var module in story.Circuit.Modules)yield return Insert(module,story.Circuit.Sockets.Single(s=>s.Identity==module.Identity));
            yield return PullBreaker(Object.FindAnyObjectByType<HabitatBreaker>());
            for(int i=0;i<60;i++)yield return null;
            Move(new Vector3(2.5f,0,-2.8f));
            yield return Capture("expansion-lobby",new Vector3(2.5f,1.8f,-2.8f),new Vector3(-1.5f,1.3f,1.7f));
            yield return Capture("expansion-bedroom",new Vector3(-7.5f,1.7f,3.25f),new Vector3(-7.6f,1.1f,6.5f));
            yield return Capture("expansion-laboratory",new Vector3(-7.5f,1.8f,.15f),new Vector3(-7.8f,1.25f,-2.6f));
            yield return Capture("expansion-engineering",new Vector3(-11.7f,1.8f,1.8f),new Vector3(-14.1f,1.3f,.6f));
            yield return Capture("expansion-circuit-panel",new Vector3(-13.2f,1.65f,2.3f),new Vector3(-13.2f,1.55f,4.2f));
            var labels=Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.transform.IsChildOf(story.Circuit.transform)||t.transform.IsChildOf(story.Laboratory.transform)).ToArray();
            foreach(GameLanguage language in System.Enum.GetValues(typeof(GameLanguage)))
            {
                Object.FindAnyObjectByType<LocalizationService>().SetLanguage(language);yield return null;
                foreach(var label in labels){label.ForceMeshUpdate();Assert.That(label.isTextOverflowing,Is.False,label.name+" "+language+" "+label.text);}
            }
        }

        [UnityTest] public IEnumerator LabModulesFallOntoDeskAndRetryRestoresAllThreeAfterSocketOrHandUse()
        {
            var homes=story.Laboratory.Modules.ToDictionary(m=>m,m=>m.transform.position);
            var module=story.Laboratory.Modules[0];var grab=module.GetComponent<XRGrabInteractable>();
            Assert.That(module.GetComponent<Rigidbody>().useGravity,Is.True);
            yield return Grab(grab);hand.transform.position+=Vector3.up*.45f;yield return null;
            yield return Release(grab);
            for(int i=0;i<45;i++)yield return new WaitForFixedUpdate();
            Assert.That(module.transform.position.y,Is.InRange(1.28f,1.34f),"Released lab modules must rest on the worktop.");
            for(int attempt=0;attempt<3;attempt++)
            {
                yield return ExpansionTestSteps.Power(session);
                foreach(var item in story.Laboratory.Modules)yield return Insert(item,story.Laboratory.Sockets.Single(s=>s.Identity==item.Identity));
                Assert.That(story.Laboratory.IsSolved,Is.True);
                if(attempt==1) { yield return Grab(grab);hand.transform.position+=Vector3.left;yield return null; }
                session.RetryMission();yield return ExpansionTestSteps.Wake(session);
                Assert.That(story.Laboratory.IsSolved,Is.False);Assert.That(story.Circuit.IsSolved,Is.False);
                Assert.That(story.Laboratory.Sockets.All(s=>!s.Socket.hasSelection),Is.True);
                Assert.That(hand.hasSelection,Is.False);
                foreach(var item in story.Laboratory.Modules)
                {
                    Assert.That(item.gameObject.activeInHierarchy,Is.True);
                    Assert.That(Vector3.Distance(item.transform.position,homes[item]),Is.LessThan(.06f),"Returned module "+item.Identity);
                }
            }
            yield return ExpansionTestSteps.Power(session);for(int i=0;i<70;i++)yield return null;
            Move(new Vector3(-7.5f,0,1.7f));
            yield return Capture("expansion-fixed-lab-reset",new Vector3(-6.5f,1.75f,-.45f),new Vector3(-5.65f,1.60f,-1.82f));
        }

        [UnityTest] public IEnumerator BaseDoorRemainsVisibleAndSolidUntilOpenedAndControlIsBesideBedroom()
        {
            var environment=session.GetComponent<MissionEnvironment>();var door=environment.Door;
            Assert.That(life.DoorOpen,Is.False);Assert.That(door.activeInHierarchy,Is.True);
            Assert.That(door.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.name.Contains("PressureDoor")),Is.True);
            Assert.That(Physics.Raycast(new Vector3(3,1.25f,1.7f),Vector3.right,out var hit,1.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(hit.collider.transform==door.transform||hit.collider.transform.IsChildOf(door.transform),Is.True,hit.collider.name);
            Assert.That(story.ControlPoint.position.x,Is.InRange(-10,-8.9f));
            Assert.That(story.ControlPoint.position.z,Is.EqualTo(2.785f).Within(.01f));
            yield return ExpansionTestSteps.Power(session);session.Mission.Tick(4.1f,true,false);
            Assert.That(life.DoorOpen,Is.False);Assert.That(door.activeInHierarchy,Is.True);
            Assert.That(life.TryOpenDoor(),Is.False,"Power and oxygen repair cannot open an unrepaired airlock.");
            for(int i=0;i<70;i++)yield return null;
            yield return Capture("expansion-fixed-base-door",new Vector3(1.8f,1.65f,1.7f),new Vector3(4,1.3f,1.7f));
            yield return Capture("expansion-fixed-bedroom-control",new Vector3(-9f,1.65f,1.05f),new Vector3(-8.8f,1.6f,2.85f));
            life.AirlockRepair.SkipRepair();Move(life.DoorControl.position+Vector3.left*.8f);
            Assert.That(life.TryOpenDoor(),Is.True);yield return null;
            Assert.That(door.activeInHierarchy,Is.False,"Opening removes both the pressure door skin and its barrier.");
            Assert.That(Physics.Raycast(new Vector3(3,1.25f,1.7f),Vector3.right,1.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),Is.False);
            session.RetryMission();yield return ExpansionTestSteps.Wake(session);
            Assert.That(life.DoorOpen,Is.False);Assert.That(door.activeInHierarchy,Is.True);
            Assert.That(door.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.name.Contains("PressureDoor")),Is.True);
        }

        private void Move(Vector3 p)
        {
            var body=life.Player;var center=body.transform.TransformPoint(body.center);body.enabled=false;
            body.transform.position+=new Vector3(p.x-center.x,0,p.z-center.z);body.enabled=true;Physics.SyncTransforms();
        }
        private IEnumerator Grab(XRGrabInteractable grab)
        {
            grab.movementType=XRBaseInteractable.MovementType.Instantaneous;
            hand.transform.SetPositionAndRotation(grab.transform.position,grab.transform.rotation);yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)grab);yield return null;yield return null;
            Assert.That(hand.IsSelecting(grab),Is.True,grab.name);
        }
        private IEnumerator Release(XRGrabInteractable grab)
        {if(hand.IsSelecting(grab))manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)grab);yield return null;yield return null;}
        private IEnumerator Insert(StationPatchModule module,StationPatchSocket socket)
        {
            var grab=module.GetComponent<XRGrabInteractable>();yield return Grab(grab);
            hand.transform.position+=socket.Socket.attachTransform.position-module.transform.position;
            for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
            yield return Release(grab);
            for(int i=0;i<45&&!socket.Socket.IsSelecting(grab);i++)yield return new WaitForFixedUpdate();
            Assert.That(socket.Socket.IsSelecting(grab),Is.True,"Physical insertion: "+module.name+" into "+socket.name);
        }
        private IEnumerator PullBreaker(HabitatBreaker breaker)
        {
            var lever=breaker.Lever;hand.transform.position=lever.transform.TransformPoint(new Vector3(0,.3f,0));yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)lever);yield return null;
            for(float angle=0;angle<=110;angle+=5){float r=angle*Mathf.Deg2Rad;hand.transform.position=lever.transform.TransformPoint(new Vector3(0,Mathf.Cos(r)*.3f,Mathf.Sin(r)*.3f));yield return null;}
            manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)lever);yield return null;
        }
        private IEnumerator Don()
        {
            var handle=session.GetComponent<LifeSupportSceneLayout>().SuitHandle.Physical;
            Move(life.SuitRack.position+Vector3.left*.8f);hand.transform.position=handle.transform.position;yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)handle);yield return null;
            session.Advance(life.Config.DonSeconds);manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)handle);yield return null;
        }
        private IEnumerator Capture(string name,Vector3 eye,Vector3 target)
        {
            var obj=new GameObject("Expansion capture");var camera=obj.AddComponent<Camera>();camera.CopyFrom(session.Player.Camera);camera.enabled=false;
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.fieldOfView=75;
            if(name!="expansion-hypoxia")camera.cullingMask|=1<<LayerMask.NameToLayer("Player Head");
            var render=new RenderTexture(1600,1000,24);var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                render.Create();camera.targetTexture=render;camera.enabled=true;for(int i=0;i<5;i++)yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=render});RenderTexture.active=render;
                texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();Directory.CreateDirectory("Docs/Previews");PreviewEvidence.Write("Docs/Previews/"+name+".png",texture.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;render.Release();Object.Destroy(render);Object.Destroy(texture);Object.Destroy(obj);}
        }
    }
}
