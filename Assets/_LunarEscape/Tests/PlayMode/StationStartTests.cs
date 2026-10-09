using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace LunarEscape.Tests
{
    // 生命保障主场景开局：出生在开始屏前；整站断电，需拉下照明总闸；关键行动奖励时间；
    // 圆盘可向任意方向平移且移速提高。
    public sealed class StationStartTests
    {
        private StationMissionSession session; private StationMission mission; private LifeSupportMission life;
        private HabitatBreaker breaker; private MissionTimeBonus bonus; private XRInteractionManager manager;
        private PilotTestHand hand; private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            foreach (var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None)) simulator.gameObject.SetActive(false);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false;
            mission = session.Mission; life = session.GetComponent<LifeSupportMission>();
            breaker = Object.FindAnyObjectByType<HabitatBreaker>();
            bonus = session.GetComponent<MissionTimeBonus>();
            Assert.That(breaker, Is.Not.Null, "请先执行 Lunar Escape → Install Station Start: Lights, Spawn, Movement (Life Support Scene)");
            manager = Object.FindAnyObjectByType<XRInteractionManager>();
            hand = new GameObject("Station start test hand").AddComponent<PilotTestHand>();
            hand.interactionLayers = -1;
            while (Time.time < 1.05f) yield return null;
        }

        [TearDown] public void Restore() { Time.captureDeltaTime = previousDelta; if (hand != null) Object.Destroy(hand.gameObject); }

        [Test] public void PlayerStartsInFrontOfTheStartConsoleFacingIt()
        {
            var spawn = session.SpawnPoint.position;
            Assert.That(spawn.z, Is.InRange(1.8f, 2.6f), "出生在工作台与物资架之间");
            Assert.That(Vector3.Dot(session.SpawnPoint.forward, Vector3.forward), Is.GreaterThan(0.95f), "面朝开始屏");
            var body = session.Exit.PlayerBody; var center = body.transform.TransformPoint(body.center);
            Assert.That(Vector2.Distance(new Vector2(center.x, center.z), new Vector2(spawn.x, spawn.z)), Is.LessThan(0.3f), "载入后玩家就在出生点");
            var overlaps = Physics.OverlapCapsule(new Vector3(spawn.x, 0.4f, spawn.z), new Vector3(spawn.x, 1.5f, spawn.z), 0.25f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(c => c != body && !c.transform.IsChildOf(session.Player.transform)).Select(c => c.name).ToArray();
            Assert.That(overlaps, Is.Empty, "出生点不与家具重叠：" + string.Join(", ", overlaps));
        }

        [Test] public void TouchpadMovesInEveryDirectionAndFaster()
        {
            var moves = Object.FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(moves, Is.Not.Empty);
            foreach (var move in moves)
            {
                Assert.That(move.enableStrafe, Is.True, "圆盘左右也能平移");
                Assert.That(move.moveSpeed, Is.EqualTo(1.75f).Within(0.001f), "移速比原来的 1.4 m/s 快 25%");
                var action = move.leftHandMoveInput.inputActionReference.action;
                Assert.That(action.bindings.Any(b => b.processors.Contains("scaleVector2")), Is.False, "不再只保留前后分量");
            }
        }

        [UnityTest] public IEnumerator HabitatStaysDarkUntilTheBreakerIsPulledAfterStart()
        {
            var lamps = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.name == "Habitat interior lamp").ToArray();
            Assert.That(lamps, Is.Not.Empty);
            yield return null;
            Assert.That(lamps.All(l => l.intensity == 0f), "开局整站断电");
            Assert.That(breaker.IsOn, Is.False);

            yield return PullBreaker();
            Assert.That(breaker.IsOn, Is.False, "开始任务前总闸联锁");

            session.BeginMission();
            float remaining = mission.RemainingSeconds; float air = life.BaseOxygen;
            yield return PullBreaker();
            Assert.That(breaker.IsOn, "拉下总闸恢复照明");
            for (float t = 0; t < 2f; t += Time.deltaTime) yield return null;
            Assert.That(lamps.All(l => l.intensity > 0.5f), "闪烁后稳定亮起");
            Assert.That(mission.RemainingSeconds, Is.EqualTo(remaining + bonus.LightsSeconds).Within(0.01f), "恢复照明奖励时间");
            Assert.That(life.BaseOxygen, Is.GreaterThan(air), "奖励同时补充基地空气");

            session.RetryMission();
            yield return null; yield return null;
            Assert.That(breaker.IsOn, Is.False, "重新开始后再次断电");
            Assert.That(lamps.All(l => l.intensity == 0f));
        }

        [UnityTest] public IEnumerator AirlockRepairEarnsTimeOncePerAttempt()
        {
            session.BeginMission();
            float remaining = mission.RemainingSeconds;
            life.AirlockRepair.SkipRepair();
            yield return null;
            Assert.That(mission.RemainingSeconds, Is.EqualTo(remaining + bonus.AirlockSeconds).Within(0.01f));
            life.AirlockRepair.SkipRepair();
            Assert.That(mission.RemainingSeconds, Is.EqualTo(remaining + bonus.AirlockSeconds).Within(0.01f), "同一轮只奖励一次");
        }

        [Test] public void SuitStartsFullAndBaseHasMoreAir()
        {
            Assert.That(life.SuitOxygen, Is.EqualTo(100f).Within(0.01f));
            Assert.That(life.SuitPower, Is.EqualTo(100f).Within(0.01f));
            Assert.That(life.Config.BaseOxygenRange.x, Is.GreaterThanOrEqualTo(70f));
            Assert.That(mission.Config.RepairWindowSeconds, Is.GreaterThanOrEqualTo(200f));
        }

        // 握住总闸手柄，沿弧线向下、向自己拉到底，然后松手。
        private IEnumerator PullBreaker()
        {
            var lever = breaker.Lever;
            const float radius = 0.3f;
            hand.transform.position = lever.transform.TransformPoint(new Vector3(0f, radius, 0f));
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)lever);
            for (float a = 0f; a <= 110f; a += 5f)
            {
                float r = a * Mathf.Deg2Rad;
                hand.transform.position = lever.transform.TransformPoint(new Vector3(0f, Mathf.Cos(r) * radius, Mathf.Sin(r) * radius));
                yield return null;
            }
            yield return null;
            if (hand.IsSelecting(lever)) manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)lever);
            yield return null;
        }
    }
}
