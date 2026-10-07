using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    // 实体驾驶台：启动、入轨与 RCS 对接都能用开关和手控器完成，规则仍由原任务判定。
    // 用一只“测试手”交互器经真实 XRInteractionManager 选择操纵件并移动/转动它；
    // 真实 VIVE 手柄的握持手感需在头显上确认。
    // 最简交互器：始终处于“握住”状态，位置由测试直接设置。
    internal sealed class PilotTestHand : XRBaseInteractor { }

    public sealed class PilotConsoleTests
    {
        private StationMissionSession session; private AscentMission flight; private DockingMission docking;
        private HatchBoardingController hatch; private PilotConsole console; private XRInteractionManager manager;
        private PilotTestHand hand; private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("10_LunarStation_Docking"); yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false;
            flight = session.GetComponent<AscentMission>(); docking = session.GetComponent<DockingMission>();
            hatch = Object.FindAnyObjectByType<HatchBoardingController>();
            console = Object.FindAnyObjectByType<PilotConsole>(FindObjectsInactive.Include);
            manager = Object.FindAnyObjectByType<XRInteractionManager>();
            while (Time.time < 1.05f) yield return null;
        }

        [TearDown] public void Restore()
        {
            Time.captureDeltaTime = previousDelta;
            if (hand != null) Object.Destroy(hand.gameObject);
        }

        [Test] public void ConsoleIsInstalledOnCockpitLayerOnly()
        {
            Assert.That(console, Is.Not.Null, "请先执行 Lunar Escape → Install Pilot Console (Docking Scene)");
            int cockpit = InteractionLayerMask.GetMask("Cockpit");
            Assert.That(cockpit, Is.Not.Zero);
            var controls = console.GetComponentsInChildren<CockpitControl>(true);
            Assert.That(controls.Length, Is.EqualTo(12));
            Assert.That(controls.All(c => c.interactionLayers.value == cockpit));
            Assert.That(session.GetComponent<FlightSeatLock>().CockpitLayers.value, Is.EqualTo(cockpit));
        }

        [UnityTest] public IEnumerator SeatedHandsReachOnlyTheCockpit()
        {
            var interactors = session.Player.GetComponentsInChildren<XRBaseInteractor>(true);
            var grabbing = interactors.Where(i => (i.interactionLayers.value & InteractionLayerMask.GetMask("Default")) != 0).ToArray();
            Assert.That(grabbing, Is.Not.Empty);
            Board();
            yield return null;
            int cockpit = InteractionLayerMask.GetMask("Cockpit");
            Assert.That(grabbing.All(i => i.interactionLayers.value == cockpit), "坐上操作位后，原本能抓物体的手只能操作驾驶台");
            Assert.That(interactors.Except(grabbing).All(i => i.interactionLayers.value == 0), "传送等其他交互器保持关闭");
            Assert.That(console.gameObject.activeInHierarchy, "黑屏切换后驾驶台随面板出现");
        }

        [UnityTest] public IEnumerator StartupAndOrbitBurnWithSwitches()
        {
            Board(); MakeHand();
            yield return Press(console.Navigation);
            Assert.That(flight.Operation, Is.EqualTo(StartupOperation.None), "未通电时导航开关被拒绝");
            yield return Press(console.Power); Assert.That(flight.Powered);
            yield return Press(console.Navigation); flight.Tick(2); Assert.That(flight.NavigationReady);
            yield return Press(console.Engine); flight.Tick(2); Assert.That(flight.EngineReady);

            yield return Press(console.Ignition);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Startup), "保护盖关着时不能点火");
            yield return Press(console.IgnitionCover); Assert.That(console.IgnitionCover.Latched, "掀开保护盖");
            yield return Press(console.Ignition);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Ignition));

            flight.Tick(3); flight.Tick(flight.OrbitAscentSeconds - flight.AirborneSeconds);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.OrbitalInsertion));
            yield return Press(console.BurnCover); yield return Press(console.Burn);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Circularizing));
        }

        [UnityTest] public IEnumerator SticksDriveRcsAndReleaseOnLetGo()
        {
            Board(); MakeHand(); Launch();
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Rendezvous));

            // 平移：握住手柄向右推 4 cm（超过 35% 行程门限），右平移推进接通；回中后断开。
            var stick = console.Translation;
            yield return Grab(stick);
            hand.transform.position += stick.transform.right * 0.04f;
            yield return Frames(3);
            Assert.That(docking.IsCommandActive(DockCommand.Right));
            Assert.That(docking.IsCommandActive(DockCommand.Left), Is.False);
            hand.transform.position -= stick.transform.right * 0.04f;
            yield return Frames(3);
            Assert.That(docking.IsCommandActive(DockCommand.Right), Is.False);
            hand.transform.position += stick.transform.forward * 0.04f;
            yield return Frames(3);
            Assert.That(docking.IsCommandActive(DockCommand.Forward));
            yield return Release(stick);
            Assert.That(docking.ActiveCommandCount, Is.Zero, "松手立即停止全部推进");

            // 姿态：握住圆球，手腕向前转 20°（绕 +X）为低头，向右转为右偏航。
            var ball = console.Rotation;
            yield return Grab(ball);
            hand.transform.rotation = Quaternion.AngleAxis(20f, ball.transform.right) * hand.transform.rotation;
            yield return Frames(3);
            Assert.That(docking.IsCommandActive(DockCommand.PitchDown));
            hand.transform.rotation = Quaternion.AngleAxis(-20f, ball.transform.right) * hand.transform.rotation;
            hand.transform.rotation = Quaternion.AngleAxis(20f, ball.transform.up) * hand.transform.rotation;
            yield return Frames(3);
            Assert.That(docking.IsCommandActive(DockCommand.PitchDown), Is.False);
            Assert.That(docking.IsCommandActive(DockCommand.YawRight));
            yield return Release(ball);
            Assert.That(docking.ActiveCommandCount, Is.Zero);

            // 制动按住才有效；对接辅助开关切换任务中的辅助状态。
            yield return Grab(console.Brake);
            Assert.That(docking.IsCommandActive(DockCommand.Brake));
            yield return Release(console.Brake);
            Assert.That(docking.IsCommandActive(DockCommand.Brake), Is.False);
            bool assist = docking.AssistanceEnabled;
            yield return Press(console.Assist);
            Assert.That(docking.AssistanceEnabled, Is.Not.EqualTo(assist));
        }

        [UnityTest] public IEnumerator RetryResetsCoversAndSwitches()
        {
            Board(); MakeHand();
            yield return Press(console.Power);
            yield return Press(console.IgnitionCover);
            Assert.That(console.Power.Latched && console.IgnitionCover.Latched);
            session.RetryMission();
            yield return null;
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.AwaitingBoarding));
            Assert.That(console.IgnitionCover.Latched, Is.False);
            Assert.That(console.Power.Latched, Is.False);
        }

        private void MakeHand()
        {
            hand = new GameObject("Test Hand").AddComponent<PilotTestHand>();
            hand.interactionLayers = InteractionLayerMask.GetMask("Cockpit");
        }

        private IEnumerator Grab(CockpitControl control)
        {
            // 手放在操纵件上（近距离握持），走与真实手柄相同的选择事件。
            hand.transform.SetPositionAndRotation(control.transform.position, control.transform.rotation);
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)control);
            yield return Frames(2);
            Assert.That(control.isSelected, control.name + " 应被握住");
        }

        private IEnumerator Release(CockpitControl control)
        {
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)control);
            yield return Frames(2);
        }

        private IEnumerator Press(CockpitControl control) { yield return Grab(control); yield return Release(control); }

        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }

        private void Board(float remaining = 60)
        {
            session.BeginMission(); session.Advance(session.Mission.Config.RepairWindowSeconds); session.Advance(session.Mission.RemainingSeconds - remaining);
            var body = session.Exit.PlayerBody; var center = body.transform.TransformPoint(body.center); body.enabled = false;
            body.transform.position += new Vector3(48 - center.x, 0, -6 - center.z); body.enabled = true; Physics.SyncTransforms();
            Assert.That(hatch.CanBoard, Is.True); hatch.RequestBoarding(); session.Advance(0); flight.Tick(.951f);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Startup));
        }

        private void Launch()
        {
            flight.PowerOn(); flight.StartNavigation(); flight.Tick(2); flight.PrepareEngine(); flight.Tick(2); flight.Ignite(); flight.Tick(3);
            flight.Tick(flight.OrbitAscentSeconds - flight.AirborneSeconds); flight.Circularize(); flight.Tick(6);
        }
    }
}
