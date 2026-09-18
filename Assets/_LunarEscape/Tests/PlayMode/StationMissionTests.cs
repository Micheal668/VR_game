using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace LunarEscape.Tests
{
    // 用同一套真实任务组件和确定时间步验证规则，不依赖实际等待秒数。
    public sealed class StationMissionTests
    {
        private GameObject owner;
        private TimedRepairTask repair;
        private StationMission mission;
        private StationMissionConfig config;

        [SetUp]
        public void CreateMission()
        {
            owner = new GameObject("Test Station Mission");
            repair = owner.AddComponent<TimedRepairTask>();
            mission = owner.AddComponent<StationMission>();
            config = ScriptableObject.CreateInstance<StationMissionConfig>();
            Configure(10f, 2f, 5f, 3f, 2f);
        }

        [TearDown]
        public void RemoveMission()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void SuccessfulRepairAddsBonusAndCompletedMissionStaysLocked()
        {
            var phases = new List<StationMissionPhase>();
            mission.PhaseChanged += phases.Add;
            mission.Tick(100f, true, true);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
            Assert.That(repair.Progress, Is.Zero, "简报期间不能提前维修。");

            mission.Begin();
            mission.Tick(1f, true, false);
            mission.Begin();
            Assert.That(mission.RemainingSeconds, Is.EqualTo(9f), "重复开始不能延长当前维修窗口。");
            mission.Tick(1f, true, false);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Stabilized));
            Assert.That(mission.RepairRestored, Is.True);
            Assert.That(mission.RemainingSeconds, Is.EqualTo(2f));
            Assert.That(mission.EvacuationBudgetSeconds, Is.EqualTo(8f));

            mission.Tick(2f, false, false);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(8f));
            mission.Tick(1f, false, true);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Completed));
            Assert.That(mission.IsTerminal, Is.True);
            Assert.That(mission.FailureReason, Is.EqualTo(StationMissionFailure.None));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(7f), "完成界面应保留余时。");
            mission.Tick(100f, true, false);
            mission.Tick(100f, false, true);
            mission.Begin();
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Completed));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(7f));
            Assert.That(phases, Is.EqualTo(new[]
            {
                StationMissionPhase.Repair, StationMissionPhase.Stabilized,
                StationMissionPhase.Evacuation, StationMissionPhase.Completed
            }), "每个阶段只通知一次，不能重复触发警报或成功。");
        }

        [Test]
        public void LargeStepsDistributeTimeAndInvalidTimeCannotAffectTheAttempt()
        {
            Configure(2f, 1f, 3f, 2f, 1f);
            mission.Begin();
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                mission.Tick(invalid, true, true);
                Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Repair));
                Assert.That(mission.RemainingSeconds, Is.EqualTo(2f));
                Assert.That(repair.Progress, Is.Zero);
            }

            mission.Tick(4f, false, false);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(1f), "2秒维修窗口后，只把多出的2秒扣到3秒撤离期。");
            mission.Tick(1f, false, true);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Failed), "到达出口与归零同帧时，超时优先。");
            Assert.That(mission.FailureReason, Is.EqualTo(StationMissionFailure.EvacuationTimeout));
            Assert.That(mission.RemainingSeconds, Is.Zero);
            mission.Tick(0f, true, true);
            mission.Tick(50f, true, true);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Failed));

            mission.ResetMission();
            mission.Begin();
            mission.Tick(3f, true, false);
            Assert.That(mission.RepairRestored, Is.True);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(4f), "维修1秒、稳定1秒后，撤离期只消耗剩余1秒。");
            mission.ResetMission();
            mission.Begin();
            mission.Tick(50f, true, false);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Failed));
            Assert.That(mission.RepairRestored, Is.True);
            Assert.That(mission.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void RepairDeadlineAcceptsBeforeAndExactlyButRejectsAfter()
        {
            foreach (float duration in new[] { 0.75f, 1f, 1.25f })
            {
                Configure(1f, 2f, 5f, 3f, duration);
                mission.Begin();
                mission.Tick(1f, true, false);
                if (duration <= 1f)
                {
                    Assert.That(mission.RepairRestored, Is.True, "截止前或恰好截止时完成均有效。");
                    Assert.That(repair.State, Is.EqualTo(RepairState.Complete));
                    Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Stabilized));
                    Assert.That(mission.EvacuationBudgetSeconds, Is.EqualTo(8f));
                    Assert.That(mission.RemainingSeconds, Is.EqualTo(2f - (1f - duration)).Within(0.0001f));
                }
                else
                {
                    Assert.That(mission.RepairRestored, Is.False);
                    Assert.That(repair.Progress, Is.EqualTo(0.8f).Within(0.0001f));
                    Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
                    Assert.That(mission.EvacuationBudgetSeconds, Is.EqualTo(5f));
                    Assert.That(mission.RemainingSeconds, Is.EqualTo(5f));
                }
            }
        }

        [Test]
        public void PausingPreservesRepairButUsesWindowAndTimeoutRaisesAlarmWithoutBonus()
        {
            Configure(4f, 2f, 5f, 3f, 2f);
            var phases = new List<StationMissionPhase>();
            mission.PhaseChanged += phases.Add;
            mission.Begin();
            mission.Tick(0.5f, true, true);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Repair), "维修阶段提前到出口不能完成任务。");
            mission.Tick(1f, false, false);
            Assert.That(repair.State, Is.EqualTo(RepairState.Paused));
            Assert.That(repair.Progress, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(2.5f));

            mission.Tick(2.5f, false, false);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(mission.RepairRestored, Is.False);
            Assert.That(mission.IsTerminal, Is.False, "维修窗口结束只触发撤离警报，不立即判整场失败。");
            Assert.That(mission.EvacuationBudgetSeconds, Is.EqualTo(5f));
            Assert.That(phases.Count(phase => phase == StationMissionPhase.Evacuation), Is.EqualTo(1));
            Assert.That(phases.Contains(StationMissionPhase.Stabilized), Is.False);
            mission.Tick(1f, true, false);
            Assert.That(repair.Progress, Is.EqualTo(0.25f).Within(0.0001f), "警报后继续接触设备不能补领奖励。");
            Assert.That(mission.RemainingSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void ResetAndReentrantChangeHandlersCannotAdvanceANewAttempt()
        {
            mission.Begin();
            mission.Tick(5f, true, false);
            mission.Tick(1f, false, true);
            Assert.That(mission.IsTerminal, Is.True);
            mission.ResetMission();
            AssertFreshBriefing();

            bool restarted = false;
            Action restartOnStable = () =>
            {
                if (restarted || mission.Phase != StationMissionPhase.Stabilized) return;
                restarted = true;
                mission.ResetMission();
                mission.Begin();
            };
            mission.Changed += restartOnStable;
            mission.Begin();
            mission.Tick(100f, true, true);
            mission.Changed -= restartOnStable;
            Assert.That(restarted, Is.True);
            AssertNewRepairAttempt();

            // 维修自己的 Changed 也可能重试；旧 Completed 回调不得污染新一轮。
            mission.ResetMission();
            restarted = false;
            Action restartOnRepairComplete = () =>
            {
                if (restarted || repair.State != RepairState.Complete) return;
                restarted = true;
                mission.ResetMission();
                mission.Begin();
            };
            repair.Changed += restartOnRepairComplete;
            mission.Begin();
            mission.Tick(100f, true, true);
            repair.Changed -= restartOnRepairComplete;
            Assert.That(restarted, Is.True);
            AssertNewRepairAttempt();
            mission.ResetMission();
            AssertFreshBriefing();
        }

        private void Configure(float window, float stabilized, float evacuation, float bonus, float repairDuration)
        {
            config.Configure(window, stabilized, evacuation, bonus);
            repair.Configure(repairDuration);
            mission.Configure(config, repair);
        }

        private void AssertFreshBriefing()
        {
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
            Assert.That(mission.RemainingSeconds, Is.Zero);
            Assert.That(mission.IsTerminal, Is.False);
            Assert.That(mission.RepairRestored, Is.False);
            Assert.That(mission.FailureReason, Is.EqualTo(StationMissionFailure.None));
            Assert.That(mission.EvacuationBudgetSeconds, Is.EqualTo(config.BaseEvacuationSeconds));
            Assert.That(repair.State, Is.EqualTo(RepairState.Ready));
            Assert.That(repair.Progress, Is.Zero);
        }

        private void AssertNewRepairAttempt()
        {
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Repair));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(config.RepairWindowSeconds));
            Assert.That(mission.RepairRestored, Is.False);
            Assert.That(mission.EvacuationBudgetSeconds, Is.EqualTo(config.BaseEvacuationSeconds));
            Assert.That(repair.Progress, Is.Zero);
            Assert.That(repair.State, Is.EqualTo(RepairState.Ready));
        }
    }

    public sealed class StationMissionSceneTests
    {
        private StationMissionSession session;
        private StationMission mission;
        private TimedRepairTask repair;
        private RepairContact contact;
        private RepairTool tool;
        private EvacuationZone exit;
        private MissionEnvironment environment;
        private LocalizationService localization;
        private GameObject door;
        private TeleportationArea corridorTeleport;

        [UnitySetUp]
        public IEnumerator LoadEvacuationScene()
        {
            yield return SceneManager.LoadSceneAsync("06_LunarStation_Evacuation", LoadSceneMode.Single);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>();
            Assert.That(session, Is.Not.Null);
            // 只停自动时钟；Advance 仍读取真实抓取接触和真实玩家区域位置。
            session.enabled = false;
            mission = session.Mission;
            repair = mission.RepairTask;
            contact = Object.FindAnyObjectByType<RepairContact>();
            tool = Object.FindAnyObjectByType<RepairTool>();
            exit = session.Exit;
            environment = Object.FindAnyObjectByType<MissionEnvironment>();
            localization = Object.FindAnyObjectByType<LocalizationService>();
            door = FindObject("Evacuation Door");
            corridorTeleport = FindObject("Corridor Teleport Surface").GetComponent<TeleportationArea>();
            Assert.That(contact.enabled, Is.False, "旧接触组件不能另开一个维修时钟。");
            Assert.That(Object.FindObjectsByType<EventSystem>().Count(system => system.isActiveAndEnabled), Is.EqualTo(1));
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
        }

        [UnityTest]
        public IEnumerator ActiveSimulatorHeadModeRestoresMouseAndStartsMissionThroughRealInput()
        {
            var simulator = Object.FindAnyObjectByType<CollisionAwareSimulator>();
            Assert.That(simulator, Is.Not.Null);
            Assert.That(simulator.isActiveAndEnabled, Is.True);
            var previousMode = simulator.currentState.targetedDeviceInput;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse mouse = null;
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                // 与玩家切到 Head 相同：保持模拟器运行，由官方逻辑恢复鼠标 UI。
#pragma warning disable CS0618
                simulator.targetedDeviceInput = TargetedDevices.HMD;
#pragma warning restore CS0618
                yield return null;
                yield return null;
                Assert.That(simulator.currentState.targetedDeviceInput, Is.EqualTo(TargetedDevices.HMD));
                Assert.That(simulator.isActiveAndEnabled, Is.True);
                var eventSystem = EventSystem.current;
                Assert.That(eventSystem, Is.Not.Null);
                Assert.That(eventSystem.currentInputModule, Is.InstanceOf<XRUIInputModule>());
                var module = (XRUIInputModule)eventSystem.currentInputModule;
                Assert.That(module.enableMouseInput, Is.True,
                    "切换Head后官方RestoreInputModuleInput应自行恢复鼠标，测试不能代替它打开开关。");

                var button = FindObject("Begin Mission").GetComponent<Button>();
                var rect = button.GetComponent<RectTransform>();
                var canvas = button.GetComponentInParent<Canvas>();
                Assert.That(button.isActiveAndEnabled && button.interactable, Is.True);
                Assert.That(canvas.worldCamera, Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                var screenPoint = canvas.worldCamera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
                Assert.That(screenPoint.z, Is.GreaterThan(0f));
                Assert.That(canvas.worldCamera.pixelRect.Contains(screenPoint), Is.True,
                    "开始按钮必须位于玩家画面内。");
                var hits = new List<RaycastResult>();
                eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPoint }, hits);
                Assert.That(hits.Any(hit => hit.gameObject.GetComponentInParent<Button>() == button), Is.True,
                    "开始按钮没有被屏幕射线命中：" + string.Join(", ", hits.Select(hit => hit.gameObject.name)));

                mouse = InputSystem.AddDevice<Mouse>("Mission Start Test Mouse");
                var state = new MouseState { position = screenPoint };
                InputState.Change(mouse, state);
                yield return null;
                yield return null;
                Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
                InputState.Change(mouse, state.WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                InputState.Change(mouse, state.WithButton(MouseButton.Left, false));
                yield return null;
                yield return null;

                Assert.That(simulator.isActiveAndEnabled, Is.True);
                Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Repair),
                    "Head模式中的真实鼠标按下/松开必须能开始任务。");
                Assert.That(mission.RemainingSeconds, Is.EqualTo(mission.Config.RepairWindowSeconds));
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
                if (simulator != null)
                {
#pragma warning disable CS0618
                    simulator.targetedDeviceInput = previousMode;
#pragma warning restore CS0618
                }
            }
        }

        [UnityTest]
        public IEnumerator BeginButtonAndHeldToolDriveOneRepairClockThenOpenEvacuation()
        {
            Assert.That(mission.Config.RepairWindowSeconds, Is.EqualTo(90f));
            Assert.That(mission.Config.StabilizedSeconds, Is.EqualTo(4f));
            Assert.That(mission.Config.BaseEvacuationSeconds, Is.EqualTo(60f));
            Assert.That(mission.Config.RepairBonusSeconds, Is.EqualTo(30f));
            Assert.That(environment.IsDoorOpen, Is.False);
            Assert.That(door.GetComponent<Collider>().enabled, Is.True);
            Assert.That(corridorTeleport.enabled, Is.False);
            Assert.That(FindObject("Room Teleport Surface").GetComponent<TeleportationArea>().enabled, Is.True);

            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var handObject = new GameObject("Mission Repair Test Hand");
            var hand = handObject.AddComponent<XRRayInteractor>();
            hand.interactionManager = manager;
            var grab = tool.GetComponent<XRGrabInteractable>();
            yield return null;
            try
            {
                manager.SelectEnter((IXRSelectInteractor)hand, grab);
                MoveToolTipTo(contact.RepairPoint.position);
                Assert.That(contact.HasValidContact(), Is.True);
                session.Advance(repair.DurationSeconds);
                Assert.That(repair.Progress, Is.Zero, "开始前即使拿着正确工具接触也不能提前累计。");

                Click("Begin Mission");
                session.Advance(repair.DurationSeconds * 0.25f);
                Assert.That(repair.Progress, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(mission.RemainingSeconds,
                    Is.EqualTo(90f - repair.DurationSeconds * 0.25f).Within(0.0001f));
                yield return null;
                Assert.That(repair.Progress, Is.EqualTo(0.25f).Within(0.0001f),
                    "停用session后帧更新不应通过旧RepairContact额外推进维修。");

                if (!grab.isSelected) manager.SelectEnter((IXRSelectInteractor)hand, grab);
                MoveToolTipTo(contact.RepairPoint.position);
                Assert.That(contact.HasValidContact(), Is.True);
                session.Advance(repair.DurationSeconds * 0.75f);
                Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Stabilized));
                Assert.That(environment.IsDoorOpen, Is.False);
                Assert.That(corridorTeleport.enabled, Is.False);
                session.Advance(mission.Config.StabilizedSeconds);
                Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
                Assert.That(mission.RemainingSeconds, Is.EqualTo(90f));
                Assert.That(environment.IsDoorOpen, Is.True);
                Assert.That(door.activeSelf, Is.False);
                Assert.That(corridorTeleport.enabled, Is.True);
                AssertAlarmLights(true);
                manager.SelectExit((IXRSelectInteractor)hand, grab);
            }
            finally
            {
                Object.Destroy(handObject);
            }
        }

        [UnityTest]
        public IEnumerator DoorBlocksTheRouteUntilAlarmAndCorridorFloorHasNoGap()
        {
            Physics.SyncTransforms();
            var lower = new Vector3(3.2f, 0.3f, 1.7f);
            var upper = new Vector3(3.2f, 1.6f, 1.7f);
            var blocked = Physics.CapsuleCastAll(lower, upper, 0.15f, Vector3.right, 3.9f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Assert.That(blocked.Any(hit => hit.collider.gameObject == door), Is.True,
                "警报前真实胶囊路径应被撤离门阻挡。");

            Click("Begin Mission");
            session.Advance(mission.Config.RepairWindowSeconds);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Physics.SyncTransforms();
            var clear = Physics.CapsuleCastAll(lower, upper, 0.15f, Vector3.right, 3.9f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Assert.That(clear, Is.Empty, "开门后从室内到集结区的身体路径仍被阻挡：" +
                string.Join(", ", clear.Select(hit => hit.collider.name)));

            for (float x = 3.4f; x <= 7.5f; x += 0.2f)
            {
                bool grounded = Physics.Raycast(new Vector3(x, 0.35f, 1.7f), Vector3.down,
                    out var hit, 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Assert.That(grounded, Is.True, "走廊在x=" + x + "处缺少连续地板。");
                Assert.That(hit.point.y, Is.InRange(-0.01f, 0.01f));
                Assert.That(hit.normal.y, Is.GreaterThan(0.9f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ToolsAndHandsCannotEvacuateButTheBoundPlayerBodyCan()
        {
            Click("Begin Mission");
            session.Advance(mission.Config.RepairWindowSeconds);
            var destination = exit.Volume.transform.TransformPoint(exit.Volume.center);
            tool.GetComponent<Rigidbody>().position = destination;
            var modality = session.Player.GetComponent<XRInputModalityManager>();
            Assert.That(modality.rightController, Is.Not.Null);
            modality.rightController.transform.position = destination;
            Physics.SyncTransforms();
            Assert.That(exit.ContainsPlayer, Is.False, "工具和手柄位于集结区不代表身体已到达。");
            session.Advance(0.25f);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));

            MovePlayerHorizontallyTo(new Vector3(exit.Volume.bounds.min.x - 0.02f, 0f, destination.z));
            Assert.That(exit.ContainsPlayer, Is.False, "胶囊边缘碰到区域但身体中心在外时，不算到达。");
            MovePlayerHorizontallyTo(destination);
            Assert.That(exit.ContainsPlayer, Is.True);
            exit.PlayerBody.enabled = false;
            Assert.That(exit.ContainsPlayer, Is.False, "停用的身体不能继续报告到达。");
            exit.PlayerBody.enabled = true;
            float remaining = mission.RemainingSeconds;
            session.Advance(0f);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Completed));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(remaining));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetryButtonsResetHeldToolAndWorldAfterSuccessAndFailure()
        {
            Click("Mission Panel Language 2");
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var handObject = new GameObject("Mission Retry Test Hand");
            var hand = handObject.AddComponent<XRRayInteractor>();
            hand.interactionManager = manager;
            var grab = tool.GetComponent<XRGrabInteractable>();
            var body = tool.GetComponent<Rigidbody>();
            var initialPosition = body.position;
            var initialRotation = body.rotation;
            bool originalThrow = grab.throwOnDetach;
            int repairCompletions = 0;
            repair.Completed += () => repairCompletions++;
            yield return null;
            try
            {
                foreach (var outcome in new[] { StationMissionPhase.Completed, StationMissionPhase.Failed })
                {
                    Click("Begin Mission");
                    mission.Tick(repair.DurationSeconds + mission.Config.StabilizedSeconds, true, false);
                    Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
                    manager.SelectEnter((IXRSelectInteractor)hand, grab);
                    Assert.That(grab.isSelected, Is.True);
                    MovePlayerHorizontallyTo(exit.Volume.transform.TransformPoint(exit.Volume.center));
                    body.position = new Vector3(6.7f, 1.4f, 1.7f);
                    body.linearVelocity = new Vector3(5f, 3f, 2f);
                    body.angularVelocity = Vector3.one * 4f;
                    session.Advance(outcome == StationMissionPhase.Completed ? 1f : mission.RemainingSeconds);
                    Assert.That(mission.Phase, Is.EqualTo(outcome));
                    int completedBeforeRetry = repairCompletions;
                    Click(outcome == StationMissionPhase.Completed ? "Retry At Exit" : "Restart After Failure");

                    Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
                    Assert.That(mission.RemainingSeconds, Is.Zero);
                    Assert.That(mission.EvacuationBudgetSeconds, Is.EqualTo(60f));
                    Assert.That(mission.RepairRestored, Is.False);
                    Assert.That(mission.FailureReason, Is.EqualTo(StationMissionFailure.None));
                    Assert.That(repair.Progress, Is.Zero);
                    Assert.That(repair.State, Is.EqualTo(RepairState.Ready));
                    Assert.That(grab.isSelected, Is.False);
                    Assert.That(body.linearVelocity.sqrMagnitude, Is.LessThan(0.0001f));
                    Assert.That(body.angularVelocity.sqrMagnitude, Is.LessThan(0.0001f));
                    Assert.That(Vector3.Distance(body.position, initialPosition), Is.LessThan(0.05f));
                    Assert.That(Quaternion.Angle(body.rotation, initialRotation), Is.LessThan(0.1f));
                    Assert.That(exit.PlayerBody.enabled, Is.True);
                    Assert.That(exit.ContainsPlayer, Is.False);
                    Assert.That(Vector3.ProjectOnPlane(session.Player.Camera.transform.position -
                        session.SpawnPoint.position, Vector3.up).magnitude, Is.LessThan(0.02f));
                    Assert.That(environment.IsDoorOpen, Is.False);
                    Assert.That(door.activeSelf, Is.True);
                    Assert.That(corridorTeleport.enabled, Is.False);
                    AssertAlarmLights(false);
                    Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.Russian));
                    Assert.That(repairCompletions, Is.EqualTo(completedBeforeRetry));
                    yield return null;
                    yield return null;
                    Assert.That(grab.throwOnDetach, Is.EqualTo(originalThrow));
                    Assert.That(Vector3.Distance(body.position, initialPosition), Is.LessThan(0.2f),
                        "XRI延迟投掷不能在重试后再次把工具抛走。");
                    Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
                    Assert.That(repairCompletions, Is.EqualTo(completedBeforeRetry));
                }
                Assert.That(repairCompletions, Is.EqualTo(2), "每轮仅一次维修完成，重试不产生旧完成事件。");
            }
            finally
            {
                Object.Destroy(handObject);
            }
        }

        [UnityTest]
        public IEnumerator BothPanelsStaySynchronizedInEveryLanguageAndMissionPhase()
        {
            var allLabels = SceneComponents<TMP_Text>().ToArray();
            var catalog = JsonUtility.FromJson<LocalizationService.Table>(localization.Catalog.text);
            var characters = catalog.entries.SelectMany(entry => new[] { entry.zh, entry.en, entry.ru })
                .SelectMany(value => string.Format(CultureInfo.InvariantCulture, value, 30, 30, 30, 30, 30, 30, 30))
                .Where(character => !char.IsWhiteSpace(character)).Distinct().ToArray();
            foreach (var font in allLabels.Select(label => label.font).Distinct())
            {
                Assert.That(font, Is.Not.Null);
                foreach (char character in characters)
                    Assert.That(font.HasCharacter(character), Is.True, font.name + " 缺少字符 " + character);
            }

            for (int state = 0; state < 8; state++)
            {
                PreparePresentationState(state);
                float remaining = mission.RemainingSeconds;
                float progress = repair.Progress;
                var phase = mission.Phase;
                for (int language = 0; language < 3; language++)
                {
                    if (phase == StationMissionPhase.Failed) localization.SetLanguage((GameLanguage)language);
                    else Click("Mission Panel Language " + language);
                    Assert.That(localization.CurrentLanguage, Is.EqualTo((GameLanguage)language));
                    AssertPanelsAndLayout(allLabels);
                    int exitLanguage = (language + 1) % 3;
                    if (phase == StationMissionPhase.Failed) localization.SetLanguage((GameLanguage)exitLanguage);
                    else Click("Exit Mission Panel Language " + exitLanguage);
                    Assert.That(localization.CurrentLanguage, Is.EqualTo((GameLanguage)exitLanguage));
                    AssertPanelsAndLayout(allLabels);
                    Assert.That(mission.RemainingSeconds, Is.EqualTo(remaining));
                    Assert.That(repair.Progress, Is.EqualTo(progress));
                    Assert.That(mission.Phase, Is.EqualTo(phase));
                }
            }
            yield return null;
        }

        private void PreparePresentationState(int state)
        {
            session.RetryMission();
            if (state == 0) return;
            session.BeginMission();
            if (state == 7)
            {
                session.Advance(mission.Config.RepairWindowSeconds);
                return;
            }
            if (state <= 2)
            {
                mission.Tick(repair.DurationSeconds * 0.25f, true, false);
                if (state == 2) mission.Tick(1f, false, false);
                return;
            }
            mission.Tick(repair.DurationSeconds, true, false);
            if (state == 3) return;
            mission.Tick(mission.Config.StabilizedSeconds, false, false);
            if (state == 5) mission.Tick(1f, false, true);
            if (state == 6) mission.Tick(mission.RemainingSeconds, false, false);
        }

        private void AssertPanelsAndLayout(TMP_Text[] labels)
        {
            foreach (var names in new[]
            {
                new[] { "Mission Title", "Exit Mission Title" },
                new[] { "Mission Instructions", "Exit Mission Instructions" },
                new[] { "Mission Clock", "Exit Mission Clock" },
                new[] { "Mission Repair Summary", "Exit Repair Summary" }
            })
            {
                var main = FindObject(names[0]).GetComponent<LocalizedText>();
                var rear = FindObject(names[1]).GetComponent<LocalizedText>();
                Assert.That(rear.Key, Is.EqualTo(main.Key));
                Assert.That(rear.GetComponent<TMP_Text>().text, Is.EqualTo(main.GetComponent<TMP_Text>().text));
            }
            string stage = mission.Phase.ToString().ToLowerInvariant();
            Assert.That(FindObject("Mission Title").GetComponent<TMP_Text>().text,
                Is.EqualTo(localization.Format("mission." + stage + ".title")));
            Canvas.ForceUpdateCanvases();
            foreach (var label in labels.Where(label => label.isActiveAndEnabled))
            {
                label.ForceMeshUpdate();
                Assert.That(label.text, Does.Not.StartWith("["), label.name + " 出现缺失语言键。");
                Assert.That(label.isTextOverflowing, Is.False,
                    localization.CurrentLanguage + "/" + mission.Phase + "/" + label.name + " 文字超出面板：" + label.text);
            }
        }

        private void MoveToolTipTo(Vector3 position) => tool.transform.position += position - tool.Tip.position;

        private void MovePlayerHorizontallyTo(Vector3 position)
        {
            var body = exit.PlayerBody;
            Vector3 center = body.transform.TransformPoint(body.center);
            bool enabled = body.enabled;
            body.enabled = false;
            body.transform.position += new Vector3(position.x - center.x, 0f, position.z - center.z);
            body.enabled = enabled;
            Physics.SyncTransforms();
        }

        private static void AssertAlarmLights(bool enabled)
        {
            var lights = SceneComponents<Light>().Where(light => light.name.StartsWith("Alarm Light ")).ToArray();
            Assert.That(lights.Length, Is.EqualTo(2));
            Assert.That(lights.All(light => light.enabled == enabled), Is.True);
        }

        private static void Click(string name)
        {
            var button = FindObject(name).GetComponent<Button>();
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True, name + " 必须能点击。");
            button.onClick.Invoke();
        }

        private static GameObject FindObject(string name)
        {
            var transform = SceneComponents<Transform>().FirstOrDefault(candidate => candidate.name == name);
            Assert.That(transform, Is.Not.Null, "场景缺少 " + name);
            return transform.gameObject;
        }

        private static IEnumerable<T> SceneComponents<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
