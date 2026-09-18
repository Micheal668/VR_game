using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LunarEscape.Tests
{
    public sealed class FailureSummaryTests
    {
        private StationMissionSession session;
        private StationMission mission;
        private MissionFailureLock failureLock;
        private MissionFailurePresenter presenter;
        private CollisionAwareSimulator simulator;
        private MissionTeleportationProvider teleport;
        private XRGrabInteractable grab;
        private Rigidbody toolBody;
        private LocalizationService localization;
        private float previousFrameDuration;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousFocus;

        [SetUp]
        public void EnableDeterministicDeviceUpdates()
        {
            previousFrameDuration = Time.captureDeltaTime;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = 1f / 30f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("06_LunarStation_Evacuation", LoadSceneMode.Single);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>();
            Assert.That(session, Is.Not.Null);
            session.enabled = false;
            mission = session.Mission;
            failureLock = Object.FindAnyObjectByType<MissionFailureLock>();
            presenter = Object.FindAnyObjectByType<MissionFailurePresenter>();
            simulator = Object.FindAnyObjectByType<CollisionAwareSimulator>();
            teleport = session.Player.GetComponentInChildren<MissionTeleportationProvider>(true);
            grab = Object.FindAnyObjectByType<RepairTool>().GetComponent<XRGrabInteractable>();
            toolBody = grab.GetComponent<Rigidbody>();
            localization = Object.FindAnyObjectByType<LocalizationService>();
            Assert.That(failureLock, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(teleport, Is.Not.Null);
            SetMovement(Vector3.zero);
            SetVectorInput(simulator.keyboardRotationDeltaInput, Vector2.zero);
            SetVectorInput(simulator.mouseRotationDeltaInput, Vector2.zero);
            SetVectorInput(simulator.mouseScrollInput, Vector2.zero);
            while (Time.time <= 1.05f) yield return null;
            yield return Frames(3);
        }

        [TearDown]
        public void RestoreDeviceSettings()
        {
            if (simulator != null)
            {
                SetMovement(Vector3.zero);
                SetVectorInput(simulator.keyboardRotationDeltaInput, Vector2.zero);
            }
            Time.captureDeltaTime = previousFrameDuration;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
        }

        [UnityTest]
        public IEnumerator TimeoutAutomaticallyShowsOneModalAndLeavesOnlyRestartInteractive()
        {
            Assert.That(presenter.IsVisible, Is.False);
            Assert.That(failureLock.IsLocked, Is.False);
            StartEvacuation();
            session.Advance(mission.RemainingSeconds - 0.01f);
            // 最后一步交给真实Update，验证倒计时自动弹出结算而不是测试主动显示窗口。
            session.enabled = true;
            yield return Frames(2);
            session.enabled = false;

            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Failed));
            Assert.That(mission.RemainingSeconds, Is.Zero);
            Assert.That(mission.FailureReason, Is.EqualTo(StationMissionFailure.EvacuationTimeout));
            Assert.That(failureLock.IsLocked, Is.True);
            Assert.That(simulator.BodyMovementEnabled, Is.False);
            Assert.That(presenter.IsVisible, Is.True);
            Assert.That(SceneComponents<Transform>().Count(item => item.name == "Failure Summary"), Is.EqualTo(1));
            var interactive = SceneComponents<Button>()
                .Where(button => button.isActiveAndEnabled && button.IsInteractable()).ToArray();
            Assert.That(interactive, Is.EqualTo(new[] { presenter.RestartButton }),
                "失败后不能留下语言、旧重试或其他可点击按钮。");
            Assert.That(presenter.RestartButton.name, Is.EqualTo("Restart After Failure"));
            Assert.That(presenter.Root.GetComponent<GraphicRaycaster>().enabled, Is.True);
            Assert.That(presenter.Root.GetComponent<TrackedDeviceGraphicRaycaster>().enabled, Is.True);
            foreach (var provider in session.Player.GetComponentsInChildren<LocomotionProvider>(true))
                Assert.That(provider.enabled, Is.False, provider.name + " 仍能请求移动。");
            mission.Begin();
            session.Advance(100f);
            Assert.That(failureLock.IsLocked && presenter.IsVisible, Is.True);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Failed));
        }

        [UnityTest]
        public IEnumerator FailedSimulatorCannotWalkButHeadAndHandTrackingContinue()
        {
            FailMission();
            yield return Frames(2);
            var origin = session.Player.Origin.transform;
            Vector3 deathPosition = origin.position;
            simulator.translateXSpeed = simulator.translateZSpeed = 2f;
            simulator.bodyTranslateMultiplier = 2f;
            foreach (var mode in new[] { TargetedDevices.FPS, TargetedDevices.HMD, TargetedDevices.HMD | TargetedDevices.LeftDevice })
            {
                yield return SelectMode(mode);
                SetMovement(new Vector3(1f, -1f, 1f));
                yield return Frames(15);
                SetMovement(Vector3.zero);
                Assert.That(Vector3.Distance(origin.position, deathPosition), Is.LessThan(0.01f),
                    mode + " 的模拟移动不能逃离失败锁定。");
            }

            yield return SelectMode(TargetedDevices.HMD);
            var head = session.Player.Camera.transform;
            Quaternion initialLook = head.rotation;
            SetVectorInput(simulator.keyboardRotationDeltaInput, new Vector2(4f, 0f));
            yield return Frames(15);
            SetVectorInput(simulator.keyboardRotationDeltaInput, Vector2.zero);
            Assert.That(Quaternion.Angle(head.rotation, initialLook), Is.GreaterThan(5f),
                "失败锁不能停止头部追踪和转头。");

            simulator.usePointAndClick = false;
            yield return SelectMode(TargetedDevices.RightDevice);
            var modality = session.Player.GetComponent<XRInputModalityManager>();
            var hand = modality.rightController.GetComponentInChildren<TrackedPoseDriver>(true);
            Assert.That(hand.isActiveAndEnabled, Is.True);
            float initialHandHeight = hand.transform.position.y;
            simulator.translateYSpeed = 0.5f;
            SetMovement(Vector3.up);
            yield return Frames(15);
            SetMovement(Vector3.zero);
            yield return Frames(2);
            Assert.That(hand.transform.position.y, Is.GreaterThan(initialHandHeight + 0.15f),
                "失败后手部追踪仍需工作，才能操作射线UI。");
            Assert.That(Vector3.Distance(origin.position, deathPosition), Is.LessThan(0.01f));
            Assert.That(simulator.isActiveAndEnabled, Is.True);
            Assert.That(failureLock.IsLocked, Is.True);
        }

        [UnityTest]
        public IEnumerator FailedGrabAndTeleportAreBlockedAndPendingTeleportCannotReplayAfterRetry()
        {
            var originalConstraints = toolBody.constraints;
            var rayObject = new GameObject("Failure Grab Input Hand");
            var ray = rayObject.AddComponent<XRRayInteractor>();
            ray.interactionManager = Object.FindAnyObjectByType<XRInteractionManager>();
            ray.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            AimAtTool(ray);
            yield return Frames(2);
            try
            {
                ray.selectInput.QueueManualState(true, 1f);
                yield return Frames(3);
                Assert.That(grab.isSelected, Is.True, "先证明这条真实射线输入在正常状态下确实能抓住工具。");
                var pending = new TeleportRequest
                {
                    destinationPosition = new Vector3(2f, 0f, -1f),
                    destinationRotation = Quaternion.identity,
                    matchOrientation = MatchOrientation.None
                };
                Assert.That(teleport.QueueTeleportRequest(pending), Is.True);
                // 同一帧失败，在提供者Update消费请求前撤销它。
                FailMission();
                Vector3 deathPosition = session.Player.Origin.transform.position;
                Vector3 frozenToolPosition = toolBody.position;
                Assert.That(grab.isSelected, Is.False);
                Assert.That(grab.enabled, Is.False);
                Assert.That(toolBody.constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
                Assert.That(teleport.CanTeleport, Is.False);
                Assert.That(teleport.QueueTeleportRequest(pending), Is.False);
                ray.selectInput.QueueManualState(false, 0f);
                yield return Frames(2);
                AimAtTool(ray);
                ray.selectInput.QueueManualState(true, 1f);
                yield return Frames(5);
                Assert.That(grab.isSelected, Is.False, "再次按下抓取输入不能选中死亡后的工具。");
                Assert.That(Vector3.Distance(toolBody.position, frozenToolPosition), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(session.Player.Origin.transform.position, deathPosition), Is.LessThan(0.01f));

                ray.selectInput.QueueManualState(false, 0f);
                session.RetryMission();
                yield return Frames(5);
                AssertAtSpawn();
                Assert.That(grab.enabled, Is.True);
                Assert.That(toolBody.isKinematic, Is.False);
                Assert.That(toolBody.constraints, Is.EqualTo(originalConstraints));
                AimAtTool(ray);
                yield return Frames(2);
                ray.selectInput.QueueManualState(true, 1f);
                yield return Frames(3);
                Assert.That(grab.isSelected, Is.True, "重新开始后原来的射线抓取应恢复。");
                ray.selectInput.QueueManualState(false, 0f);
                yield return Frames(2);
                Assert.That(teleport.CanTeleport, Is.True);
                Assert.That(teleport.QueueTeleportRequest(pending), Is.True);
                yield return Frames(5);
                Assert.That(Vector3.ProjectOnPlane(session.Player.Camera.transform.position -
                    pending.destinationPosition, Vector3.up).magnitude, Is.LessThan(0.05f),
                    "重新开始后新发出的传送请求应恢复工作。");
            }
            finally
            {
                Object.Destroy(rayObject);
            }
        }

        [UnityTest]
        public IEnumerator ModalBlocksBackgroundClicksAndRealMouseRestartRestoresSpawnAndControls()
        {
            localization.SetLanguage(GameLanguage.Russian);
            yield return SelectMode(TargetedDevices.RightDevice);
            Assert.That(simulator.pointAndClickActive, Is.True,
                "先确认手柄点选确实接管了输入，避免只测原本就可用的鼠标模式。");
            Vector3 toolStart = toolBody.position;
            MovePlayerHorizontally(new Vector3(1f, 0f, -1.8f));
            toolBody.position = new Vector3(1.4f, 1.5f, -0.5f);
            FailMission();
            yield return Frames(2);
            Assert.That(simulator.currentState.targetedDeviceInput, Is.EqualTo(TargetedDevices.RightDevice),
                "死亡结算应直接允许鼠标重开，不要求玩家先退出手柄模式。");
            var module = EventSystem.current.currentInputModule as XRUIInputModule;
            Assert.That(module, Is.Not.Null);
            Assert.That(module.enableMouseInput, Is.True);
            var backgroundLanguage = FindObject("Mission Panel Language 1").GetComponent<Button>();
            var backgroundRetry = FindObject("Retry Mission").GetComponent<Button>();
            Assert.That(backgroundLanguage.IsInteractable(), Is.False);
            Assert.That(backgroundRetry.IsInteractable(), Is.False);
            var mouse = InputSystem.AddDevice<Mouse>("Failure Modal Test Mouse");
            try
            {
                Vector2 blockedPoint = ScreenPoint(backgroundLanguage);
                var hits = Raycast(blockedPoint);
                Assert.That(hits.Any(hit => hit.gameObject.GetComponentInParent<Button>() == backgroundLanguage), Is.False);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(
                    presenter.RestartButton.GetComponent<RectTransform>(), blockedPoint,
                    presenter.RestartButton.GetComponentInParent<Canvas>().worldCamera), Is.False,
                    "背后按钮测试点不能恰巧落在合法的重新开始按钮上。");
                yield return MouseClick(mouse, blockedPoint);
                Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Failed));
                Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.Russian));

                Vector2 restartPoint = ScreenPoint(presenter.RestartButton);
                var restartHits = Raycast(restartPoint);
                Assert.That(restartHits.Count, Is.GreaterThan(0));
                Assert.That(restartHits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(presenter.RestartButton),
                    "结算按钮必须优先接收真实鼠标射线。");
                yield return MouseClick(mouse, restartPoint);
                Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
                Assert.That(failureLock.IsLocked, Is.False);
                Assert.That(presenter.IsVisible, Is.False);
                Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.Russian));
                Assert.That(simulator.BodyMovementEnabled, Is.True);
                Assert.That(session.Exit.PlayerBody.enabled, Is.True);
                AssertAtSpawn();
                Assert.That(grab.enabled && !grab.isSelected, Is.True);
                Assert.That(toolBody.isKinematic, Is.False);
                Assert.That(Vector3.Distance(toolBody.position, toolStart), Is.LessThan(0.15f));
                Assert.That(mission.RepairTask.Progress, Is.Zero);
                Assert.That(mission.RemainingSeconds, Is.Zero);

                // 重开后恢复正常手柄模式语义；原场景鼠标UI仍按约定先切到Head。
                yield return SelectMode(TargetedDevices.HMD);
                Assert.That(backgroundLanguage.IsInteractable(), Is.True);
                yield return MouseClick(mouse, ScreenPoint(backgroundLanguage));
                Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.English),
                    "复位后原场景UI也必须恢复实际点击。");
                Vector3 initialPosition = session.Player.Origin.transform.position;
                simulator.translateXSpeed = 2f;
                simulator.bodyTranslateMultiplier = 2f;
                SetMovement(Vector3.right);
                yield return Frames(15);
                SetMovement(Vector3.zero);
                Assert.That(session.Player.Origin.transform.position.x, Is.GreaterThan(initialPosition.x + 0.5f),
                    "重新开始后不能只隐藏弹窗而忘记解锁行走。");
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator FailureSummaryExplainsBothRepairOutcomesInAllThreeLanguagesWithoutOverflow()
        {
            foreach (bool restored in new[] { false, true })
            {
                foreach (GameLanguage language in new[] { GameLanguage.Chinese, GameLanguage.English, GameLanguage.Russian })
                {
                    session.RetryMission();
                    localization.SetLanguage(language);
                    session.BeginMission();
                    if (restored)
                        mission.Tick(mission.RepairTask.DurationSeconds + mission.Config.StabilizedSeconds, true, false);
                    else session.Advance(mission.Config.RepairWindowSeconds);
                    session.Advance(mission.RemainingSeconds);
                    Assert.That(presenter.IsVisible, Is.True);
                    Assert.That(mission.RepairRestored, Is.EqualTo(restored));
                    var summary = FindObject("Failure Repair Summary").GetComponent<LocalizedText>();
                    Assert.That(summary.Key, Is.EqualTo(restored ? "failure.summary.repaired" : "failure.summary.unrepaired"));
                    Canvas.ForceUpdateCanvases();
                    foreach (var localized in presenter.Root.GetComponentsInChildren<LocalizedText>())
                    {
                        var label = localized.GetComponent<TMP_Text>();
                        Assert.That(localized.Key, Does.StartWith("failure."));
                        Assert.That(label.text, Is.EqualTo(localization.Format(localized.Key)));
                        Assert.That(label.text, Is.Not.Empty);
                        foreach (char character in label.text.Where(character => !char.IsWhiteSpace(character)).Distinct())
                            Assert.That(label.font.HasCharacter(character), Is.True,
                                language + ": " + label.name + " 缺少字符 " + character);
                        label.ForceMeshUpdate();
                        Assert.That(label.isTextOverflowing, Is.False,
                            language + ": " + label.name + " 文字超出死亡结算面板。");
                    }
                    Assert.That(presenter.Root.GetComponentsInChildren<Button>().Length, Is.EqualTo(1));
                    Assert.That(presenter.RestartButton.IsInteractable(), Is.True);
                    Assert.That(failureLock.IsLocked, Is.True);
                }
            }
            yield return null;
        }

        private void StartEvacuation()
        {
            session.BeginMission();
            mission.Tick(mission.Config.RepairWindowSeconds, false, false);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
        }

        private void FailMission()
        {
            StartEvacuation();
            session.Advance(mission.RemainingSeconds);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Failed));
        }

        private IEnumerator SelectMode(TargetedDevices mode)
        {
#pragma warning disable CS0618
            simulator.targetedDeviceInput = mode;
#pragma warning restore CS0618
            yield return Frames(2);
            Assert.That(simulator.currentState.targetedDeviceInput, Is.EqualTo(mode));
        }

        private void SetMovement(Vector3 value)
        {
            SetFloatInput(simulator.translateXInput, value.x);
            SetFloatInput(simulator.translateYInput, value.y);
            SetFloatInput(simulator.translateZInput, value.z);
        }

        private static void SetFloatInput(XRInputValueReader<float> reader, float value)
        {
            reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            reader.manualValue = value;
        }

        private static void SetVectorInput(XRInputValueReader<Vector2> reader, Vector2 value)
        {
            reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            reader.manualValue = value;
        }

        private void AimAtTool(XRRayInteractor ray)
        {
            Vector3 target = grab.GetComponentsInChildren<Collider>().First(collider => collider.enabled && !collider.isTrigger).bounds.center;
            Vector3 start = target - Vector3.forward * 0.6f + Vector3.up * 0.1f;
            ray.transform.SetPositionAndRotation(start, Quaternion.LookRotation(target - start));
            Physics.SyncTransforms();
        }

        private void MovePlayerHorizontally(Vector3 destination)
        {
            var body = session.Exit.PlayerBody;
            Vector3 center = body.transform.TransformPoint(body.center);
            body.enabled = false;
            body.transform.position += new Vector3(destination.x - center.x, 0f, destination.z - center.z);
            body.enabled = true;
            Physics.SyncTransforms();
        }

        private void AssertAtSpawn() => Assert.That(Vector3.ProjectOnPlane(session.Player.Camera.transform.position -
            session.SpawnPoint.position, Vector3.up).magnitude, Is.LessThan(0.03f),
            "重试后必须回到出生点，旧传送或锁定末帧不能把玩家再搬走。");

        private static Vector2 ScreenPoint(Button button)
        {
            var canvas = button.GetComponentInParent<Canvas>();
            var rect = button.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            if (camera != null) Assert.That(camera.pixelRect.Contains(point), Is.True, button.name + " 不在屏幕内。");
            return point;
        }

        private static List<RaycastResult> Raycast(Vector2 point)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
            return results;
        }

        private static IEnumerator MouseClick(Mouse mouse, Vector2 point)
        {
            var state = new MouseState { position = point };
            InputState.Change(mouse, state);
            yield return Frames(2);
            InputState.Change(mouse, state.WithButton(MouseButton.Left));
            yield return Frames(2);
            InputState.Change(mouse, state.WithButton(MouseButton.Left, false));
            yield return Frames(2);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static GameObject FindObject(string name) =>
            SceneComponents<Transform>().Single(item => item.name == name).gameObject;

        private static IEnumerable<T> SceneComponents<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
