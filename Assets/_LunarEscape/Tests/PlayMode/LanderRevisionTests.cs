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
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace LunarEscape.Tests
{
    // 从真实 08 场景检查登舱门槛、碰撞、空间尺度和飞行画面；不连接头显。
    public sealed class LanderRevisionTests
    {
        private StationMissionSession session;
        private AscentMission flight;
        private HatchBoardingController hatch;
        private LunarViewLayout layout;
        private FlightScenePresenter scene;
        private CollisionAwareSimulator simulator;
        private float previousFrame;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousFocus;
        [UnitySetUp]
        public IEnumerator Load()
        {
            previousFrame=Time.captureDeltaTime; Time.captureDeltaTime=1f/30;
            previousBackground=InputSystem.settings.backgroundBehavior; previousFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("08_LunarStation_Ascent",LoadSceneMode.Single); yield return null;
            session=Object.FindAnyObjectByType<StationMissionSession>(); session.enabled=false;
            flight=session.GetComponent<AscentMission>(); layout=session.GetComponent<LunarViewLayout>(); scene=session.GetComponent<FlightScenePresenter>();
            hatch=Object.FindAnyObjectByType<HatchBoardingController>(); simulator=Object.FindAnyObjectByType<CollisionAwareSimulator>();
            Set(simulator.translateXInput,0); Set(simulator.translateYInput,0); Set(simulator.translateZInput,0);
            Set(simulator.keyboardRotationDeltaInput,Vector2.zero); Set(simulator.mouseRotationDeltaInput,Vector2.zero); Set(simulator.mouseScrollInput,Vector2.zero);
            while(Time.time<1.05f) yield return null;
        }
        [TearDown]
        public void Restore()
        {
            Time.captureDeltaTime=previousFrame; InputSystem.settings.backgroundBehavior=previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=previousFocus;
        }
        private void Evacuate(float remaining=60)
        {
            session.BeginMission(); session.Advance(session.Mission.Config.RepairWindowSeconds);
            session.Advance(session.Mission.RemainingSeconds-remaining);
        }
        private void MoveBody(Vector3 target)
        {
            var body=session.Exit.PlayerBody; var center=body.transform.TransformPoint(body.center);
            body.enabled=false; body.transform.position+=new Vector3(target.x-center.x,0,target.z-center.z); body.enabled=true; Physics.SyncTransforms();
        }
        private void Board(float remaining=60)
        {
            Evacuate(remaining); MoveBody(session.Exit.Volume.bounds.center); Assert.That(hatch.CanBoard,Is.True);
            hatch.RequestBoarding(); session.Advance(0); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.FadeOut));
            flight.Tick(.951f); Assert.That(scene.IsSeated,Is.True);
        }
        private void Launch()
        {
            flight.PowerOn(); flight.StartNavigation(); flight.Tick(flight.Config.NavigationSeconds);
            flight.PrepareEngine(); flight.Tick(flight.Config.EngineSeconds); flight.Ignite(); flight.Tick(flight.Config.IgnitionSeconds);
        }
        [UnityTest]
        public IEnumerator ExteriorAndWindowUseSameMeshesAndMeterScaleWithSidewaysHatch()
        {
            Assert.That(Vector3.Distance(layout.Lander.position,Vector3.zero),Is.GreaterThan(40));
            Assert.That(Mathf.Abs(Vector3.Dot(-layout.Door.forward,Vector3.right)),Is.LessThan(.01f),"舱门朝向应与基地出口相差 90 度。");
            var meshes=layout.Lander.GetComponentsInChildren<MeshFilter>();
            Assert.That(meshes.Sum(m=>m.sharedMesh.triangles.Length/3),Is.GreaterThan(90000));
            var modelBounds=BoundsOf(layout.Lander.GetComponentsInChildren<MeshRenderer>());
            Assert.That(modelBounds.size.y,Is.EqualTo(7).Within(.03f)); Assert.That(modelBounds.size.x,Is.GreaterThan(8));
            var source=layout.BaseExterior.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<TMP_Text>()==null).ToArray();
            var copies=layout.BaseProxy.GetComponentsInChildren<MeshRenderer>(true); Assert.That(copies.Length,Is.EqualTo(source.Length));
            for(int i=0;i<source.Length;i++)
            {
                Assert.That(copies[i].GetComponent<MeshFilter>().sharedMesh,Is.SameAs(source[i].GetComponent<MeshFilter>().sharedMesh));
                Assert.That(Vector3.Distance(copies[i].transform.localPosition,source[i].transform.position),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(copies[i].transform.localScale,source[i].transform.lossyScale),Is.LessThan(.001f));
            }
            Assert.That(BoundsOf(source).size.z,Is.GreaterThan(20));
            Assert.That(session.Player.Camera.farClipPlane,Is.GreaterThan(1600),"远处月面不能被相机裁掉。");
            Assert.That(Object.FindObjectsByType<CargoBoardingController>(FindObjectsInactive.Include,FindObjectsSortMode.None),Is.Empty);
            Capture("exterior",new Vector3(38,3,-11),layout.Lander.position+Vector3.up*3.2f);
            Capture("route",new Vector3(8,1.7f,1.7f),layout.Lander.position+Vector3.up*2.5f);
            yield return null;
        }
        [UnityTest]
        public IEnumerator OnlyPlayerAtLadderCanBoardAndWalkingIntoApproachDoesNotTriggerIt()
        {
            MoveBody(session.Exit.Volume.bounds.center); hatch.RequestBoarding(); Assert.That(hatch.CanBoard,Is.False);
            Evacuate(); MoveBody(new Vector3(8,0,1.7f)); hatch.RequestBoarding(); session.Advance(.1f);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding));
            MoveBody(layout.Lander.position+Vector3.left*5); hatch.RequestBoarding(); session.Advance(.1f);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding));
            MoveBody(session.Exit.Volume.bounds.center); session.Advance(.1f); Assert.That(hatch.CanBoard,Is.True);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding),"只走到梯子下不能自动登舱。");
            session.Exit.PlayerBody.enabled=false; Assert.That(hatch.CanBoard,Is.False); hatch.RequestBoarding(); session.Exit.PlayerBody.enabled=true;
            Assert.That(Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None).Where(a=>a.name.StartsWith("Lander Route")).All(a=>a.enabled),Is.True);
            hatch.RequestBoarding(); session.Advance(0); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.FadeOut));
            yield return null;
            Assert.That(hatch.CanBoard,Is.False); Assert.That(scene.GroundRoot.activeSelf,Is.True);
            Assert.That(Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None).Where(a=>a.name.StartsWith("Lander Route")).All(a=>!a.enabled),Is.True);
            flight.Tick(.951f); session.RetryMission(); yield return null;
            Board(); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.Startup),"重新开始后舱门仍应可用。");
        }
        [UnityTest]
        public IEnumerator LadderAndCabinBlockActualSimulatorWalking()
        {
            Evacuate(); MoveBody(new Vector3(48,0,-4.3f)); session.Player.MatchOriginUpCameraForward(Vector3.up,Vector3.forward);
#pragma warning disable CS0618
            simulator.targetedDeviceInput=TargetedDevices.FPS;
#pragma warning restore CS0618
            Set(simulator.translateZInput,1);
            for(int i=0;i<65;i++) yield return null;
            Set(simulator.translateZInput,0);
            var center=session.Exit.PlayerBody.transform.TransformPoint(session.Exit.PlayerBody.center);
            Assert.That(center.z,Is.LessThan(-1.85f),"梯子阻挡行走，不能穿进舱体。");
            Assert.That(center.y,Is.InRange(.5f,1.5f)); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding));
            // 舱体侧面也有实体碰撞，不能绕开梯子从侧面走入。
            MoveBody(new Vector3(44,0,1.7f)); session.Player.MatchOriginUpCameraForward(Vector3.up,Vector3.right);
            Set(simulator.translateZInput,1); for(int i=0;i<65;i++) yield return null; Set(simulator.translateZInput,0);
            var wall=layout.Lander.Find("Closed Descent Stage").GetComponent<BoxCollider>(); var body=session.Exit.PlayerBody;
            Assert.That(body.transform.TransformPoint(body.center).x,Is.LessThanOrEqualTo(wall.bounds.min.x-body.radius+body.skinWidth+.01f));
        }
        [UnityTest]
        public IEnumerator HatchUsesRealMouseInputAndThreeLanguagesWithoutClipping()
        {
            Evacuate(); MoveBody(new Vector3(48,0,-4.8f)); session.Player.MatchOriginUpCameraForward(Vector3.up,Vector3.forward);
            var language=Object.FindAnyObjectByType<LocalizationService>();
            foreach(var value in new[]{GameLanguage.Chinese,GameLanguage.English,GameLanguage.Russian})
            {
                language.SetLanguage(value); yield return null;
                foreach(var label in hatch.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing,Is.False,value+" "+label.text);
                    Assert.That(label.text,Does.Not.Contain("lander."));
                    foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c))) Assert.That(label.font.HasCharacter(c,true),Is.True,value+" "+c);
                }
            }
            language.SetLanguage(GameLanguage.Chinese); yield return null;
            Capture("hatch",session.Player.Camera.transform.position,layout.Door.position-Vector3.up*.5f);
            // 舱门在梯子顶端：通过官方模拟头显的旋转输入抬头，再以真实鼠标点击。
#pragma warning disable CS0618
            simulator.targetedDeviceInput=TargetedDevices.HMD;
#pragma warning restore CS0618
            Set(simulator.keyboardRotationDeltaInput,new Vector2(0,28/simulator.rotateYSensitivity)); yield return null;
            Set(simulator.keyboardRotationDeltaInput,Vector2.zero); yield return null; yield return null;
            var button=hatch.GetComponentInChildren<Button>(); Assert.That(button.IsInteractable(),Is.True);
            Canvas.ForceUpdateCanvases(); var canvas=button.GetComponentInParent<Canvas>(); var rect=button.GetComponent<RectTransform>();
            Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center));
            Assert.That(canvas.worldCamera.pixelRect.Contains(point),Is.True,"舱门按钮应在玩家抬头可见范围内。");
            var results=new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},results);
            Assert.That(results.Count,Is.GreaterThan(0)); Assert.That(results[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button));
            var mouse=InputSystem.AddDevice<Mouse>("Hatch Test Mouse");
            try
            {
                var state=new MouseState{position=point}; InputState.Change(mouse,state); yield return null; yield return null;
                InputState.Change(mouse,state.WithButton(MouseButton.Left)); yield return null; yield return null;
                InputState.Change(mouse,state.WithButton(MouseButton.Left,false)); yield return null; yield return null;
                session.Advance(0); Assert.That(flight.Phase,Is.EqualTo(AscentPhase.FadeOut));
            }
            finally { if(mouse.added)InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest]
        public IEnumerator XrDoorSelectionUsesSameBodyAndDistanceGate()
        {
            Evacuate(); yield return null;
            var door=hatch.GetComponent<XRSimpleInteractable>(); var manager=Object.FindAnyObjectByType<XRInteractionManager>();
            // 当前官方 Rig 的日常远距选择由 Near-Far 组件承担，XRRay 只在传送模式启用。
            var ray=session.Player.GetComponentsInChildren<XRBaseInteractor>().OfType<IXRSelectInteractor>().First(r=>((Behaviour)r).isActiveAndEnabled);
            manager.SelectEnter((IXRSelectInteractor)ray,door); session.Advance(0);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding));
            if(door.isSelected)manager.SelectExit((IXRSelectInteractor)ray,door);
            MoveBody(session.Exit.Volume.bounds.center); yield return null;
            manager.SelectEnter((IXRSelectInteractor)ray,door); session.Advance(0);
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.FadeOut));
            flight.Tick(.951f); yield return null;
            Assert.That(scene.IsSeated,Is.True);
        }
        [UnityTest]
        public IEnumerator LastFrameHatchClickCannotWinAgainstExpiredClock()
        {
            Evacuate(.01f); MoveBody(session.Exit.Volume.bounds.center); hatch.RequestBoarding(); session.Advance(.02f);
            Assert.That(session.Mission.Phase,Is.EqualTo(StationMissionPhase.Failed));
            Assert.That(flight.Phase,Is.EqualTo(AscentPhase.AwaitingBoarding)); Assert.That(scene.GroundRoot.activeSelf,Is.True);
            yield return null;
        }
        [UnityTest]
        public IEnumerator AscentTurnsAfterVerticalClearanceAndBaseStaysVisibleAtRealScale()
        {
            Board(); yield return null; var animation=scene.FlightWorld.GetComponent<LunarWindowAnimation>();
            var eye=session.Player.Camera.transform.position; var restOrigin=session.Player.Origin.transform.position;
            Capture("prelaunch",eye,layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)));
            float firstWidth=BoundsOf(layout.BaseProxy.GetComponentsInChildren<MeshRenderer>()).size.z;
            Assert.That(firstWidth,Is.GreaterThan(20)); AssertWindowLine(eye,layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)));
            Launch(); flight.Tick(2); yield return null;
            Assert.That(animation.Altitude,Is.GreaterThan(0)); Assert.That(animation.PitchDegrees,Is.Zero); Assert.That(animation.Downrange,Is.Zero);
            for(int i=0;i<10;i++) {flight.Tick(1);yield return null;AssertWindowLine(eye,layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)));}
            Assert.That(animation.PitchDegrees,Is.GreaterThan(1)); Assert.That(animation.Downrange,Is.GreaterThan(1));
            AssertWindowLine(eye,layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)));
            Capture("turn",eye,layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)));
            for(int i=0;i<18;i++) {flight.Tick(1);yield return null;AssertWindowLine(eye,layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)));}
            Assert.That(animation.PitchDegrees,Is.InRange(50,60)); Assert.That(animation.Altitude,Is.EqualTo(50).Within(.01f));
            Assert.That(layout.BaseProxy.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(BoundsOf(layout.BaseProxy.GetComponentsInChildren<MeshRenderer>()).size.z,Is.EqualTo(firstWidth).Within(.001f));
            AssertWindowLine(eye,layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)));
            Assert.That(Vector3.Distance(session.Player.Origin.transform.position,restOrigin),Is.LessThan(.001f));
            Capture("high",eye,layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)));
        }
        [UnityTest]
        public IEnumerator BlastShakeScalesWithDepartureMarginWithoutWritingHeadPoseAndResets()
        {
            float previousAmplitude=0;
            foreach(float remaining in new[]{60f,30f,18f})
            {
                Board(remaining); Launch(); flight.Tick(flight.BaseRemaining); yield return null;
                var impact=scene.FlightWorld.GetComponent<FlightImpactMotion>(); var animation=scene.FlightWorld.GetComponent<LunarWindowAnimation>();
                Assert.That(impact.Amplitude,Is.GreaterThan(previousAmplitude)); previousAmplitude=impact.Amplitude;
                var eye=session.Player.Camera.transform.position; var headLocal=session.Player.Camera.transform.localPosition; var headRotation=session.Player.Camera.transform.localRotation;
                Assert.That(animation.ExplosionVisible,Is.True); Assert.That(layout.BaseProxy.activeSelf,Is.False);
                var target=layout.OutsideWorld.TransformPoint(new Vector3(0,2.2f,0)); AssertWindowLine(eye,target);
                Capture("blast-"+remaining,eye,target);
                var rest=new Vector3(100,0,0); float maximum=0;
                for(int i=0;i<30;i++)
                {
                    maximum=Mathf.Max(maximum,Vector3.Distance(scene.FlightWorld.transform.position,rest)); yield return null;
                    Assert.That(Vector3.Distance(session.Player.Camera.transform.localPosition,headLocal),Is.LessThan(.0001f));
                    Assert.That(Quaternion.Angle(session.Player.Camera.transform.localRotation,headRotation),Is.LessThan(.001f));
                }
                Assert.That(maximum,Is.GreaterThan(0)); Assert.That(maximum,Is.LessThan(.02f));
                Assert.That(Vector3.Distance(scene.FlightWorld.transform.position,rest),Is.LessThan(.0001f));
                session.RetryMission(); yield return null;
                Assert.That(scene.FlightWorld.activeSelf,Is.False); Assert.That(Vector3.Distance(scene.FlightWorld.transform.position,rest),Is.LessThan(.0001f));
            }
        }
        private void AssertWindowLine(Vector3 eye,Vector3 target)
        {
            Physics.SyncTransforms();
            var hits=Physics.RaycastAll(eye,target-eye,Vector3.Distance(eye,target),~0,QueryTriggerInteraction.Ignore);
            var blocked=hits.Where(h=>h.transform.IsChildOf(scene.FlightWorld.transform)).ToArray();
            Assert.That(blocked,Is.Empty,"基地中心不能被舱壁/窗框挡住。 eye="+eye+" target="+target+" hits="+string.Join(",",blocked.Select(h=>h.transform.name)));
        }
        private static Bounds BoundsOf(IEnumerable<MeshRenderer> renderers)
        {
            var items=renderers.ToArray(); var bounds=items[0].bounds; foreach(var r in items.Skip(1))bounds.Encapsulate(r.bounds); return bounds;
        }
        private static void Set(XRInputValueReader<float> r,float v){r.inputSourceMode=XRInputValueReader.InputSourceMode.ManualValue;r.manualValue=v;}
        private static void Set(XRInputValueReader<Vector2> r,Vector2 v){r.inputSourceMode=XRInputValueReader.InputSourceMode.ManualValue;r.manualValue=v;}
        private static void Capture(string name,Vector3 position,Vector3 target)
        {
            var owner=new GameObject("Lander preview camera");var camera=owner.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.fieldOfView=65;camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            var texture=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGB32);var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{texture.Create();camera.targetTexture=texture;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=texture});RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1600,1100),0,0);pixels.Apply();string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.development/lander-"+name+".png"));File.WriteAllBytes(path,pixels.EncodeToPNG());TestContext.Progress.WriteLine(path);}
            finally{RenderTexture.active=previous;camera.targetTexture=null;texture.Release();Object.Destroy(pixels);Object.Destroy(texture);Object.Destroy(owner);}
        }
    }
}
