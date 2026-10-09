using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LunarEscape.Tests
{
    // 气闸 A 抢修（生命保障主场景）：三处故障用三种不同手部操作排除，拉杆完成循环后解除门锁；
    // 开门仍走生命保障规则（压力屏开门、没穿航天服致命）；重开全部复原。
    public sealed class AirlockRepairTests
    {
        private StationMissionSession session; private StationMission mission; private LifeSupportMission life; private AirlockRepair airlock;
        private AirlockTestDriver driver; private LifeSupportSteps steps; private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            foreach (var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None)) simulator.gameObject.SetActive(false);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false;
            mission = session.Mission; life = session.GetComponent<LifeSupportMission>();
            airlock = Object.FindAnyObjectByType<AirlockRepair>();
            Assert.That(airlock, Is.Not.Null, "请先执行 Lunar Escape → Install Airlock Repair (Life Support Scene)");
            driver = new AirlockTestDriver(airlock); steps = new LifeSupportSteps(session);
            while (Time.time < 1.05f) yield return null;
        }

        [TearDown] public void Restore() { Time.captureDeltaTime = previousDelta; driver?.Dispose(); steps?.Dispose(); }

        [Test] public void SceneUsesAirlockRepairInsteadOfToolLatch()
        {
            Assert.That(life.AirlockRepair, Is.SameAs(airlock));
            Assert.That(life.DoorRepairContact, Is.Null, "旧的门锁工具维修已移除");
            Assert.That(airlock.Faults.Length, Is.EqualTo(3));
            Assert.That(driver.Fuse.Socket.hasSelection || driver.Fuse.Socket.startingSelectedInteractable != null, "开局烧坏的保险丝插在座里");
            Assert.That(driver.Latches.Bolts.Length, Is.EqualTo(3));
            Assert.That(driver.Latches.Bolts.Select(b => b.transform.position.y).Min(), Is.LessThan(0.6f), "最低一颗螺栓需要蹲下");
        }

        [UnityTest] public IEnumerator NothingCanBeRepairedBeforeTheMissionStarts()
        {
            yield return driver.TurnValve(700f);
            Assert.That(driver.Valve.Valve.Angle, Is.Zero, "开始任务前手轮不计数");
            Assert.That(airlock.FixedCount, Is.Zero);
        }

        [UnityTest] public IEnumerator ThreeRepairsAndLeverUnlockHatchThenSuitedCrewOpensIt()
        {
            session.BeginMission();
            yield return driver.ReplaceFuse();
            Assert.That(driver.Fuse.IsFixed, "备用保险丝插入后驱动通电");
            yield return driver.TurnValve(720f);
            Assert.That(driver.Valve.IsFixed, "转两圈后压差进入绿区: " + driver.Valve.Valve.Angle);
            yield return driver.UnboltAll(Object.FindAnyObjectByType<RepairTool>());
            Assert.That(driver.Latches.IsFixed);
            Assert.That(airlock.AllFixed);
            Assert.That(life.DoorRepaired, Is.False, "拉杆前门锁仍在");

            yield return driver.PullLever(1.8f);
            Assert.That(airlock.IsReleased, "拉杆保持到开锁循环结束");
            Assert.That(life.DoorRepaired, "生命保障认为舱门已修好");
            Assert.That(life.DoorOpen, Is.False, "解除门锁不会自己开门");

            steps.Don();
            steps.MoveBody(life.DoorControl.position + Vector3.left * .8f);
            Assert.That(life.TryOpenDoor(), Is.True);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
        }

        [UnityTest] public IEnumerator LeverRefusesWhileFaultsRemain()
        {
            session.BeginMission();
            bool denied = false;
            airlock.Denied += () => denied = true;
            yield return driver.PullLever(2f);
            Assert.That(denied, "故障未排除时拉杆被拒绝");
            Assert.That(airlock.IsReleased, Is.False);
            Assert.That(life.DoorRepaired, Is.False);
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
        }
    }
}
