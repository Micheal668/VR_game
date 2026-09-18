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
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace LunarEscape.Tests
{
    // 第六步验收：真实场景、真实鼠标输入、碰撞与大时间步的轨道规则。
    public sealed class OrbitalSceneTests
    {
        private StationMissionSession session;private AscentMission flight;private FlightScenePresenter scene;
        private HatchBoardingController hatch;private LunarViewLayout layout;private CargoInventory cargo;
        private CollisionAwareSimulator simulator;private float previousFrame;
        private InputSettings.BackgroundBehavior previousBackground;private InputSettings.EditorInputBehaviorInPlayMode previousFocus;
        [UnitySetUp]
        public IEnumerator Load()
        {
            previousFrame=Time.captureDeltaTime;Time.captureDeltaTime=1f/30;
            previousBackground=InputSystem.settings.backgroundBehavior;previousFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("09_LunarStation_Orbit");yield return null;
            session=Object.FindAnyObjectByType<StationMissionSession>();session.enabled=false;flight=session.GetComponent<AscentMission>();scene=session.GetComponent<FlightScenePresenter>();
            layout=session.GetComponent<LunarViewLayout>();cargo=session.GetComponent<CargoInventory>();hatch=Object.FindAnyObjectByType<HatchBoardingController>();simulator=Object.FindAnyObjectByType<CollisionAwareSimulator>();
            Set(simulator.translateXInput,0);Set(simulator.translateYInput,0);Set(simulator.translateZInput,0);
            Set(simulator.keyboardRotationDeltaInput,Vector2.zero);Set(simulator.mouseRotationDeltaInput,Vector2.zero);Set(simulator.mouseScrollInput,Vector2.zero);
            while(Time.time<1.05f)yield return null;
        }
        [TearDown] public void Restore(){Time.captureDeltaTime=previousFrame;InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousFocus;}
        private void Evacuate(float remaining=60){session.BeginMission();session.Advance(session.Mission.Config.RepairWindowSeconds);session.Advance(session.Mission.RemainingSeconds-remaining);}
        private void Move(Vector3 p)
        {
            var body=session.Exit.PlayerBody;var center=body.transform.TransformPoint(body.center);body.enabled=false;body.transform.position+=new Vector3(p.x-center.x,0,p.z-center.z);body.enabled=true;Physics.SyncTransforms();
        }
        private void Board(float remaining=60,params CargoKind[] supplies)
        {
            Evacuate(remaining);foreach(var kind in supplies)Assert.That(cargo.TryHold(cargo.Items.First(i=>i.Kind==kind)),Is.True);
            Move(new Vector3(48,0,-6));Assert.That(hatch.CanBoard,Is.True);hatch.RequestBoarding();session.Advance(0);flight.Tick(.951f);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Startup));
        }
        private void Launch(){flight.PowerOn();flight.StartNavigation();flight.Tick(2);flight.PrepareEngine();flight.Tick(2);flight.Ignite();flight.Tick(3);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Ascent));}
        private void ReachInsertion(){flight.Tick(flight.OrbitAscentSeconds-flight.AirborneSeconds);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.OrbitalInsertion));}
        [UnityTest]
        public IEnumerator LargeBoardingControlWorksWithLevelViewFromMultipleApproaches()
        {
#pragma warning disable CS0618
            simulator.targetedDeviceInput=TargetedDevices.HMD;
#pragma warning restore CS0618
            yield return null;yield return null;
            foreach(var p in new[]{new Vector3(46.1f,0,-6.6f),new Vector3(48,0,-6.7f),new Vector3(50,0,-6.7f),new Vector3(48,0,-3.8f)})
            {
                Evacuate();Move(p);session.Player.MatchOriginUpCameraForward(Vector3.up,Vector3.ProjectOnPlane(hatch.AccessibleButton.transform.position-session.Player.Camera.transform.position,Vector3.up));
                yield return null;yield return null;Assert.That(hatch.CanBoard,Is.True);session.Advance(.01f);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding));
                Capture("boarding-"+p.x+"-"+p.z,session.Player.Camera.transform.position,hatch.AccessibleButton.transform.position);
                yield return Click(hatch.AccessibleButton);session.Advance(0);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.FadeOut));session.RetryMission();yield return null;
            }
        }
        [UnityTest]
        public IEnumerator AcceptedBoardingSurvivesSmallMovementButDeadlineStillWins()
        {
            Evacuate();Move(new Vector3(8,0,1.7f));hatch.RequestBoarding();session.Advance(.1f);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding));
            Move(new Vector3(48,0,-6));hatch.RequestBoarding();Move(new Vector3(48,0,-8));session.Advance(.1f);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.FadeOut));
            session.RetryMission();yield return null;Evacuate(.01f);Move(new Vector3(48,0,-6));hatch.RequestBoarding();session.Advance(.02f);
            Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Failed));Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding));
        }
        [UnityTest]
        public IEnumerator ActualTerrainHasDeepCratersAndWalkableFlatEscapeRoute()
        {
            var terrain=session.GetComponent<LunarTerrainLayout>();var mesh=terrain.WalkableSurface.sharedMesh;
            Assert.That(mesh.vertices.Min(v=>v.y),Is.LessThan(-3));Assert.That(mesh.vertices.Max(v=>v.y),Is.GreaterThan(2));
            foreach(var p in new[]{new Vector3(25,0,16),new Vector3(25,0,24),new Vector3(44,0,-18),new Vector3(20,0,2)})
            {Assert.That(terrain.WalkableSurface.Raycast(new Ray(p+Vector3.up*40,Vector3.down),out var hit,100),Is.True);Assert.That(hit.point.y,Is.EqualTo(LunarTerrainProfile.Height(p.x,p.z)).Within(.03f));}
            foreach(var source in terrain.GroundVisual.GetComponentsInChildren<MeshFilter>())Assert.That(terrain.FlightVisual.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh==source.sharedMesh),Is.True);
            foreach(var wall in scene.GroundRoot.GetComponentsInChildren<Transform>().Where(t=>t.name=="Moon Perimeter"))Assert.That(wall.GetComponent<Collider>().bounds.min.y,Is.LessThan(-8));
            Evacuate();Move(new Vector3(8,0,1.7f));session.Player.MatchOriginUpCameraForward(Vector3.up,Vector3.right);
#pragma warning disable CS0618
            simulator.targetedDeviceInput=TargetedDevices.FPS;
#pragma warning restore CS0618
            Set(simulator.translateZInput,1);for(int i=0;i<350;i++)yield return null;Set(simulator.translateZInput,0);
            var center=session.Exit.PlayerBody.transform.TransformPoint(session.Exit.PlayerBody.center);Assert.That(center.x,Is.GreaterThan(30));Assert.That(center.y,Is.InRange(.5f,1.5f));
            Capture("terrain",new Vector3(12,2,-9),new Vector3(30,0,18));
        }
        [UnityTest]
        public IEnumerator CabinHasSeparatedHumanSizedCrewStationsAndVisibleCompanion()
        {
            Board();yield return null;var crew=scene.FlightWorld.GetComponent<CrewCabinLayout>();Assert.That(crew.Companion.activeInHierarchy,Is.True);
            Assert.That(Vector3.Distance(crew.PlayerStation.position,crew.CompanionStation.position),Is.GreaterThan(1.8f));Assert.That(crew.CabinSize.x,Is.GreaterThan(4));
            var bounds=BoundsOf(crew.Companion.GetComponentsInChildren<MeshRenderer>());Assert.That(bounds.size.y,Is.InRange(1.7f,1.9f));Assert.That(bounds.size.x,Is.LessThan(1.25f));
            Assert.That(bounds.min.x-session.Player.Camera.transform.position.x,Is.GreaterThan(1));
            Capture("crew",session.Player.Camera.transform.position,crew.CompanionStation.position+Vector3.up*1.1f);
            Capture("cabin",session.Player.Camera.transform.position,scene.Panel.transform.position);Capture("window-surface",session.Player.Camera.transform.position,layout.OutsideWorld.TransformPoint(new Vector3(0,2,0)));
        }
        [UnityTest]
        public IEnumerator FlightContinuesAfterBlastAndRequiresRealMouseInsertionClick()
        {
            Board(60,CargoKind.DataCore,CargoKind.LunarSample);yield return null;Launch();
            flight.Circularize();Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Ascent));flight.Tick(2);yield return null;
            Assert.That(OrbitalTrajectory.Sample(flight).Pitch,Is.Zero);var origin=session.Player.Origin.transform.position;
            flight.Tick(flight.BaseRemaining);yield return null;Assert.That(flight.Damage,Is.EqualTo(AscentDamage.None));Assert.That(flight.BaseExploded,Is.True);
            Capture("blast",session.Player.Camera.transform.position,layout.OutsideWorld.TransformPoint(new Vector3(0,2,0)));
            flight.Tick(flight.LowFlightSeconds-flight.AirborneSeconds+5);yield return null;
            Capture("transition",session.Player.Camera.transform.position,session.Player.Camera.transform.position+Vector3.left*20);
            flight.Tick(10);yield return null;Capture("climb",session.Player.Camera.transform.position,session.Player.Camera.transform.position+Vector3.left*20);
            ReachInsertion();yield return null;Assert.That(flight.IsTerminal,Is.False);Assert.That(scene.FlightWorld.GetComponent<OrbitalMoonView>().Blend,Is.EqualTo(1));
            session.Player.MatchOriginUpCameraForward(Vector3.up,Vector3.forward);yield return null;
            float power=flight.Power;yield return Click(scene.Panel.GetComponent<OrbitalPanelPresenter>().CircularizeButton);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Circularizing));Assert.That(flight.Power,Is.EqualTo(power-2).Within(.001f));
            flight.Circularize();Assert.That(flight.Power,Is.EqualTo(power-2).Within(.001f));flight.Tick(6);yield return null;
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Orbit));Assert.That(OrbitalTrajectory.Sample(flight).Altitude,Is.EqualTo(100000).Within(.02f));Assert.That(OrbitalTrajectory.Sample(flight).SpeedKmS,Is.InRange(1.63f,1.64f));
            Assert.That(cargo.GetCount(CargoKind.DataCore),Is.EqualTo(1));Assert.That(cargo.GetCount(CargoKind.LunarSample),Is.EqualTo(1));
            float oxygen=flight.Oxygen;flight.Tick(1000);Assert.That(flight.Oxygen,Is.EqualTo(oxygen));Assert.That(session.Player.Origin.transform.position,Is.EqualTo(origin));
            Capture("orbit",session.Player.Camera.transform.position,session.Player.Camera.transform.position+Vector3.left*20);
            Capture("orbit-panel",session.Player.Camera.transform.position,scene.Panel.transform.position);
        }
        [UnityTest]
        public IEnumerator InsertionWaitStillConsumesOxygenAndCanFail()
        {Board();Launch();ReachInsertion();float oxygen=flight.Oxygen;flight.Tick(oxygen);Assert.That(flight.Failure,Is.EqualTo(AscentFailure.OxygenDepleted));yield return null;}
        [UnityTest]
        public IEnumerator PowerDepletionDuringFinalBurnCannotBecomeOrbitSuccess()
        {
            var config=Object.Instantiate(flight.Config);config.ConfigureVitals(240,100,45);flight.Configure(config,session.Mission,cargo);
            try{Board();Launch();ReachInsertion();Assert.That(flight.CanCircularize,Is.True);flight.Circularize();flight.Tick(6);Assert.That(flight.Failure,Is.EqualTo(AscentFailure.PowerDepleted));Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Failed));}
            finally{Object.Destroy(config);}yield return null;
        }
        [UnityTest]
        public IEnumerator LateFlightDamageCanBeRepairedAndReachOrbitWithThreeChosenSupplies()
        {
            Board(18,CargoKind.RepairKit,CargoKind.MedicalKit,CargoKind.Battery);Launch();flight.Tick(flight.BaseRemaining);Assert.That(flight.Damage,Is.EqualTo(AscentDamage.Heavy));
            Assert.That(flight.UseSupply(CargoKind.RepairKit),Is.True);Assert.That(flight.UseSupply(CargoKind.MedicalKit),Is.True);Assert.That(flight.UseSupply(CargoKind.Battery),Is.True);
            ReachInsertion();flight.Circularize();flight.Tick(6);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Orbit));yield return null;
        }
        [UnityTest]
        public IEnumerator RetryRestoresTerrainCrewControlsAndNextFlightCanComplete()
        {
            Board();Launch();ReachInsertion();flight.Circularize();flight.Tick(6);yield return null;session.RetryMission();yield return null;
            Assert.That(scene.GroundRoot.activeSelf,Is.True);Assert.That(scene.FlightWorld.activeSelf,Is.False);Assert.That(flight.AirborneSeconds,Is.Zero);Assert.That(flight.Oxygen,Is.EqualTo(180));
            Board();yield return null;Assert.That(scene.FlightWorld.GetComponent<OrbitalMoonView>().Blend,Is.Zero);Assert.That(session.GetComponent<LunarTerrainLayout>().FlightVisual.activeSelf,Is.True);
            Assert.That(scene.FlightWorld.GetComponent<CrewCabinLayout>().Companion.activeInHierarchy,Is.True);Launch();ReachInsertion();flight.Circularize();flight.Tick(6);Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Orbit));
        }
        [UnityTest]
        public IEnumerator AddedControlsFitAllThreeLanguagesAcrossFlightStages()
        {
            var language=Object.FindAnyObjectByType<LocalizationService>();Evacuate();Move(new Vector3(48,0,-6));yield return null;
            foreach(var lang in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian}){language.SetLanguage(lang);yield return null;CheckLabels(hatch.AccessibleButton.transform.parent.gameObject);}
            hatch.RequestBoarding();session.Advance(0);flight.Tick(.951f);yield return null;
            for(int stage=0;stage<4;stage++)
            {
                foreach(var lang in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian}){language.SetLanguage(lang);yield return null;CheckLabels(scene.Panel);CheckLabels(scene.FlightWorld.GetComponent<CrewCabinLayout>().CompanionStation.parent.Find("Commander Status").gameObject);}
                if(stage==0){Launch();ReachInsertion();}else if(stage==1)flight.Circularize();else if(stage==2)flight.Tick(6);
            }
        }
        [Test]
        public void TrajectoryIsContinuousAtVerticalTurnClimbAndOrbitBoundaries()
        {
            const float low=58,total=98;
            foreach(float t in new[]{3f,7f,low,total})
            {
                var before=OrbitalTrajectory.Sample(t-.001f,low,total,100,0);var after=OrbitalTrajectory.Sample(t+.001f,low,total,100,0);
                Assert.That(Mathf.Abs(after.Altitude-before.Altitude),Is.LessThan(.15f));Assert.That(Mathf.Abs(after.Pitch-before.Pitch),Is.LessThan(.02f));
            }
            var prev=OrbitalTrajectory.Sample(0,low,total,100,0);
            for(float t=.1f;t<total+1;t+=.1f){var next=OrbitalTrajectory.Sample(t,low,total,100,0);Assert.That(next.Altitude,Is.GreaterThanOrEqualTo(prev.Altitude-.1f));Assert.That(next.Pitch,Is.GreaterThanOrEqualTo(prev.Pitch-.01f));Assert.That(next.Pitch-prev.Pitch,Is.LessThan(2));prev=next;}
        }
        private static void CheckLabels(GameObject root)
        {foreach(var label in root.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();Assert.That(label.isTextOverflowing,Is.False,label.name+" "+label.text);foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c)))Assert.That(label.font.HasCharacter(c,true),Is.True,c+" "+label.text);}}
        private static IEnumerator Click(Button button)
        {
            Assert.That(button.IsInteractable(),Is.True,button.name);Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();var rect=button.GetComponent<RectTransform>();
            Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center));Assert.That(canvas.worldCamera.pixelRect.Contains(point),Is.True,button.name+" outside view "+point);
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button),string.Join(",",hits.Select(h=>h.gameObject.name)));
            var mouse=InputSystem.AddDevice<Mouse>("Orbital test mouse");try{var state=new MouseState{position=point};InputState.Change(mouse,state);yield return null;yield return null;InputState.Change(mouse,state.WithButton(MouseButton.Left));yield return null;yield return null;InputState.Change(mouse,state.WithButton(MouseButton.Left,false));yield return null;yield return null;}
            finally{if(mouse.added)InputSystem.RemoveDevice(mouse);}
        }
        private static Bounds BoundsOf(IEnumerable<MeshRenderer> rs){var all=rs.ToArray();var b=all[0].bounds;foreach(var r in all.Skip(1))b.Encapsulate(r.bounds);return b;}
        private static void Set(XRInputValueReader<float> r,float v){r.inputSourceMode=XRInputValueReader.InputSourceMode.ManualValue;r.manualValue=v;}
        private static void Set(XRInputValueReader<Vector2> r,Vector2 v){r.inputSourceMode=XRInputValueReader.InputSourceMode.ManualValue;r.manualValue=v;}
        private static void Capture(string name,Vector3 eye,Vector3 target)
        {
            var obj=new GameObject("Orbital test camera");var camera=obj.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.fieldOfView=70;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            var rt=new RenderTexture(1600,1100,24);var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{rt.Create();camera.targetTexture=rt;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,1100),0,0);pixels.Apply();File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../../.development/orbit-"+name+".png")),pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.Destroy(pixels);Object.Destroy(rt);Object.Destroy(obj);}
        }
    }
}
