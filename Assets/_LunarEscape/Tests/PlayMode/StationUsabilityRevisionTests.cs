using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    internal static class UsabilityTestSteps
    {
        public static void Move(CharacterController body, Vector3 point)
        {
            var center = body.transform.TransformPoint(body.center); body.enabled = false;
            body.transform.position += new Vector3(point.x - center.x, 0, point.z - center.z); body.enabled = true; Physics.SyncTransforms();
        }
        public static void ReturnSuit(DayOneMission day)
        {
            Move(day.Player, day.Suit.Handle.transform.position + Vector3.left * .6f);
            Assert.That(day.Suit.TryReturn(), Is.True, "在有空气的基地内归还墙架");
        }
        public static IEnumerator Capture(string name, Vector3 position, Quaternion rotation, float fov = 65)
        {
            var camera = new GameObject("Usability capture").AddComponent<Camera>(); camera.CopyFrom(Camera.main);
            camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            var render = new RenderTexture(1600, 1000, 24); var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                render.Create(); camera.targetTexture = render; camera.enabled = true;
                for (int i = 0; i < 4; i++) yield return null;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = render });
                RenderTexture.active = render; texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
                Directory.CreateDirectory("Docs/Previews"); File.WriteAllBytes("Docs/Previews/" + name + ".png", texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(camera.gameObject); Object.Destroy(texture); Object.Destroy(render); }
        }
    }

    public sealed class StationUsabilityRevisionTests
    {
        private float delta;
        private DayOneMission day;
        private StationPreparation preparation;
        private XRRayInteractor hand;
        private RemoteSocketPlacement placement;
        [UnitySetUp] public IEnumerator Load()
        {
            delta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return PreparationTestSteps.Morning();
            day = Object.FindAnyObjectByType<DayOneMission>(); preparation = Object.FindAnyObjectByType<StationPreparation>();
        }
        [TearDown] public void Cleanup() { Time.captureDeltaTime = delta; if (hand != null) Object.Destroy(hand.gameObject); }
        private void MakeHand()
        {
            hand = new GameObject("Remote placement test hand").AddComponent<XRRayInteractor>(); hand.interactionLayers = -1;
            hand.interactionManager = Object.FindAnyObjectByType<XRInteractionManager>();
            hand.selectInput.inputSourceMode = hand.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            hand.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;
            placement = hand.gameObject.AddComponent<RemoteSocketPlacement>();
        }
        private void Hold(XRGrabInteractable grab)
        {
            hand.transform.SetPositionAndRotation(grab.transform.position, grab.transform.rotation);
            hand.selectInput.manualPerformed = true; hand.selectInput.manualValue = 1;
            hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
        }
        private void Aim(XRSocketInteractor socket, Vector3 approach)
        {
            var point = socket.attachTransform.position;
            hand.transform.SetPositionAndRotation(point + approach, Quaternion.LookRotation(-approach)); Physics.SyncTransforms();
        }
        private void Trigger(bool down) { hand.activateInput.manualValue = down ? 1 : 0; hand.activateInput.manualPerformed = down; }

        [UnityTest] public IEnumerator TriggerPlacesHeldSuppliesAtDistanceAndChargingActuallyAdvances()
        {
            var inputs = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>().GetComponentsInChildren<NearFarInteractor>(true);
            Assert.That(inputs.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(inputs.All(h => h.GetComponent<RemoteSocketPlacement>() != null), "左右真实手柄都安装远距放置");
            MakeHand(); yield return null;
            foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery })
            {
                var item = preparation.Cargo.First(c => c.Item.Kind == kind && c.gameObject.activeSelf);
                var dock = preparation.Docks.First(d => d.Kind == kind);
                Hold(item.Item.Grab); yield return null;
                Aim(dock.Socket, (kind == CargoKind.Oxygen ? Vector3.forward : Vector3.right) * 1.5f + Vector3.up * .5f);
                for (int i = 0; i < 4; i++) yield return null;
                Assert.That(placement.Target, Is.SameAs(dock.Socket), kind + " 接口应成为瞄准目标");
                Assert.That(item.Fraction, Is.Zero);
                Assert.That(placement.TryPlace(preparation.Docks.First(d => d.Kind != kind).Socket), Is.False, "错误接口不放置");
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position = (hand.transform.position + dock.Socket.attachTransform.position) * .5f; blocker.transform.localScale = Vector3.one * .35f;
                Physics.SyncTransforms(); Assert.That(placement.TryPlace(dock.Socket), Is.False, "不能隔着实体障碍放入");
                Object.Destroy(blocker); yield return null;
                Trigger(true);
                for (int i = 0; i < 12; i++) yield return null;
                Assert.That(dock.Connected, Is.SameAs(item), "扣扳机真实输入应把手里物品交给插槽");
                Assert.That(item.Fraction, Is.GreaterThan(0)); Assert.That(item.PreparedOnDayOne, Is.True);
                Assert.That(hand.IsSelecting(item.Item.Grab), Is.False, "不同时挂在手和插槽上");
                Trigger(false); hand.selectInput.manualPerformed = false; hand.selectInput.manualValue = 0;
                yield return null;
            }
            yield return UsabilityTestSteps.Capture("usability-charge-ports", new Vector3(-12.6f, 1.8f, 1.8f), Quaternion.LookRotation(new Vector3(-2.1f, -.5f, -.7f)), 75);
            var kit = preparation.Cargo.First(c => c.Item.Kind == CargoKind.RepairKit && c.gameObject.activeSelf).Item;
            Hold(kit.Grab); yield return null;
            var top = new Vector3(-5.40f, 1.22f, -2.48f);
            hand.transform.SetPositionAndRotation(top + new Vector3(-1.2f, .65f, 0), Quaternion.LookRotation(new Vector3(1.2f, -.65f, 0)));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(placement.HasSurfaceTarget, Is.True, "普通物品也能瞄准台面放置"); Trigger(true);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(kit.State, Is.EqualTo(CargoState.World)); Assert.That(kit.Grab.isSelected, Is.False);
            Assert.That(Vector2.Distance(new Vector2(kit.transform.position.x, kit.transform.position.z), new Vector2(top.x, top.z)), Is.LessThan(.25f));
            Assert.That(kit.transform.position.y, Is.GreaterThan(1.20f));
            preparation.CarryToNextDay(); yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport"); PreparationTestSteps.DisableSimulator();
            var session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false; yield return ExpansionTestSteps.Wake(session);
            var life = session.GetComponent<LifeSupportMission>(); life.RestorePreparedSuit(50, 50);
            var steps = new LifeSupportSteps(session); MakeHand(); yield return null;
            try
            {
                steps.Don(); preparation = Object.FindAnyObjectByType<StationPreparation>();
                var oxygen = preparation.Cargo.First(c => c.Item.Kind == CargoKind.Oxygen && c.gameObject.activeSelf);
                var port = preparation.Docks.First(d => d.Kind == CargoKind.Oxygen);
                Hold(oxygen.Item.Grab); yield return null;
                Aim(port.Socket, Vector3.forward * 1.5f + Vector3.up * .5f);
                for (int i = 0; i < 4; i++) yield return null;
                float before = life.SuitOxygen; Trigger(true);
                for (int i = 0; i < 12; i++) yield return null;
                Assert.That(port.Connected, Is.SameAs(oxygen)); Assert.That(oxygen.Item.State, Is.EqualTo(CargoState.World));
                Assert.That(life.SuitOxygen, Is.EqualTo(before), "第二天瞄准放置时不能同时消耗物资给航天服补给");
            }
            finally { steps.Dispose(); }
        }

        [UnityTest] public IEnumerator HealthyFuseStaysInOpenableBoxThenBurntFuseCanBeReplacedOnDayTwo()
        {
            var fuse = Object.FindAnyObjectByType<FuseFault>(); Assert.That(fuse, Is.Not.Null);
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
            Assert.That(fuse.Burnt.Good, Is.True); Assert.That(fuse.IsFixed, Is.True);
            Assert.That(Vector3.Distance(fuse.Burnt.transform.position, fuse.Socket.attachTransform.position), Is.LessThan(.04f));
            MakeHand(); yield return null;
            hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)fuse.Cover);
            hand.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)fuse.Cover);
            for (int i = 0; i < 18; i++) yield return null;
            Assert.That(fuse.CoverOpen, Is.True); Assert.That(fuse.IsFixed, Is.True);
            Assert.That(hand.interactionManager.IsSelectPossible(hand, fuse.Burnt.Grab), Is.False, "第一天可检查完好芯，无需拆坏正常设备");
            var view = fuse.Socket.transform.position + fuse.Socket.transform.forward * 1.05f + Vector3.up * .08f;
            yield return UsabilityTestSteps.Capture("usability-day1-fuse-open", view, Quaternion.LookRotation(fuse.Socket.transform.position - view), 50);
            preparation.CarryToNextDay(); yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport"); PreparationTestSteps.DisableSimulator();
            var session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false; yield return ExpansionTestSteps.Wake(session);
            fuse = Object.FindAnyObjectByType<FuseFault>();
            Assert.That(fuse.Burnt.Good, Is.False); Assert.That(fuse.Socket.IsSelecting(fuse.Burnt.Grab), Is.True); Assert.That(fuse.IsFixed, Is.False);
            MakeHand(); yield return null;
            hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)fuse.Cover);
            hand.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)fuse.Cover);
            for (int i = 0; i < 18; i++) yield return null;
            Assert.That(hand.interactionManager.IsSelectPossible(hand, fuse.Burnt.Grab), Is.True);
            Hold(fuse.Burnt.Grab);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(hand.IsSelecting(fuse.Burnt.Grab), Is.True, "打开箱盖后实际握住坏芯");
            Assert.That(fuse.Socket.hasSelection, Is.False, "拔出后插座应立即腾空");
            hand.transform.position += fuse.Socket.transform.forward * .4f + fuse.Socket.transform.right * .5f;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            hand.interactionManager.CancelInteractableSelection((IXRSelectInteractable)fuse.Burnt.Grab);
            hand.selectInput.manualPerformed = false; hand.selectInput.manualValue = 0; yield return null;
            Hold(fuse.Spare.Grab); yield return null;
            Aim(fuse.Socket, fuse.Socket.transform.forward * 1.2f + Vector3.up * .1f);
            for (int i = 0; i < 4; i++) yield return null;
            var blockers = Physics.RaycastAll(hand.transform.position, (fuse.Socket.attachTransform.position - hand.transform.position).normalized, 1.25f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Select(h => h.collider.name + "@" + h.distance.ToString("0.00"));
            Assert.That(placement.Target, Is.SameAs(fuse.Socket), $"可接受={placement.Accepts(fuse.Socket, fuse.Spare.Grab)}，路径={placement.HasClearPath(fuse.Socket, fuse.Spare.Grab)}，箱盖={fuse.CoverOpen}，插槽占用={fuse.Socket.firstInteractableSelected}，握持={hand.firstInteractableSelected}，遮挡={string.Join(",", blockers)}"); Trigger(true);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(fuse.IsFixed, Is.True, "第二天拔出烧坏芯，远距放入备用芯后供电恢复");
        }

        [UnityTest] public IEnumerator SuitUseReturnSleepAndRefillPreserveTheActualRemainingResources()
        {
            Assert.That(day.Suit.Oxygen, Is.EqualTo(100)); Assert.That(day.Suit.Power, Is.EqualTo(100));
            day.Suit.Don(); UsabilityTestSteps.Move(day.Player, new Vector3(11, 0, 5)); day.Suit.Advance(180, true);
            Assert.That(day.Suit.TryReturn(), Is.False, "舱外不能脱下并归还航天服");
            foreach (var panel in day.Panels) panel.CleanInstantly(); foreach (var site in day.Sites) site.CompleteInstantly();
            for (int i = 0; i < 25; i++) yield return null;
            for (int i = 0; i < 3; i++) PreparationTestSteps.Connect(day.Rack.Slots[i], day.Sites[i].Core.Grab);
            Assert.That(day.Sleep(), Is.False, "必须先归还航天服才可睡觉");
            UsabilityTestSteps.ReturnSuit(day); float oxygen = day.Suit.Oxygen, power = day.Suit.Power;
            Assert.That(oxygen, Is.InRange(89, 91)); Assert.That(power, Is.InRange(91, 93));
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(day.Suit.Oxygen, Is.EqualTo(oxygen)); Assert.That(day.Suit.Power, Is.EqualTo(power));
            foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery })
            {
                var item = preparation.Cargo.First(c => c.Item.Kind == kind && c.gameObject.activeSelf);
                var dock = preparation.Docks.First(d => d.Kind == kind); PreparationTestSteps.Connect(dock.Socket, item.Item.Grab); dock.Advance(20);
            }
            Assert.That(day.Sleep(), Is.True);
            for (int i = 0; i < 350 && SceneManager.GetActiveScene().name != "11_LunarStation_LifeSupport"; i++) yield return null;
            PreparationTestSteps.DisableSimulator(); yield return null;
            var session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false; yield return ExpansionTestSteps.Wake(session);
            var life = session.GetComponent<LifeSupportMission>();
            Assert.That(life.SuitWorn, Is.False); Assert.That(life.SuitOxygen, Is.EqualTo(oxygen)); Assert.That(life.SuitPower, Is.EqualTo(power));
            var steps = new LifeSupportSteps(session); MakeHand(); yield return null;
            try
            {
                steps.Don(); Assert.That(session.GetComponent<SuitHudPresenter>().Hud.activeSelf, Is.True);
                foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery })
                {
                    var item = life.Inventory.Items.First(c => c.Kind == kind && c.gameObject.activeSelf); Hold(item.Grab);
                    Assert.That(life.TryUseSupply(item), Is.True);
                    Assert.That(kind == CargoKind.Oxygen ? life.SuitOxygen : life.SuitPower, Is.EqualTo(100));
                }
                session.RetryMission();
                Assert.That(life.SuitWorn, Is.False); Assert.That(life.SuitOxygen, Is.EqualTo(oxygen)); Assert.That(life.SuitPower, Is.EqualTo(power));
            }
            finally { steps.Dispose(); }
        }

        [UnityTest] public IEnumerator RearArrayAndWallLaboratoryAreReachableAndIdenticalAcrossDays()
        {
            var poses = day.Panels.Select(p => p.transform.position).ToArray();
            foreach (var panel in day.Panels)
            {
                Assert.That(panel.transform.position.x, Is.LessThan(-18));
                Assert.That(Vector3.Angle(-panel.DustLayer.transform.forward, Vector3.up), Is.EqualTo(30).Within(.05f));
                Assert.That(panel.DustLayer.bounds.size.x, Is.GreaterThan(3.4f));
                Assert.That(panel.GetComponentInChildren<BoxCollider>(), Is.Not.Null);
            }
            Assert.That(GameObject.Find("Solar Array B"), Is.Null);
            var oldTable = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Sample sorting table"); Assert.That(oldTable, Is.Null);
            var body = day.Player; UsabilityTestSteps.Move(body, new Vector3(-7.8f, 0, -.3f));
            var start = body.transform.position;
            for (int i = 0; i < 70; i++) { body.Move(Vector3.back * .04f); yield return null; }
            Assert.That(body.transform.position.z, Is.LessThan(start.z - 2.5f), "移除桌子后可以走到墙上分析仪前");
            var rackPosition = day.Rack.transform.position;
            Assert.That(rackPosition.z, Is.LessThan(-3.8f));
            UsabilityTestSteps.Move(body, new Vector3(-7.5f, 0, 1.2f));
            yield return UsabilityTestSteps.Capture("usability-rear-solar", new Vector3(-17.1f, 3.2f, 8.8f), Quaternion.LookRotation(new Vector3(-4.2f, -1.7f, -6.8f)), 75);
            yield return UsabilityTestSteps.Capture("usability-day1-laboratory", new Vector3(-7.5f, 1.7f, -.1f), Quaternion.LookRotation(new Vector3(.15f, -.20f, -3)), 75);
            preparation.CarryToNextDay(); yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport"); PreparationTestSteps.DisableSimulator(); yield return null;
            preparation = Object.FindAnyObjectByType<StationPreparation>();
            Assert.That(preparation.Panels.Select(p => p.transform.position).ToArray(), Is.EqualTo(poses));
            Assert.That(preparation.Research.Rack.transform.position, Is.EqualTo(rackPosition));
        }
    }
}
