using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace LunarEscape.Tests
{
    // 经真实头显绑定与 TrackedPoseDriver 回放追踪，不直接修改相机来伪造站立高度。
    public sealed class VrWakeTests
    {
        private XRHMD headset;
        private HTCViveControllerProfile.ViveController controller;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private float previousDelta;
        private Vector3 headPose;

        [UnitySetUp]
        public IEnumerator PrepareHeadset()
        {
            yield return null;
            previousDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            headPose = new Vector3(0, 1.7f, 0);
            SceneManager.sceneLoaded += ConfigureFloorTracking;
        }

        [TearDown]
        public void Cleanup()
        {
            SceneManager.sceneLoaded -= ConfigureFloorTracking;
            if (headset != null && headset.added) InputSystem.RemoveDevice(headset);
            if (controller != null && controller.added) InputSystem.RemoveDevice(controller);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.captureDeltaTime = previousDelta;
        }

        private void ConfigureFloorTracking(Scene scene, LoadSceneMode mode)
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            // 无实体 XR 子系统的测试进程：模拟 Floor 模式由运行时归零的高度偏移。
            if (origin != null) origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
        }

        private void SetHeadPose(Vector3 position, bool tracked = true, Quaternion? rotation = null)
        {
            using (StateEvent.From(headset, out var state))
            {
                headset.centerEyePosition.WriteValueIntoEvent(position, state);
                headset.devicePosition.WriteValueIntoEvent(position, state);
                headset.centerEyeRotation.WriteValueIntoEvent(rotation ?? Quaternion.identity, state);
                headset.deviceRotation.WriteValueIntoEvent(rotation ?? Quaternion.identity, state);
                headset.isTracked.WriteValueIntoEvent(tracked ? 1f : 0f, state);
                headset.trackingState.WriteValueIntoEvent(tracked ? 3 : 0, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator SleepKeepsRealHeadHeightAndTrackingAfterDayChange()
        {
            yield return SceneManager.LoadSceneAsync("12_LunarStation_DayOne");
            // 重现玩家在第一天切换到实体 VR：只停用当天的模拟器，第二天保留原场景加载行为。
            foreach (var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None))
                simulator.gameObject.SetActive(false);
            headset = InputSystem.AddDevice<XRHMD>();
            SetHeadPose(headPose);
            for (int i = 0; i < 360; i++) yield return null;
            var day = Object.FindAnyObjectByType<DayOneMission>();
            var firstOrigin = Object.FindAnyObjectByType<XROrigin>();
            Assert.That(firstOrigin.Camera.transform.localPosition.y, Is.EqualTo(headPose.y).Within(.01f));
            day.Suit.Don();
            foreach (var panel in day.Panels) panel.CleanInstantly();
            foreach (var site in day.Sites) site.CompleteInstantly();
            yield return null;
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var slots = day.Rack.GetComponentsInChildren<XRSocketInteractor>();
            for (int i = 0; i < day.Sites.Length; i++)
            {
                var core = day.Sites[i].Core;
                core.transform.position = slots[i].attachTransform.position;
                manager.SelectEnter((IXRSelectInteractor)slots[i], (IXRSelectInteractable)core.Grab);
                yield return null;
            }
            yield return null;
            UsabilityTestSteps.ReturnSuit(day);
            Assert.That(day.Sleep(), Is.True);
            for (int i = 0; i < 720 && SceneManager.GetActiveScene().name != "11_LunarStation_LifeSupport"; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("11_LunarStation_LifeSupport"));
            Assert.That(headset.added, Is.True, "第二天场景不能重新启动模拟器并移除实体头显");
            var session = Object.FindAnyObjectByType<StationMissionSession>();
            var story = session.GetComponent<StationExpansionMission>();
            for (int i = 0; i < 400 && !story.IntroComplete; i++) yield return null;
            for (int i = 0; i < 120; i++) yield return null;
            var origin = session.Player;
            var driver = origin.Camera.GetComponent<TrackedPoseDriver>();
            var gravity = origin.GetComponentInChildren<GravityProvider>(true);
            Assert.That(story.IntroComplete, Is.True);
            AssertHardwareOwnsTracking(origin);
            Assert.That(gravity.enabled, Is.True);
            Assert.That(driver.positionInput.action.enabled, Is.True, "换场景后头显位置输入必须保持启用");
            Assert.That(origin.Camera.transform.position.y - session.SpawnPoint.position.y, Is.EqualTo(headPose.y).Within(.1f), "醒来后应保持实体头显相对地面的实际高度");
            SetHeadPose(headPose + new Vector3(.08f, -.25f, .03f));
            yield return null;
            Assert.That(origin.Camera.transform.localPosition.y, Is.EqualTo(headPose.y - .25f).Within(.01f), "醒来后蹲下仍应由头显追踪驱动");
            Assert.That(origin.Camera.transform.position.y, Is.EqualTo(session.SpawnPoint.position.y + headPose.y - .25f).Within(.1f));
            var rotation = Quaternion.Euler(8, 22, 0);
            SetHeadPose(headPose, rotation: rotation);
            yield return null;
            Assert.That(Quaternion.Angle(origin.Camera.transform.localRotation, rotation), Is.LessThan(.1f), "转头仍由真实输入更新");
            SetHeadPose(headPose);

            InputSystem.RegisterLayout<HTCViveControllerProfile.ViveController>();
            controller = InputSystem.AddDevice<HTCViveControllerProfile.ViveController>();
            InputSystem.SetDeviceUsage(controller, "LeftHand");
            var start = origin.Camera.transform.position;
            SetMove(true);
            for (int i = 0; i < 20; i++) yield return null;
            SetMove(false);
            yield return null;
            var stopped = origin.Camera.transform.position;
            Assert.That(Vector3.ProjectOnPlane(stopped - start, Vector3.up).magnitude, Is.GreaterThan(.05f), "醒来后 VIVE 触摸板按压应能行走");
            for (int i = 0; i < 15; i++) yield return null;
            Assert.That(Vector3.ProjectOnPlane(origin.Camera.transform.position - stopped, Vector3.up).magnitude, Is.LessThan(.01f), "松开触摸板应停止");
            Assert.That(origin.Camera.transform.position.y, Is.EqualTo(session.SpawnPoint.position.y + headPose.y).Within(.1f));
        }

        [UnityTest]
        public IEnumerator HeadsetPresentBeforeLoadingEitherDayCannotBeReplacedBySimulator()
        {
            foreach (var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None))
                simulator.gameObject.SetActive(false);
            headset = InputSystem.AddDevice<XRHMD>();
            SetHeadPose(headPose);
            foreach (var scene in new[] { "12_LunarStation_DayOne", "11_LunarStation_LifeSupport" })
            {
                yield return SceneManager.LoadSceneAsync(scene);
                Assert.That(headset.added, Is.True, scene + " 加载期间不得移除已连接的实体头显");
                for (int i = 0; i < 320; i++) yield return null;
                var origin = Object.FindAnyObjectByType<XROrigin>();
                AssertHardwareOwnsTracking(origin);
                Assert.That(origin.Camera.transform.position.y, Is.EqualTo(headPose.y).Within(.1f));
            }
        }

        [UnityTest]
        public IEnumerator HeadsetConnectingDuringWakeTakesOverAndTrackingCanRecover()
        {
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            for (int i = 0; i < 20; i++) yield return null;
            var simulator = Object.FindAnyObjectByType<CollisionAwareSimulator>();
            Assert.That(simulator, Is.Not.Null, "未连接头显时保留键鼠模拟器");
            Assert.That(simulator.isActiveAndEnabled, Is.True);
            headset = InputSystem.AddDevice<XRHMD>();
            Assert.That(headset.added, Is.True, "延迟接入的实体头显不得被模拟设备管理器移除");
            SetHeadPose(headPose, false);
            for (int i = 0; i < 30; i++) yield return null;
            SetHeadPose(headPose);
            for (int i = 0; i < 320; i++) yield return null;
            var origin = Object.FindAnyObjectByType<XROrigin>();
            AssertHardwareOwnsTracking(origin);
            Assert.That(origin.Camera.transform.position.y, Is.EqualTo(headPose.y).Within(.1f));

            SetHeadPose(Vector3.zero, false);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(simulator.gameObject.activeInHierarchy, Is.False, "短暂丢失追踪时不能自动退回模拟头显");
            SetHeadPose(headPose + Vector3.up * .08f);
            yield return null;
            Assert.That(origin.Camera.transform.localPosition.y, Is.EqualTo(headPose.y + .08f).Within(.01f));
        }

        [UnityTest]
        public IEnumerator RaisedHandsLeaveHelmetTasksVisibleWithoutAWristPanel()
        {
            yield return SceneManager.LoadSceneAsync("12_LunarStation_DayOne");
            PreparationTestSteps.DisableSimulator(); headset = InputSystem.AddDevice<XRHMD>(); SetHeadPose(headPose);
            for (int i = 0; i < 360; i++) yield return null;
            InputSystem.RegisterLayout<HTCViveControllerProfile.ViveController>();
            controller = InputSystem.AddDevice<HTCViveControllerProfile.ViveController>(); InputSystem.SetDeviceUsage(controller, "LeftHand");
            using (StateEvent.From(controller, out var state))
            {
                controller.devicePosition.WriteValueIntoEvent(new Vector3(-.20f, 1.39f, .46f), state);
                controller.deviceRotation.WriteValueIntoEvent(Quaternion.Euler(65, 60, 110), state);
                controller.pointerPosition.WriteValueIntoEvent(new Vector3(-.20f, 1.39f, .46f), state);
                controller.pointerRotation.WriteValueIntoEvent(Quaternion.Euler(65, 60, 110), state);
                controller.isTracked.WriteValueIntoEvent(1f, state); controller.trackingState.WriteValueIntoEvent(3, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
            var day = Object.FindAnyObjectByType<DayOneMission>(); var origin = Object.FindAnyObjectByType<XROrigin>();
            var hud = Object.FindAnyObjectByType<SuitHudPresenter>();
            Assert.That(hud.Hud.activeSelf, Is.False); day.Suit.Don();
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(hud.Hud.activeSelf, Is.True, "第一天穿戴后也恢复氧气、电量与任务 HUD");
            Assert.That(hud.WristControls, Is.Null, "第一天不再创建遮挡视线的手腕任务板");
            Assert.That(origin.GetComponentInChildren<ReadableWristDisplay>(true), Is.Null);
            var left = origin.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Left Controller");
            Assert.That(Vector3.Distance(left.localPosition, new Vector3(-.20f, 1.39f, .46f)), Is.LessThan(.01f), "真实手柄的指向姿态经过绑定更新左手");
            var eye = origin.Camera.transform; var rect = hud.Hud.GetComponent<RectTransform>(); var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            // 左右眼分别检查阅读区域，防止只在普通游戏窗口中看着正确。
            foreach (float offset in new[] { -.032f, .032f })
            {
                var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(eye.position + eye.right * offset, eye.rotation, Vector3.one).inverse;
                var projection = Matrix4x4.Perspective(90, 1, .05f, 100);
                foreach (var corner in corners)
                {
                    var clip = projection * view * new Vector4(corner.x, corner.y, corner.z, 1);
                    Assert.That(clip.w, Is.GreaterThan(0)); Assert.That(Mathf.Abs(clip.x / clip.w), Is.LessThan(.98f)); Assert.That(Mathf.Abs(clip.y / clip.w), Is.LessThan(.98f));
                }
            }
            var language = Object.FindAnyObjectByType<LocalizationService>();
            foreach (var value in new[] { GameLanguage.Chinese, GameLanguage.English, GameLanguage.Russian })
            {
                language.SetLanguage(value); day.Refresh(); yield return null;
                foreach (var text in hud.Hud.GetComponentsInChildren<TMPro.TMP_Text>())
                { text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, value + " " + text.text); }
                yield return UsabilityTestSteps.Capture("usability-headset-hud-" + value, eye.position, eye.rotation, 90);
            }
            language.SetLanguage(GameLanguage.Chinese); day.Refresh();
            using (StateEvent.From(controller, out var lowered))
            {
                controller.devicePosition.WriteValueIntoEvent(new Vector3(-.25f, .75f, .12f), lowered);
                controller.pointerPosition.WriteValueIntoEvent(new Vector3(-.25f, .75f, .12f), lowered);
                InputSystem.QueueEvent(lowered);
            }
            InputSystem.Update();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(hud.Hud.activeSelf, Is.True, "放下手后头盔任务指引保持可见");
            yield return UsabilityTestSteps.Capture("usability-helmet-hud-Chinese", eye.position, eye.rotation, 90);
            Assert.That(hud.Oxygen.DisplayedPercent, Is.EqualTo(day.Suit.Oxygen).Within(.02f));
            Assert.That(hud.Power.DisplayedPercent, Is.EqualTo(day.Suit.Power).Within(.02f));
            Assert.That(hud.Hud.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.Contains(day.CurrentTask)), Is.True);
            UsabilityTestSteps.ReturnSuit(day); yield return null;
            Assert.That(hud.Hud.activeSelf, Is.False);
        }

        private void AssertHardwareOwnsTracking(XROrigin origin)
        {
            Assert.That(headset.added, Is.True);
            foreach (var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.That(simulator.gameObject.activeInHierarchy, Is.False, "实体 VR 下整个模拟器及其设备管理器必须停用");
            foreach (var device in InputSystem.devices)
                Assert.That(device, Is.Not.InstanceOf<XRSimulatedHMD>(), "虚拟头显不得与实体头显竞争绑定");
            Assert.That(origin.Camera.GetComponent<TrackedPoseDriver>().isActiveAndEnabled, Is.True);
        }

        private void SetMove(bool pressed)
        {
            using (StateEvent.From(controller, out var state))
            {
                controller.trackpad.WriteValueIntoEvent(Vector2.up, state);
                controller.trackpadTouched.WriteValueIntoEvent(1f, state);
                controller.trackpadClicked.WriteValueIntoEvent(pressed ? 1f : 0f, state);
                controller.isTracked.WriteValueIntoEvent(1f, state);
                controller.trackingState.WriteValueIntoEvent(3, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }
    }
}
