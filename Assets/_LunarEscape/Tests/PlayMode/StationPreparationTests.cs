using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    internal static class PreparationTestSteps
    {
        public static void DisableSimulator()
        { foreach (var s in Object.FindObjectsByType<CollisionAwareSimulator>(FindObjectsSortMode.None)) s.gameObject.SetActive(false); }
        public static IEnumerator Morning()
        {
            yield return SceneManager.LoadSceneAsync("12_LunarStation_DayOne"); DisableSimulator();
            var day = Object.FindAnyObjectByType<DayOneMission>();
            for (int i = 0; i < 220 && day.Current == DayOneMission.Stage.Waking; i++) yield return null;
            Assert.That(day.Current, Is.EqualTo(DayOneMission.Stage.Working));
        }
        public static void Connect(XRSocketInteractor socket, XRGrabInteractable grab)
        {
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            if (grab.isSelected) manager.CancelInteractableSelection((IXRSelectInteractable)grab);
            manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)grab);
            Assert.That(socket.IsSelecting(grab), Is.True);
        }
        public static void Disconnect(XRGrabInteractable grab)
        { if (grab.isSelected) grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab); }
        // 旧的补给、HUD 与飞行用例从“第一天已准备齐全”的真实工作站流程开始。
        public static IEnumerator PreparedDayTwo()
        {
            yield return Morning();
            var prep = Object.FindAnyObjectByType<StationPreparation>();
            foreach (var item in prep.Cargo.Where(c => c.NeedsCharging && c.gameObject.activeSelf))
            {
                var port = prep.Docks.First(d => d.Kind == item.Item.Kind);
                Connect(port.Socket, item.Item.Grab); port.Advance(20); Disconnect(item.Item.Grab);
                Assert.That(item.Fraction, Is.EqualTo(1));
            }
            foreach (var site in prep.Sites) site.CompleteInstantly();
            for (int i = 0; i < 25; i++) yield return null;
            for (int i = 0; i < prep.Research.Cores.Length; i++) Connect(prep.Research.Rack.Slots[i], prep.Research.Cores[i].Grab);
            prep.Research.Advance(20);
            Assert.That(prep.Research.Completed, Is.EqualTo(3));
            prep.CarryToNextDay();
            yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport"); DisableSimulator(); yield return null;
        }
    }

    public sealed class StationPreparationTests
    {
        private StationPreparation prep;
        private float oldDelta;
        [UnitySetUp] public IEnumerator Load()
        {
            oldDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return PreparationTestSteps.Morning(); prep = Object.FindAnyObjectByType<StationPreparation>();
            Assert.That(prep, Is.Not.Null); Assert.That(prep.Initialized, Is.True);
        }
        [TearDown] public void Restore() => Time.captureDeltaTime = oldDelta;
        private PreparedCargo Supply(CargoKind kind) => prep.Cargo.First(c => c.Item.Kind == kind && c.gameObject.activeSelf);
        private SupplyChargingDock Port(CargoKind kind) => prep.Docks.First(d => d.Kind == kind);
        private IEnumerator NextDay()
        {
            prep.CarryToNextDay(); yield return SceneManager.LoadSceneAsync("11_LunarStation_LifeSupport");
            PreparationTestSteps.DisableSimulator(); yield return null;
            prep = Object.FindAnyObjectByType<StationPreparation>();
        }
        private static IEnumerator Wake(StationMissionSession session)
        { session.enabled = false; yield return ExpansionTestSteps.Wake(session); }

        [UnityTest] public IEnumerator EmptySuppliesRequireTheCorrectPhysicalPortAndPauseWhenRemoved()
        {
            Assert.That(prep.Cargo.Length, Is.EqualTo(18)); Assert.That(prep.Docks.Length, Is.EqualTo(6));
            Assert.That(prep.Cargo.Where(c => c.NeedsCharging).All(c => c.Fraction == 0 && !c.PreparedOnDayOne), Is.True);
            Assert.That(prep.Cargo.Where(c => c.IsResearch).All(c => !c.gameObject.activeSelf && !c.CanTake), Is.True);
            var oxygen = Supply(CargoKind.Oxygen); var battery = Supply(CargoKind.Battery); var port = Port(CargoKind.Oxygen);
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            Assert.That(port.Accepts(battery.Item), Is.False); Assert.That(manager.IsSelectPossible(port.Socket, battery.Item.Grab), Is.False);
            Assert.That(manager.IsSelectPossible(prep.Research.Rack.Slots[0], oxygen.Item.Grab), Is.False);
            var hand = new GameObject("Preparation hand").AddComponent<PilotTestHand>();
            hand.interactionLayers = -1;
            try
            {
                hand.transform.SetPositionAndRotation(oxygen.transform.position, oxygen.transform.rotation);
                yield return null;
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)oxygen.Item.Grab);
                yield return null; yield return null;
                var grab = oxygen.Item.Grab;
                Assert.That(hand.IsSelecting(grab), Is.True);
                hand.transform.position += port.Socket.attachTransform.position - grab.GetAttachTransform(port.Socket).position;
                for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(grab.GetAttachTransform(port.Socket).position, port.Socket.attachTransform.position), Is.LessThan(.10f), "瓶子随手移到接口");
                manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                for (int i = 0; i < 45 && port.Connected != oxygen; i++) yield return new WaitForFixedUpdate();
                Assert.That(port.Connected, Is.SameAs(oxygen), "放到接口后，真实 XR 插槽接住瓶子");
                Assert.That(oxygen.Item.State, Is.EqualTo(CargoState.World));
                port.Advance(5); Assert.That(oxygen.Fraction, Is.InRange(.25f, .35f));
                Assert.That(oxygen.PreparedOnDayOne, Is.True);
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                hand.transform.position += Vector3.forward * .8f;
                for (int i = 0; i < 4; i++) yield return null;
                Assert.That(port.Connected, Is.Null);
                float stopped = oxygen.Fraction;
                for (int i = 0; i < 20; i++) yield return null;
                Assert.That(oxygen.Fraction, Is.EqualTo(stopped), "拿出后暂停充装");
            }
            finally { Object.Destroy(hand.gameObject); }
        }

        [UnityTest] public IEnumerator SleepCarriesPartialChargesResearchAndMovedSuppliesAndRetryRestoresThem()
        {
            var day = Object.FindAnyObjectByType<DayOneMission>(); day.Suit.Don();
            foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery })
            { var port = Port(kind); PreparationTestSteps.Connect(port.Socket, Supply(kind).Item.Grab); port.Advance(5); }
            foreach (var panel in prep.Panels) panel.CleanInstantly();
            foreach (var site in prep.Sites) site.CompleteInstantly();
            for (int i = 0; i < 25; i++) yield return null;
            prep.Research.enabled = false;
            PreparationTestSteps.Connect(prep.Research.Rack.Slots[0], prep.Research.Cores[0].Grab); prep.Research.Advance(18);
            PreparationTestSteps.Connect(prep.Research.Rack.Slots[1], prep.Research.Cores[1].Grab); prep.Research.Advance(9);
            PreparationTestSteps.Connect(prep.Research.Rack.Slots[2], prep.Research.Cores[2].Grab);
            Assert.That(prep.Research.Completed, Is.EqualTo(1));
            var moved = Supply(CargoKind.RepairKit); moved.transform.position += Vector3.up * .2f; moved.Item.Body.position = moved.transform.position;
            yield return null;
            var ids = prep.Cargo.Where(c => c.gameObject.activeSelf).Select(c => c.Id).OrderBy(s => s).ToArray();
            var fractions = prep.Cargo.ToDictionary(c => c.Id, c => c.Fraction);
            var movedId = moved.Id; var movedPosition = moved.transform.position;
            UsabilityTestSteps.ReturnSuit(day);
            Assert.That(day.Sleep(), Is.True);
            for (int i = 0; i < 350 && SceneManager.GetActiveScene().name != "11_LunarStation_LifeSupport"; i++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("11_LunarStation_LifeSupport"));
            PreparationTestSteps.DisableSimulator(); yield return null;
            prep = Object.FindAnyObjectByType<StationPreparation>();
            Assert.That(prep.Cargo.Where(c => c.gameObject.activeSelf).Select(c => c.Id).OrderBy(s => s), Is.EqualTo(ids));
            foreach (var entry in prep.Cargo) Assert.That(entry.Fraction, Is.EqualTo(fractions[entry.Id]).Within(.002f), entry.Id);
            Assert.That(prep.Research.GetProgress(0), Is.EqualTo(1)); Assert.That(prep.Research.GetProgress(1), Is.EqualTo(.5f));
            Assert.That(prep.Research.SlotFor(1), Is.EqualTo(1)); Assert.That(prep.Research.Rack.Stored, Is.EqualTo(3));
            Assert.That(prep.Sites.All(s => s.Completed), Is.True, "采样孔保持已钻取外观");
            // 睡觉时物理仍可自然落下，继承的是换日前的最终落点。
            var restored = prep.Cargo.Single(c => c.Id == movedId);
            Assert.That(Vector2.Distance(new Vector2(restored.transform.position.x, restored.transform.position.z), new Vector2(movedPosition.x, movedPosition.z)), Is.LessThan(.05f));
            foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery }) Assert.That(Port(kind).Connected, Is.SameAs(Supply(kind)));
            var session = Object.FindAnyObjectByType<StationMissionSession>(); yield return Wake(session);
            var batteryBefore = Supply(CargoKind.Battery).Fraction; Port(CargoKind.Battery).Advance(20);
            Assert.That(Supply(CargoKind.Battery).Fraction, Is.EqualTo(batteryBefore), "断电不能充电");
            yield return ExpansionTestSteps.Power(session);
            var oxygenBefore = Supply(CargoKind.Oxygen).Fraction; Port(CargoKind.Oxygen).Advance(20);
            Assert.That(Supply(CargoKind.Oxygen).Fraction, Is.EqualTo(oxygenBefore), "氧气机修好之前不能继续充氧");
            session.Mission.Tick(session.Mission.RepairTask.RemainingSeconds + .01f, true, false);
            foreach (var port in prep.Docks) port.Advance(30);
            Assert.That(Supply(CargoKind.Oxygen).Fraction, Is.EqualTo(1)); Assert.That(Supply(CargoKind.Battery).Fraction, Is.EqualTo(1));
            prep.Research.Advance(30); Assert.That(prep.Research.Completed, Is.EqualTo(3));
            session.RetryMission();
            foreach (var entry in prep.Cargo) Assert.That(entry.Fraction, Is.EqualTo(fractions[entry.Id]).Within(.002f), "重试 " + entry.Id);
            Assert.That(prep.Research.Completed, Is.EqualTo(1)); Assert.That(prep.Research.GetProgress(1), Is.EqualTo(.5f));
            Assert.That(prep.Cargo.Where(c => c.gameObject.activeSelf).Select(c => c.Id).OrderBy(s => s), Is.EqualTo(ids));
        }

        [UnityTest] public IEnumerator NoPreparationMeansNoUsableSuppliesEvenAfterPowerAndRepair()
        {
            yield return NextDay(); var session = Object.FindAnyObjectByType<StationMissionSession>(); yield return Wake(session);
            yield return ExpansionTestSteps.Power(session);
            session.Mission.Tick(session.Mission.RepairTask.RemainingSeconds + .01f, true, false);
            foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery })
            {
                var item = Supply(kind); var port = Port(kind);
                PreparationTestSteps.Connect(port.Socket, item.Item.Grab); port.Advance(100);
                Assert.That(item.Fraction, Is.Zero); Assert.That(item.Item.UsableFraction, Is.Zero);
                Assert.That(session.GetComponent<LifeSupportMission>().CanUseSupply(item.Item), Is.False);
            }
            Assert.That(prep.Research.Data.All(c => !c.gameObject.activeSelf), Is.True);
            Assert.That(prep.Research.Soil.All(c => !c.gameObject.activeSelf), Is.True);
            yield return PreparationTestSteps.Morning(); prep = Object.FindAnyObjectByType<StationPreparation>();
            Assert.That(prep.Cargo.Where(c => c.NeedsCharging).All(c => !c.PreparedOnDayOne && c.Fraction == 0), Is.True, "新一轮第一天没有上轮资源");
        }

        [UnityTest] public IEnumerator ResearchPausesOnRemovalProducesEachRewardOnceAndLayoutMatchesAcrossDays()
        {
            var roots = new[] { "Oxygen Repair Station", "Backup Circuit Panel", "Laboratory Module Terminal", "Habitat Environmental Console", "Habitat Supply Console", "Suit Service Console", "Oxygen Filling Bench", "Battery Charging Bench", "Lunar Sample Analyzer", "Day One Tool Rack" };
            var poses = roots.ToDictionary(n => n, n => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == n).position);
            prep.Research.enabled = false; prep.Sites[0].CompleteInstantly();
            for (int i = 0; i < 25; i++) yield return null;
            var core = prep.Research.Cores[0]; var slot = prep.Research.Rack.Slots[0];
            PreparationTestSteps.Connect(slot, core.Grab); prep.Research.Advance(6);
            PreparationTestSteps.Disconnect(core.Grab); prep.Research.Advance(30);
            Assert.That(prep.Research.GetProgress(0), Is.EqualTo(1f / 3).Within(.001f));
            PreparationTestSteps.Connect(slot, core.Grab); prep.Research.Advance(12);
            Assert.That(prep.Research.Data.Count(c => c.gameObject.activeSelf), Is.EqualTo(1));
            Assert.That(prep.Research.Soil.Count(c => c.gameObject.activeSelf), Is.EqualTo(1));
            prep.Research.Data[0].gameObject.SetActive(false);
            PreparationTestSteps.Disconnect(core.Grab); PreparationTestSteps.Connect(slot, core.Grab); prep.Research.Advance(100);
            Assert.That(prep.Research.Data[0].gameObject.activeSelf, Is.False, "取走产物后不能重复产出");
            prep.Research.Data[0].gameObject.SetActive(true);
            var language = Object.FindAnyObjectByType<LocalizationService>();
            foreach (var lang in new[] { GameLanguage.Chinese, GameLanguage.English, GameLanguage.Russian })
            {
                language.SetLanguage(lang); yield return null;
                prep.Research.enabled = true; yield return null; prep.Research.enabled = false;
                var labels = prep.GetComponentsInChildren<TMP_Text>(true).Concat(prep.Cargo.SelectMany(c => c.GetComponentsInChildren<TMP_Text>(true)))
                    .Concat(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Task List"));
                foreach (var label in labels.Where(t => t.gameObject.activeInHierarchy))
                { label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing, Is.False, lang + " " + label.name + " " + label.text); Assert.That(label.text.Contains("prep."), Is.False); }
                yield return Capture("preparation-day1-lab-" + lang, new Vector3(-7.7f, 1.9f, -.04f), new Vector3(-7.9f, 1.25f, -1.55f), 60);
            }
            yield return Capture("preparation-day1-engineering", new Vector3(-11.65f, 1.8f, 1.7f), new Vector3(-14.4f, 1.3f, 1.7f), 75);
            yield return Capture("preparation-day1-oxygen", new Vector3(-14.7f, 1.85f, 1.65f), new Vector3(-14.7f, 1.18f, .43f), 60);
            yield return NextDay();
            foreach (var entry in poses)
            { var root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == entry.Key); Assert.That(Vector3.Distance(root.position, entry.Value), Is.LessThan(.001f), entry.Key); }
            var session = Object.FindAnyObjectByType<StationMissionSession>(); yield return Wake(session); yield return ExpansionTestSteps.Power(session);
            yield return Capture("preparation-day2-engineering", new Vector3(-11.65f, 1.8f, 1.7f), new Vector3(-14.4f, 1.3f, 1.7f), 75);
            yield return Capture("preparation-day2-lab", new Vector3(-7.7f, 1.9f, -.04f), new Vector3(-7.9f, 1.25f, -1.55f), 60);
        }

        [UnityTest] public IEnumerator HalfFullSuppliesRefillSuitAndShipProportionallyAndRetryCannotDuplicateThem()
        {
            foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery })
            {
                var item = Supply(kind); var port = Port(kind);
                PreparationTestSteps.Connect(port.Socket, item.Item.Grab); port.Advance(10); PreparationTestSteps.Disconnect(item.Item.Grab);
                Assert.That(item.Fraction, Is.EqualTo(.5f));
            }
            yield return NextDay();
            var session = Object.FindAnyObjectByType<StationMissionSession>(); yield return Wake(session);
            var life = session.GetComponent<LifeSupportMission>(); var inventory = session.GetComponent<CargoInventory>();
            var settings = Object.Instantiate(life.Config);
            JsonUtility.FromJsonOverwrite("{\"suitOxygenRange\":{\"x\":30,\"y\":30},\"suitPowerRange\":{\"x\":30,\"y\":30}}", settings);
            life.Configure(settings, session, inventory, life.SuitRack, life.DoorControl, life.HabitatVolume);
            session.RetryMission(); yield return ExpansionTestSteps.Wake(session);
            life.RestorePreparedSuit(30, 30); // 半满补给测试从明确的低余量开始；跨天继承另有完整睡眠测试。
            var steps = new LifeSupportSteps(session); var hand = new GameObject("Fraction supply hand").AddComponent<PilotTestHand>();
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var flight = session.GetComponent<AscentMission>(); var flightSettings = Object.Instantiate(flight.Config);
            try
            {
                yield return null; steps.Don();
                foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery })
                {
                    var item = Supply(kind).Item;
                    manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
                    float before = kind == CargoKind.Oxygen ? life.SuitOxygen : life.SuitPower;
                    Assert.That(life.TryUseSupply(item), Is.True);
                    Assert.That(kind == CargoKind.Oxygen ? life.SuitOxygen : life.SuitPower,
                        Is.EqualTo(before + (kind == CargoKind.Oxygen ? settings.OxygenRefillPercent : settings.BatteryRefillPercent) * .5f).Within(.001f));
                    Assert.That(life.TryUseSupply(item), Is.False); Assert.That(item.State, Is.EqualTo(CargoState.Consumed));
                }
                session.RetryMission(); yield return ExpansionTestSteps.Wake(session); steps.Don();
                foreach (var kind in new[] { CargoKind.Oxygen, CargoKind.Battery })
                {
                    var item = Supply(kind).Item; Assert.That(item.UsableFraction, Is.EqualTo(.5f));
                    manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
                    var volume = Object.FindAnyObjectByType<CargoPackZone>().Volume;
                    item.transform.position = volume.transform.TransformPoint(volume.center) - item.transform.TransformVector(item.Body.centerOfMass);
                    item.Body.position = item.transform.position; Physics.SyncTransforms();
                    Assert.That(inventory.TryPack(item), Is.True);
                }
                flightSettings.ConfigureVitals(100, 100, 30); flight.Configure(flightSettings, session.Mission, inventory);
                Object.FindAnyObjectByType<BeamBlockade>().ClearInstantly(); steps.Board(flight);
                float oxygen = flight.Oxygen, power = flight.Power;
                Assert.That(flight.UseSupply(CargoKind.Oxygen), Is.True); Assert.That(flight.Oxygen, Is.EqualTo(oxygen + 45).Within(.001f));
                Assert.That(flight.UseSupply(CargoKind.Battery), Is.True); Assert.That(flight.Power, Is.EqualTo(power + 22.5f).Within(.001f));
                Assert.That(flight.UseSupply(CargoKind.Oxygen), Is.False); Assert.That(flight.UseSupply(CargoKind.Battery), Is.False);
            }
            finally { steps.Dispose(); Object.Destroy(hand.gameObject); Object.Destroy(settings); Object.Destroy(flightSettings); }
        }

        private static IEnumerator Capture(string name, Vector3 eye, Vector3 target, float fov)
        {
            var obj = new GameObject("Preparation capture"); var camera = obj.AddComponent<Camera>();
            camera.CopyFrom(Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>().Camera); camera.enabled = false;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye)); camera.fieldOfView = fov;
            var render = new RenderTexture(1600, 1000, 24); var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                render.Create(); camera.targetTexture = render; camera.enabled = true; for (int i = 0; i < 5; i++) yield return null;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = render }); RenderTexture.active = render;
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply(); Directory.CreateDirectory("Docs/Previews"); PreviewEvidence.Write("Docs/Previews/" + name + ".png", texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; render.Release(); Object.Destroy(render); Object.Destroy(texture); Object.Destroy(obj); }
        }
    }
}
