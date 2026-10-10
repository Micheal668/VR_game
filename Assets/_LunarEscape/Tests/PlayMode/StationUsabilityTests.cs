using System.Collections;
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

namespace LunarEscape.Tests
{
    public sealed class StationUsabilityTests
    {
        private StationMissionSession session;
        private XRInteractionManager manager;
        private PilotTestHand hand;
        private float oldDelta;
        private DockingConfig settings;
        [UnitySetUp] public IEnumerator Load()
        {
            oldDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/30;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            foreach(var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None))simulator.gameObject.SetActive(false);
            session=Object.FindAnyObjectByType<StationMissionSession>();session.enabled=false;
            manager=Object.FindAnyObjectByType<XRInteractionManager>();
            hand=new GameObject("Usability test hand").AddComponent<PilotTestHand>();hand.interactionLayers=-1;
            yield return ExpansionTestSteps.Wake(session);
        }
        [TearDown] public void Cleanup()
        {Time.captureDeltaTime=oldDelta;if(hand!=null)Object.Destroy(hand.gameObject);if(settings!=null)Object.Destroy(settings);}
        private IEnumerator Grab(XRGrabInteractable grab,Quaternion rotation)
        {
            grab.movementType=XRBaseInteractable.MovementType.Instantaneous;
            grab.transform.rotation=rotation;grab.GetComponent<Rigidbody>().rotation=rotation;
            hand.transform.SetPositionAndRotation(grab.transform.position,rotation);yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)grab);yield return null;yield return null;
            Assert.That(hand.IsSelecting(grab),Is.True);
        }
        private IEnumerator MoveTip(Transform tip,Vector3 target)
        {hand.transform.position+=target-tip.position;for(int i=0;i<6;i++)yield return null;Physics.SyncTransforms();}

        [UnityTest] public IEnumerator BothWrenchEndsReleaseBoltsWithoutPenetratingDoor()
        {
            yield return ExpansionTestSteps.Power(session);for(int frame=0;frame<65;frame++)yield return null;
            var tool=Object.FindAnyObjectByType<RepairTool>();Assert.That(tool.SecondaryTip,Is.Not.Null);
            var door=session.GetComponent<MissionEnvironment>().Door.GetComponent<BoxCollider>();
            var bolts=Object.FindObjectsByType<LatchBolt>(FindObjectsSortMode.None).Take(2).ToArray();
            ExpansionTestSteps.Move(session,new Vector3(2.9f,0,1.7f));
            yield return Grab(tool.Grab,Quaternion.LookRotation(Vector3.up,Vector3.left));
            for(int i=0;i<2;i++)
            {
                var bolt=bolts[i];var end=i==0?tool.Tip:tool.SecondaryTip;
                yield return MoveTip(end,bolt.ContactPoint+bolt.transform.forward*.02f);
                foreach(var collider in tool.Grab.colliders)
                {
                    bool overlaps=Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,
                        door,door.transform.position,door.transform.rotation,out _,out _);
                    Assert.That(overlaps,Is.False,"The wrench must stay in front of the solid pressure door.");
                }
                Assert.That(bolt.Engaged,Is.True);
                for(int frame=0;frame<50&&!bolt.Released;frame++)yield return null;
                Assert.That(bolt.Released,Is.True,"Either working end should repair from the outside face.");
            }
            yield return Capture("usability-wrench-surface",bolts[1].transform.position+new Vector3(-.85f,.25f,-.3f),bolts[1].transform.position);
        }

        [UnityTest] public IEnumerator BottleSurfaceAtMouthShowsReadyAndPacksOnReleaseWithoutChasingHead()
        {
            yield return ExpansionTestSteps.Power(session);for(int frame=0;frame<65;frame++)yield return null;
            var pack=Object.FindAnyObjectByType<CargoPackZone>();var cargo=session.GetComponent<CargoInventory>();
            var bottle=cargo.Items.First(i=>i.Kind==CargoKind.Oxygen);
            ExpansionTestSteps.Move(session,new Vector3(-2.4f,0,.3f));yield return null;
            yield return Grab(bottle.Grab,Quaternion.identity);
            var bounds=bottle.Grab.colliders[0].bounds;
            var target=pack.Volume.transform.TransformPoint(pack.Volume.center+new Vector3(.30f,pack.Volume.size.y*.5f+bounds.extents.y+.05f,0));
            yield return MoveTip(bottle.transform,target);
            Assert.That(pack.Contains(bottle),Is.False,"The bottle centre deliberately stays above the storage box.");
            Assert.That(pack.CanStow(bottle),Is.True);Assert.That(pack.ReadyItem,Is.SameAs(bottle));
            Assert.That(pack.GetComponentsInChildren<LocalizedText>().Single(t=>t.name=="Pouch Label").Key,Is.EqualTo("cargo.pack.ready"));
            var heading=pack.transform.rotation;var eye=session.Player.Camera.transform;var old=eye.rotation;
            eye.rotation=Quaternion.Euler(25,60,0);for(int i=0;i<5;i++)yield return null;
            Assert.That(Quaternion.Angle(heading,pack.transform.rotation),Is.LessThan(.01f));eye.rotation=old;
            yield return Capture("usability-pack-ready",session.Player.Camera.transform.position,pack.transform.position+Vector3.up*.12f);
            manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)bottle.Grab);yield return null;
            Assert.That(bottle.State,Is.EqualTo(CargoState.Packed));Assert.That(bottle.gameObject.activeSelf,Is.False);
            var battery=cargo.Items.First(i=>i.Kind==CargoKind.Battery);
            yield return Grab(battery.Grab,Quaternion.identity);yield return MoveTip(battery.transform,pack.transform.position+Vector3.right*1.3f);
            Assert.That(pack.CanStow(battery),Is.False);manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)battery.Grab);
            for(int i=0;i<5;i++)yield return null;Assert.That(battery.State,Is.EqualTo(CargoState.World));
            yield return Grab(battery.Grab,Quaternion.identity);yield return MoveTip(battery.transform,pack.transform.position);
            manager.CancelInteractableSelection((IXRSelectInteractable)battery.Grab);yield return null;
            Assert.That(battery.State,Is.EqualTo(CargoState.World),"A cancelled selection must never auto-store cargo.");
        }

        private void OrbitAt(Vector3 position,Vector3 velocity,Vector3 euler)
        {
            var flight=session.GetComponent<AscentMission>();var docking=session.GetComponent<DockingMission>();
            settings=Object.Instantiate(docking.Config);settings.ConfigureStart(position,velocity,euler);docking.Configure(flight,settings);
            var steps=new LifeSupportSteps(session);try{steps.Board(flight);}finally{steps.Dispose();}
            flight.PowerOn();flight.StartNavigation();flight.Tick(flight.Config.NavigationSeconds);
            flight.PrepareEngine();flight.Tick(flight.Config.EngineSeconds);flight.Ignite();flight.Tick(flight.Config.IgnitionSeconds);
            flight.Tick(flight.OrbitAscentSeconds-flight.AirborneSeconds);flight.Circularize();flight.Tick(flight.Config.CircularizationSeconds);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Rendezvous));
        }
        private IEnumerator FinishAssisted()
        {
            var flight=session.GetComponent<AscentMission>();var docking=session.GetComponent<DockingMission>();
            for(int i=0;i<1800&&!flight.IsTerminal;i++){flight.Tick(.05f);if(i%60==0)yield return null;}
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Docked),flight.Failure+" "+docking.Position+" "+docking.Velocity+" "+docking.AlignmentError);
            Assert.That(docking.GuidanceKey,Is.EqualTo("life.dock.complete"));Assert.That(docking.CaptureProgress,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator AssistanceAlignsFromOffsetAndShowsPortAndActiveGuidance()
        {
            OrbitAt(new Vector3(1.2f,.5f,-8),new Vector3(0,0,.35f),new Vector3(8,15,0));
            var flight=session.GetComponent<AscentMission>();var docking=session.GetComponent<DockingMission>();
            Assert.That(docking.Config.StagedAssistance,Is.True);Assert.That(docking.CanAssist,Is.True);
            docking.SetCommand(DockCommand.Brake,true);Assert.That(docking.GuidanceKey,Is.EqualTo("life.dock.release"));docking.ReleaseControls();
            flight.Tick(.1f);Assert.That(docking.AssistanceActive,Is.True);Assert.That(docking.GuidanceKey,Is.EqualTo("life.dock.aligning"));
            for(int i=0;i<8;i++)yield return null;
            var guide=Object.FindAnyObjectByType<DockingApproachGuide>();Assert.That(guide.Ring.enabled,Is.True);
            Assert.That(guide.Ring.startColor.g,Is.GreaterThan(guide.Ring.startColor.r));
            var localization=Object.FindAnyObjectByType<LocalizationService>();
            var guidance=Object.FindObjectsByType<LocalizedText>(FindObjectsSortMode.None).Single(t=>t.name=="Optical Guidance");
            foreach(var language in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian})
            {
                localization.SetLanguage(language);
                foreach(string key in new[]{"complete","capturing_progress","assist_disabled","front","range","offset","angle","brake","release","aligning","approaching"})
                {
                    guidance.SetKey("life.dock."+key,50);var label=guidance.GetComponent<TMP_Text>();label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing,Is.False,language+" "+label.text);
                    foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c)))Assert.That(label.font.HasCharacter(c,true),Is.True,"Missing glyph "+c);
                }
            }
            localization.SetLanguage(GameLanguage.Chinese);flight.Tick(.02f);yield return null;
            yield return Capture("usability-docking-assist",session.Player.Camera.transform.position,new Vector3(99.55f,1.5f,1.38f));
            yield return Capture("usability-docking-port",session.Player.Camera.transform.position,guide.Ring.transform.position);
            yield return FinishAssisted();
        }
        [UnityTest] public IEnumerator CloseOffCentreApproachBacksAwayThenCaptures()
        {
            OrbitAt(new Vector3(.8f,.25f,-.45f),Vector3.zero,new Vector3(0,12,0));
            var flight=session.GetComponent<AscentMission>();var docking=session.GetComponent<DockingMission>();
            flight.Tick(1);Assert.That(docking.State,Is.EqualTo(DockingState.Approaching));Assert.That(docking.Position.z,Is.LessThan(-.45f));
            yield return FinishAssisted();
        }
        [UnityTest] public IEnumerator FastPortCollisionStillFailsAndManualAlignedCaptureWorks()
        {
            OrbitAt(new Vector3(0,0,-1),Vector3.forward*.9f,Vector3.zero);
            var flight=session.GetComponent<AscentMission>();flight.Tick(1);
            Assert.That(flight.Failure,Is.EqualTo(AscentFailure.DockingCollision));
            session.RetryMission();yield return ExpansionTestSteps.Wake(session);Object.Destroy(settings);settings=null;
            OrbitAt(new Vector3(.2f,0,-.6f),Vector3.forward*.10f,new Vector3(0,4,0));
            var docking=session.GetComponent<DockingMission>();docking.ToggleAssistance();flight.Tick(4);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Docked),"Aligned slow contact can mechanically capture with assist turned off.");
        }
        private IEnumerator Capture(string name,Vector3 eye,Vector3 target)
        {
            var obj=new GameObject("Usability capture");var camera=obj.AddComponent<Camera>();camera.CopyFrom(session.Player.Camera);camera.enabled=false;
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));camera.fieldOfView=65;
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
