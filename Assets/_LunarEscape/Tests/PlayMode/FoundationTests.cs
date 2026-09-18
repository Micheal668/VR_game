using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    // 验证真正加载后的场景、交互事件和物理碰撞，而不是只检查脚本文本。
    public sealed class FoundationTests
    {
        [UnitySetUp]
        public IEnumerator LoadRoom()
        {
            yield return SceneManager.LoadSceneAsync("04_LunarStation_Foundation", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RoomHasCameraFloorAndConfiguredTool()
        {
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(GameObject.Find("Floor - teleport surface").GetComponent<Collider>(), Is.Not.Null);
            var tool = Object.FindFirstObjectByType<XRGrabInteractable>();
            Assert.That(tool, Is.Not.Null);
            Assert.That(tool.GetComponent<Rigidbody>().useGravity, Is.True);
            Assert.That(tool.GetComponentsInChildren<Collider>().Length, Is.GreaterThan(0));
            Assert.That(GameObject.Find("Practice Status").GetComponent<TMP_Text>().font, Is.Not.Null);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    Assert.That(component, Is.Not.Null, "Missing script in " + root.name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ToolkitGrabAndReleaseUpdatePracticeFeedback()
        {
            var manager = Object.FindFirstObjectByType<XRInteractionManager>();
            var grab = Object.FindFirstObjectByType<XRGrabInteractable>();
            // 无头显批量测试没有追踪设备，用独立的官方射线组件验证抓取事件链。
            // 这个对象只在测试中创建；键鼠到实际手柄的链路另在编辑器检查。
            var testHand = new GameObject("Test Hand");
            var ray = testHand.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            IXRSelectInteractor hand = ray;
            yield return null;
            var practice = grab.GetComponent<ToolPractice>();
            manager.SelectEnter(hand, grab);
            Assert.That(grab.isSelected, Is.True);
            Assert.That(practice.HasBeenGrabbed, Is.True);
            Assert.That(GameObject.Find("Practice Status").GetComponent<TMP_Text>().text, Does.Contain("TOOL ACQUIRED"));
            manager.SelectExit(hand, grab);
            Assert.That(grab.isSelected, Is.False);
            Assert.That(practice.HasBeenReleased, Is.True);
            Assert.That(GameObject.Find("Practice Status").GetComponent<TMP_Text>().text, Does.Contain("COMPLETE"));
            Object.Destroy(testHand);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ToolFallsOntoBenchAndReturnsIfLost()
        {
            var body = Object.FindFirstObjectByType<ReturnFallenTool>().GetComponent<Rigidbody>();
            body.position = new Vector3(0, 1.7f, 0.45f);
            body.linearVelocity = Vector3.zero;
            for (int i = 0; i < 65; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.InRange(1f, 1.15f), "Tool should rest on the bench, not fall through it.");
            body.position = new Vector3(0, -5f, 0);
            body.linearVelocity = Vector3.zero;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.GreaterThan(0.9f));
            Assert.That(body.position.z, Is.EqualTo(0.45f).Within(0.05f));
        }
    }
}
