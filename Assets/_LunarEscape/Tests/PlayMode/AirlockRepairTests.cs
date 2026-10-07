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
    // 气闸 A 抢修：三处故障用三种不同手部操作排除，拉杆放行后舱门才会在撤离时打开；
    // 警报前完成获得撤离奖励，警报后仍可在撤离倒计时中完成；重开全部复原。
    public sealed class AirlockRepairTests
    {
        private StationMissionSession session; private StationMission mission; private AirlockRepair airlock;
        private AirlockTestDriver driver; private GameObject door; private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("10_LunarStation_Docking"); yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false;
            mission = session.Mission;
            airlock = Object.FindAnyObjectByType<AirlockRepair>();
            Assert.That(airlock, Is.Not.Null, "请先执行 Lunar Escape → Install Airlock Repair and Cargo Belt (Docking Scene)");
            door = session.GetComponentsInChildren<Transform>(true).Concat(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                .First(t => t.name == "Evacuation Door").gameObject;
            driver = new AirlockTestDriver(airlock);
            while (Time.time < 1.05f) yield return null;
        }

        [TearDown] public void Restore() { Time.captureDeltaTime = previousDelta; driver?.Dispose(); }

        [Test] public void SceneUsesAirlockRepairInsteadOfToolPoint()
        {
            Assert.That(session.Airlock, Is.SameAs(airlock));
            Assert.That(airlock.Faults.Length, Is.EqualTo(3));
            Assert.That(driver.Fuse.Socket.hasSelection || driver.Fuse.Socket.startingSelectedInteractable != null, "开局烧坏的保险丝插在座里");
            Assert.That(driver.Latches.Bolts.Length, Is.EqualTo(3));
            Assert.That(driver.Latches.Bolts.Select(b => b.transform.position.y).Min(), Is.LessThan(0.6f), "最低一颗螺栓需要蹲下");
            Assert.That(mission.Config.RepairWindowSeconds, Is.GreaterThanOrEqualTo(150f), "三处故障需要更长的维修窗口");
        }

        [UnityTest] public IEnumerator NothingCanBeRepairedBeforeTheMissionStarts()
        {
            yield return driver.TurnValve(700f);
            Assert.That(driver.Valve.Valve.Angle, Is.Zero, "开始任务前手轮不计数");
            Assert.That(airlock.FixedCount, Is.Zero);
        }

        [UnityTest] public IEnumerator FullRepairBeforeAlarmEarnsBonusAndOpensDoorOnEvacuation()
        {
            session.BeginMission();
            Assert.That(door.activeSelf, "开局舱门关闭");

            yield return driver.ReplaceFuse();
            Assert.That(driver.Fuse.IsFixed, "备用保险丝插入后驱动通电");

            yield return driver.TurnValve(720f);
            Assert.That(driver.Valve.IsFixed, "转两圈后压差进入绿区: " + driver.Valve.Valve.Angle);

            var tool = Object.FindAnyObjectByType<RepairTool>();
            yield return driver.UnboltAll(tool);
            Assert.That(driver.Latches.IsFixed);
            Assert.That(airlock.AllFixed);

            yield return driver.PullLever(airlock.Lever != null ? 1.8f : 0f);
            Assert.That(airlock.IsReleased, "拉杆保持到开锁循环结束");

            session.Advance(0.5f);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Stabilized));
            Assert.That(mission.RepairRestored);
            Assert.That(door.activeSelf, "警报前门仍关着");
            session.Advance(mission.RemainingSeconds);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(mission.Config.BaseEvacuationSeconds + mission.Config.RepairBonusSeconds).Within(0.01f));
            Assert.That(door.activeSelf, Is.False, "放行后的气闸在撤离时打开");
        }

        [UnityTest] public IEnumerator LeverRefusesWhileFaultsRemain()
        {
            session.BeginMission();
            bool denied = false;
            airlock.Denied += () => denied = true;
            yield return driver.PullLever(2f);
            Assert.That(denied, "故障未排除时拉杆被拒绝");
            Assert.That(airlock.IsReleased, Is.False);
        }

        [UnityTest] public IEnumerator OverTurningTheValveVentsUntilTurnedBack()
        {
            session.BeginMission();
            yield return driver.TurnValve(900f);
            Assert.That(driver.Valve.OverPressure, "拧过头进入超压");
            Assert.That(driver.Valve.IsFixed, Is.False);
            yield return driver.TurnValve(-180f);
            Assert.That(driver.Valve.IsFixed, "往回拧进入绿区: " + driver.Valve.Valve.Angle);
        }

        [UnityTest] public IEnumerator UnrepairedAirlockStaysShutDuringEvacuationUntilReleased()
        {
            session.BeginMission();
            session.Advance(mission.Config.RepairWindowSeconds);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(mission.RepairRestored, Is.False);
            Assert.That(door.activeSelf, "警报不会让卡死的气闸自己打开");

            yield return driver.ReplaceFuse();
            yield return driver.TurnValve(720f);
            yield return driver.UnboltAll(Object.FindAnyObjectByType<RepairTool>());
            yield return driver.PullLever(1.8f);
            Assert.That(airlock.IsReleased);
            Assert.That(door.activeSelf, Is.False, "撤离中修好后立即开门");
            Assert.That(mission.RepairRestored, Is.False, "警报后完成不再给奖励");
        }

        [UnityTest] public IEnumerator RetryRestoresEveryFault()
        {
            session.BeginMission();
            yield return driver.ReplaceFuse();
            yield return driver.TurnValve(720f);
            Assert.That(airlock.FixedCount, Is.EqualTo(2));
            session.RetryMission();
            yield return AirlockTestDriver.Frames(3);
            Assert.That(airlock.FixedCount, Is.Zero);
            Assert.That(driver.Fuse.CoverOpen, Is.False);
            Assert.That(driver.Fuse.IsSeated(driver.Fuse.Burnt), "烧坏的保险丝回到插座");
            Assert.That(driver.Valve.Valve.Angle, Is.Zero);
            Assert.That(driver.Latches.Bolts.All(b => !b.Released));
            Assert.That(airlock.IsReleased, Is.False);
            Assert.That(door.activeSelf);
        }
    }
}
