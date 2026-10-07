using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    // 在最终主场景验证美术替换，不借用仍保留旧外观的教学场景。
    public sealed class StationArtSceneTests
    {
        private StationMissionSession session;
        private CargoInventory cargo;
        private LunarViewLayout layout;
        private FlightScenePresenter presentation;
        private float previousDelta;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousFocus;
        private readonly List<GameObject> temporary = new();

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            previousDelta = Time.captureDeltaTime;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            Time.captureDeltaTime = 1f / 30;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("10_LunarStation_Docking", LoadSceneMode.Single);
            yield return null;
            session = Object.FindAnyObjectByType<StationMissionSession>();
            Assert.That(session, Is.Not.Null);
            session.enabled = false; // 物理和 XR 正常运行，任务时钟由测试显式推进。
            cargo = session.GetComponent<CargoInventory>();
            layout = session.GetComponent<LunarViewLayout>();
            presentation = session.GetComponent<FlightScenePresenter>();
        }

        [TearDown]
        public void RestoreSettings()
        {
            Time.captureDeltaTime = previousDelta;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
            foreach (var obj in temporary)
                if (obj != null) Object.Destroy(obj);
            temporary.Clear();
        }

        [UnityTest]
        public IEnumerator RealisticStationAndPropsUseVisibleMaterialsAndSharedFlightMeshes()
        {
            var exterior = NamedChild(layout.BaseExterior.transform, "Station Realistic Exterior");
            var interior = NamedChild(presentation.GroundRoot.transform, "Station Realistic Interior");
            AssertVisibleArt(exterior);
            AssertVisibleArt(interior);

            var proxyFilters = layout.BaseProxy.GetComponentsInChildren<MeshFilter>(true);
            var proxyMeshes = proxyFilters.Select(filter => filter.sharedMesh).ToHashSet();
            var sourceRenderers = exterior.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
            foreach (var renderer in sourceRenderers)
            {
                var source = renderer.GetComponent<MeshFilter>();
                Assert.That(source, Is.Not.Null, renderer.name);
                Assert.That(proxyMeshes.Contains(source.sharedMesh), Is.True,
                    "飞行远景必须复用新的基地外观网格：" + renderer.name);
                Assert.That(proxyFilters.Any(filter => filter.sharedMesh == source.sharedMesh &&
                    filter.GetComponent<MeshRenderer>() != null &&
                    filter.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(renderer.sharedMaterials)), Is.True,
                    "基地外观和远景必须共享材质：" + renderer.name);
            }
            Assert.That(layout.BaseProxy.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(layout.BaseProxy.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(layout.BaseProxy.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty,
                "基地远景不应运行室内交互或任务脚本。");

            Assert.That(cargo.Items.Count, Is.EqualTo(7));
            Assert.That(cargo.Items.Select(item => item.Kind).Distinct().Count(), Is.EqualTo(6));
            foreach (var item in cargo.Items)
            {
                AssertPropVisual(item.transform);
                Assert.That(item.Inventory, Is.SameAs(cargo));
                Assert.That(item.GetComponent<Collider>(), Is.Not.Null, item.name);
                Assert.That(item.Body.mass, Is.EqualTo(cargo.Config.GetWeightKg(item.Kind)));
                var oldRenderer = item.GetComponent<Renderer>();
                Assert.That(oldRenderer == null || !oldRenderer.enabled, Is.True,
                    item.name + " 的旧方块外观必须隐藏。");
            }
            AssertPropVisual(Object.FindAnyObjectByType<RepairTool>().transform);

            foreach (string name in new[] { "Floor - teleport surface", "Wall Rear", "Wall Systems", "Wall Left",
                "Wall Right Front", "Wall Right Rear", "Workbench Surface" })
            {
                var collider = NamedChild(layout.BaseExterior.transform, name).GetComponent<Collider>();
                Assert.That(collider != null && collider.enabled, Is.True,
                    "更换可见外壳后仍需要原来的身体/道具碰撞：" + name);
            }

            yield return Capture("exterior", new Vector3(14, 6.4f, -14), new Vector3(-1.5f, 2, 0), fieldOfView: 62);
            yield return Capture("interior", new Vector3(-2.2f, 1.65f, -2.75f), new Vector3(0.5f, 1.5f, 1.5f), fieldOfView: 72);
            yield return Capture("supplies", new Vector3(0, 1.75f, -0.3f), new Vector3(0, 1.12f, 3.1f));
            yield return Capture("workbench", new Vector3(1.45f, 1.9f, -0.9f), new Vector3(0, 1.05f, 0.58f), fieldOfView: 60);
            var gameplayCamera = session.Player.Camera;
            yield return Capture("gameplay", gameplayCamera.transform.position,
                gameplayCamera.transform.position + gameplayCamera.transform.forward, artPreview: false);
        }

        [UnityTest]
        public IEnumerator RealWrenchSelectionRepairsAndOpeningDoorKeepsBodyRouteClear()
        {
            var tool = Object.FindAnyObjectByType<RepairTool>();
            var grab = tool.GetComponent<XRGrabInteractable>();
            var contact = Object.FindAnyObjectByType<RepairContact>();
            var task = session.Mission.RepairTask;
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var hand = CreateHand(manager);
            var initialToolPosition = tool.transform.position;
            yield return null;

            var door = NamedChild(layout.BaseExterior.transform, "Evacuation Door").gameObject;
            Physics.SyncTransforms();
            Assert.That(CorridorHits().Any(hit => hit.collider.gameObject == door), Is.True,
                "开始撤离前，真实身体路径必须被关闭的气闸门阻挡。");
            session.BeginMission();
            MoveTip(tool, contact.RepairPoint.position);
            session.Advance(0.1f);
            Assert.That(task.Progress, Is.Zero, "工具外观接触目标但没有抓取时不能维修。");

            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            Assert.That(tool.IsHeld, Is.True);
            MoveTip(tool, contact.RepairPoint.position + Vector3.right * contact.ContactRadius * 2);
            session.Advance(0.1f);
            Assert.That(task.Progress, Is.Zero, "新工具仍以工具头位置判定接触。");
            MoveTip(tool, contact.RepairPoint.position);
            session.Advance(task.DurationSeconds * 0.25f);
            Assert.That(task.Progress, Is.EqualTo(0.25f).Within(0.001f));

            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            MoveTip(tool, contact.RepairPoint.position);
            session.Advance(0.1f);
            Assert.That(task.State, Is.EqualTo(RepairState.Paused));
            Assert.That(task.Progress, Is.EqualTo(0.25f).Within(0.001f));
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            MoveTip(tool, contact.RepairPoint.position);
            session.Advance(task.RemainingSeconds);
            Assert.That(session.Mission.RepairRestored, Is.True);
            Assert.That(task.State, Is.EqualTo(RepairState.Complete));
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);

            session.Advance(session.Mission.RemainingSeconds);
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Evacuation));
            Physics.SyncTransforms();
            Assert.That(door.activeSelf, Is.False);
            var hits = CorridorHits();
            Assert.That(hits, Is.Empty, "写实门框或装饰阻挡撤离路径：" + string.Join(", ", hits.Select(hit => hit.collider.name)));
            for (float x = 3.4f; x <= 8f; x += 0.25f)
            {
                Assert.That(Physics.Raycast(new Vector3(x, 0.35f, 1.7f), Vector3.down, out var hit, 0.5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, "走廊缺地板 x=" + x);
                Assert.That(hit.point.y, Is.InRange(-0.02f, 0.02f));
                Assert.That(hit.normal.y, Is.GreaterThan(0.9f));
            }
            session.RetryMission();
            yield return null;
            Assert.That(door.activeSelf, Is.True);
            Assert.That(tool.IsHeld, Is.False);
            Assert.That(Vector3.Distance(tool.transform.position, initialToolPosition), Is.LessThan(0.1f));
            Assert.That(task.Progress, Is.Zero);
            AssertPropVisual(tool.transform);
        }

        [UnityTest]
        public IEnumerator SuppliesStayOnShelfThenGrabPackBoardExplodeAndResetWithTheirModels()
        {
            var starts = cargo.Items.Select(item => item.transform.position).ToArray();
            var shelf = NamedChild(presentation.GroundRoot.transform, "Supply Shelf").GetComponent<Collider>();
            // 等待十五秒，能发现替换模型时误加碰撞、重力或重心造成的翻倒和掉架。
            for (int frame = 0; frame < 450; frame++) yield return null;
            for (int index = 0; index < cargo.Items.Count; index++)
            {
                var candidate = cargo.Items[index];
                var collider = candidate.GetComponent<Collider>();
                float shelfTop = shelf.bounds.max.y;
                Assert.That(collider, Is.Not.Null, candidate.name);
                // 写实医疗盒和电路板比原方块更薄，稳定落架应以碰撞包络底面判断。
                Assert.That(collider.bounds.min.y, Is.InRange(shelfTop - 0.035f, shelfTop + 0.035f), candidate.name);
                Assert.That(candidate.Body.position.y, Is.GreaterThan(shelfTop), candidate.name);
                var horizontalDrift = candidate.Body.position - starts[index];
                horizontalDrift.y = 0;
                Assert.That(horizontalDrift.magnitude, Is.LessThan(0.15f), candidate.name);
                Assert.That(Vector3.Distance(candidate.Body.position, starts[index]), Is.LessThan(0.15f), candidate.name);
            }

            session.BeginMission();
            session.Advance(session.Mission.Config.RepairWindowSeconds);
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var hand = CreateHand(manager);
            var pack = Object.FindAnyObjectByType<CargoPackZone>();
            var item = cargo.Items.Single(candidate => candidate.Kind == CargoKind.MedicalKit);
            var visual = item.transform.Find("Realistic Prop");
            var shelfPosition = item.transform.position;
            yield return null;

            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
            Assert.That(item.State, Is.EqualTo(CargoState.Held));
            MoveItem(item, shelfPosition);
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
            Assert.That(item.State, Is.EqualTo(CargoState.World));
            Assert.That(cargo.TotalCount, Is.Zero, "正常放回货架必须释放携带额度。");
            for (int frame = 0; frame < 30; frame++) yield return null;
            Assert.That(item.Body.position.y, Is.GreaterThan(shelf.bounds.max.y));

            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
            MoveItem(item, pack.Volume.bounds.center + Vector3.up * 0.52f);
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
            for (int frame = 0; frame < 60; frame++) yield return null;
            Assert.That(item.State, Is.EqualTo(CargoState.Packed), "真实松手后自然落入包口应收纳同一件新模型。");
            Assert.That(visual.gameObject.activeInHierarchy, Is.False);
            Assert.That(cargo.PackedCount, Is.EqualTo(1));
            Assert.That(cargo.Discard(CargoKind.MedicalKit), Is.True);
            Assert.That(item.State, Is.EqualTo(CargoState.World));
            Assert.That(visual.gameObject.activeInHierarchy, Is.True);
            Assert.That(item.Body.isKinematic, Is.False);
            Assert.That(item.Body.useGravity, Is.True);

            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
            MoveItem(item, pack.Volume.bounds.center);
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)item.Grab);
            Assert.That(item.State, Is.EqualTo(CargoState.Packed));
            var hatch = Object.FindAnyObjectByType<HatchBoardingController>();
            Assert.That(hatch.CanBoard, Is.False, "室内收纳不能绕过登舱距离限制。");
            MovePlayerToBoardingZone();
            Assert.That(hatch.CanBoard, Is.True);
            hatch.RequestBoarding();
            Assert.That(item.State, Is.EqualTo(CargoState.Loaded));
            session.Advance(0);
            var flight = session.GetComponent<AscentMission>();
            CompleteBoarding(flight);
            yield return null;
            Assert.That(presentation.GroundRoot.activeSelf, Is.False);
            Assert.That(layout.BaseProxy.activeInHierarchy, Is.True);
            flight.PowerOn();
            flight.StartNavigation();
            flight.Tick(flight.Config.NavigationSeconds);
            flight.PrepareEngine();
            flight.Tick(flight.Config.EngineSeconds);
            flight.Ignite();
            flight.Tick(flight.Config.IgnitionSeconds);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Ascent));
            flight.Tick(flight.BaseRemaining);
            yield return null;
            Assert.That(flight.BaseExploded, Is.True);
            Assert.That(layout.BaseProxy.activeSelf, Is.False, "基地爆炸必须隐藏新外观远景。");

            session.RetryMission();
            yield return null;
            yield return null;
            Assert.That(presentation.GroundRoot.activeSelf, Is.True);
            Assert.That(cargo.TotalCount, Is.Zero);
            Assert.That(NamedChild(layout.BaseExterior.transform, "Evacuation Door").gameObject.activeSelf, Is.True);
            for (int index = 0; index < cargo.Items.Count; index++)
            {
                var candidate = cargo.Items[index];
                Assert.That(candidate.State, Is.EqualTo(CargoState.World));
                Assert.That(candidate.gameObject.activeInHierarchy, Is.True);
                Assert.That(candidate.Grab.enabled, Is.True);
                Assert.That(candidate.Body.isKinematic, Is.False);
                Assert.That(Vector3.Distance(candidate.transform.position, starts[index]), Is.LessThan(0.15f), candidate.name);
                AssertPropVisual(candidate.transform);
            }
            Assert.That(presentation.GroundRoot.GetComponentsInChildren<Transform>(true)
                .Count(child => child.name == "Station Realistic Exterior"), Is.EqualTo(1));

            // 飞行世界休眠时无需改其子节点；下次登舱启用它时必须重新显示基地。
            session.BeginMission();
            session.Advance(session.Mission.Config.RepairWindowSeconds);
            MovePlayerToBoardingZone();
            hatch.RequestBoarding();
            session.Advance(0);
            CompleteBoarding(flight);
            yield return null;
            Assert.That(layout.BaseProxy.activeInHierarchy, Is.True);
            Assert.That(flight.BaseExploded, Is.False);
        }

        private XRRayInteractor CreateHand(XRInteractionManager manager)
        {
            var obj = new GameObject("Station art integration hand");
            temporary.Add(obj);
            var hand = obj.AddComponent<XRRayInteractor>();
            hand.interactionManager = manager;
            hand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            return hand;
        }

        private static Transform NamedChild(Transform parent, string name)
        {
            var matches = parent.GetComponentsInChildren<Transform>(true).Where(child => child.name == name).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), parent.name + " 内应有且仅有一个 " + name);
            return matches[0];
        }

        private static void AssertPropVisual(Transform owner)
        {
            var visual = owner.Find("Realistic Prop");
            Assert.That(visual, Is.Not.Null, owner.name + " 缺少已接入的写实模型。");
            Assert.That(owner.GetComponentsInChildren<Transform>(true).Count(child => child.name == "Realistic Prop"), Is.EqualTo(1));
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, owner.name);
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty, owner.name);
            AssertVisibleArt(visual);
        }

        private static void AssertVisibleArt(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.enabled).ToArray();
            Assert.That(renderers, Is.Not.Empty, root.name);
            var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
            Assert.That(materials.All(material => material != null && material.shader != null && material.shader.isSupported &&
                material.shader.name != "Hidden/InternalErrorShader"), Is.True, root.name + " 材质不可缺失或使用错误着色器。");
            Assert.That(materials.Any(material => material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null), Is.True,
                root.name + " 应使用已导入的表面贴图。");
        }

        private static RaycastHit[] CorridorHits() => Physics.CapsuleCastAll(new Vector3(3.2f, 0.3f, 1.7f),
            new Vector3(3.2f, 1.6f, 1.7f), 0.15f, Vector3.right, 4.7f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        private static void MoveTip(RepairTool tool, Vector3 destination)
        {
            var position = tool.transform.position + destination - tool.Tip.position;
            tool.GetComponent<Rigidbody>().position = position;
            tool.transform.position = position;
            Physics.SyncTransforms();
        }

        private static void MoveItem(CargoItem item, Vector3 destination)
        {
            item.Body.position = destination;
            item.transform.position = destination;
            if (!item.Body.isKinematic)
            {
                item.Body.linearVelocity = Vector3.zero;
                item.Body.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
        }

        private void MovePlayerToBoardingZone()
        {
            var body = session.Exit.PlayerBody;
            var center = body.transform.TransformPoint(body.center);
            var destination = session.Exit.Volume.bounds.center;
            body.enabled = false;
            body.transform.position += new Vector3(destination.x - center.x, 0, destination.z - center.z);
            body.enabled = true;
            Physics.SyncTransforms();
        }

        private static void CompleteBoarding(AscentMission flight)
        {
            flight.Tick(flight.Config.FadeOutSeconds + flight.Config.BlackSeconds + flight.Config.FadeInSeconds + 0.001f);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Startup));
        }

        private static IEnumerator Capture(string name, Vector3 eye, Vector3 target, bool artPreview = true, float fieldOfView = 70)
        {
            var mainCamera = Camera.main;
            var obj = new GameObject("Station art verification camera");
            var camera = obj.AddComponent<Camera>();
            camera.CopyFrom(mainCamera);
            camera.enabled = false;
            if (artPreview)
            {
                camera.fieldOfView = fieldOfView;
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            }
            else camera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
            var canvasStates = new Dictionary<Canvas, bool>();
            var rendererStates = new Dictionary<Renderer, bool>();
            var renderTexture = new RenderTexture(1600, 1100, 24);
            var pixels = new Texture2D(1600, 1100, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                if (artPreview)
                {
                    // 仅为美术查看临时隐藏遮挡，不改变场景中的实际 UI、角色或交互配置。
                    foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        canvasStates.Add(canvas, canvas.enabled);
                        canvas.enabled = false;
                    }
                    var player = Object.FindAnyObjectByType<StationMissionSession>().Player;
                    var avatar = player.GetComponentInChildren<TrackedCrewSuit>(true);
                    if (avatar != null && avatar.Suit != null)
                        foreach (var renderer in avatar.Suit.GetComponentsInChildren<Renderer>(true))
                            rendererStates[renderer] = renderer.enabled;
                    // 随身腰包与身体一样属于第一人称表示，后移的美术相机无需显示其框架。
                    var pouch = Object.FindAnyObjectByType<CargoPackZone>();
                    if (pouch != null)
                        foreach (var renderer in pouch.GetComponentsInChildren<Renderer>(true))
                            rendererStates[renderer] = renderer.enabled;
                    foreach (var renderer in rendererStates.Keys) renderer.enabled = false;
                }
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                camera.enabled = true;
                // 等待 GPU Resident Drawer 注册新相机，避免截图缺失刚导入的模型。
                for (int frame = 0; frame < 8; frame++)
                {
                    if (!artPreview) camera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
                    yield return null;
                }
                if (!artPreview) camera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = renderTexture });
                RenderTexture.active = renderTexture;
                pixels.ReadPixels(new Rect(0, 0, 1600, 1100), 0, 0);
                pixels.Apply();
                var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Previews"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "station-" + name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                foreach (var state in canvasStates)
                    if (state.Key != null) state.Key.enabled = state.Value;
                foreach (var state in rendererStates)
                    if (state.Key != null) state.Key.enabled = state.Value;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                renderTexture.Release();
                Object.Destroy(pixels);
                Object.Destroy(renderTexture);
                Object.Destroy(obj);
            }
        }
    }
}
