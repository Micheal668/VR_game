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
    // 撤离腰带：腰前不再有收纳平台；撤离时出现三个插槽，收纳的物资按类别进插槽并显示缩小模型，
    // 抓住插槽能取回同一件物资，额度规则保持不变。
    public sealed class CargoBeltTests
    {
        private StationMissionSession session; private CargoInventory cargo; private CargoBelt belt;
        private CargoPackZone zone; private XRInteractionManager manager; private PilotTestHand hand; private float previousDelta;

        [UnitySetUp] public IEnumerator Load()
        {
            previousDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30;
            yield return SceneManager.LoadSceneAsync("10_LunarStation_Docking"); yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>(); session.enabled = false;
            cargo = session.GetComponent<CargoInventory>();
            belt = Object.FindAnyObjectByType<CargoBelt>();
            zone = Object.FindAnyObjectByType<CargoPackZone>();
            manager = Object.FindAnyObjectByType<XRInteractionManager>();
            hand = new GameObject("Belt Test Hand").AddComponent<PilotTestHand>();
            hand.interactionLayers = -1;
            while (Time.time < 1.05f) yield return null;
        }

        [TearDown] public void Restore() { Time.captureDeltaTime = previousDelta; if (hand != null) Object.Destroy(hand.gameObject); }

        [UnityTest] public IEnumerator OldPlatformIsGoneAndBeltAppearsOnlyDuringEvacuation()
        {
            Assert.That(belt, Is.Not.Null, "请先执行 Lunar Escape → Install Airlock Repair and Cargo Belt (Docking Scene)");
            foreach (var name in new[] { "Pouch Base", "Pouch Side", "Pouch Label" })
                Assert.That(zone.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).All(t => !t.gameObject.activeInHierarchy), name);
            Assert.That(belt.Slots.Length, Is.EqualTo(3));
            yield return null;
            Assert.That(belt.Slots.All(s => !s.gameObject.activeInHierarchy), "撤离前不显示腰带");
            session.BeginMission();
            session.Advance(session.Mission.Config.RepairWindowSeconds);
            yield return null;
            Assert.That(belt.Slots.All(s => s.gameObject.activeInHierarchy), "撤离时显示三个插槽");
            Assert.That(belt.Slots.All(s => (s.transform.position - zone.transform.position).magnitude < 0.45f));
        }

        [UnityTest] public IEnumerator PackedItemsFillSlotsByKindAndCanBeTakenBack()
        {
            session.BeginMission();
            session.Advance(session.Mission.Config.RepairWindowSeconds);
            yield return null;
            var medical = cargo.Items.Where(i => i.Kind == CargoKind.MedicalKit).ToArray();
            var oxygen = cargo.Items.First(i => i.Kind == CargoKind.Oxygen);

            yield return PackItem(medical[0]);
            Assert.That(medical[0].State, Is.EqualTo(CargoState.Packed));
            var slot = belt.Slots.Single(s => s.Kind == CargoKind.MedicalKit);
            Assert.That(slot.ReplicaRoot.childCount, Is.EqualTo(1), "插槽显示医疗包的缩小模型");
            Assert.That(slot.ReplicaRoot.GetComponentsInChildren<Collider>(true), Is.Empty, "缩小模型没有碰撞");
            if (medical.Length > 1)
            {
                yield return PackItem(medical[1]);
                Assert.That(belt.Slots.Count(s => s.Kind == CargoKind.MedicalKit), Is.EqualTo(1), "同类物资叠在同一插槽");
            }
            yield return PackItem(oxygen);
            Assert.That(belt.Slots.Count(s => s.Kind.HasValue), Is.EqualTo(2));

            // 抓住插槽：下一帧手里就是同类物资本身，额度保持一致。
            int total = cargo.TotalCount;
            hand.transform.position = slot.transform.position;
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)slot);
            yield return AirlockTestDriver.Frames(3);
            var taken = medical.FirstOrDefault(i => i.State == CargoState.Held);
            Assert.That(taken, Is.Not.Null, "从插槽取回一件医疗包");
            Assert.That(hand.IsSelecting(taken.Grab));
            Assert.That(cargo.TotalCount, Is.EqualTo(total), "取回不改变已携带数量");
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)taken.Grab);
        }

        private IEnumerator PackItem(CargoItem item)
        {
            hand.transform.position = item.transform.position;
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
            yield return null;
            Assert.That(item.State, Is.EqualTo(CargoState.Held));
            item.Body.position = zone.Volume.bounds.center;
            item.transform.position = zone.Volume.bounds.center;
            hand.transform.position = zone.Volume.bounds.center;
            Physics.SyncTransforms();
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
            yield return AirlockTestDriver.Frames(2);
        }
    }
}
