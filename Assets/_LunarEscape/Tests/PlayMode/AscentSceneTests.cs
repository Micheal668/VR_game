using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape.Tests
{
    public sealed class AscentSceneTests
    {
        private StationMissionSession session;
        private AscentMission flight;
        private CargoInventory cargo;
        private CollisionAwareSimulator simulator;
        private FlightScenePresenter scene;
        private float previousFrame;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousFocus;
        [UnitySetUp]
        public IEnumerator Load()
        {
            previousFrame=Time.captureDeltaTime; Time.captureDeltaTime=1f/30;
            previousBackground=InputSystem.settings.backgroundBehavior;
            previousFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("08_LunarStation_Ascent",LoadSceneMode.Single);
            yield return null;
            session=Object.FindAnyObjectByType<StationMissionSession>(); session.enabled=false;
            flight=session.GetComponent<AscentMission>(); cargo=session.GetComponent<CargoInventory>(); scene=session.GetComponent<FlightScenePresenter>();
            simulator=Object.FindAnyObjectByType<CollisionAwareSimulator>();
            SetValue(simulator.translateXInput,0); SetValue(simulator.translateYInput,0); SetValue(simulator.translateZInput,0);
            SetVector(simulator.keyboardRotationDeltaInput,Vector2.zero); SetVector(simulator.mouseRotationDeltaInput,Vector2.zero); SetVector(simulator.mouseScrollInput,Vector2.zero);
            while(Time.time<1.05f) yield return null;
        }
        [TearDown]
        public void Restore()
        {
            Time.captureDeltaTime=previousFrame; InputSystem.settings.backgroundBehavior=previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=previousFocus;
        }
        private void Board(float remaining=60, params CargoKind[] kinds)
        {
            session.BeginMission(); session.Advance(session.Mission.Config.RepairWindowSeconds);
            foreach(var kind in kinds) Assert.That(cargo.TryHold(cargo.Items.First(i=>i.Kind==kind && i.State==CargoState.World)),Is.True);
            var body=session.Exit.PlayerBody; var center=body.transform.TransformPoint(body.center); var dest=session.Exit.Volume.bounds.center;
            body.enabled=false; body.transform.position+=new Vector3(dest.x-center.x,0,dest.z-center.z); body.enabled=true; Physics.SyncTransforms();
            var hatch=Object.FindAnyObjectByType<HatchBoardingController>(); Assert.That(hatch.CanBoard,Is.True); hatch.RequestBoarding();
            session.Advance(session.Mission.RemainingSeconds-remaining);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.FadeOut));
            Assert.That(flight.BaseRemaining,Is.EqualTo(remaining).Within(.001f));
        }
        private void Startup() => flight.Tick(flight.Config.FadeOutSeconds+flight.Config.BlackSeconds+flight.Config.FadeInSeconds+.001f);
        private void Launch()
        {
            flight.PowerOn(); flight.StartNavigation(); flight.Tick(flight.Config.NavigationSeconds);
            flight.PrepareEngine(); flight.Tick(flight.Config.EngineSeconds); flight.Ignite(); flight.Tick(flight.Config.IgnitionSeconds);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Ascent));
        }
        [UnityTest]
        public IEnumerator StartupSequenceConsumesTimeAndRejectsOutOfOrderOrDoubleClicks()
        {
            Board(); Startup(); float initial=flight.Power;
            flight.Ignite(); flight.PrepareEngine(); flight.StartNavigation();
            Assert.That(flight.Power,Is.EqualTo(initial)); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Startup));
            flight.PowerOn(); flight.PowerOn(); Assert.That(flight.Power,Is.EqualTo(initial-5));
            flight.StartNavigation(); flight.Tick(1); float remaining=flight.OperationRemaining; flight.StartNavigation();
            Assert.That(flight.OperationRemaining,Is.EqualTo(remaining)); flight.PrepareEngine(); Assert.That(flight.EngineReady,Is.False);
            flight.Tick(remaining); flight.PrepareEngine(); float after=flight.Power; flight.PrepareEngine(); Assert.That(flight.Power,Is.EqualTo(after));
            flight.Tick(flight.Config.EngineSeconds); flight.Ignite(); float ignitionPower=flight.Power; flight.Ignite(); Assert.That(flight.Power,Is.EqualTo(ignitionPower));
            flight.Tick(flight.Config.IgnitionSeconds); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Ascent));
            Assert.That(flight.DepartureMargin,Is.EqualTo(60-.951f-7).Within(.01f)); yield return null;
        }
        [UnityTest]
        public IEnumerator BlinkChangesWorldOnlyWhenBlackAndKeepsHeadAndUiTrackingLive()
        {
            Board(); var origin=session.Player.Origin.transform; var groundPosition=origin.position;
            flight.Tick(flight.Config.FadeOutSeconds/2); yield return null;
            Assert.That(scene.FadeAlpha,Is.EqualTo(.5f).Within(.02f)); Assert.That(scene.GroundRoot.activeSelf,Is.True);
            Assert.That(Vector3.Distance(origin.position,groundPosition),Is.LessThan(.01f));
            flight.Tick(flight.Config.FadeOutSeconds/2); yield return null;
            Assert.That(scene.FadeAlpha,Is.EqualTo(1)); Assert.That(scene.IsSeated,Is.True);
            Assert.That(scene.GroundRoot.activeSelf,Is.False); Assert.That(scene.FlightWorld.activeInHierarchy,Is.True);
            Assert.That(origin.position.x,Is.EqualTo(100).Within(.1f));
            flight.Tick(flight.Config.BlackSeconds+flight.Config.FadeInSeconds+.01f); yield return null;
            Assert.That(scene.FadeAlpha,Is.Zero); Assert.That(simulator.BodyMovementEnabled,Is.False);
            var locked=origin.position; var rotation=origin.rotation;
#pragma warning disable CS0618
            simulator.targetedDeviceInput=TargetedDevices.FPS;
#pragma warning restore CS0618
            SetValue(simulator.translateZInput,1);
            var transformer=session.Player.GetComponentInChildren<XRBodyTransformer>();
            transformer.QueueTransformation(new XROriginMovement { motion=Vector3.right,forceUnconstrained=true });
            var teleport=session.Player.GetComponentInChildren<MissionTeleportationProvider>(true);
            Assert.That(teleport.QueueTeleportRequest(new TeleportRequest { destinationPosition=Vector3.zero }),Is.False);
            SetVector(simulator.keyboardRotationDeltaInput,new Vector2(18,0));
            var headBefore=session.Player.Camera.transform.localRotation;
            for(int i=0;i<8;i++) yield return null;
            SetValue(simulator.translateZInput,0); SetVector(simulator.keyboardRotationDeltaInput,Vector2.zero);
            Assert.That(Vector3.Distance(origin.position,locked),Is.LessThan(.01f)); Assert.That(Quaternion.Angle(origin.rotation,rotation),Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(session.Player.Camera.transform.localRotation,headBefore),Is.GreaterThan(.1f),"转头追踪仍应工作。");
            Assert.That(session.Player.GetComponentsInChildren<XRRayInteractor>(true).Any(r=>r.enabled && r.enableUIInteraction),Is.True);
            Assert.That(Find("Flight Start 0").GetComponent<Button>().IsInteractable(),Is.True);
            Find("Flight Start 0").GetComponent<Button>().onClick.Invoke(); Assert.That(flight.Powered,Is.True);
        }
        [UnityTest]
        public IEnumerator BoardingDoesNotResetDeadlineAndExplosionWinsIgnitionTie()
        {
            Board(.2f); flight.Tick(.2f); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Failed));
            Assert.That(flight.Failure,Is.EqualTo(AscentFailure.BaseExplosion)); Assert.That(scene.FadeAlpha,Is.Zero);
            session.RetryMission(); Board(7.95f); Startup();
            flight.PowerOn(); flight.StartNavigation(); flight.Tick(2); flight.PrepareEngine(); flight.Tick(2); flight.Ignite(); flight.Tick(3);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Failed)); Assert.That(flight.Failure,Is.EqualTo(AscentFailure.BaseExplosion)); yield return null;
        }
        [UnityTest]
        public IEnumerator EarlyDepartureAvoidsDamageAndPreservesScientificCargo()
        {
            Board(60,CargoKind.DataCore,CargoKind.LunarSample); Startup(); Launch();
            flight.Tick(flight.BaseRemaining); Assert.That(flight.BaseExploded,Is.True); Assert.That(flight.Damage,Is.EqualTo(AscentDamage.None));
            Assert.That(flight.Health,Is.EqualTo(100)); flight.Tick(flight.Config.RecoverySeconds);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Completed)); Assert.That(cargo.GetCount(CargoKind.DataCore),Is.EqualTo(1));
            Assert.That(cargo.GetCount(CargoKind.LunarSample),Is.EqualTo(1)); Assert.That(flight.UseSupply(CargoKind.Oxygen),Is.False);
            float frozen=flight.Oxygen; flight.Tick(100); Assert.That(flight.Oxygen,Is.EqualTo(frozen)); yield return null;
        }
        [UnityTest]
        public IEnumerator LateDepartureNeedsTreatmentAndRepairAndConsumesOnlyOnce()
        {
            Board(18,CargoKind.Oxygen,CargoKind.RepairKit,CargoKind.MedicalKit); Startup();
            Assert.That(flight.UseSupply(CargoKind.MedicalKit),Is.False); Assert.That(flight.UseSupply(CargoKind.RepairKit),Is.False);
            Assert.That(cargo.TotalCount,Is.EqualTo(3)); Launch(); flight.Tick(flight.BaseRemaining);
            Assert.That(flight.Damage,Is.EqualTo(AscentDamage.Heavy)); Assert.That(flight.Health,Is.EqualTo(45));
            Assert.That(flight.LeakPerSecond,Is.EqualTo(5)); Assert.That(flight.InjuryPerSecond,Is.EqualTo(1.8f));
            flight.Tick(1); float oxygen=flight.Oxygen, health=flight.Health;
            Assert.That(flight.UseSupply(CargoKind.Oxygen),Is.True); Assert.That(flight.Oxygen,Is.EqualTo(oxygen+90).Within(.001f));
            Assert.That(flight.UseSupply(CargoKind.MedicalKit),Is.True); Assert.That(flight.Health,Is.EqualTo(health+40).Within(.001f)); Assert.That(flight.InjuryPerSecond,Is.Zero);
            Assert.That(flight.UseSupply(CargoKind.RepairKit),Is.True); Assert.That(flight.LeakPerSecond,Is.Zero);
            Assert.That(flight.UseSupply(CargoKind.Oxygen),Is.False); Assert.That(flight.SuppliesUsed,Is.EqualTo(3));
            Assert.That(cargo.TotalCount,Is.Zero); Assert.That(cargo.TotalWeightKg,Is.Zero); Assert.That(cargo.TypeCount,Is.Zero);
            Assert.That(cargo.Items.Count(i=>i.State==CargoState.Consumed),Is.EqualTo(3));
            flight.Tick(flight.RecoveryRemaining); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Completed)); yield return null;
        }
        [UnityTest]
        public IEnumerator BatteryAddsRealPowerAndCannotBeWastedAtFullCharge()
        {
            Board(60,CargoKind.Battery); Startup(); Assert.That(flight.UseSupply(CargoKind.Battery),Is.False);
            Launch(); float before=flight.Power;
            Assert.That(flight.UseSupply(CargoKind.Battery),Is.True); Assert.That(flight.Power,Is.EqualTo(Mathf.Min(100,before+45)));
            Assert.That(cargo.GetCount(CargoKind.Battery),Is.Zero); Assert.That(flight.UseSupply(CargoKind.Battery),Is.False); yield return null;
        }
        [UnityTest]
        public IEnumerator SevereDamageWithoutSuppliesFailsAndOnlyRestartRemains()
        {
            Board(18); Startup(); Launch(); flight.Tick(flight.BaseRemaining); flight.Tick(1000);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Failed));
            var failure=session.GetComponent<MissionFailurePresenter>(); Assert.That(failure.IsVisible,Is.True); Assert.That(scene.Panel.activeInHierarchy,Is.False);
            Assert.That(flight.Failure,Is.EqualTo(AscentFailure.OxygenDepleted));
            var enabled=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(false)).Where(b=>b.IsInteractable()).ToArray();
            Assert.That(enabled.Length,Is.EqualTo(1)); Assert.That(enabled[0],Is.EqualTo(failure.RestartButton));
            failure.RestartButton.onClick.Invoke(); yield return null;
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding)); Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Briefing));
            Assert.That(scene.GroundRoot.activeInHierarchy,Is.True); Assert.That(scene.FlightWorld.activeInHierarchy,Is.False);
            Assert.That(simulator.BodyMovementEnabled,Is.True); Assert.That(failure.IsVisible,Is.False);
            Assert.That(Vector3.Distance(session.Player.Camera.transform.position,new Vector3(session.SpawnPoint.position.x,session.Player.Camera.transform.position.y,session.SpawnPoint.position.z)),Is.LessThan(.03f));
            Board(); Startup(); Assert.That(flight.CanPowerOn,Is.True);
        }
        [UnityTest]
        public IEnumerator ReplayRestoresConsumedCargoAndLoadedToolsToTheirOriginalState()
        {
            Board(60,CargoKind.Oxygen,CargoKind.Battery); Startup(); flight.PowerOn(); Assert.That(flight.UseSupply(CargoKind.Oxygen),Is.True);
            session.RetryMission(); yield return null;
            Assert.That(cargo.TotalCount,Is.Zero);
            foreach(var item in cargo.Items) { Assert.That(item.State,Is.EqualTo(CargoState.World)); Assert.That(item.gameObject.activeInHierarchy && item.Grab.enabled,Is.True,item.name); Assert.That(item.Body.constraints,Is.Not.EqualTo(RigidbodyConstraints.FreezeAll)); }
            Board(60,CargoKind.Oxygen); Startup(); Assert.That(flight.UseSupply(CargoKind.Oxygen),Is.True); yield return null;
        }
        [UnityTest]
        public IEnumerator FlightPanelAcceptsActualMouseInputAfterSeatLock()
        {
            Board(60,CargoKind.Oxygen); Startup();
            for(int i=0;i<3;i++) yield return null;
            var mouse=InputSystem.AddDevice<Mouse>("Flight Panel Test Mouse");
            try
            {
                foreach(var name in new[]{"Flight Start 0","Flight Use Oxygen"})
                {
                    var button=Find(name).GetComponent<Button>(); var canvas=button.GetComponentInParent<Canvas>(); var rect=button.GetComponent<RectTransform>();
                    Canvas.ForceUpdateCanvases(); Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center));
                    Assert.That(canvas.worldCamera.pixelRect.Contains(point),Is.True,name+" should be visible");
                    var results=new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},results);
                    Assert.That(results.Count,Is.GreaterThan(0)); Assert.That(results[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button));
                    var state=new MouseState{position=point}; InputState.Change(mouse,state); yield return null; yield return null;
                    InputState.Change(mouse,state.WithButton(MouseButton.Left)); yield return null; yield return null;
                    InputState.Change(mouse,state.WithButton(MouseButton.Left,false)); yield return null; yield return null;
                }
                Assert.That(flight.Powered,Is.True); Assert.That(cargo.GetCount(CargoKind.Oxygen),Is.Zero); Assert.That(flight.SuppliesUsed,Is.EqualTo(1));
            }
            finally { if(mouse.added) InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest]
        public IEnumerator ModerateDepartureProducesLightDamageAndMedicalStopsActualLoss()
        {
            Board(30,CargoKind.RepairKit,CargoKind.MedicalKit); Startup(); Launch(); flight.Tick(flight.BaseRemaining);
            Assert.That(flight.Damage,Is.EqualTo(AscentDamage.Light)); Assert.That(flight.Health,Is.EqualTo(80)); Assert.That(flight.LeakPerSecond,Is.EqualTo(.8f));
            float oxygen=flight.Oxygen; flight.Tick(2); Assert.That(oxygen-flight.Oxygen,Is.EqualTo(3.6f).Within(.002f));
            Assert.That(flight.UseSupply(CargoKind.RepairKit),Is.True); Assert.That(flight.UseSupply(CargoKind.MedicalKit),Is.True);
            Assert.That(flight.Health,Is.EqualTo(100)); oxygen=flight.Oxygen; flight.Tick(2); Assert.That(oxygen-flight.Oxygen,Is.EqualTo(2).Within(.002f)); yield return null;
        }
        [UnityTest]
        public IEnumerator InjuriesAndPowerCanEachCauseFailureBeforeCompletion()
        {
            Board(18,CargoKind.Oxygen); Startup(); Launch(); flight.Tick(flight.BaseRemaining);
            flight.UseOxygen(); flight.Tick(25); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Failed)); Assert.That(flight.Failure,Is.EqualTo(AscentFailure.HealthDepleted));
            session.RetryMission();
            var original=flight.Config; var lowPower=Object.Instantiate(original); lowPower.ConfigureVitals(130,100,50);
            try
            {
                flight.Configure(lowPower,session.Mission,cargo); Board(18); Startup(); Launch(); flight.Tick(flight.BaseRemaining);
                Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Failed)); Assert.That(flight.Failure,Is.EqualTo(AscentFailure.PowerDepleted));
            }
            finally { flight.Configure(original,session.Mission,cargo); Object.Destroy(lowPower); }
            yield return null;
        }
        [UnityTest]
        public IEnumerator ActualFrameClockAdvancesOnceAcrossBoardingAndFade()
        {
            Board(60); float before=flight.BaseRemaining; session.enabled=true;
            for(int i=0;i<6;i++) yield return null;
            session.enabled=false;
            Assert.That(before-flight.BaseRemaining,Is.EqualTo(6f/30).Within(.04f));
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.FadeOut));
            flight.Tick(-1); flight.Tick(float.NaN); flight.Tick(float.PositiveInfinity);
            Assert.That(float.IsNaN(flight.Oxygen),Is.False); yield return null;
        }
        [UnityTest]
        public IEnumerator ThreeLanguagePanelAndRenderedWindowShowUsableFlightScene()
        {
            Board(18,CargoKind.Oxygen,CargoKind.RepairKit,CargoKind.MedicalKit); Startup(); yield return null;
            Capture("panel",new Vector3(100,1.7f,-.5f),new Vector3(100,1.53f,1.55f));
            Capture("surface",new Vector3(100,1.7f,-.5f),new Vector3(90,1.7f,1));
            var language=Object.FindAnyObjectByType<LocalizationService>();
            foreach(var value in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian})
            {
                language.SetLanguage(value); yield return null;
                foreach(var label in scene.Panel.GetComponentsInChildren<TMP_Text>(false))
                {
                    label.ForceMeshUpdate(); Assert.That(label.text,Does.Not.Contain("flight."),label.name);
                    Assert.That(label.text,Does.Not.Contain("{0}"),label.name);
                    Assert.That(label.isTextOverflowing,Is.False,value+" "+label.name+": "+label.text);
                    foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c))) Assert.That(label.font.HasCharacter(c,true),Is.True,value+" glyph "+c);
                }
            }
            language.SetLanguage(GameLanguage.Chinese); Launch(); flight.Tick(flight.BaseRemaining);
            for(int i=0;i<3;i++) yield return null;
            Assert.That(Find("Distant Base").activeInHierarchy,Is.False);
            Assert.That(Object.FindAnyObjectByType<LunarWindowAnimation>().ExplosionVisible,Is.True);
            Capture("blast",new Vector3(100,1.7f,-.5f),new Vector3(91,.3f,1));
            for(int i=0;i<35;i++) yield return null;
            Capture("debris",new Vector3(100,1.7f,-.5f),new Vector3(91,.3f,1));
            Capture("damage",new Vector3(100,1.7f,-.5f),new Vector3(100,1.53f,1.55f));
            flight.UseOxygen(); flight.UseRepair(); flight.UseMedical(); flight.Tick(flight.RecoveryRemaining);
            for(int i=0;i<3;i++) yield return null;
            Capture("complete",new Vector3(100,1.7f,-.5f),new Vector3(100,1.53f,1.55f));
        }
        private static GameObject Find(string name)=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name==name).gameObject;
        private static void SetValue(XRInputValueReader<float> reader,float value) { reader.inputSourceMode=XRInputValueReader.InputSourceMode.ManualValue; reader.manualValue=value; }
        private static void SetVector(XRInputValueReader<Vector2> reader,Vector2 value) { reader.inputSourceMode=XRInputValueReader.InputSourceMode.ManualValue; reader.manualValue=value; }
        private static void Capture(string name,Vector3 position,Vector3 target)
        {
            var owner=new GameObject("Flight preview camera"); var camera=owner.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled=false; camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.fieldOfView=65; camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            var texture=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGB32); var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false); var previous=RenderTexture.active;
            try { texture.Create(); camera.targetTexture=texture; RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=texture }); RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1600,1100),0,0); pixels.Apply(); string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.development/ascent-"+name+".png")); File.WriteAllBytes(path,pixels.EncodeToPNG()); TestContext.Progress.WriteLine(path); }
            finally { RenderTexture.active=previous; camera.targetTexture=null; texture.Release(); Object.Destroy(pixels); Object.Destroy(texture); Object.Destroy(owner); }
        }
    }
}
