using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace LunarEscape.Tests
{
    public sealed class BoardingSceneTests
    {
        private StationMissionSession session;
        private StationMission mission;
        private CollisionAwareSimulator simulator;
        private LocalizationService localization;
        private float previousFrameDuration;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousFocus;

        [SetUp]
        public void UseDeterministicInput()
        {
            previousFrameDuration = Time.captureDeltaTime;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = 1f / 30f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [UnitySetUp]
        public IEnumerator LoadBoardingScene()
        {
            yield return SceneManager.LoadSceneAsync("07_LunarStation_CargoBoarding", LoadSceneMode.Single);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>();
            Assert.That(session, Is.Not.Null);
            session.enabled = false;
            mission = session.Mission;
            simulator = Object.FindAnyObjectByType<CollisionAwareSimulator>();
            localization = Object.FindAnyObjectByType<LocalizationService>();
            Assert.That(session.RequiresExitConfirmation, Is.True);
            Assert.That(simulator, Is.Not.Null);
            SetMovement(Vector3.zero);
            SetVectorInput(simulator.keyboardRotationDeltaInput, Vector2.zero);
            SetVectorInput(simulator.mouseRotationDeltaInput, Vector2.zero);
            SetVectorInput(simulator.mouseScrollInput, Vector2.zero);
            while (Time.time <= 1.05f) yield return null;
            yield return Frames(3);
        }

        [TearDown]
        public void RestoreInputSettings()
        {
            if (simulator != null) SetMovement(Vector3.zero);
            Time.captureDeltaTime = previousFrameDuration;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
        }

        [UnityTest]
        public IEnumerator OnlyPlayerInsideCabinCanConfirmBoardingAndWalkingInDoesNotComplete()
        {
            StartEvacuation();
            var cabin = session.Exit.Volume.bounds.center;
            Assert.That(cabin.x, Is.GreaterThan(20f), "胜利区域必须移到登船舱内。");
            var confirm = FindObject("Confirm Boarding").GetComponent<Button>();

            // 原走廊终点和室外路线只是在途中，远程调用按钮事件也不能完成。
            foreach (float x in new[] { 7f, 14f, 21f })
            {
                MoveBodyTo(new Vector3(x, 0f, 1.7f));
                Assert.That(session.Exit.ContainsPlayer, Is.False);
                confirm.onClick.Invoke();
                session.Advance(0f);
                Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            }

            var tool = Object.FindAnyObjectByType<RepairTool>().GetComponent<Rigidbody>();
            tool.position = cabin;
            var modality = session.Player.GetComponent<XRInputModalityManager>();
            modality.rightController.transform.position = cabin;
            Physics.SyncTransforms();
            Assert.That(session.Exit.ContainsPlayer, Is.False, "工具或手伸进船舱不能代替玩家身体。");
            confirm.onClick.Invoke();
            session.Advance(0f);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));

            MoveBodyTo(new Vector3(cabin.x, 0f, cabin.z));
            Assert.That(session.Exit.ContainsPlayer, Is.True);
            session.Advance(0.25f);
            yield return null;
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation),
                "进入船舱后仍需主动确认登船，不能沿用旧出口的自动完成。");
            float remaining = mission.RemainingSeconds;
            Assert.That(confirm.IsInteractable(), Is.True);
            confirm.onClick.Invoke();
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation),
                "确认按钮只提交请求，不能跳过本帧任务时钟直接结算。");
            session.Advance(0f);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Completed));
            Assert.That(mission.RemainingSeconds, Is.EqualTo(remaining));
        }

        [UnityTest]
        public IEnumerator MoonRouteHasContinuousFloorAndRealWalkingUsesTheExistingClockAndCollision()
        {
            StartEvacuation();
            Physics.SyncTransforms();
            for (float x = 7.6f; x < 25f; x += 0.4f)
            {
                Assert.That(Physics.Raycast(new Vector3(x, 0.3f, 1.7f), Vector3.down,
                    out var hit, 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                    Is.True, "走廊/月面/船舱连接处缺少地面，x=" + x);
                Assert.That(hit.point.y, Is.InRange(-0.02f, 0.02f));
                Assert.That(hit.normal.y, Is.GreaterThan(0.9f));
            }

#pragma warning disable CS0618
            simulator.targetedDeviceInput = TargetedDevices.FPS;
#pragma warning restore CS0618
            yield return Frames(2);
            session.Player.MatchOriginUpCameraForward(Vector3.up, Vector3.forward);
            MoveBodyTo(new Vector3(8.3f, 0f, 1.7f));
            simulator.translateXSpeed = simulator.translateZSpeed = 2f;
            simulator.bodyTranslateMultiplier = 2f;
            float budget = mission.EvacuationBudgetSeconds;
            float remaining = mission.RemainingSeconds;
            double startTime = Time.timeAsDouble;
            session.enabled = true;
            SetMovement(Vector3.right);
            yield return Frames(60);
            SetMovement(Vector3.zero);
            session.enabled = false;
            float elapsed = (float)(Time.timeAsDouble - startTime);
            Assert.That(session.Player.Camera.transform.position.x, Is.InRange(15.7f, 17.1f),
                "FPS 输入必须经真实身体碰撞系统走过室外路线。");
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Assert.That(mission.EvacuationBudgetSeconds, Is.EqualTo(budget), "出门不能新开或重置撤离计时。");
            Assert.That(mission.RemainingSeconds, Is.EqualTo(remaining - elapsed).Within(0.08f),
                "室外行走期间仍由同一个任务时钟每帧扣时。");

            MoveBodyTo(new Vector3(12f, 0f, 1.7f));
            SetMovement(Vector3.forward);
            yield return Frames(30);
            float stoppedZ = session.Player.Camera.transform.position.z;
            yield return Frames(15);
            SetMovement(Vector3.zero);
            Assert.That(stoppedZ, Is.InRange(2f, 2.95f), "月面路径边界必须挡住身体。");
            Assert.That(session.Player.Camera.transform.position.z, Is.EqualTo(stoppedZ).Within(0.05f));
            Assert.That(session.Player.Origin.transform.position.y, Is.GreaterThan(-0.1f));
        }

        [UnityTest]
        public IEnumerator BoardingRequestCannotBeatTimeoutInTheSameClockStep()
        {
            StartEvacuation();
            Vector3 cabin = session.Exit.Volume.bounds.center;
            MoveBodyTo(new Vector3(cabin.x, 0f, cabin.z));
            Assert.That(session.Exit.ContainsPlayer, Is.True);
            session.Advance(mission.RemainingSeconds - 0.01f);
            FindObject("Confirm Boarding").GetComponent<Button>().onClick.Invoke();
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            session.Advance(0.02f);
            FindObject("Confirm Boarding").GetComponent<Button>().onClick.Invoke();
            Assert.That(session.ConfirmExit(), Is.False);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Failed));
            Assert.That(mission.FailureReason, Is.EqualTo(StationMissionFailure.EvacuationTimeout));
            Assert.That(mission.RemainingSeconds, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CargoAndBoardingTextIsTranslatedAndFitsInAllThreeLanguages()
        {
            var table = JsonUtility.FromJson<LocalizationService.Table>(localization.Catalog.text);
            foreach (string key in new[]
            {
                "cargo.kind.Oxygen", "cargo.kind.RepairKit", "cargo.kind.Battery",
                "cargo.kind.MedicalKit", "cargo.kind.DataCore", "cargo.kind.LunarSample",
                "cargo.capacity", "cargo.pack", "cargo.route", "cargo.boarding", "cargo.confirm",
                "cargo.reject.types", "cargo.reject.weight", "cargo.reject.quantity", "cargo.reject.phase"
            })
            {
                var entry = table.entries.Single(item => item.key == key);
                foreach (string translation in new[] { entry.zh, entry.en, entry.ru })
                    Assert.That(string.IsNullOrWhiteSpace(translation), Is.False, key);
            }

            for (int stage = 0; stage < 4; stage++)
            {
                session.RetryMission();
                if (stage > 0)
                {
                    StartEvacuation();
                    var inventory = Object.FindAnyObjectByType<CargoInventory>();
                    foreach (var item in inventory.Items.Where(item => item.Kind == CargoKind.Oxygen
                        || item.Kind == CargoKind.LunarSample || item.Kind == CargoKind.DataCore))
                        Assert.That(inventory.TryHold(item), Is.True);
                    Assert.That(inventory.TotalWeightKg, Is.EqualTo(12f));
                    Assert.That(inventory.TryHold(inventory.Items.Single(item => item.Kind == CargoKind.MedicalKit)), Is.False);
                }
                if (stage == 2)
                {
                    Vector3 cabin = session.Exit.Volume.bounds.center;
                    MoveBodyTo(new Vector3(cabin.x, 0f, cabin.z));
                    FindObject("Confirm Boarding").GetComponent<Button>().onClick.Invoke();
                    session.Advance(0f);
                    Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Completed));
                }
                if (stage == 3) session.Advance(mission.RemainingSeconds);
                var phase = mission.Phase;
                float remaining = mission.RemainingSeconds;
                foreach (GameLanguage language in new[] { GameLanguage.Chinese, GameLanguage.English, GameLanguage.Russian })
                {
                    localization.SetLanguage(language);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var labels = SceneManager.GetActiveScene().GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TMP_Text>())
                        .Where(label => label.isActiveAndEnabled).ToArray();
                    Assert.That(labels.Length, Is.GreaterThan(10));
                    foreach (var label in labels)
                    {
                        Assert.That(label.text, Does.Not.StartWith("["), label.name + " 缺少语言键。");
                        label.ForceMeshUpdate();
                        Assert.That(label.isTextOverflowing, Is.False,
                            language + "/" + phase + "/" + label.name + " 文字超出面板：" + label.text);
                        foreach (char character in label.text.Where(character => !char.IsWhiteSpace(character)).Distinct())
                            Assert.That(label.font.HasCharacter(character), Is.True,
                                language + "/" + label.name + " 缺少字符 " + character);
                    }
                    Assert.That(mission.Phase, Is.EqualTo(phase));
                    Assert.That(mission.RemainingSeconds, Is.EqualTo(remaining), "切换语言不能改变任务时间。");
                }
            }
        }

        [UnityTest]
        public IEnumerator CaptureCargoRouteAndCabinPreviews()
        {
            StartEvacuation();
            localization.SetLanguage(GameLanguage.Chinese);
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            CapturePreview("supplies", new Vector3(0f, 1.8f, 1.4f), new Vector3(0f, 1.8f, 3.6f), 95f);
            CapturePreview("route", new Vector3(9.3f, 1.8f, 1.7f), new Vector3(22.8f, 1.3f, 1.7f));
            MoveBodyTo(new Vector3(21f, 0f, 1.7f));
            yield return Frames(2);
            CapturePreview("cabin", new Vector3(22.7f, 1.7f, 1.7f), new Vector3(25.82f, 1.78f, 1.7f));
            CapturePreview("window", new Vector3(23f, 1.7f, 1.7f), new Vector3(23.5f, 1.7f, 4.5f), 65f);
        }

        private static void CapturePreview(string name, Vector3 position, Vector3 target, float fieldOfView = 55f)
        {
            var owner = new GameObject("Cargo preview camera");
            var camera = owner.AddComponent<Camera>();
            camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.fieldOfView = fieldOfView;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            var texture = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                texture.Create();
                camera.targetTexture = texture;
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    var request = new RenderPipeline.StandardRequest { destination = texture };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    {
                        TestContext.Progress.WriteLine("Preview unavailable: current render pipeline does not support StandardRequest.");
                        return;
                    }
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                pixels.Apply();
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.development/cargo-preview-" + name + ".png"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                TestContext.Progress.WriteLine(path);
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                texture.Release();
                Object.Destroy(pixels);
                Object.Destroy(texture);
                Object.Destroy(owner);
            }
        }

        private void StartEvacuation()
        {
            session.BeginMission();
            session.Advance(mission.Config.RepairWindowSeconds);
            Assert.That(mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
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

        private void SetMovement(Vector3 value)
        {
            SetFloatInput(simulator.translateXInput, value.x);
            SetFloatInput(simulator.translateYInput, value.y);
            SetFloatInput(simulator.translateZInput, value.z);
        }

        private static void SetFloatInput(XRInputValueReader<float> reader, float value)
        {
            reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            reader.manualValue = value;
        }

        private static void SetVectorInput(XRInputValueReader<Vector2> reader, Vector2 value)
        {
            reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            reader.manualValue = value;
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
