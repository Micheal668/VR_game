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
    public sealed class CrewQuartersTests
    {
        [UnityTest]
        public IEnumerator BothDaysHaveClearLobbyAndReachableWallSupplies()
        {
            foreach (string scene in new[] { "12_LunarStation_DayOne", "11_LunarStation_LifeSupport" })
            {
                yield return SceneManager.LoadSceneAsync(scene);
                PreparationTestSteps.DisableSimulator();
                yield return null;
                var hoop = Object.FindAnyObjectByType<CrewHoop>();
                Assert.That(hoop, Is.Not.Null, scene);
                Assert.That(hoop.Ball.throwOnDetach, Is.True);
                Assert.That(GameObject.Find("Solid Round Supply Table"), Is.Null);
                Assert.That(Physics.Raycast(new Vector3(0, 1.7f, 0), Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore), Is.True);
                Assert.That(hit.point.y, Is.EqualTo(0f).Within(.06f), hit.collider.name);
                var kits = Object.FindAnyObjectByType<StationPreparation>().Cargo.Where(c => c.Item.Kind == CargoKind.RepairKit || c.Item.Kind == CargoKind.MedicalKit);
                Assert.That(kits.Count(), Is.EqualTo(6));
                foreach (var kit in kits) Assert.That(kit.transform.position.z, Is.LessThan(-3f), kit.name);
            }
        }

        [UnityTest]
        public IEnumerator DislodgedAndCancelledBallsReturnWithoutScoring()
        {
            yield return SceneManager.LoadSceneAsync("12_LunarStation_DayOne");
            PreparationTestSteps.DisableSimulator();
            var hoop = Object.FindAnyObjectByType<CrewHoop>();
            var ball = hoop.Ball;
            var body = ball.GetComponent<Rigidbody>();
            var home = body.position;
            body.position = home + Vector3.left * 9f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(body.position, home), Is.LessThan(.08f));

            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var hand = new GameObject("Basketball test hand").AddComponent<PilotTestHand>();
            hand.interactionManager = manager;
            hand.transform.position = home;
            yield return null;
            var previousTimeScale = Time.timeScale;
            try
            {
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)ball);
                manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)ball);
                yield return null;
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)ball);
                Assert.That(ball.isSelected);
                manager.CancelInteractableSelection((IXRSelectInteractable)ball);
                yield return null;
                body.position = hoop.Rim.position + Vector3.up * .3f;
                body.linearVelocity = Vector3.down;
                Time.timeScale = 4f;
                yield return new WaitForSeconds(8.3f);
                Assert.That(hoop.Score, Is.Zero);
                Assert.That(Vector3.Distance(body.position, home), Is.LessThan(.08f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.Destroy(hand.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ReleasedBallScoresOnDownwardCrossingAndReturnsToShelf()
        {
            yield return SceneManager.LoadSceneAsync("12_LunarStation_DayOne");
            PreparationTestSteps.DisableSimulator();
            var hoop = Object.FindAnyObjectByType<CrewHoop>();
            var ball = hoop.Ball;
            var home = ball.transform.position;
            var body = ball.GetComponent<Rigidbody>();
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var hand = new GameObject("Basketball test hand").AddComponent<PilotTestHand>();
            hand.transform.position = home;
            hand.interactionManager = manager;
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)ball);
            Assert.That(ball.isSelected, Is.True);
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)ball);
            yield return null;
            // Launch upwards through the open rim. Only the downward pass is a basket.
            body.position = hoop.Rim.position - Vector3.up * .18f;
            body.linearVelocity = Vector3.up * 3.1f;
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 150 && body.position.y <= hoop.Rim.position.y; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.GreaterThan(hoop.Rim.position.y));
            Assert.That(hoop.Score, Is.Zero);
            for (int i = 0; i < 350 && hoop.Score == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(hoop.Score, Is.EqualTo(1));
            body.position = home + Vector3.left * 9;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(body.position, home), Is.LessThan(.08f));
            Assert.That(hoop.Score, Is.EqualTo(1));
            Object.Destroy(hand.gameObject);
        }
    }
}
