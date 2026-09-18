using System.Collections;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace LunarEscape.Tests
{
    // 使用场景内的真实工具和玩家身体，避免只检查配置却漏掉重复施加重力。
    public sealed class LunarGravityTests
    {
        private const float LunarAcceleration = 1.62f;
        private float previousCaptureDeltaTime;
        private float previousFixedDeltaTime;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

        [SetUp]
        public void UseDeterministicFrameDuration()
        {
            previousCaptureDeltaTime = Time.captureDeltaTime;
            previousFixedDeltaTime = Time.fixedDeltaTime;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = Time.fixedDeltaTime = 0.02f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [UnitySetUp]
        public IEnumerator LoadMissionRoom()
        {
            yield return SceneManager.LoadSceneAsync("06_LunarStation_Evacuation", LoadSceneMode.Single);
            for (int frame = 0; frame < 5; frame++) yield return null;
            // 重力必须由项目配置提供；测试不把 Physics.gravity 改成期望值。
            Assert.That(Physics.gravity, Is.EqualTo(new Vector3(0f, -LunarAcceleration, 0f)));
        }

        [TearDown]
        public void RestoreTimingAndInputSettings()
        {
            Time.captureDeltaTime = previousCaptureDeltaTime;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        }

        [UnityTest]
        public IEnumerator ReleasedToolFallsOneMetreWithLunarAcceleration()
        {
            var reset = Object.FindAnyObjectByType<ReturnFallenTool>();
            var grab = reset.GetComponent<XRGrabInteractable>();
            var body = reset.GetComponent<Rigidbody>();
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var handObject = new GameObject("Lunar gravity test hand");
            var hand = handObject.AddComponent<XRRayInteractor>();
            hand.interactionManager = manager;
            bool originalThrow = grab.throwOnDetach;
            try
            {
                Assert.That(grab.throwVelocityScale, Is.EqualTo(1f), "投掷不能额外放大释放速度。");
                // 真实抓取/释放之后清除初速度，单独测自由落体。
                grab.throwOnDetach = false;
                yield return null;
                manager.SelectEnter((IXRSelectInteractor)hand, grab);
                Assert.That(grab.isSelected, Is.True);
                manager.SelectExit((IXRSelectInteractor)hand, grab);
                yield return null;
                yield return null;
                Assert.That(grab.isSelected, Is.False);
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.useGravity, Is.True);
                Assert.That(body.linearDamping, Is.Zero);

                // 房间右后方没有桌面；整个 1 米下落都不会接触墙、地板或玩家。
                body.position = new Vector3(2f, 2.3f, -2.5f);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
                Physics.SyncTransforms();
                float startHeight = body.position.y;
                double startTime = Time.fixedTimeAsDouble;
                for (int step = 0; step < 85 && startHeight - body.position.y < 1f; step++)
                    yield return new WaitForFixedUpdate();

                float elapsed = (float)(Time.fixedTimeAsDouble - startTime);
                float measuredAcceleration = -body.linearVelocity.y / elapsed;
                TestContext.WriteLine($"Released tool: g={measuredAcceleration:F3} m/s², 1 m fall={elapsed:F3} s.");
                Assert.That(startHeight - body.position.y, Is.GreaterThanOrEqualTo(1f));
                Assert.That(measuredAcceleration, Is.EqualTo(LunarAcceleration).Within(0.08f));
                Assert.That(elapsed, Is.InRange(1.05f, 1.18f),
                    "月球重力下静止下落 1 米应约 1.11 秒（允许物理步长误差）。");
            }
            finally
            {
                if (grab.isSelected) manager.SelectExit((IXRSelectInteractor)hand, grab);
                grab.throwOnDetach = originalThrow;
                reset.ResetToStart();
                Object.Destroy(handObject);
            }
        }

        [UnityTest]
        public IEnumerator AirbornePlayerBodyReceivesLunarGravityOnce()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var gravity = origin.GetComponentInChildren<GravityProvider>();
            var body = origin.GetComponent<CharacterController>();
            Assert.That(gravity, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(gravity.useGravity, Is.True);
            Assert.That(gravity.gravityAccelerationModifier, Is.EqualTo(1f));
            Assert.That(body.enabled, Is.True);
            var player = origin.Origin.transform;
            Vector3 initialPosition = player.position;
            try
            {
                body.enabled = false;
                player.position = new Vector3(2f, 1.2f, -2.5f);
                body.enabled = true;
                gravity.ResetFallForce();
                Physics.SyncTransforms();
                yield return null;
                float previousHeight = player.position.y;
                yield return null;
                float firstVelocity = (player.position.y - previousHeight) / Time.deltaTime;
                double firstSampleTime = Time.timeAsDouble;
                float lastVelocity = firstVelocity;

                // 测量 CharacterController 实际位移的加速度：若另一路重力叠加也会失败。
                for (int frame = 0; frame < 20; frame++)
                {
                    Assert.That(gravity.isGrounded, Is.False, "测量期间玩家必须仍在空中。");
                    previousHeight = player.position.y;
                    yield return null;
                    lastVelocity = (player.position.y - previousHeight) / Time.deltaTime;
                }

                float elapsed = (float)(Time.timeAsDouble - firstSampleTime);
                float measuredAcceleration = -(lastVelocity - firstVelocity) / elapsed;
                TestContext.WriteLine($"Actual player body: g={measuredAcceleration:F3} m/s².");
                Assert.That(player.position.y, Is.LessThan(1.1f), "玩家身体必须真正下降。");
                Assert.That(measuredAcceleration, Is.EqualTo(LunarAcceleration).Within(0.08f),
                    "玩家应沿用同一个 1.62 m/s² 重力，不能再次乘以六分之一或重复叠加。");
            }
            finally
            {
                body.enabled = false;
                player.position = initialPosition;
                body.enabled = true;
                gravity.ResetFallForce();
                Physics.SyncTransforms();
            }
        }
    }
}
