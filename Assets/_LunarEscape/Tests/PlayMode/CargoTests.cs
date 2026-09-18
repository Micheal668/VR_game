using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    public sealed class CargoTests
    {
        private GameObject owner;
        private StationMission mission;
        private StationMissionConfig missionConfig;
        private CargoConfig cargoConfig;
        private CargoInventory inventory;
        private CargoItem[] items;
        private CargoPackZone pack;
        private EvacuationZone ship;

        [SetUp]
        public void CreateCargoRules()
        {
            owner = new GameObject("Cargo rule test world");
            var repair = owner.AddComponent<TimedRepairTask>();
            repair.Configure(1f);
            mission = owner.AddComponent<StationMission>();
            missionConfig = ScriptableObject.CreateInstance<StationMissionConfig>();
            missionConfig.Configure(5f, 1f, 60f, 30f);
            mission.Configure(missionConfig, repair);
            cargoConfig = ScriptableObject.CreateInstance<CargoConfig>();
            inventory = owner.AddComponent<CargoInventory>();
            var manager = owner.AddComponent<XRInteractionManager>();
            var created = new List<CargoItem>();
            foreach (CargoKind kind in System.Enum.GetValues(typeof(CargoKind)))
            {
                int count = kind == CargoKind.Oxygen ? 3 : 2;
                for (int index = 0; index < count; index++)
                {
                    var itemObject = new GameObject(kind + " rule test " + index);
                    itemObject.transform.SetParent(owner.transform);
                    itemObject.transform.position = new Vector3(created.Count * 2f, 1f, -8f);
                    itemObject.AddComponent<BoxCollider>().size = Vector3.one * 0.2f;
                    itemObject.AddComponent<Rigidbody>();
                    itemObject.AddComponent<XRGrabInteractable>().interactionManager = manager;
                    var item = itemObject.AddComponent<CargoItem>();
                    item.Configure(inventory, kind);
                    created.Add(item);
                }
            }
            items = created.ToArray();
            var dropPoint = new GameObject("Cargo drop point").transform;
            dropPoint.SetParent(owner.transform);
            dropPoint.position = new Vector3(-3f, 1f, 0f);
            inventory.Configure(mission, cargoConfig, items, dropPoint);
            var packObject = new GameObject("Cargo test pack");
            packObject.transform.SetParent(owner.transform);
            packObject.transform.position = new Vector3(0f, 1f, 0f);
            var packVolume = packObject.AddComponent<BoxCollider>();
            packVolume.isTrigger = true;
            packVolume.size = Vector3.one;
            pack = packObject.AddComponent<CargoPackZone>();
            pack.Configure(inventory, packVolume);
            var bodyObject = new GameObject("Cargo test player body");
            bodyObject.transform.SetParent(owner.transform);
            bodyObject.transform.position = new Vector3(-5f, 0f, 0f);
            var body = bodyObject.AddComponent<CharacterController>();
            body.center = Vector3.up;
            body.height = 2f;
            var shipObject = new GameObject("Cargo test ship zone");
            shipObject.transform.SetParent(owner.transform);
            shipObject.transform.position = new Vector3(5f, 1f, 0f);
            var shipVolume = shipObject.AddComponent<BoxCollider>();
            shipVolume.isTrigger = true;
            shipVolume.size = new Vector3(2f, 3f, 2f);
            ship = shipObject.AddComponent<EvacuationZone>();
            ship.Configure(shipVolume, body);
            inventory.ConfigureShipZone(ship);
        }

        [TearDown]
        public void RemoveCargoRules()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(missionConfig);
            Object.DestroyImmediate(cargoConfig);
        }

        [Test]
        public void ExactTwelveKilogramsAcrossThreeKindsIsAcceptedWithAssignedWeights()
        {
            Assert.That(cargoConfig.MaxTypes, Is.EqualTo(3));
            Assert.That(cargoConfig.MaxWeightKg, Is.EqualTo(12f));
            foreach (var expected in new[]
            {
                (CargoKind.Oxygen, 3f, 2), (CargoKind.RepairKit, 4f, 1),
                (CargoKind.Battery, 4f, 1), (CargoKind.MedicalKit, 2f, 1),
                (CargoKind.DataCore, 1f, 1), (CargoKind.LunarSample, 5f, 1)
            })
            {
                Assert.That(cargoConfig.GetWeightKg(expected.Item1), Is.EqualTo(expected.Item2));
                Assert.That(cargoConfig.GetMaxQuantity(expected.Item1), Is.EqualTo(expected.Item3));
            }
            StartEvacuation();
            Hold(CargoKind.Oxygen);
            Hold(CargoKind.Oxygen, 1);
            Hold(CargoKind.LunarSample);
            Hold(CargoKind.DataCore);
            Assert.That(inventory.TotalCount, Is.EqualTo(4));
            Assert.That(inventory.TypeCount, Is.EqualTo(3));
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(12f));
            Assert.That(inventory.HeldCount, Is.EqualTo(4));
        }

        [Test]
        public void WeightAndTypeLimitsRejectOnlyTheNewItemWithoutChangingExistingCargo()
        {
            StartEvacuation();
            Hold(CargoKind.Oxygen);
            Hold(CargoKind.Oxygen, 1);
            Hold(CargoKind.LunarSample);
            Assert.That(inventory.TryHold(Item(CargoKind.RepairKit)), Is.False);
            Assert.That(inventory.LastRejection, Is.EqualTo(CargoRejection.WeightLimit));
            Assert.That(Item(CargoKind.RepairKit).State, Is.EqualTo(CargoState.World));
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(11f));
            Assert.That(inventory.TypeCount, Is.EqualTo(2));

            mission.ResetMission();
            StartEvacuation();
            Hold(CargoKind.RepairKit);
            Hold(CargoKind.Battery);
            Hold(CargoKind.MedicalKit);
            Assert.That(inventory.TryHold(Item(CargoKind.DataCore)), Is.False);
            Assert.That(inventory.LastRejection, Is.EqualTo(CargoRejection.TypeLimit));
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(10f));
            Assert.That(inventory.TypeCount, Is.EqualTo(3));
            Assert.That(inventory.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public void EachKindRejectsItsExtraCopyWithoutReservingWeightOrQuantity()
        {
            foreach (CargoKind kind in System.Enum.GetValues(typeof(CargoKind)))
            {
                mission.ResetMission();
                StartEvacuation();
                int cap = kind == CargoKind.Oxygen ? 2 : 1;
                for (int index = 0; index < cap; index++) Hold(kind, index);
                float weight = inventory.TotalWeightKg;
                Assert.That(inventory.TryHold(Item(kind, cap)), Is.False, kind.ToString());
                Assert.That(inventory.LastRejection, Is.EqualTo(CargoRejection.QuantityLimit));
                Assert.That(Item(kind, cap).State, Is.EqualTo(CargoState.World));
                Assert.That(inventory.GetCount(kind), Is.EqualTo(cap));
                Assert.That(inventory.TotalCount, Is.EqualTo(cap));
                Assert.That(inventory.TotalWeightKg, Is.EqualTo(weight));
            }
        }

        [Test]
        public void HeldPackedAndShipCargoShareLimitsAndDropOrDiscardActuallyFreesThem()
        {
            StartEvacuation();
            Hold(CargoKind.Oxygen);
            Hold(CargoKind.Oxygen, 1);
            Hold(CargoKind.LunarSample);
            Pack(Item(CargoKind.Oxygen));
            Pack(Item(CargoKind.LunarSample));
            Assert.That(inventory.HeldCount, Is.EqualTo(1));
            Assert.That(inventory.PackedCount, Is.EqualTo(2));
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(11f));
            Assert.That(inventory.LoadPackedIntoShip(), Is.False, "远离船舱不能装船。");
            Assert.That(inventory.LastRejection, Is.EqualTo(CargoRejection.NotAtShip));
            EnterShip();
            Assert.That(inventory.LoadPackedIntoShip(), Is.True);
            Assert.That(inventory.LoadedCount, Is.EqualTo(2));
            Assert.That(inventory.HeldCount, Is.EqualTo(1));
            Assert.That(inventory.PackedCount, Is.Zero);
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(11f));
            Assert.That(inventory.TryHold(Item(CargoKind.RepairKit)), Is.False,
                "收进包或装船不能腾出同一局的携带额度。");

            Assert.That(inventory.DropHeld(Item(CargoKind.Oxygen, 1)), Is.True);
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(8f));
            Hold(CargoKind.RepairKit);
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(12f));
            Assert.That(inventory.TypeCount, Is.EqualTo(3));
            Assert.That(inventory.LoadAllCarriedIntoShip(), Is.True);
            Assert.That(inventory.LoadedCount, Is.EqualTo(3));
            Assert.That(inventory.HeldCount + inventory.PackedCount, Is.Zero);
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(12f));
            Assert.That(inventory.Discard(CargoKind.LunarSample), Is.True);
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(7f));
            Assert.That(inventory.TypeCount, Is.EqualTo(2));
            Assert.That(Item(CargoKind.LunarSample).State, Is.EqualTo(CargoState.World));
        }

        [Test]
        public void PublicCargoMutationsCannotChangeAnyNonEvacuationPhase()
        {
            foreach (var phase in new[]
            {
                StationMissionPhase.Briefing, StationMissionPhase.Repair, StationMissionPhase.Stabilized,
                StationMissionPhase.Completed, StationMissionPhase.Failed
            })
            {
                mission.ResetMission();
                if (phase == StationMissionPhase.Repair) mission.Begin();
                if (phase == StationMissionPhase.Stabilized)
                {
                    mission.Begin();
                    mission.Tick(mission.RepairTask.DurationSeconds, true, false);
                }
                if (phase == StationMissionPhase.Completed || phase == StationMissionPhase.Failed)
                {
                    StartEvacuation();
                    Hold(CargoKind.Oxygen);
                    Hold(CargoKind.MedicalKit);
                    Pack(Item(CargoKind.MedicalKit));
                    EnterShip();
                    Assert.That(inventory.LoadPackedIntoShip(), Is.True);
                    mission.Tick(phase == StationMissionPhase.Completed ? 0f : mission.RemainingSeconds, false, true);
                }
                Assert.That(mission.Phase, Is.EqualTo(phase));
                var states = items.Select(item => item.State).ToArray();
                int count = inventory.TotalCount;
                float weight = inventory.TotalWeightKg;
                Assert.That(inventory.CanAcquire(Item(CargoKind.DataCore), out var rejection), Is.False);
                Assert.That(rejection, Is.EqualTo(CargoRejection.WrongPhase));
                Assert.That(inventory.TryHold(Item(CargoKind.DataCore)), Is.False);
                Assert.That(inventory.DropHeld(Item(CargoKind.Oxygen)), Is.False);
                Assert.That(inventory.TryPack(Item(CargoKind.Oxygen)), Is.False);
                Assert.That(inventory.LoadPackedIntoShip(), Is.False);
                Assert.That(inventory.LoadAllCarriedIntoShip(), Is.False);
                Assert.That(inventory.Discard(CargoKind.MedicalKit), Is.False);
                Assert.That(items.Select(item => item.State), Is.EqualTo(states));
                Assert.That(inventory.TotalCount, Is.EqualTo(count));
                Assert.That(inventory.TotalWeightKg, Is.EqualTo(weight));
            }
        }

        private CargoItem Item(CargoKind kind, int index = 0) => items.Where(item => item.Kind == kind).ElementAt(index);
        private void Hold(CargoKind kind, int index = 0) => Assert.That(inventory.TryHold(Item(kind, index)), Is.True, kind.ToString());

        private void Pack(CargoItem item)
        {
            item.Body.position = pack.Volume.bounds.center;
            item.transform.position = item.Body.position;
            Physics.SyncTransforms();
            Assert.That(pack.Contains(item), Is.True);
            Assert.That(inventory.TryPack(item), Is.True);
        }

        private void EnterShip()
        {
            ship.PlayerBody.enabled = false;
            ship.PlayerBody.transform.position = new Vector3(5f, 0f, 0f);
            ship.PlayerBody.enabled = true;
            Physics.SyncTransforms();
            Assert.That(ship.ContainsPlayer, Is.True);
        }

        private void StartEvacuation()
        {
            mission.Begin();
            mission.Tick(mission.Config.RepairWindowSeconds, false, false);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
        }
    }

    public sealed class CargoSceneTests
    {
        private StationMissionSession session;
        private CargoInventory inventory;
        private CargoPackZone pack;
        private XRInteractionManager manager;
        private XRRayInteractor hand;
        private float previousFrameDuration;

        [UnitySetUp]
        public IEnumerator LoadCargoScene()
        {
            previousFrameDuration = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("07_LunarStation_CargoBoarding", LoadSceneMode.Single);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>();
            session.enabled = false;
            inventory = Object.FindAnyObjectByType<CargoInventory>();
            pack = Object.FindAnyObjectByType<CargoPackZone>();
            manager = Object.FindAnyObjectByType<XRInteractionManager>();
            Assert.That(inventory, Is.Not.Null);
            Assert.That(pack, Is.Not.Null);
            Assert.That(inventory.Items.Count, Is.EqualTo(7));
            var handObject = new GameObject("Cargo integration test hand");
            hand = handObject.AddComponent<XRRayInteractor>();
            hand.interactionManager = manager;
            hand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            if (hand != null) Object.Destroy(hand.gameObject);
            Time.captureDeltaTime = previousFrameDuration;
        }

        [UnityTest]
        public IEnumerator RealGrabReleasePacksDropsAndSecuresTheSameCargoWhenBoarding()
        {
            var oxygen = inventory.Items.Where(item => item.Kind == CargoKind.Oxygen).ToArray();
            Assert.That(inventory.TryHold(oxygen[0]), Is.False, "简报阶段不能提前拿走物资。");
            Assert.That(manager.IsSelectPossible(hand, oxygen[0].Grab), Is.False,
                "真实 XRI 选择过滤器也必须在简报阶段拒绝抓取。");
            StartEvacuation();
            Assert.That(manager.IsSelectPossible(hand, oxygen[0].Grab), Is.True,
                "警报后同一交互器必须能够选择合法物资。");
            SelectItem(oxygen[0]);
            Assert.That(oxygen[0].Grab.isSelected, Is.True);
            Assert.That(oxygen[0].State, Is.EqualTo(CargoState.Held));
            float remaining = session.Mission.RemainingSeconds;
            session.enabled = true;
            yield return Frames(3);
            session.enabled = false;
            Assert.That(oxygen[0].Grab.isSelected, Is.True, "计时样本必须覆盖真实持有期间。");
            Assert.That(session.Mission.RemainingSeconds, Is.LessThan(remaining), "手持和整理物资时倒计时仍要继续。");
            yield return ReleaseIntoPack(oxygen[0]);
            Assert.That(oxygen[0].State, Is.EqualTo(CargoState.Packed));
            Assert.That(oxygen[0].Grab.isSelected, Is.False);
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(3f));

            SelectItem(oxygen[1]);
            Assert.That(inventory.GetCount(CargoKind.Oxygen), Is.EqualTo(2));
            oxygen[1].Grab.throwOnDetach = false;
            MoveCargoCenterTo(oxygen[1], new Vector3(1.8f, 1.5f, -1f));
            hand.selectInput.QueueManualState(false, 0f);
            manager.SelectExit((IXRSelectInteractor)hand, oxygen[1].Grab);
            yield return Frames(2);
            Assert.That(oxygen[1].State, Is.EqualTo(CargoState.World));
            Assert.That(oxygen[1].Body.isKinematic, Is.False);
            Assert.That(inventory.GetCount(CargoKind.Oxygen), Is.EqualTo(1));
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(3f));

            foreach (var kind in new[] { CargoKind.MedicalKit, CargoKind.DataCore })
            {
                var item = inventory.Items.Single(candidate => candidate.Kind == kind);
                SelectItem(item);
                yield return ReleaseIntoPack(item);
            }
            var fourthType = inventory.Items.Single(item => item.Kind == CargoKind.Battery);
            Assert.That(inventory.TypeCount, Is.EqualTo(3));
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(6f));
            Assert.That(((IXRSelectInteractable)fourthType.Grab).IsSelectableBy(hand), Is.False,
                "物品实际注册的 XRI 过滤器必须拒绝第四类。");
            Assert.That(manager.IsSelectPossible(hand, fourthType.Grab), Is.False,
                "不能只在规则 API 限制额度，却仍允许交互管理器抓取第四类。");
            Assert.That(fourthType.State, Is.EqualTo(CargoState.World));
            Assert.That(fourthType.Grab.isSelected, Is.False);

            MoveBodyTo(session.Exit.Volume.bounds.center);
            FindObject("Confirm Boarding").GetComponent<Button>().onClick.Invoke();
            session.Advance(0f);
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Completed));
            Assert.That(oxygen[0].State, Is.EqualTo(CargoState.Loaded));
            Assert.That(inventory.LoadedCount, Is.EqualTo(3));
            Assert.That(inventory.HeldCount + inventory.PackedCount, Is.Zero);
            Assert.That(inventory.TotalWeightKg, Is.EqualTo(6f));
        }

        [UnityTest]
        public IEnumerator FailureFreezesWorldHeldPackedAndLoadedCargoAndRetryRestoresEveryItem()
        {
            var starts = inventory.Items.ToDictionary(item => item, item => item.Body.position);
            var rotations = inventory.Items.ToDictionary(item => item, item => item.Body.rotation);
            var constraints = inventory.Items.ToDictionary(item => item, item => item.Body.constraints);
            var localization = Object.FindAnyObjectByType<LocalizationService>();
            localization.SetLanguage(GameLanguage.Russian);
            StartEvacuation();
            var oxygen = inventory.Items.First(item => item.Kind == CargoKind.Oxygen);
            SelectItem(oxygen);
            yield return ReleaseIntoPack(oxygen);
            MoveBodyTo(session.Exit.Volume.bounds.center);
            Assert.That(inventory.LoadPackedIntoShip(), Is.True);
            Assert.That(oxygen.State, Is.EqualTo(CargoState.Loaded));
            MoveBodyTo(session.SpawnPoint.position);
            yield return null;
            var medical = inventory.Items.Single(item => item.Kind == CargoKind.MedicalKit);
            SelectItem(medical);
            yield return ReleaseIntoPack(medical);
            var data = inventory.Items.Single(item => item.Kind == CargoKind.DataCore);
            SelectItem(data);
            Assert.That(data.Grab.isSelected, Is.True);
            Assert.That(inventory.HeldCount, Is.EqualTo(1));
            Assert.That(inventory.PackedCount, Is.EqualTo(1));
            Assert.That(inventory.LoadedCount, Is.EqualTo(1));
            session.Advance(session.Mission.RemainingSeconds);
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Failed));
            hand.selectInput.QueueManualState(false, 0f);
            var frozenPositions = inventory.Items.ToDictionary(item => item, item => item.Body.position);
            foreach (var item in inventory.Items)
            {
                Assert.That(item.Grab.isSelected, Is.False, item.name);
                Assert.That(item.Grab.enabled, Is.False, item.name);
                Assert.That(item.Body.isKinematic || item.Body.constraints == RigidbodyConstraints.FreezeAll,
                    Is.True, item.name + " 失败时仍有自由物理运动。");
            }
            yield return Frames(5);
            foreach (var item in inventory.Items)
                Assert.That(Vector3.Distance(item.Body.position, frozenPositions[item]), Is.LessThan(0.01f), item.name);

            session.RetryMission();
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Briefing));
            Assert.That(inventory.TotalCount, Is.Zero);
            Assert.That(inventory.TypeCount, Is.Zero);
            Assert.That(inventory.TotalWeightKg, Is.Zero);
            Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.Russian));
            foreach (var item in inventory.Items)
            {
                Assert.That(item.State, Is.EqualTo(CargoState.World));
                Assert.That(item.gameObject.activeInHierarchy, Is.True);
                Assert.That(item.Grab.enabled && !item.Grab.isSelected, Is.True, item.name);
                Assert.That(item.Body.isKinematic, Is.False);
                Assert.That(item.Body.constraints, Is.EqualTo(constraints[item]));
                Assert.That(item.Body.linearVelocity.sqrMagnitude + item.Body.angularVelocity.sqrMagnitude, Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(item.Body.position, starts[item]), Is.LessThan(0.04f), item.name);
                Assert.That(Quaternion.Angle(item.Body.rotation, rotations[item]), Is.LessThan(0.2f), item.name);
            }
            yield return Frames(3);
            foreach (var item in inventory.Items)
                Assert.That(Vector3.Distance(item.Body.position, starts[item]), Is.LessThan(0.1f),
                    item.name + " 重试后被延迟投掷或旧状态再次带走。");
        }

        private void StartEvacuation()
        {
            session.BeginMission();
            session.Advance(session.Mission.Config.RepairWindowSeconds);
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
        }

        private IEnumerator ReleaseIntoPack(CargoItem item)
        {
            item.Grab.throwOnDetach = false;
            // 腰包每帧跟随身体，使用当前 Transform 的中心，不读取上一物理帧的 bounds。
            Vector3 target = pack.Volume.transform.TransformPoint(pack.Volume.center);
            MoveCargoCenterTo(item, target);
            Vector3 localCenter = pack.Volume.transform.InverseTransformPoint(item.Body.worldCenterOfMass)
                - pack.Volume.center;
            Assert.That(pack.Contains(item), Is.True,
                $"{item.name}: state={item.State}, active={item.isActiveAndEnabled}, selected={item.Grab.isSelected}, "
                + $"packActive={pack.isActiveAndEnabled && pack.Volume.enabled}, target={target:F4}, "
                + $"body={item.Body.position:F4}, transform={item.transform.position:F4}, "
                + $"centerOfMass={item.Body.worldCenterOfMass:F4}, packLocalCenter={localCenter:F4}, size={pack.Volume.size:F4}");
            hand.selectInput.QueueManualState(false, 0f);
            manager.SelectExit((IXRSelectInteractor)hand, item.Grab);
            yield return Frames(2);
            Assert.That(item.State, Is.EqualTo(CargoState.Packed), "真实松手事件必须将包内物品收纳。");
        }

        private static void MoveCargoCenterTo(CargoItem item, Vector3 worldCenter)
        {
            // 实际资源开启了 Rigidbody 插值。测试瞬移必须同步渲染 Transform 和物理姿态，
            // 否则 SyncTransforms 会把旧插值姿态重新推回物理世界；保持插值设置本身不变。
            Vector3 position = item.Body.position + worldCenter - item.Body.worldCenterOfMass;
            Quaternion rotation = item.Body.rotation;
            item.transform.SetPositionAndRotation(position, rotation);
            item.Body.position = position;
            item.Body.rotation = rotation;
            Physics.SyncTransforms();
        }

        private void SelectItem(CargoItem item)
        {
            Vector3 target = item.Body.position;
            Vector3 origin = target - Vector3.forward * 0.7f + Vector3.up * 0.1f;
            hand.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(target - origin));
            hand.selectInput.QueueManualState(true, 1f);
            Physics.SyncTransforms();
            manager.SelectEnter((IXRSelectInteractor)hand, item.Grab);
            Assert.That(item.Grab.isSelected, Is.True, item.name);
            Assert.That(item.State, Is.EqualTo(CargoState.Held), item.name);
        }

        private void MoveBodyTo(Vector3 destination)
        {
            var body = session.Exit.PlayerBody;
            Vector3 center = body.transform.TransformPoint(body.center);
            body.enabled = false;
            body.transform.position += new Vector3(destination.x - center.x, 0f, destination.z - center.z);
            body.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator Frames(int count)
        {
            for (int frame = 0; frame < count; frame++) yield return null;
        }

        private static GameObject FindObject(string name) => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(item => item.name == name).gameObject;
    }
}
