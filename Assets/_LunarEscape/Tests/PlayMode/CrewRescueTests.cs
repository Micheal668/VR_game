using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    // 主场景集成：选择实体把手/物资、沿路线跟随、正常登舱和任务时钟，不直接写入乘员结果。
    public sealed class CrewRescueTests
    {
        private StationMissionSession session;
        private CrewMission crew;
        private GroundCrewController ground;
        private CargoInventory cargo;
        private AscentMission flight;
        private DockingMission docking;
        private MissionScore score;
        private CrewPanelPresenter crewPanel;
        private MissionScorePresenter scorePanel;
        private XRInteractionManager manager;
        private XRRayInteractor hand;
        private readonly List<Object> temporary = new();
        private float previousDelta;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousFocus;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            previousDelta = Time.captureDeltaTime;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = 1f / 30;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("10_LunarStation_Docking", LoadSceneMode.Single);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>();
            Assert.That(session, Is.Not.Null);
            session.enabled = false; // 保持 XR 与渲染运行，只显式推进一次任务时钟。
            crew = session.GetComponent<CrewMission>();
            ground = session.GetComponent<GroundCrewController>();
            cargo = session.GetComponent<CargoInventory>();
            flight = session.GetComponent<AscentMission>();
            docking = session.GetComponent<DockingMission>();
            score = session.GetComponent<MissionScore>();
            crewPanel = Object.FindAnyObjectByType<CrewPanelPresenter>();
            scorePanel = Object.FindAnyObjectByType<MissionScorePresenter>();
            Assert.That(crew != null && crew.IsConfigured && ground != null && score != null, Is.True,
                "主场景必须接入真实乘员与评分组件。");
            Assert.That(crewPanel != null && scorePanel != null, Is.True);
            manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var handObject = new GameObject("Crew integration XR hand"); temporary.Add(handObject);
            hand = handObject.AddComponent<XRRayInteractor>(); hand.interactionManager = manager;
            hand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            hand.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            yield return null;
        }

        [TearDown]
        public void RestoreSettings()
        {
            Time.captureDeltaTime = previousDelta;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
            foreach (var item in temporary) if (item != null) Object.Destroy(item);
            temporary.Clear();
        }

        [UnityTest]
        public IEnumerator RealRescueHandleRequiresNearbyBodyAndFiveSecondsAndPausesOnRelease()
        {
            BeginEvacuation();
            var handle = ground.RescueHandle.Interactable;
            Assert.That(handle, Is.Not.Null);
            int completions = 0; crew.RescueCompleted += () => ++completions;

            // 即使实体手选中了把手，远处玩家身体仍不满足救援要求。
            MoveBody(crew.RescueAnchor.position + Vector3.right * 8);
            Select(handle, crew.RescueAnchor.position); session.Advance(1);
            Assert.That(crew.RescueProgressSeconds, Is.Zero);
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Trapped)); Release(handle);

            MoveToCommander(); Select(handle, crew.RescueAnchor.position);
            float health = crew.CommanderHealth, remaining = session.Mission.RemainingSeconds;
            session.Advance(2);
            Assert.That(crew.RescueProgressSeconds, Is.EqualTo(2).Within(.001f));
            Assert.That(session.Mission.RemainingSeconds, Is.EqualTo(remaining - 2).Within(.001f));
            Assert.That(crew.CommanderHealth, Is.EqualTo(health - 2 * crew.Config.UnrepairedInjuryPerSecond).Within(.001f));
            Release(handle); session.Advance(1);
            Assert.That(crew.RescueProgressSeconds, Is.EqualTo(2).Within(.001f));
            yield return CheckLanguages(crewPanel.GroundPanel);
            // 在玩家实际视点记录救援暂停状态：保留障碍、接口和操作面板。
            MoveBody(crew.Commander.position + new Vector3(.7f, 0, -2f));
            yield return Capture("crew-rescue", session.Player.Camera.transform.position,
                crew.Commander.position + new Vector3(.9f, 1.25f, 0), 70);
            MoveToCommander();
            Select(handle, crew.RescueAnchor.position); session.Advance(2.99f);
            Assert.That(crew.CommanderRescued, Is.False); Assert.That(completions, Is.Zero);
            Vector3 before = crew.Commander.position;
            session.Advance(.01f);
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Following));
            Assert.That(crew.RescueProgressSeconds, Is.EqualTo(5).Within(.001f));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(Vector3.Distance(before, crew.Commander.position), Is.LessThan(.01f),
                "恰好完成救援时没有剩余行走时间，不能瞬间追到玩家。");
            Release(handle); session.Advance(1); Assert.That(completions, Is.EqualTo(1));
            yield return CheckLanguages(crewPanel.GroundPanel);
        }

        [UnityTest]
        public IEnumerator MedicalNeedsActualHeldContactAndActivationThenConsumesOnlyOnceAndRetryRestoresIt()
        {
            BeginEvacuation(); MoveToCommander();
            var medical = cargo.Items.Single(item => item.Kind == CargoKind.MedicalKit);
            var original = medical.transform.position;
            MoveItem(medical, ground.MedicalPort.position);
            Assert.That(ground.TryTreatHeldMedical(), Is.False, "未抓取的医疗盒接触角色不能治疗。");
            Assert.That(cargo.TryHold(medical), Is.True);
            Assert.That(ground.TryTreatHeldMedical(), Is.False, "仅账本 Held，没有 XR 选择，不能治疗。");
            Assert.That(cargo.DropHeld(medical), Is.True);
            MoveItem(medical, ground.MedicalPort.position + Vector3.right * 3);
            Select(medical.Grab, medical.transform.position);
            Assert.That(ground.TryTreatHeldMedical(), Is.False);
            float before = crew.CommanderHealth;
            MoveItemWithHand(medical, ground.MedicalPort.position);
            Assert.That(crew.CanTreatCommanderWith(medical), Is.True);
            Assert.That(crew.CommanderHealth, Is.EqualTo(before), "贴近接口后仍需主动使用。");

            // 使用真实 interactor 的激活输入，让 XRI 发出物品 activated 事件。
            hand.activateInput.QueueManualState(true, 1, true, false);
            for (int frame = 0; frame < 4; frame++) yield return null;
            hand.activateInput.QueueManualState(false, 0, false, true);
            Assert.That(medical.State, Is.EqualTo(CargoState.Consumed));
            Assert.That(crew.CommanderHealth, Is.EqualTo(Mathf.Min(100, before + crew.Config.MedicalRestore)).Within(.001f));
            Assert.That(crew.CommanderInjuryPerSecond, Is.Zero);
            Assert.That(crew.GroundMedicalKitsUsed, Is.EqualTo(1));
            Assert.That(cargo.GetCount(CargoKind.MedicalKit), Is.Zero);
            Assert.That(ground.TryTreatHeldMedical(), Is.False); session.Advance(3);
            Assert.That(crew.GroundMedicalKitsUsed, Is.EqualTo(1));

            session.RetryMission(); yield return null;
            AssertReset();
            Assert.That(medical.State, Is.EqualTo(CargoState.World));
            Assert.That(Vector3.Distance(medical.transform.position, original), Is.LessThan(.1f));
            Assert.That(medical.Grab.isSelected, Is.False);
        }

        [UnityTest]
        public IEnumerator RescuedCommanderFollowsRouteBoardsUsesSharedMedicalAndAllThreeSurviveWithDeliveredScience()
        {
            ConfigureApproach(new Vector3(0, 0, -35));
            BeginEvacuation(repair: true);
            Pack(CargoKind.MedicalKit); Pack(CargoKind.DataCore); Pack(CargoKind.LunarSample);
            Rescue();
            FollowRoute();
            Assert.That(ground.ReachedBoarding, Is.True);
            Assert.That(crew.IsCommanderAtBoardingPoint, Is.True);
            BoardCurrent(); yield return null;
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Boarded));
            Assert.That(crew.CommanderBoarded, Is.True);
            var cabin = session.GetComponent<FlightScenePresenter>().FlightWorld.GetComponent<CrewCabinLayout>();
            Assert.That(cabin.Companion.activeInHierarchy, Is.True);
            Assert.That(crewPanel.CabinMedicalButton.interactable, Is.True);
            float beforeHealth = crew.CommanderHealth;
            crewPanel.CabinMedicalButton.onClick.Invoke();
            Assert.That(crew.CommanderHealth, Is.EqualTo(Mathf.Min(100, beforeHealth + crew.Config.MedicalRestore)).Within(.001f));
            Assert.That(crew.CommanderInjuryPerSecond, Is.Zero);
            Assert.That(crew.FlightMedicalKitsUsed, Is.EqualTo(1));
            Assert.That(cargo.GetCount(CargoKind.MedicalKit), Is.Zero);
            crewPanel.CabinMedicalButton.onClick.Invoke(); Assert.That(crew.FlightMedicalKitsUsed, Is.EqualTo(1));

            float oxygen = flight.Oxygen;
            session.enabled = true; yield return null; session.enabled = false;
            Assert.That(flight.Oxygen, Is.EqualTo(oxygen - Time.deltaTime * flight.OxygenRate).Within(.002f),
                "新增一名指挥官不能再次扣除整舱供氧，真实 Update 只推进一次。");
            yield return CheckLanguages(crewPanel.CabinPanel);
            yield return Capture("crew-boarded", session.Player.Camera.transform.position, cabin.Companion.transform.position + Vector3.up * 1.2f);
            LaunchAndOrbit();
            Drive(DockCommand.Forward, 2); flight.Tick(35); Drive(DockCommand.Brake, 2); Drive(DockCommand.Forward, .55f);
            docking.ReleaseControls();
            for (int step = 0; step < 1800 && !flight.IsTerminal; step++) flight.Tick(.1f);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Docked));
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Survived));
            Assert.That(crew.SurvivorCount, Is.EqualTo(3));
            Assert.That(score.Result, Is.Not.Null);
            Assert.That(score.Result.Snapshot.Outcome, Is.EqualTo(MissionScoreOutcome.Docked));
            Assert.That(score.Result.CrewScore, Is.EqualTo(3900));
            Assert.That(score.Result.ScienceScore, Is.EqualTo(1200));
            Assert.That(score.Result.Snapshot.RecoveredDataCores, Is.EqualTo(1));
            Assert.That(score.Result.Snapshot.RecoveredLunarSamples, Is.EqualTo(1));
            Assert.That(score.Result.Total, Is.InRange(9000, 10000));
            AssertFrozen();
            yield return CheckLanguages(scorePanel.Panel, "crew-score-success");
        }

        [UnityTest]
        public IEnumerator RescuedButDistantCommanderCannotAutoBoardAndDiesWhenBaseExplodes()
        {
            BeginEvacuation(); Rescue();
            Vector3 commanderAtRescue = crew.Commander.position;
            MoveBody(crew.BoardingAnchor.position);
            Assert.That(crew.IsCommanderAtBoardingPoint, Is.False);
            Assert.That(ground.ReachedBoarding, Is.False);
            BoardCurrent(); yield return null;
            Assert.That(Vector3.Distance(commanderAtRescue, crew.Commander.position), Is.LessThan(.01f));
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.LeftBehind));
            Assert.That(crew.CommanderRescued, Is.True); Assert.That(crew.CommanderBoarded, Is.False);
            Assert.That(session.GetComponent<FlightScenePresenter>().FlightWorld.GetComponent<CrewCabinLayout>().Companion.activeInHierarchy, Is.False);
            Assert.That(score.HasResult, Is.False, "登舱不是当前任务的结算终点。");
            Launch(); flight.Tick(flight.BaseRemaining + .001f);
            Assert.That(flight.BaseExploded, Is.True);
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Dead));
            Assert.That(crew.CommanderHealth, Is.Zero);
            Assert.That(crew.GetStatus(CrewRole.OrbitalPilot).IsAlive, Is.True);
            Assert.That(score.HasResult, Is.False, "基地爆炸但飞行仍在继续时不能提前结算。");
            session.RetryMission(); yield return null; AssertReset();
        }

        [UnityTest]
        public IEnumerator CollisionFinalizesCrewBeforeScoreEvenWhenScoreSubscribesFirstAndRetryClearsOnlyNewAttempt()
        {
            // 反转订阅顺序，让评分先收到 PhaseChanged，仍必须等人员结局稳定。
            crew.enabled = false; crew.enabled = true;
            ConfigureApproach(new Vector3(0, 0, -1), Vector3.forward * 20);
            BeginEvacuation(); Pack(CargoKind.DataCore); Pack(CargoKind.LunarSample);
            MoveBody(crew.BoardingAnchor.position); BoardCurrent(); LaunchAndOrbit(); flight.Tick(.1f);
            Assert.That(flight.Failure, Is.EqualTo(AscentFailure.DockingCollision));
            Assert.That(crew.IsOutcomeResolved, Is.True); Assert.That(crew.SurvivorCount, Is.Zero);
            Assert.That(score.Result, Is.Not.Null);
            var result = score.Result;
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(MissionScoreOutcome.FlightFailed));
            Assert.That(result.Snapshot.SurvivorCount, Is.Zero);
            Assert.That(result.Snapshot.Player.State, Is.EqualTo(CrewState.Dead));
            Assert.That(result.Snapshot.Commander.State, Is.EqualTo(CrewState.Dead));
            Assert.That(result.Snapshot.OrbitalPilot.State, Is.EqualTo(CrewState.Dead));
            Assert.That(result.Snapshot.LoadedDataCores, Is.EqualTo(1));
            Assert.That(result.ScienceScore, Is.Zero); Assert.That(result.ResourceScore, Is.Zero);
            AssertFrozen();
            yield return CheckLanguages(scorePanel.Panel, "crew-score-collision");
            session.RetryMission(); yield return null; AssertReset();
            Assert.That(result.Snapshot.SurvivorCount, Is.Zero, "重试不能回写上一轮不可变快照。");
            Assert.That(result.Snapshot.Failure, Is.EqualTo(AscentFailure.DockingCollision));
        }

        [UnityTest]
        public IEnumerator RescueCompletionCannotOverrideEvacuationDeadlineInTheSameStep()
        {
            BeginEvacuation(); MoveToCommander();
            session.Advance(session.Mission.RemainingSeconds - 5);
            int completions = 0; crew.RescueCompleted += () => ++completions;
            Select(ground.RescueHandle.Interactable, crew.RescueAnchor.position);
            session.Advance(4.99f);
            Assert.That(crew.RescueProgressSeconds, Is.EqualTo(4.99f).Within(.001f));
            Assert.That(crew.CommanderRescued, Is.False);
            session.Advance(.02f);
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Failed));
            Assert.That(session.Mission.RemainingSeconds, Is.Zero);
            Assert.That(completions, Is.Zero);
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Dead));
            Assert.That(crew.PlayerState, Is.EqualTo(CrewState.Dead));
            Assert.That(crew.GetStatus(CrewRole.OrbitalPilot).IsAlive, Is.True);
            Assert.That(score.Result.Snapshot.Outcome, Is.EqualTo(MissionScoreOutcome.StationFailed));
            Assert.That(score.Result.CrewScore, Is.EqualTo(500));
            Assert.That(score.Result.EmergencyScore, Is.Zero);
            Assert.That(ground.TryTreatHeldMedical(), Is.False);
            AssertFrozen();
            yield return CheckLanguages(scorePanel.Panel);
        }

        [UnityTest]
        public IEnumerator RetryInsideTimeCallbackCannotApplyOldRescueTimeToTheNewAttempt()
        {
            // 让重试监听者排在 Crew 前面；重开并重新报警后，旧步长仍不能流入新一轮。
            crew.enabled = false;
            bool restarted = false;
            void RetryDuringStep(float seconds)
            {
                if (restarted || session.Mission.Phase != StationMissionPhase.Evacuation) return;
                restarted = true; Release(ground.RescueHandle.Interactable);
                session.RetryMission(); BeginEvacuation(); MoveToCommander();
                Select(ground.RescueHandle.Interactable, crew.RescueAnchor.position);
            }
            session.Mission.TimeAdvanced += RetryDuringStep;
            crew.enabled = true;
            try
            {
                BeginEvacuation(); MoveToCommander();
                Select(ground.RescueHandle.Interactable, crew.RescueAnchor.position);
                session.Advance(3);
                Assert.That(restarted, Is.True);
                Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
                Assert.That(session.Mission.RemainingSeconds, Is.EqualTo(session.Mission.EvacuationBudgetSeconds));
                Assert.That(crew.RescueProgressSeconds, Is.Zero);
                Assert.That(crew.CommanderHealth, Is.EqualTo(crew.Config.UnrepairedHealth));
                Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Trapped));
                Assert.That(score.HasResult, Is.False);
                session.Advance(1);
                Assert.That(crew.RescueProgressSeconds, Is.EqualTo(1).Within(.001f));
                Assert.That(crew.CommanderHealth, Is.EqualTo(crew.Config.UnrepairedHealth - crew.Config.UnrepairedInjuryPerSecond).Within(.001f));
            }
            finally { session.Mission.TimeAdvanced -= RetryDuringStep; }
            yield return null;
        }

        private void BeginEvacuation(bool repair = false)
        {
            session.BeginMission();
            if (repair)
            {
                var tool = Object.FindAnyObjectByType<RepairTool>();
                var contact = Object.FindAnyObjectByType<RepairContact>();
                var grab = tool.GetComponent<XRGrabInteractable>(); Select(grab, tool.transform.position);
                var position = tool.transform.position + contact.RepairPoint.position - tool.Tip.position;
                tool.GetComponent<Rigidbody>().position = position; tool.transform.position = position; Physics.SyncTransforms();
                session.Advance(session.Mission.RepairTask.DurationSeconds); Release(grab);
                Assert.That(session.Mission.RepairRestored, Is.True);
                session.Advance(session.Mission.RemainingSeconds);
            }
            else session.Advance(session.Mission.Config.RepairWindowSeconds);
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Trapped));
        }
        private void Rescue()
        {
            MoveToCommander(); Select(ground.RescueHandle.Interactable, crew.RescueAnchor.position);
            session.Advance(crew.Config.RescueSeconds); Release(ground.RescueHandle.Interactable);
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Following));
        }
        private void FollowRoute()
        {
            Assert.That(ground.Waypoints.Count, Is.GreaterThan(3));
            foreach (var waypoint in ground.Waypoints.Skip(1))
            {
                MoveBody(waypoint.position);
                for (int step = 0; step < 240; step++)
                {
                    float distance = Vector3.Distance(crew.Commander.position, waypoint.position);
                    if (distance < .86f && waypoint != ground.Waypoints[ground.Waypoints.Count - 1]) break;
                    if (ground.ReachedBoarding) break;
                    var before = crew.Commander.position; float progress = ground.RouteProgress;
                    session.Advance(.25f);
                    Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Following));
                    Assert.That(ground.RouteProgress, Is.GreaterThanOrEqualTo(progress));
                    Assert.That(Vector3.Distance(before, crew.Commander.position), Is.LessThanOrEqualTo(.43f),
                        "跟随每步最多按 1.7m/s 行走，不能传送或穿越整段路线。");
                }
                Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            }
        }
        private void Pack(CargoKind kind)
        {
            var item = cargo.Items.First(candidate => candidate.Kind == kind && candidate.State == CargoState.World);
            Select(item.Grab, item.transform.position);
            var pack = Object.FindAnyObjectByType<CargoPackZone>();
            Vector3 position = pack.Volume.transform.TransformPoint(pack.Volume.center) - item.transform.TransformVector(item.Body.centerOfMass);
            MoveItemWithHand(item, position); Release(item.Grab);
            Assert.That(item.State, Is.EqualTo(CargoState.Packed), "实际 XR 松手入包：" + kind);
        }
        private void BoardCurrent()
        {
            var hatch = Object.FindAnyObjectByType<HatchBoardingController>();
            Assert.That(hatch.CanBoard, Is.True); hatch.RequestBoarding(); session.Advance(0);
            flight.Tick(flight.Config.FadeOutSeconds + flight.Config.BlackSeconds + flight.Config.FadeInSeconds + .001f);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Startup));
        }
        private void Launch()
        {
            flight.PowerOn(); flight.StartNavigation(); flight.Tick(flight.Config.NavigationSeconds);
            flight.PrepareEngine(); flight.Tick(flight.Config.EngineSeconds); flight.Ignite(); flight.Tick(flight.Config.IgnitionSeconds);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Ascent));
        }
        private void LaunchAndOrbit()
        {
            Launch(); flight.Tick(flight.OrbitAscentSeconds - flight.AirborneSeconds); flight.Circularize();
            flight.Tick(flight.Config.CircularizationSeconds); Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Rendezvous));
        }
        private void ConfigureApproach(Vector3 position, Vector3 velocity = default)
        {
            var config = Object.Instantiate(docking.Config); temporary.Add(config);
            config.ConfigureStart(position, velocity, Vector3.zero); docking.Configure(flight, config);
        }
        private void Drive(DockCommand command, float seconds)
        { docking.SetCommand(command, true); flight.Tick(seconds); docking.SetCommand(command, false); }
        private void MoveToCommander() => MoveBody(crew.Commander.position + Vector3.right * .9f);
        private void MoveBody(Vector3 destination)
        {
            var body = session.Exit.PlayerBody; var centre = body.transform.TransformPoint(body.center);
            body.enabled = false; body.transform.position += new Vector3(destination.x - centre.x, 0, destination.z - centre.z); body.enabled = true;
            Physics.SyncTransforms();
        }
        private void Select(XRBaseInteractable item, Vector3 position)
        {
            hand.transform.position = position; hand.selectInput.manualPerformed = true; hand.selectInput.manualValue = 1;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)item);
            Assert.That(item.isSelected, Is.True, item.name);
        }
        private void Release(XRBaseInteractable item)
        {
            hand.selectInput.manualPerformed = false; hand.selectInput.manualValue = 0;
            if (item.isSelected) manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)item);
        }
        private void MoveItemWithHand(CargoItem item, Vector3 destination)
        { hand.transform.position += destination - item.transform.position; MoveItem(item, destination); }
        private static void MoveItem(CargoItem item, Vector3 destination)
        {
            item.Body.position = destination; item.transform.position = destination;
            if (!item.Body.isKinematic) { item.Body.linearVelocity = Vector3.zero; item.Body.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
        }
        private void AssertFrozen()
        {
            var result = score.Result; float oxygen = flight.Oxygen, health = crew.CommanderHealth;
            flight.Tick(1000); session.Advance(1000); crew.SetRescueHeld(true);
            Assert.That(score.Result, Is.SameAs(result));
            Assert.That(flight.Oxygen, Is.EqualTo(oxygen)); Assert.That(crew.CommanderHealth, Is.EqualTo(health));
            Assert.That(crew.IsRescueHeld, Is.False);
        }
        private void AssertReset()
        {
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
            Assert.That(crew.CommanderState, Is.EqualTo(CrewState.Ready));
            Assert.That(crew.PlayerState, Is.EqualTo(CrewState.Ready)); Assert.That(crew.OrbitalPilotState, Is.EqualTo(CrewState.Ready));
            Assert.That(crew.CommanderHealth, Is.EqualTo(100)); Assert.That(crew.RescueProgressSeconds, Is.Zero);
            Assert.That(crew.MedicalKitsUsed, Is.Zero); Assert.That(crew.CommanderRescued || crew.CommanderBoarded || crew.IsOutcomeResolved, Is.False);
            Assert.That(ground.RouteProgress, Is.Zero); Assert.That(score.HasResult, Is.False);
            Assert.That(Vector3.Distance(crew.Commander.position, ground.Waypoints[0].position), Is.LessThan(.01f));
        }
        private IEnumerator CheckLanguages(GameObject panel, string screenshotPrefix = null)
        {
            var language = Object.FindAnyObjectByType<LocalizationService>();
            foreach (var value in new[] { GameLanguage.Chinese, GameLanguage.English, GameLanguage.Russian })
            {
                language.SetLanguage(value); yield return null;
                Assert.That(panel.activeInHierarchy, Is.True);
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, value + ": " + text.name + " " + text.text);
                    Assert.That(text.text.Contains('{'), Is.False, text.text);
                    foreach (char character in text.text.Where(character => !char.IsWhiteSpace(character)))
                        Assert.That(text.font.HasCharacter(character, true), Is.True, value + ": missing " + character);
                }
                if (screenshotPrefix != null)
                {
                    var camera = session.Player.Camera;
                    yield return Capture(screenshotPrefix + "-" + value, camera.transform.position, camera.transform.position + camera.transform.forward);
                }
            }
            language.SetLanguage(GameLanguage.Chinese); yield return null;
        }
        private static IEnumerator Capture(string name, Vector3 eye, Vector3 target, float fieldOfView = 0)
        {
            var obj = new GameObject("Crew verification camera"); var camera = obj.AddComponent<Camera>();
            camera.CopyFrom(Camera.main); camera.enabled = false; camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            if (fieldOfView > 0) camera.fieldOfView = fieldOfView;
            var texture = new RenderTexture(1600, 1100, 24); var pixels = new Texture2D(1600, 1100, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                texture.Create(); camera.targetTexture = texture; camera.enabled = true;
                for (int frame = 0; frame < 8; frame++) yield return null;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = texture });
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, 1600, 1100), 0, 0); pixels.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Previews")); Directory.CreateDirectory(folder);
                PreviewEvidence.Write(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            { RenderTexture.active = previous; camera.targetTexture = null; texture.Release(); Object.Destroy(pixels); Object.Destroy(texture); Object.Destroy(obj); }
        }
    }
}
