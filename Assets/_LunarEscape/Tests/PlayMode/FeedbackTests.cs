using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LunarEscape.Tests
{
    // 震动与音效反馈：合成音频有效、场景已安装反馈组件、声音只跟随真实玩法状态出现。
    // 真实手柄震动无法在编辑器中测量，这里只验证连接关系和声音；震动手感需在头显上确认。
    public sealed class FeedbackTests
    {
        private StationMissionSession session; private AscentMission flight; private DockingMission docking;
        private CargoInventory cargo; private HatchBoardingController hatch; private FlightFeedback feedback;
        private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            // 与 LifeSupportTests 相同：键鼠模拟器运行时会生成自己的界面按钮，不属于游戏面板。
            foreach (var simulator in UnityEngine.Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None)) simulator.gameObject.SetActive(false);
            yield return null;
            session = UnityEngine.Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false;
            flight = session.GetComponent<AscentMission>(); docking = session.GetComponent<DockingMission>();
            cargo = session.GetComponent<CargoInventory>(); hatch = UnityEngine.Object.FindAnyObjectByType<HatchBoardingController>();
            feedback = flight.GetComponent<FlightFeedback>();
            while (Time.time < 1.05f) yield return null;
            yield return ExpansionTestSteps.Wake(session);
        }

        [TearDown] public void Restore() => Time.captureDeltaTime = previousDelta;

        [Test] public void SynthesizedClipsAreFiniteAndLoopsAreSeamless()
        {
            foreach (FeedbackSound sound in Enum.GetValues(typeof(FeedbackSound)))
            {
                var clip = FeedbackSounds.Get(sound);
                Assert.That(clip, Is.Not.Null, sound.ToString());
                Assert.That(clip.samples, Is.GreaterThan(100), sound.ToString());
                var data = new float[clip.samples]; clip.GetData(data, 0);
                Assert.That(data.All(v => float.IsFinite(v) && Mathf.Abs(v) <= 1f), sound.ToString());
                Assert.That(data.Max(v => Mathf.Abs(v)), Is.GreaterThan(0.01f), sound + " 不能是静音");
                Assert.That(FeedbackSounds.Get(sound), Is.SameAs(clip), "同一种声音只生成一次");
            }
            // 循环点（末尾→开头）的跳变不能大于声音内部相邻样本的最大跳变，否则每次循环都会“咔哒”一下。
            foreach (var sound in new[] { FeedbackSound.RepairLoop, FeedbackSound.ThrusterLoop, FeedbackSound.EngineLoop })
            {
                var clip = FeedbackSounds.Get(sound); var data = new float[clip.samples]; clip.GetData(data, 0);
                float maxStep = 0f;
                for (int i = 1; i < data.Length; i++) maxStep = Mathf.Max(maxStep, Mathf.Abs(data[i] - data[i - 1]));
                Assert.That(Mathf.Abs(data[data.Length - 1] - data[0]), Is.LessThanOrEqualTo(maxStep + 1e-4f), sound.ToString());
            }
        }

        [Test] public void MainSceneHasFeedbackWiredWithoutChangingButtons()
        {
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(buttons, Is.Not.Empty);
            // 键鼠模拟器自带的开发界面不属于游戏面板，不需要反馈。
            bool IsSimulatorUi(Button b) { for (var t = b.transform; t != null; t = t.parent) if (t.name.Contains("Simulator")) return true; return false; }
            var missing = buttons.Where(b => !IsSimulatorUi(b) && b.GetComponent<ButtonPressFeedback>() == null).Select(b => b.transform.parent != null ? b.transform.parent.name + "/" + b.name : b.name).ToArray();
            Assert.That(missing, Is.Empty, "请先执行 Lunar Escape → Install Interaction Feedback (All Scenes)；缺少反馈的按钮：" + string.Join(", ", missing));
            Assert.That(feedback, Is.Not.Null);
            Assert.That(session.GetComponent<AlarmFeedback>(), Is.Not.Null);
            Assert.That(cargo.GetComponent<CargoFeedback>(), Is.Not.Null);
            Assert.That(session.Mission.RepairTask.GetComponent<RepairFeedback>(), Is.Not.Null);
            Assert.That(cargo.Items.All(i => i.GetComponent<GrabFeedback>() != null));
            Assert.That(UnityEngine.Object.FindObjectsByType<RepairTool>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .All(t => t.GetComponent<GrabFeedback>() != null));
        }

        [UnityTest] public IEnumerator EngineAndThrusterSoundsFollowFlightState()
        {
            var engine = Loop("Engine Loop Audio"); var thruster = Loop("Thruster Loop Audio");
            yield return Frames(5);
            Assert.That(engine.volume, Is.EqualTo(0f), "未点火前没有发动机声");

            Board(); flight.PowerOn(); flight.StartNavigation(); flight.Tick(2); flight.PrepareEngine(); flight.Tick(2);
            flight.Ignite(); flight.Tick(1.5f);
            yield return Frames(15);
            Assert.That(engine.volume, Is.GreaterThan(0.05f), "点火中发动机声逐渐响起");

            flight.Tick(1.5f); Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Ascent));
            flight.Tick(flight.OrbitAscentSeconds - flight.AirborneSeconds); flight.Circularize(); flight.Tick(6);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Rendezvous));
            yield return Frames(30);
            Assert.That(engine.volume, Is.LessThan(0.01f), "入轨后滑行时发动机安静");
            Assert.That(thruster.volume, Is.EqualTo(0f));

            docking.SetCommand(DockCommand.Left, true);
            yield return Frames(10);
            Assert.That(thruster.volume, Is.GreaterThan(0.1f), "按住 RCS 按钮时有喷气声");
            docking.SetCommand(DockCommand.Left, false);
            yield return Frames(10);
            Assert.That(thruster.volume, Is.EqualTo(0f), "松手立即停止喷气声");
        }

        private AudioSource Loop(string name) => feedback.GetComponentsInChildren<AudioSource>(true).Single(s => s.name == name);

        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }

        // 生命保障主场景：穿航天服、解除气闸、开门、走到梯旁登舱。
        private void Board()
        {
            var steps = new LifeSupportSteps(session);
            try { steps.Board(flight); } finally { steps.Dispose(); }
        }
    }
}
