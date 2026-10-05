using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Unity.XR.CoreUtils;

namespace LunarEscape.Tests
{
    // 使用官方 Vive 布局注入按压、触摸与追踪状态，验证实际场景读取的绑定。
    public sealed class ViveControlsTests
    {
        private HTCViveControllerProfile.ViveController left, right;
        private ContinuousMoveProvider move;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private float frameDuration;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            background = InputSystem.settings.backgroundBehavior;
            frameDuration = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.RegisterLayout<HTCViveControllerProfile.ViveController>();
            left = InputSystem.AddDevice<HTCViveControllerProfile.ViveController>();
            right = InputSystem.AddDevice<HTCViveControllerProfile.ViveController>();
            InputSystem.SetDeviceUsage(left, "LeftHand"); InputSystem.SetDeviceUsage(right, "RightHand");
            yield return SceneManager.LoadSceneAsync("10_LunarStation_Docking");
            yield return null;
            move = Object.FindAnyObjectByType<ContinuousMoveProvider>();
            Assert.That(move, Is.Not.Null);
        }

        [TearDown]
        public void Cleanup()
        {
            if (left != null && left.added) InputSystem.RemoveDevice(left);
            if (right != null && right.added) InputSystem.RemoveDevice(right);
            InputSystem.settings.backgroundBehavior = background;
            Time.captureDeltaTime = frameDuration;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        }

        private static void Set(HTCViveControllerProfile.ViveController device, Vector2 axis, bool click, bool tracked = true)
        {
            using (StateEvent.From(device, out var state))
            {
                device.trackpad.WriteValueIntoEvent(axis, state);
                device.trackpadTouched.WriteValueIntoEvent(1f, state);
                device.trackpadClicked.WriteValueIntoEvent(click ? 1f : 0f, state);
                device.isTracked.WriteValueIntoEvent(tracked ? 1f : 0f, state);
                device.trackingState.WriteValueIntoEvent(tracked ? 3 : 0, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }

        [Test]
        public void BothHandsRequirePhysicalClickAndTracking()
        {
            foreach (var device in new[] { left, right })
            {
                var action = device == left ? move.leftHandMoveInput.inputActionReference.action : move.rightHandMoveInput.inputActionReference.action;
                Assert.That(action.enabled, Is.True);
                Set(device, Vector2.up, false);
                Assert.That(action.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "仅触摸不能移动");
                Set(device, Vector2.up, true);
                Assert.That(action.ReadValue<Vector2>().y, Is.GreaterThan(.9f), "先触摸后按压也应前进");
                Set(device, Vector2.down, true);
                Assert.That(action.ReadValue<Vector2>().y, Is.LessThan(-.9f), "按住下半区应后退");
                Set(device, new Vector2(1, .1f), true);
                Assert.That(action.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "左右边缘和中心抖动不能行走");
                Set(device, Vector2.up, false);
                Assert.That(action.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "松开后即使仍触摸也应停止");
                Set(device, Vector2.up, true, false);
                Assert.That(action.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "追踪丢失应停止");
                Set(device, Vector2.up, true);
                InputSystem.RemoveDevice(device);
                Assert.That(action.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "断开手柄应停止");
            }
        }

        [UnityTest]
        public IEnumerator PressMovesBodyReleaseStopsAndNeverTurns()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var head = origin.Camera.transform;
            head.localPosition = new Vector3(0, 1.65f, 0); head.localRotation = Quaternion.identity;
            var rotation = origin.transform.rotation;
            var start = origin.transform.position;
            Set(left, Vector2.up, false);
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(Vector2.Distance(new Vector2(start.x, start.z), new Vector2(origin.transform.position.x, origin.transform.position.z)), Is.LessThan(.005f));
            Set(left, Vector2.up, true);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(origin.transform.position.z, Is.GreaterThan(start.z + .01f), "必须真正移动玩家，不能仅让绑定测试通过");
            Set(left, Vector2.up, false);
            yield return null;
            var stopped = origin.transform.position;
            Set(right, Vector2.right, true);
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(origin.transform.position.z, Is.EqualTo(stopped.z).Within(.005f));
            Assert.That(Quaternion.Angle(rotation, origin.transform.rotation), Is.LessThan(.01f));
            Assert.That(Object.FindObjectsByType<SnapTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(p => !p.enabled));
            Assert.That(Object.FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(p => !p.enabled));
            Assert.That(Object.FindObjectsByType<TeleportationProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(p => !p.enabled));
        }

        [Test]
        public void ArmsKeepBoneLengthsAndReturnToRestWhenTrackingIsLost()
        {
            var driver = Object.FindAnyObjectByType<TrackedCrewSuit>();
            var bones = driver.Suit.Bones;
            var lengths = bones.Select(b => b.localPosition.magnitude).ToArray();
            Set(left, Vector2.zero, false); Set(right, Vector2.zero, false);
            // 超出人体可达范围的输入不得把手腕和袖口扯长，也不得影响真实追踪节点。
            driver.LeftController.position = driver.transform.position + new Vector3(-3, 3, 2);
            driver.RightController.position = driver.transform.position + new Vector3(3, 3, 2);
            var trackedPosition = driver.LeftController.position;
            for (int frame = 0; frame < 120; frame++) driver.SendMessage("LateUpdate");
            Assert.That(Quaternion.Angle(bones[3].localRotation, Quaternion.identity), Is.GreaterThan(10));
            for (int i = 3; i < bones.Length; i++) Assert.That(bones[i].localPosition.magnitude, Is.EqualTo(lengths[i]).Within(.0001f));
            Assert.That(driver.LeftController.position, Is.EqualTo(trackedPosition));
            Set(left, Vector2.zero, false, false); Set(right, Vector2.zero, false, false);
            for (int frame = 0; frame < 120; frame++) driver.SendMessage("LateUpdate");
            for (int i = 3; i < bones.Length; i++) Assert.That(Quaternion.Angle(bones[i].localRotation, Quaternion.identity), Is.LessThan(.1f));
        }
    }
}
