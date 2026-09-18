using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace LunarEscape.Tests
{
    // 核心任务使用确定的时间步，避免测试依赖机器帧率。
    public sealed class TimedRepairTaskTests
    {
        private GameObject owner;
        private TimedRepairTask task;

        [SetUp]
        public void CreateTask()
        {
            owner = new GameObject("Test Repair Task");
            task = owner.AddComponent<TimedRepairTask>();
            task.Configure(2f);
        }

        [TearDown]
        public void RemoveTask() => Object.DestroyImmediate(owner);

        [Test]
        public void NoValidContactLeavesTaskReady()
        {
            int changes = 0;
            int completions = 0;
            task.Changed += () => changes++;
            task.Completed += () => completions++;

            task.Tick(false, 10f);

            Assert.That(task.Progress, Is.Zero);
            Assert.That(task.State, Is.EqualTo(RepairState.Ready));
            Assert.That(changes, Is.Zero);
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void InterruptedContactPreservesProgressAndCanResume()
        {
            int changes = 0;
            task.Changed += () => changes++;

            task.Tick(true, 0.5f);
            Assert.That(task.State, Is.EqualTo(RepairState.Working));
            Assert.That(task.Progress, Is.EqualTo(0.25f).Within(0.0001f));

            task.Tick(false, 20f);
            Assert.That(task.State, Is.EqualTo(RepairState.Paused));
            Assert.That(task.Progress, Is.EqualTo(0.25f).Within(0.0001f));

            task.Tick(true, 0.5f);
            Assert.That(task.State, Is.EqualTo(RepairState.Working));
            Assert.That(task.Progress, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void CompletionOccursOncePerPracticeAndResetAllowsAnother()
        {
            int completions = 0;
            task.Completed += () => completions++;

            task.Tick(true, 10f);
            task.Tick(true, 10f);
            task.Tick(false, 10f);
            Assert.That(task.Progress, Is.EqualTo(1f));
            Assert.That(task.State, Is.EqualTo(RepairState.Complete));
            Assert.That(completions, Is.EqualTo(1));

            task.ResetTask();
            Assert.That(task.Progress, Is.Zero);
            Assert.That(task.State, Is.EqualTo(RepairState.Ready));
            Assert.That(task.DurationSeconds, Is.EqualTo(2f));
            task.Tick(true, 2f);
            Assert.That(completions, Is.EqualTo(2));
        }
    }

    public sealed class RepairSceneTests
    {
        private TimedRepairTask task;
        private RepairContact contact;
        private LocalizationService localization;
        private TMP_Text status;

        [UnitySetUp]
        public IEnumerator LoadRepairRoom()
        {
            yield return SceneManager.LoadSceneAsync("05_LunarStation_Repair", LoadSceneMode.Single);
            yield return null;
            Assert.That(Object.FindObjectsByType<EventSystem>().Count(system => system.isActiveAndEnabled),
                Is.EqualTo(1), "维修场景必须只有一个活动 EventSystem，旧场景不能遗留全局输入系统。");
            task = Object.FindAnyObjectByType<TimedRepairTask>();
            contact = Object.FindAnyObjectByType<RepairContact>();
            localization = Object.FindAnyObjectByType<LocalizationService>();
            Assert.That(task, Is.Not.Null);
            Assert.That(contact, Is.Not.Null);
            Assert.That(localization, Is.Not.Null);
            // 手动推进任务；测试间隙的真实帧不应改变待验证的进度。
            contact.enabled = false;
            task.ResetTask();
            status = GameObject.Find("Repair Status").GetComponent<TMP_Text>();
        }

        [UnityTest]
        public IEnumerator OnlyHeldCorrectToolTipInsideRadiusCanRepair()
        {
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var correctTool = Object.FindObjectsByType<RepairTool>()
                .Single(tool => tool.ToolType == "maintenance");
            var correctGrab = correctTool.GetComponent<XRGrabInteractable>();
            var testHand = new GameObject("Repair Test Hand");
            var wrongObject = new GameObject("Wrong Test Tool");
            wrongObject.AddComponent<BoxCollider>();
            wrongObject.AddComponent<Rigidbody>().useGravity = false;
            var wrongGrab = wrongObject.AddComponent<XRGrabInteractable>();
            wrongGrab.interactionManager = manager;
            var wrongTool = wrongObject.AddComponent<RepairTool>();
            wrongTool.Configure(wrongObject.transform, "wrong-type");
            var ray = testHand.AddComponent<XRRayInteractor>();
            ray.interactionManager = manager;
            IXRSelectInteractor hand = ray;
            yield return null;

            try
            {
                var point = contact.RepairPoint;
                Assert.That(point, Is.Not.Null);
                contact.Configure(point, new[] { correctTool, wrongTool }, "maintenance", contact.ContactRadius);

                MoveTipTo(correctTool, point.position);
                Assert.That(correctTool.IsHeld, Is.False);
                Assert.That(contact.HasValidContact(), Is.False, "放在维修点上的工具不能自动维修。");
                task.Tick(contact.HasValidContact(), task.DurationSeconds);
                Assert.That(task.Progress, Is.Zero);

                manager.SelectEnter(hand, wrongGrab);
                MoveTipTo(wrongTool, point.position);
                Assert.That(wrongTool.IsHeld, Is.True);
                Assert.That(contact.HasValidContact(), Is.False, "握住错误类型的工具也不能维修。");
                task.Tick(contact.HasValidContact(), task.DurationSeconds);
                Assert.That(task.Progress, Is.Zero);
                manager.SelectExit(hand, wrongGrab);

                manager.SelectEnter(hand, correctGrab);
                MoveTipTo(correctTool, point.position + Vector3.right * (contact.ContactRadius * 2f));
                Assert.That(correctTool.IsHeld, Is.True);
                Assert.That(contact.HasValidContact(), Is.False, "正确工具的工具头必须进入范围。");
                task.Tick(contact.HasValidContact(), task.DurationSeconds);
                Assert.That(task.Progress, Is.Zero);

                MoveTipTo(correctTool, point.position);
                Assert.That(contact.HasValidContact(), Is.True);
                task.Tick(contact.HasValidContact(), task.DurationSeconds * 0.25f);
                Assert.That(task.State, Is.EqualTo(RepairState.Working));
                Assert.That(task.Progress, Is.EqualTo(0.25f).Within(0.0001f));

                manager.SelectExit(hand, correctGrab);
                MoveTipTo(correctTool, point.position);
                Assert.That(correctTool.IsHeld, Is.False);
                Assert.That(contact.HasValidContact(), Is.False);
                task.Tick(contact.HasValidContact(), task.DurationSeconds);
                Assert.That(task.State, Is.EqualTo(RepairState.Paused));
                Assert.That(task.Progress, Is.EqualTo(0.25f).Within(0.0001f));

                manager.SelectEnter(hand, correctGrab);
                MoveTipTo(correctTool, point.position);
                task.Tick(contact.HasValidContact(), task.DurationSeconds * 0.75f);
                Assert.That(task.State, Is.EqualTo(RepairState.Complete));
                Assert.That(task.Progress, Is.EqualTo(1f));
                manager.SelectExit(hand, correctGrab);
            }
            finally
            {
                Object.Destroy(testHand);
                Object.Destroy(wrongObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LanguageButtonsRefreshStatusWithoutResettingProgress()
        {
            ClickButton("Language Chinese");
            Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.Chinese));
            Assert.That(status.text, Is.EqualTo("等待维修"));
            task.Tick(true, task.DurationSeconds * 0.5f);
            Assert.That(status.text, Is.EqualTo("维修中 50%"));

            ClickButton("Language English");
            Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.English));
            Assert.That(status.text, Is.EqualTo("Repairing: 50%"));
            Assert.That(task.Progress, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(task.State, Is.EqualTo(RepairState.Working));

            task.Tick(false, 1f);
            ClickButton("Language Russian");
            Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.Russian));
            Assert.That(status.text, Is.EqualTo("Пауза: 50%\nВерните наконечник в точку ремонта."));
            Assert.That(task.Progress, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(task.State, Is.EqualTo(RepairState.Paused));

            task.Tick(true, task.DurationSeconds * 0.5f);
            Assert.That(status.text, Is.EqualTo("Ремонт завершён\nКислородная система исправна."));
            ClickButton("Language Chinese");
            Assert.That(status.text, Is.EqualTo("维修完成\n氧气设备恢复正常"));
            ClickButton("Language English");
            Assert.That(status.text, Is.EqualTo("Repair Complete\nOxygen system restored."));
            Assert.That(task.Progress, Is.EqualTo(1f));
            Assert.That(task.State, Is.EqualTo(RepairState.Complete));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseInputReachesEnglishLanguageButtonThroughEventSystem()
        {
            var simulator = GameObject.Find("XR Interaction Simulator - Editor Only");
            Assert.That(simulator, Is.Not.Null);
            bool simulatorWasActive = simulator.activeSelf;
            var module = Object.FindAnyObjectByType<XRUIInputModule>();
            Assert.That(module, Is.Not.Null);
            bool mouseInputWasEnabled = module.enableMouseInput;
            simulator.SetActive(false);
            // Simulator 禁用时不会恢复它接管的鼠标开关，因此测试显式接回鼠标输入。
            module.enableMouseInput = true;
            var background = InputSystem.settings.backgroundBehavior;
            var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            // 无头测试没有聚焦的 Game 窗口；仅在本测试内把虚拟鼠标交给游戏。
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>("Repair UI Test Mouse");
            try
            {
                localization.SetLanguage(GameLanguage.Chinese);
                yield return null;
                yield return null;
                var eventSystem = EventSystem.current;
                Assert.That(eventSystem, Is.Not.Null);
                Assert.That(eventSystem.currentInputModule, Is.InstanceOf<XRUIInputModule>());
                Assert.That(eventSystem.currentInputModule, Is.SameAs(module));
                Assert.That(module.enableMouseInput, Is.True);
                Assert.That(module.enableBuiltinActionsAsFallback, Is.True);

                var button = GameObject.Find("Language English").GetComponent<Button>();
                var rect = button.GetComponent<RectTransform>();
                var canvas = button.GetComponentInParent<Canvas>();
                Assert.That(canvas.worldCamera, Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                var screenPoint = canvas.worldCamera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
                Assert.That(screenPoint.z, Is.GreaterThan(0f), "按钮必须位于场景相机前方。");
                Assert.That(canvas.worldCamera.pixelRect.Contains(screenPoint), Is.True,
                    "按钮中心必须位于相机画面内。");
                var pointer = new PointerEventData(eventSystem) { position = screenPoint };
                var hits = new List<RaycastResult>();
                eventSystem.RaycastAll(pointer, hits);
                Assert.That(hits.Any(hit => hit.gameObject.GetComponentInParent<Button>() == button), Is.True,
                    "按钮屏幕点必须被真实 UI 射线命中。命中对象：" +
                    string.Join(", ", hits.Select(hit => hit.gameObject.name)));

                // 像官方 XR 模拟器一样写入虚拟设备状态，避免依赖无头模式的系统事件。
                // 按下和松开仍跨帧经过真实 XRUIInputModule，不直接调用按钮事件。
                var state = new MouseState { position = screenPoint };
                InputState.Change(mouse, state);
                yield return null;
                yield return null;
                InputState.Change(mouse, state.WithButton(MouseButton.Left));
                yield return null;
                yield return null;
                InputState.Change(mouse, state.WithButton(MouseButton.Left, false));
                yield return null;
                yield return null;

                Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.English),
                    "屏幕上的鼠标点击必须触发语言切换。");
                Assert.That(status.text, Is.EqualTo("Awaiting Repair"));
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = focus;
                if (module != null) module.enableMouseInput = mouseInputWasEnabled;
                if (simulator != null) simulator.SetActive(simulatorWasActive);
            }
        }

        [UnityTest]
        public IEnumerator ResetButtonRestartsCompletedPracticeInCurrentLanguage()
        {
            ClickButton("Language Russian");
            task.Tick(true, task.DurationSeconds);
            ClickButton("Reset Repair");
            Assert.That(task.Progress, Is.Zero);
            Assert.That(task.State, Is.EqualTo(RepairState.Ready));
            Assert.That(localization.CurrentLanguage, Is.EqualTo(GameLanguage.Russian));
            Assert.That(status.text, Is.EqualTo("Ожидание ремонта"));
            task.Tick(true, task.DurationSeconds);
            Assert.That(task.State, Is.EqualTo(RepairState.Complete));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProgressBarGeometryMatchesTaskProgress()
        {
            var fill = GameObject.Find("Progress Fill").GetComponent<Image>();
            Assert.That(fill.sprite, Is.Not.Null, "Filled Image 没有 Sprite 时不会按进度裁剪。");
            // 读取实际渲染网格，避免 fillAmount 正确但画面仍是满条的回归。
            task.ResetTask();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(RenderedWidth(fill), Is.LessThan(0.001f),
                "0% 时进度条不应有可见面积。");

            task.Tick(true, task.DurationSeconds * 0.5f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            float halfWidth = RenderedWidth(fill);
            Assert.That(halfWidth, Is.GreaterThan(0.001f));

            task.Tick(true, task.DurationSeconds * 0.5f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            float fullWidth = RenderedWidth(fill);
            Assert.That(fullWidth, Is.GreaterThan(0.001f));
            Assert.That(halfWidth / fullWidth, Is.EqualTo(0.5f).Within(0.01f),
                "50% 时实际网格宽度应为完成时的一半。");
        }

        [UnityTest]
        public IEnumerator CatalogGlyphsExistAndAllLanguagesFitTheirPanels()
        {
            Assert.That(localization.Catalog, Is.Not.Null);
            var catalog = JsonUtility.FromJson<CatalogData>(localization.Catalog.text);
            Assert.That(catalog.entries, Is.Not.Empty);
            var labels = Object.FindObjectsByType<TMP_Text>();
            Assert.That(labels, Is.Not.Empty);
            foreach (var label in labels)
                Assert.That(label.font, Is.Not.Null, label.name + " 缺少字体。");

            var characters = catalog.entries.SelectMany(entry => new[] { entry.zh, entry.en, entry.ru })
                .SelectMany(value => string.Format(CultureInfo.InvariantCulture, value, 50, 50, 50, 50, 50, 50, 50))
                .Where(character => !char.IsWhiteSpace(character)).Distinct().ToArray();
            foreach (var font in labels.Select(label => label.font).Distinct())
                foreach (char character in characters)
                    Assert.That(font.HasCharacter(character), Is.True,
                        font.name + " 缺少字符 " + character + " (U+" + ((int)character).ToString("X4") + ")");

            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                localization.SetLanguage(language);
                task.ResetTask();
                AssertLabelsFit(labels, language);
                task.Tick(true, task.DurationSeconds * 0.5f);
                AssertLabelsFit(labels, language);
                task.Tick(false, 1f);
                AssertLabelsFit(labels, language);
                task.Tick(true, task.DurationSeconds * 0.5f);
                AssertLabelsFit(labels, language);
            }
            yield return null;
        }

        private static void MoveTipTo(RepairTool tool, Vector3 position) =>
            tool.transform.position += position - tool.Tip.position;

        private static float RenderedWidth(Image image)
        {
            // GetMesh 返回 UI 自己持有的网格；这里只读，不清空或销毁。
            var mesh = image.canvasRenderer.GetMesh();
            if (mesh == null || mesh.vertexCount == 0) return 0f;
            var vertices = mesh.vertices;
            float height = vertices.Max(vertex => vertex.y) - vertices.Min(vertex => vertex.y);
            return height > 0.001f
                ? vertices.Max(vertex => vertex.x) - vertices.Min(vertex => vertex.x) : 0f;
        }

        private static void ClickButton(string name)
        {
            var target = GameObject.Find(name);
            Assert.That(target, Is.Not.Null, "找不到按钮 " + name);
            var button = target.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True);
            // 调用真实场景按钮绑定的事件，能发现漏接 Inspector 引用。
            button.onClick.Invoke();
        }

        private static void AssertLabelsFit(TMP_Text[] labels, GameLanguage language)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var label in labels)
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False,
                    language + " 的 " + label.name + " 文字超出面板：" + label.text);
            }
        }

        [Serializable]
        private sealed class CatalogData
        {
            public CatalogEntry[] entries;
        }

        [Serializable]
        private sealed class CatalogEntry
        {
            public string key;
            public string zh;
            public string en;
            public string ru;
        }
    }
}
