using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace LunarEscape.Tests
{
    // 重现用户实际反馈：开局等待、在框口松手后自然下落，而不是把中心精确塞入触发区。
    public sealed class CargoStabilityTests
    {
        private float captureDelta;private StationMissionSession session;private CargoInventory cargo;private CargoPackZone pack;
        [UnitySetUp]public IEnumerator Load()
        {
            captureDelta=Time.captureDeltaTime;Time.captureDeltaTime=1f/30;yield return SceneManager.LoadSceneAsync("09_LunarStation_Orbit");yield return null;
            session=Object.FindAnyObjectByType<StationMissionSession>();session.enabled=false;cargo=session.GetComponent<CargoInventory>();pack=Object.FindAnyObjectByType<CargoPackZone>();
        }
        [TearDown]public void ResetTiming(){Time.captureDeltaTime=captureDelta;}
        [UnityTest]public IEnumerator SuppliesRemainOnShelfDuringInitialWaitingAndRepair()
        {
            var starts=cargo.Items.Select(i=>i.transform.position).ToArray();var shelf=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Single(c=>c.name=="Supply Shelf");
            for(int i=0;i<900;i++)yield return null;
            for(int i=0;i<cargo.Items.Count;i++)
            {var item=cargo.Items[i];TestContext.Progress.WriteLine(item.name+" start="+starts[i]+" now="+item.Body.position+" vel="+item.Body.linearVelocity);Assert.That(item.Body.position.y,Is.GreaterThan(shelf.bounds.max.y+.1f),item.name);Assert.That(Vector3.Distance(item.Body.position,starts[i]),Is.LessThan(.15f),item.name);}
        }
        [UnityTest]public IEnumerator ReleasedItemEnteringPouchFromAboveIsActuallyStored()
        {
            session.BeginMission();session.Advance(session.Mission.Config.RepairWindowSeconds);yield return null;
            var manager=Object.FindAnyObjectByType<XRInteractionManager>();var handObject=new GameObject("Pouch release integration hand");var hand=handObject.AddComponent<XRRayInteractor>();
            hand.interactionManager=manager;hand.selectInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            yield return null;IXRSelectInteractor interactor=hand;
            var item=cargo.Items.First(i=>i.Kind==CargoKind.MedicalKit);manager.SelectEnter(interactor,(IXRSelectInteractable)item.Grab);Assert.That(item.State,Is.EqualTo(CargoState.Held));
            var release=pack.Volume.bounds.center+Vector3.up*.52f;item.Body.position=release;item.transform.position=release;Physics.SyncTransforms();
            manager.SelectExit(interactor,(IXRSelectInteractable)item.Grab);
            for(int i=0;i<60;i++)yield return null;
            Assert.That(item.State,Is.EqualTo(CargoState.Packed));Assert.That(item.gameObject.activeSelf,Is.False);Assert.That(cargo.PackedCount,Is.EqualTo(1));
            Object.Destroy(handObject);
        }
        [UnityTest]public IEnumerator FallingIntoPouchRechecksQuotaInsteadOfAddingFourthType()
        {
            session.BeginMission();session.Advance(session.Mission.Config.RepairWindowSeconds);yield return null;
            var manager=Object.FindAnyObjectByType<XRInteractionManager>();var obj=new GameObject("Quota release hand");var hand=obj.AddComponent<XRRayInteractor>();
            hand.interactionManager=manager;hand.selectInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;yield return null;
            var item=cargo.Items.First(i=>i.Kind==CargoKind.MedicalKit);manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)item.Grab);
            var release=pack.Volume.bounds.center+Vector3.up*.52f;item.Body.position=release;item.transform.position=release;Physics.SyncTransforms();
            manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)item.Grab);
            foreach(var kind in new[]{CargoKind.Oxygen,CargoKind.RepairKit,CargoKind.Battery})Assert.That(cargo.TryHold(cargo.Items.First(i=>i.Kind==kind)),Is.True);
            for(int i=0;i<60;i++)yield return null;
            Assert.That(item.State,Is.EqualTo(CargoState.World));Assert.That(cargo.TypeCount,Is.EqualTo(3));Assert.That(cargo.PackedCount,Is.Zero);Assert.That(cargo.LastRejection,Is.EqualTo(CargoRejection.TypeLimit));
            Object.Destroy(obj);
        }
        [UnityTest]public IEnumerator CanceledGrabAbovePouchDoesNotCountAsIntentionalStorage()
        {
            session.BeginMission();session.Advance(session.Mission.Config.RepairWindowSeconds);yield return null;
            var manager=Object.FindAnyObjectByType<XRInteractionManager>();var obj=new GameObject("Canceled release hand");var hand=obj.AddComponent<XRRayInteractor>();
            hand.interactionManager=manager;hand.selectInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;yield return null;
            var item=cargo.Items.First(i=>i.Kind==CargoKind.MedicalKit);manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)item.Grab);
            var release=pack.Volume.bounds.center+Vector3.up*.52f;item.Body.position=release;item.transform.position=release;Physics.SyncTransforms();
            manager.CancelInteractableSelection((IXRSelectInteractable)item.Grab);
            for(int i=0;i<60;i++)yield return null;
            Assert.That(item.State,Is.EqualTo(CargoState.World));Assert.That(cargo.PackedCount,Is.Zero);Object.Destroy(obj);
        }
    }
}
