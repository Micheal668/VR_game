using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    // 测试用的手：射线交互器，侧键与扳机都用手动数值驱动，走与真实手柄相同的输入读取路径。
    internal sealed class ToolHand
    {
        public readonly XRRayInteractor Hand;
        private readonly XRInteractionManager manager;

        public ToolHand()
        {
            manager = Object.FindAnyObjectByType<XRInteractionManager>();
            Hand = new GameObject("Tool test hand").AddComponent<XRRayInteractor>();
            Hand.interactionManager = manager;
            Hand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            Hand.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
        }

        public void Grab(TriggerTool tool) => manager.SelectEnter((IXRSelectInteractor)Hand, (IXRSelectInteractable)tool.Grab);
        public void Release(TriggerTool tool) { if (Hand.IsSelecting(tool.Grab)) manager.SelectExit((IXRSelectInteractor)Hand, (IXRSelectInteractable)tool.Grab); }
        public void Trigger(float value) { Hand.activateInput.manualValue = value; Hand.activateInput.manualPerformed = value > 0.5f; }

        // 让工具的作用端（喷嘴、钻头、激光口）落在指定位置并朝向指定方向：反推手的位置。
        public void Aim(TriggerTool tool, Vector3 muzzle, Vector3 direction, Vector3 up)
        {
            var rotation = Quaternion.LookRotation(direction, up) * Quaternion.Inverse(tool.Muzzle.localRotation);
            var offset = tool.Muzzle.localPosition - tool.Grab.attachTransform.localPosition;
            Hand.transform.SetPositionAndRotation(muzzle - rotation * offset, rotation);
        }

        public void Dispose() { if (Hand != null) Object.Destroy(Hand.gameObject); }
    }

    // 第一天（场景 12）：普通班次——气闸出舱、氮气吹板、取芯钻、样品入架、睡觉进入第二天。
    public sealed class DayOneTests
    {
        private DayOneMission day; private ToolHand hand; private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("12_LunarStation_DayOne");
            foreach (var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None)) simulator.gameObject.SetActive(false);
            yield return null; yield return null;
            day = Object.FindAnyObjectByType<DayOneMission>();
            Assert.That(day, Is.Not.Null, "请先执行 Lunar Escape → Build Day One Scene");
            hand = new ToolHand();
        }

        [TearDown] public void Restore() { Time.captureDeltaTime = previousDelta; hand?.Dispose(); }

        [UnityTest] public IEnumerator SceneIsAnOrdinaryShiftWithoutTheIncident()
        {
            Assert.That(Object.FindAnyObjectByType<StationMission>(), Is.Null, "第一天没有倒计时任务");
            Assert.That(Object.FindAnyObjectByType<LifeSupportMission>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<BeamBlockade>(), Is.Null, "第二天的钢梁不在第一天");
            Assert.That(day.Panels.Length, Is.EqualTo(4));
            Assert.That(day.Sites.Length, Is.EqualTo(3));
            Assert.That(day.Rack.Capacity, Is.EqualTo(3));
            yield return null;
            var holsters = Object.FindObjectsByType<ToolHolster>(FindObjectsSortMode.None);
            Assert.That(holsters.Length, Is.EqualTo(2));
            Assert.That(holsters.All(h => h.HasTool), "开局两件工具挂在气闸工具架上");
            Assert.That(RenderSettings.sun.intensity, Is.GreaterThan(1f), "月昼");
            Assert.That(Object.FindAnyObjectByType<SpaceAudio>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<MusicDirector>().Choose(), Is.EqualTo(MusicTrack.Calm), "舱内平静的音乐");
        }

        [UnityTest] public IEnumerator MorningCallFromMissionControlThenCommander()
        {
            yield return Said("cup_d1_morning", 12f);
            yield return Said("cmd_d1_wake", 20f);
            Assert.That(day.Current, Is.EqualTo(DayOneMission.Stage.Working));
        }

        [UnityTest] public IEnumerator AirlockNeedsTheSuitAndSealsAirInside()
        {
            var airlock = day.Airlock;
            bool denied = false; airlock.Denied += _ => denied = true;
            airlock.RequestOutside();
            Assert.That(denied, "没穿航天服不能泄压出舱");
            Assert.That(airlock.Pressurized);

            day.Suit.Don();
            airlock.RequestOutside();
            for (float t = 0; t < 8f && airlock.Cycling; t += Time.deltaTime) yield return null;
            Assert.That(airlock.Cycling, Is.False);
            Assert.That(airlock.OuterOpen && !airlock.InnerOpen, "外门开、内门关");
            Assert.That(airlock.Pressurized, Is.False);
            Move(airlock.Chamber.center);
            yield return null;
            Assert.That(day.PlayerHasAir, Is.False, "抽空的气闸室里没有空气");
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(Object.FindAnyObjectByType<SpaceAudio>().InVacuum, "真空中外界声音变闷");
            Move(new Vector3(12f, 0f, 1.7f));
            yield return null;
            Assert.That(Object.FindAnyObjectByType<MusicDirector>().Choose(), Is.EqualTo(MusicTrack.Outside), "舱外换成空旷的音乐");

            Move(airlock.Chamber.center);
            airlock.RequestInside();
            for (float t = 0; t < 8f && airlock.Cycling; t += Time.deltaTime) yield return null;
            Assert.That(airlock.Pressurized && airlock.InnerOpen && !airlock.OuterOpen);
            Assert.That(day.PlayerHasAir, "复压后气闸室有空气");
        }

        [UnityTest] public IEnumerator TriggerDrivesTheNitrogenJetThatCleansAPanel()
        {
            var blower = day.Blower; var panel = day.Panels[0];
            yield return null;
            var previous = hand.Hand.selectActionTrigger;
            hand.Grab(blower);
            yield return null;
            Assert.That(blower.IsHeld, "从工具架上拿起氮气枪");
            Assert.That(hand.Hand.selectActionTrigger, Is.EqualTo(XRBaseInputInteractor.InputTriggerType.Sticky), "拿着工具时这只手改为粘手握持");
            yield return null; yield return null;
            Assert.That(blower.IsHeld, "松开侧键工具也不会掉");

            var layer = panel.DustLayer.transform;
            Vector3 normal = -layer.forward;
            hand.Trigger(0f);
            hand.Aim(blower, layer.position + normal * 0.5f, -normal, layer.up);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(panel.Cleanliness, Is.Zero, "不扣扳机不出气");

            hand.Trigger(1f);
            float tank = blower.Tank;
            // 沿板面来回扫：5×3 个点，每点吹一会儿。
            for (int pass = 0; pass < 3 && !panel.IsClean; pass++)
                for (int y = -1; y <= 1 && !panel.IsClean; y++)
                    for (int x = -2; x <= 2 && !panel.IsClean; x++)
                    {
                        var point = layer.position + layer.right * (x * 0.3f) + layer.up * (y * 0.28f);
                        hand.Aim(blower, point + normal * 0.45f, -normal, layer.up);
                        for (float t = 0; t < 0.6f && !panel.IsClean; t += Time.deltaTime) yield return null;
                    }
            Assert.That(panel.IsClean, "吹净：" + panel.Cleanliness);
            Assert.That(blower.Tank, Is.LessThan(tank), "消耗氮气");
            Assert.That(day.PanelsClean, Is.EqualTo(1));
            hand.Trigger(0f);
            hand.Release(blower);
            yield return null;
            Assert.That(hand.Hand.selectActionTrigger, Is.EqualTo(previous), "放下后恢复原来的握持方式");
        }

        [UnityTest] public IEnumerator DrillAtTheRightSpeedProducesACoreButFullThrottleJams()
        {
            var drill = day.Drill; var site = day.Sites[0];
            yield return null;
            hand.Grab(drill);
            yield return null;
            // 扳机原始值 → 去掉死区后的转速：raw = 0.08 + rpm × 0.92。
            float Raw(float rpm) => 0.08f + rpm * 0.92f;
            // 键鼠模拟器的情形：手腕拧得很歪、手也够不到地面（钻头离地约 30 cm、偏离中心 30 cm）。
            hand.Hand.transform.SetPositionAndRotation(site.Surface + new Vector3(0.3f, 1.3f, 0f), Quaternion.Euler(60f, 40f, 35f));
            yield return null; yield return null;
            Assert.That(Vector3.Angle(drill.transform.up, Vector3.up), Is.LessThan(1f), "钻始终竖直，不随手腕歪斜");
            Assert.That(drill.Seated, Is.SameAs(site), "靠近定位环自动落进导向套");
            for (float t = 0; t < 25f && !site.Completed; t += Time.deltaTime)
            {
                hand.Trigger(Raw((site.Band.x + site.Band.y) * 0.5f));
                yield return null;
            }
            Assert.That(site.Completed, "合适转速下钻到目标深度：" + site.Depth);
            Assert.That(drill.Overheated, Is.False, "合适转速不会卡钻");
            for (float t = 0; t < 1f; t += Time.deltaTime) yield return null;
            Assert.That(site.Core.gameObject.activeInHierarchy, "岩芯从孔里顶出来");
            Assert.That(day.CoresDrilled, Is.EqualTo(1));

            var second = day.Sites[1];
            bool jammed = false; drill.Jammed += _ => jammed = true;
            for (float t = 0; t < 12f && !jammed; t += Time.deltaTime)
            {
                hand.Aim(drill, new Vector3(second.Surface.x, second.Bottom - 0.01f, second.Surface.z), Vector3.down, Vector3.forward);
                hand.Trigger(1f);
                yield return null;
            }
            Assert.That(jammed && drill.Overheated, "一直满转速会过热卡钻");
            hand.Trigger(0f);
            for (float t = 0; t < 6f && drill.Overheated; t += Time.deltaTime) yield return null;
            Assert.That(drill.Overheated, Is.False, "松开扳机冷却后恢复");
        }

        [UnityTest] public IEnumerator CoresInTheLabRackThenSleepLeadsToDayTwo()
        {
            for (float t = 0; t < 6f; t += Time.deltaTime) yield return null;
            day.Suit.Don();
            foreach (var panel in day.Panels) panel.CleanInstantly();
            foreach (var site in day.Sites) site.CompleteInstantly();
            for (float t = 0; t < 1f; t += Time.deltaTime) yield return null;
            Assert.That(day.Sleep(), Is.False, "活没干完不能睡");
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var slots = day.Rack.GetComponentsInChildren<XRSocketInteractor>();
            for (int i = 0; i < day.Sites.Length; i++)
            {
                var core = day.Sites[i].Core;
                core.transform.position = slots[i].attachTransform.position;
                manager.SelectEnter((IXRSelectInteractor)slots[i], (IXRSelectInteractable)core.Grab);
                yield return null;
            }
            Assert.That(day.CoresStored, Is.EqualTo(3));
            Assert.That(day.Current, Is.EqualTo(DayOneMission.Stage.Evening), "全部完成，收工");
            yield return Said("cup_d1_night", 40f);
            Assert.That(day.Sleep(), "收工后可以睡觉");
            for (float t = 0; t < 12f && SceneManager.GetActiveScene().name != "11_LunarStation_LifeSupport"; t += Time.deltaTime) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("11_LunarStation_LifeSupport"), "醒来就是第二天的事故");
        }

        private void Move(Vector3 point)
        {
            var body = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>().GetComponent<CharacterController>();
            var center = body.transform.TransformPoint(body.center);
            body.enabled = false; body.transform.position += new Vector3(point.x - center.x, 0f, point.z - center.z); body.enabled = true;
            Physics.SyncTransforms();
        }

        private IEnumerator Said(string id, float timeout)
        {
            for (float end = Time.time + timeout; Time.time < end && !day.Voice.History.Contains(id);) yield return null;
            Assert.That(day.Voice.History, Does.Contain(id), "应说出 " + id + "；已说：" + string.Join(", ", day.Voice.History));
        }
    }

    // 第二天（场景 11）：出口被钢梁堵住，用激光切割器沿切割线切断。
    public sealed class DayTwoCutterTests
    {
        private StationMissionSession session; private BeamBlockade blockade; private ToolHand hand; private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            foreach (var simulator in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None)) simulator.gameObject.SetActive(false);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false;
            blockade = Object.FindAnyObjectByType<BeamBlockade>();
            Assert.That(blockade, Is.Not.Null, "请先执行 Lunar Escape → Install Day Two: Exit Beams and Laser Cutter");
            hand = new ToolHand();
            while (Time.time < 1.05f) yield return null;
        }

        [TearDown] public void Restore() { Time.captureDeltaTime = previousDelta; hand?.Dispose(); }

        [UnityTest] public IEnumerator DayTwoOpensWithItsTitle()
        {
            var fade = Object.FindAnyObjectByType<ScreenFade>();
            Assert.That(fade, Is.Not.Null);
            yield return null;
            Assert.That(fade.Busy, "开场显示“第二天”标题");
        }

        [UnityTest] public IEnumerator BeamsBlockTheExitUntilCutAlongTheirLines()
        {
            Assert.That(blockade.Beams.Length, Is.EqualTo(3));
            Assert.That(blockade.Cleared, Is.False);
            var doorway = blockade.transform.position;
            Assert.That(Physics.CheckCapsule(new Vector3(doorway.x, 0.35f, doorway.z), new Vector3(doorway.x, 1.6f, doorway.z), 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), "钢梁堵住出口");

            var cutter = blockade.Cutter;
            yield return null;
            hand.Grab(cutter);
            yield return null;
            Assert.That(cutter.IsHeld, "从支架上取下切割器");
            hand.Trigger(0.75f);
            foreach (var beam in blockade.Beams)
                foreach (var line in beam.Cuts)
                {
                    // 光点贴近线（25 cm），沿线来回扫，直到切开；中途过热就松开冷却。
                    var facing = beam.transform.forward;
                    for (float t = 0, s = 0; t < 20f && !line.Severed; t += Time.deltaTime, s += Time.deltaTime)
                    {
                        if (cutter.Overheated) { hand.Trigger(0f); yield return null; continue; }
                        hand.Trigger(0.75f);
                        float k = Mathf.PingPong(s * 0.8f, 1f);
                        var point = Vector3.Lerp(line.WorldFrom, line.WorldTo, k);
                        hand.Aim(cutter, point + facing * 0.25f, -facing, Vector3.up);
                        yield return null;
                    }
                    Assert.That(line.Severed, beam.name + " " + line.name + " 进度 " + line.Progress);
                }
            hand.Trigger(0f);
            Assert.That(blockade.Cleared, "三根钢梁全部切落");
            for (float t = 0; t < 3f; t += Time.deltaTime) yield return null;
            Assert.That(blockade.Beams.All(b => b.transform.position.y < 1.4f || b.Dropped), "钢梁倒下");
        }

        [UnityTest] public IEnumerator FullPowerOnTheSteelOverheatsTheCutter()
        {
            var cutter = blockade.Cutter;
            yield return null;
            hand.Grab(cutter);
            yield return null;
            var beam = blockade.Beams[0];
            bool hot = false; cutter.OverheatedEvent += _ => hot = true;
            hand.Trigger(1f);
            // 打在梁中部（不在切割线上）：只冒火花不切，但照样发热。
            for (float t = 0; t < 8f && !hot; t += Time.deltaTime)
            {
                hand.Aim(cutter, beam.transform.position + beam.transform.forward * 0.25f, -beam.transform.forward, Vector3.up);
                yield return null;
            }
            Assert.That(hot && cutter.Overheated, "满功率几秒就过热");
            Assert.That(beam.Cuts.All(c => c.Progress == 0f), "打偏不算切");
        }
    }
}
