using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LunarEscape.Tests
{
    // 经过真实模拟器 Update 和官方身体变换，不直接调用 CharacterController.Move。
    public sealed class SimulatorMovementTests
    {
        private CollisionAwareSimulator simulator;
        private XROrigin origin;
        private Transform head;
        private float previousCaptureDeltaTime;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

        [SetUp]
        public void UseFixedFrameDuration()
        {
            previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            // 批量测试没有聚焦的 Game 窗口，但仍需让模拟设备驱动真实手柄姿态。
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void RestoreFrameDuration()
        {
            if (simulator != null) SetTranslation(Vector3.zero);
            Time.captureDeltaTime = previousCaptureDeltaTime;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        }

        [UnityTest]
        public IEnumerator BothScenesHaveCollisionAdapterAndSolidRoom()
        {
            foreach (var scene in new[] { "04_LunarStation_Foundation", "05_LunarStation_Repair" })
            {
                yield return LoadRoom(scene);
                Assert.That(Object.FindObjectsByType<XRInteractionSimulator>().Length, Is.EqualTo(1));
                var transformer = Object.FindAnyObjectByType<XRBodyTransformer>();
                Assert.That(transformer, Is.Not.Null);
                Assert.That(transformer.xrOrigin, Is.SameAs(origin));
                Assert.That(transformer.constrainedBodyManipulator,
                    Is.InstanceOf<CharacterControllerBodyManipulator>());
                var capsule = ((CharacterControllerBodyManipulator)transformer.constrainedBodyManipulator).characterController;
                Assert.That(capsule, Is.Not.Null);
                Assert.That(capsule.enabled, Is.True);
                foreach (var name in new[] { "Floor - teleport surface", "Wall Rear", "Wall Systems", "Wall Left", "Wall Right" })
                {
                    var collider = GameObject.Find(name).GetComponent<BoxCollider>();
                    Assert.That(collider, Is.Not.Null, scene + ": " + name);
                    Assert.That(collider.enabled && !collider.isTrigger, Is.True, name);
                    Assert.That(Physics.GetIgnoreLayerCollision(capsule.gameObject.layer, collider.gameObject.layer),
                        Is.False, name + " 必须与玩家碰撞。");
                }
            }
        }

        [UnityTest]
        public IEnumerator FpsAndHeadMovementStopAtRoomWall()
        {
            foreach (var mode in new[] { TargetedDevices.FPS, TargetedDevices.HMD, TargetedDevices.HMD | TargetedDevices.LeftDevice })
            {
                yield return LoadRoom("05_LunarStation_Repair");
                yield return SelectMode(mode);
                var startBody = origin.Origin.transform.position;
                var startHeadLocal = head.localPosition;
                var wall = GameObject.Find("Wall Right").GetComponent<Collider>();

                SetTranslation(Vector3.right);
                yield return Frames(75);
                var stoppedPosition = head.position;
                yield return Frames(15);
                SetTranslation(Vector3.zero);
                yield return Frames(2);

                Assert.That(origin.Origin.transform.position.x, Is.GreaterThan(startBody.x + 0.5f),
                    mode + " 必须真正推动玩家身体，不能靠禁用移动通过测试。");
                Assert.That(head.position.x, Is.LessThanOrEqualTo(wall.bounds.min.x + 0.03f));
                Assert.That(head.position.x, Is.EqualTo(stoppedPosition.x).Within(0.05f),
                    mode + " 持续向墙移动后必须停在墙内。");
                Assert.That(head.localPosition.x, Is.EqualTo(startHeadLocal.x).Within(0.01f),
                    "移动必须由身体变换执行，不能把模拟头显独自挪出胶囊体。");
                Assert.That(origin.Origin.transform.position.y, Is.GreaterThanOrEqualTo(-0.1f),
                    "撞墙后不能掉到地板下方。");
            }
        }

        [UnityTest]
        public IEnumerator FpsWalkingCannotPassThroughWorkbench()
        {
            yield return LoadRoom("05_LunarStation_Repair");
            yield return SelectMode(TargetedDevices.FPS);
            float startZ = head.position.z;
            var bench = GameObject.Find("Workbench Surface").GetComponent<Collider>();
            SetTranslation(Vector3.forward);
            yield return Frames(60);
            float stoppedZ = head.position.z;
            yield return Frames(15);
            SetTranslation(Vector3.zero);
            yield return Frames(2);

            Assert.That(head.position.z, Is.GreaterThan(startZ + 0.5f));
            Assert.That(head.position.z, Is.LessThanOrEqualTo(bench.bounds.min.z + 0.09f),
                "玩家应被桌面前缘阻挡，不能穿过维修台。");
            Assert.That(head.position.z, Is.EqualTo(stoppedZ).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator FpsAndHeadVerticalInputsCannotEnterFloorOrFly()
        {
            yield return LoadRoom("05_LunarStation_Repair");
            foreach (var mode in new[] { TargetedDevices.FPS, TargetedDevices.HMD, TargetedDevices.HMD | TargetedDevices.LeftDevice })
            {
                yield return SelectMode(mode);
                var initialHead = head.position;
                var initialHeadLocal = head.localPosition;
                SetTranslation(Vector3.down);
                yield return Frames(30);
                Assert.That(head.position.y, Is.EqualTo(initialHead.y).Within(0.03f), mode + " 的向下输入不能穿地。");
                SetTranslation(Vector3.up);
                yield return Frames(30);
                SetTranslation(Vector3.zero);
                yield return Frames(2);
                Assert.That(head.position.y, Is.EqualTo(initialHead.y).Within(0.03f), mode + " 的向上输入不能飞行。");
                Assert.That(head.localPosition.y, Is.EqualTo(initialHeadLocal.y).Within(0.01f));
                Assert.That(origin.Origin.transform.position.y, Is.GreaterThanOrEqualTo(-0.1f));
            }
        }

        [UnityTest]
        public IEnumerator ControllerModeStillMovesHandUpAndDownWithoutMovingBody()
        {
            yield return LoadRoom("05_LunarStation_Repair");
            yield return SelectMode(TargetedDevices.RightDevice);
            Assert.That(simulator.deviceLifecycleManager.deviceMode,
                Is.EqualTo(SimulatedDeviceLifecycleManager.DeviceMode.Controller));
            var modality = origin.GetComponent<XRInputModalityManager>();
            Assert.That(modality, Is.Not.Null);
            Assert.That(modality.rightController, Is.Not.Null);
            var driver = modality.rightController.GetComponentInChildren<TrackedPoseDriver>(true);
            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.isActiveAndEnabled, Is.True, "真实右控制器的追踪组件必须处于活动状态。");
            // 模拟器的辅助引用可能回退到摄像机；验证场景内真正由设备驱动的手柄。
            var hand = driver.transform;
            Assert.That(hand, Is.Not.SameAs(head));
            var device = InputSystem.devices.OfType<XRSimulatedController>()
                .Single(controller => controller.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand));
            Assert.That(device.isTracked.isPressed, Is.True);
            float initialDeviceHeight = device.devicePosition.ReadValue().y;
            var initialHand = hand.position;
            var initialHead = head.position;
            var initialBody = origin.Origin.transform.position;

            SetTranslation(Vector3.up);
            yield return Frames(20);
            SetTranslation(Vector3.zero);
            yield return Frames(2);
            float raisedHeight = hand.position.y;
            Assert.That(device.devicePosition.ReadValue().y, Is.GreaterThan(initialDeviceHeight + 0.3f),
                "向上输入必须先改变模拟右控制器的设备状态。");
            Assert.That(raisedHeight, Is.GreaterThan(initialHand.y + 0.3f),
                "手柄模式必须保留向上操作工具的能力。");

            SetTranslation(Vector3.down);
            yield return Frames(20);
            SetTranslation(Vector3.zero);
            yield return Frames(2);
            Assert.That(device.devicePosition.ReadValue().y, Is.EqualTo(initialDeviceHeight).Within(0.05f));
            Assert.That(hand.position.y, Is.LessThan(raisedHeight - 0.3f));
            Assert.That(hand.position.y, Is.EqualTo(initialHand.y).Within(0.05f));
            Assert.That(Vector3.Distance(head.position, initialHead), Is.LessThan(0.03f));
            Assert.That(Vector3.Distance(origin.Origin.transform.position, initialBody), Is.LessThan(0.03f));
        }

        private IEnumerator LoadRoom(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            simulator = Object.FindAnyObjectByType<CollisionAwareSimulator>();
            origin = Object.FindAnyObjectByType<XROrigin>();
            Assert.That(simulator, Is.Not.Null, scene + " 缺少碰撞移动适配器。");
            Assert.That(origin, Is.Not.Null);
            head = origin.Camera.transform;
            simulator.usePointAndClick = false;
            simulator.translateXSpeed = simulator.translateZSpeed = 2f;
            simulator.translateYSpeed = 0.5f;
            simulator.bodyTranslateMultiplier = 2f;
            simulator.keyboardRotationDeltaInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.keyboardRotationDeltaInput.manualValue = Vector2.zero;
            simulator.mouseRotationDeltaInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.mouseRotationDeltaInput.manualValue = Vector2.zero;
            simulator.mouseScrollInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            simulator.mouseScrollInput.manualValue = Vector2.zero;
            SetTranslation(Vector3.zero);
            // 官方 FPS 在启动首秒等待设备初始化；使用确定帧时长快速越过它。
            while (Time.time <= 1.05f) yield return null;
            yield return Frames(5);
        }

        private IEnumerator SelectMode(TargetedDevices mode)
        {
            // 3.6 的 currentState 只读；这个公开 setter 会在下一帧应用模式。
#pragma warning disable CS0618
            simulator.targetedDeviceInput = mode;
#pragma warning restore CS0618
            yield return Frames(2);
            Assert.That(simulator.currentState.targetedDeviceInput, Is.EqualTo(mode));
        }

        private void SetTranslation(Vector3 input)
        {
            SetManual(simulator.translateXInput, input.x);
            SetManual(simulator.translateYInput, input.y);
            SetManual(simulator.translateZInput, input.z);
        }

        private static void SetManual(XRInputValueReader<float> reader, float value)
        {
            reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            reader.manualValue = value;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }
    }
}
