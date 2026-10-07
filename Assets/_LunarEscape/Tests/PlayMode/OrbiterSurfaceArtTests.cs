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

namespace LunarEscape.Tests
{
    // Resource limits are an asset regression budget; they do not assert a GPU-memory or frame-rate target.
    public sealed class OrbiterSurfaceArtTests
    {
        private StationMissionSession session;
        private AscentMission flight;
        private DockingMission docking;
        private FlightScenePresenter presentation;
        private OrbiterView view;
        private float previousDelta;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousFocus;
        private readonly List<Object> temporary = new();

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
            session.enabled = false;
            flight = session.GetComponent<AscentMission>();
            docking = session.GetComponent<DockingMission>();
            presentation = session.GetComponent<FlightScenePresenter>();
            view = presentation.FlightWorld.GetComponent<OrbiterView>();
        }

        [TearDown]
        public void RestoreSettings()
        {
            Time.captureDeltaTime = previousDelta;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousFocus;
            foreach (var item in temporary) if (item != null) Object.Destroy(item);
            temporary.Clear();
        }

        [UnityTest]
        public IEnumerator OrbiterUsesPortOriginWithCompactDecreasingLodsAndVisibleSurfaceMaterials()
        {
            var visual = NamedChild(view.Target, "Realistic Orbiter - Blender");
            Assert.That(view.Target.GetComponentsInChildren<LODGroup>(true).Length, Is.EqualTo(1));
            var lodGroup = visual.GetComponent<LODGroup>();
            Assert.That(lodGroup, Is.Not.Null);
            var lods = lodGroup.GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            long previousTriangles = long.MaxValue;
            float previousTransition = 1;
            long allTriangles = 0;
            var limits = new long[] { 100000, 25000, 8000 };
            for (int index = 0; index < lods.Length; index++)
            {
                Assert.That(lods[index].renderers.Length, Is.InRange(1, 8));
                var meshes = Meshes(lods[index].renderers).ToArray();
                long triangles = meshes.Sum(Triangles);
                Assert.That(triangles, Is.GreaterThan(0).And.LessThan(previousTriangles));
                Assert.That(triangles, Is.LessThanOrEqualTo(limits[index]), "Orbiter LOD" + index);
                Assert.That(meshes.All(mesh => !mesh.isReadable), Is.True, "Imported visual meshes should release their CPU geometry copies.");
                Assert.That(lods[index].screenRelativeTransitionHeight, Is.GreaterThan(0).And.LessThan(previousTransition));
                previousTriangles = triangles; previousTransition = lods[index].screenRelativeTransitionHeight; allTriangles += triangles;
            }
            Assert.That(allTriangles, Is.LessThan(150000));
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "The art must not add another physical docking solver.");
            var port = NamedChild(visual, "Anchor_DockingPort");
            var center = NamedChild(visual, "Anchor_DockingCenter");
            var normal = NamedChild(visual, "Anchor_PortNormal");
            Assert.That(view.Target.InverseTransformPoint(port.position).magnitude, Is.LessThan(.002f));
            Assert.That(Vector3.Distance(port.position, center.position), Is.LessThan(.002f));
            Assert.That(Vector3.Angle(view.Target.InverseTransformDirection(normal.position - port.position), Vector3.back), Is.LessThan(.1f));
            Assert.That(Vector3.Distance(normal.position, port.position), Is.EqualTo(1).Within(.002f));
            var bounds = LocalBounds(view.Target, lods[0].renderers);
            Assert.That(bounds.size.x, Is.InRange(15, 18), "Solar wings should keep the collision envelope's approximate span.");
            Assert.That(bounds.min.z, Is.InRange(-.3f, 0), "The interface should meet the existing port plane.");
            Assert.That(bounds.max.z, Is.InRange(8, 10));
            var lamp = CaptureLamp();
            Assert.That(lods.All(lod => !lod.renderers.Contains(lamp)), Is.True, "The capture-state light must stay independent of automatic LOD visibility.");
            var materials = Materials(visual.GetComponentsInChildren<Renderer>(true));
            AssertSupported(materials);
            Assert.That(materials.Any(material => material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null), Is.True);
            var textures = Textures(materials);
            Assert.That(textures.Count, Is.InRange(1, 8));
            Assert.That(textures.Sum(texture => (long)texture.width * texture.height), Is.LessThanOrEqualTo(8L * 1024 * 1024));

            StartAt(new Vector3(0, 0, -35));
            yield return null;
            Assert.That(view.Target.gameObject.activeInHierarchy, Is.True);
            Assert.That(lamp.enabled, Is.False);
            yield return Capture("orbiter-overall", view.Target.TransformPoint(new Vector3(13, 7, -17)), view.Target.TransformPoint(new Vector3(0, 0, 4)), true, 48);
            yield return Capture("orbiter-port", view.Target.TransformPoint(new Vector3(1.8f, 1.1f, -4.2f)), port.position + view.Target.forward * .45f, true, 55);
            yield return Capture("orbiter-cockpit-far", default, default, false);
        }

        [UnityTest]
        public IEnumerator NewOrbiterPreservesApproachCaptureCollisionAndRetryWithoutMovingHeadset()
        {
            StartAt(new Vector3(0, 0, -35));
            yield return null;
            var camera = session.Player.Camera;
            var eye = camera.transform.position;
            var attitude = camera.transform.rotation;
            var lamp = CaptureLamp();
            Drive(DockCommand.Forward, 2);
            flight.Tick(35);
            Drive(DockCommand.Brake, 2);
            Drive(DockCommand.Forward, .55f);
            docking.ReleaseControls();
            bool sawAssist = false, sawCapture = false, capturedNear = false;
            for (int step = 0; step < 1800 && docking.State != DockingState.Docked && docking.State != DockingState.Failed; step++)
            {
                flight.Tick(.1f);
                if (docking.CanAssist && !sawAssist)
                {
                    sawAssist = true;
                    yield return null;
                    Assert.That(lamp.enabled, Is.True);
                    docking.ToggleAssistance(); yield return null;
                    Assert.That(lamp.enabled, Is.False);
                    docking.ToggleAssistance(); yield return null;
                    Assert.That(lamp.enabled, Is.True);
                }
                if (!capturedNear && docking.Distance < 3 && docking.State == DockingState.Approaching)
                {
                    capturedNear = true;
                    yield return null;
                    yield return Capture("orbiter-cockpit-near", default, default, false);
                }
                if (docking.State == DockingState.Capturing && !sawCapture)
                {
                    sawCapture = true; yield return null;
                    Assert.That(lamp.enabled, Is.True);
                    Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Docking));
                }
            }
            yield return null;
            Assert.That(sawAssist && sawCapture && capturedNear, Is.True);
            Assert.That(docking.State, Is.EqualTo(DockingState.Docked), flight.Failure + " " + docking.Position);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Docked));
            Assert.That(docking.Distance, Is.EqualTo(.22f).Within(.002f));
            Assert.That(lamp.enabled, Is.True);
            Assert.That(Vector3.Distance(camera.transform.position, eye), Is.LessThan(.02f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, attitude), Is.LessThan(.1f));

            session.RetryMission(); yield return null; yield return null;
            Assert.That(presentation.GroundRoot.activeInHierarchy, Is.True);
            Assert.That(view.Target.gameObject.activeInHierarchy, Is.False);
            StartAt(new Vector3(0, 0, -1), Vector3.forward * 20);
            yield return null;
            flight.Tick(.1f); yield return null;
            Assert.That(flight.Failure, Is.EqualTo(AscentFailure.DockingCollision));
            Assert.That(docking.State, Is.EqualTo(DockingState.Failed));
            Assert.That(view.ExplosionVisible, Is.True);
            Assert.That(lamp.enabled, Is.False);
            session.RetryMission(); yield return null; yield return null;
            Assert.That(presentation.GroundRoot.activeInHierarchy, Is.True);
            Assert.That(flight.IsLocked, Is.False);
            // The dormant flight hierarchy applies its reset state when the next boarding enables it.
            Board(); yield return null;
            Assert.That(view.ExplosionVisible, Is.False);
            Assert.That(lamp.enabled, Is.False);
            Assert.That(view.Target.GetComponentsInChildren<Transform>(true).Count(child => child.name == "Realistic Orbiter - Blender"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DetailedSurfaceSharesMeshesAndKeepsEvacuationRouteAndFlightProxyClear()
        {
            var terrain = session.GetComponent<LunarTerrainLayout>();
            var layout = session.GetComponent<LunarViewLayout>();
            var groundRocks = NamedChild(presentation.GroundRoot.transform, "Realistic Lunar Rocks");
            var flightRocks = NamedChild(layout.OutsideWorld, "Flight Realistic Lunar Rocks");
            var names = new[] { "Walkable crater terrain", "Distant crater ridges", "Continuous Global Surface" };
            var vertexBudgets = new[] { 31500, 14000, 195000 };
            for (int index = 0; index < names.Length; index++)
            {
                var ground = NamedChild(terrain.GroundVisual.transform, names[index]);
                var proxy = NamedChild(terrain.FlightVisual.transform, names[index]);
                var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(proxy.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(proxy.GetComponent<Renderer>().sharedMaterial, Is.SameAs(ground.GetComponent<Renderer>().sharedMaterial));
                Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(vertexBudgets[index]), names[index]);
                if (index == 0) Assert.That(terrain.WalkableSurface.sharedMesh, Is.SameAs(mesh));
            }
            Assert.That(terrain.FlightVisual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(flightRocks.GetComponentsInChildren<Collider>(true), Is.Empty);
            var groundMeshes = Meshes(groundRocks.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            var flightMeshes = Meshes(flightRocks.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            Assert.That(flightMeshes, Is.EquivalentTo(groundMeshes));
            Assert.That(groundMeshes.Length, Is.LessThanOrEqualTo(12));
            Assert.That(groundMeshes.Sum(mesh => mesh.vertexCount), Is.LessThanOrEqualTo(3000));
            var groups = groundRocks.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length, Is.InRange(100, 300));
            Assert.That(flightRocks.GetComponentsInChildren<LODGroup>(true).Length, Is.EqualTo(groups.Length));
            Assert.That(groundRocks.GetComponentsInChildren<Collider>(true).Length, Is.InRange(1, 10));
            foreach (var group in groups)
            {
                var levels = group.GetLODs();
                Assert.That(levels.Length, Is.EqualTo(2));
                Assert.That(Meshes(levels[0].renderers).Sum(Triangles), Is.LessThanOrEqualTo(80));
                Assert.That(Meshes(levels[1].renderers).Sum(Triangles), Is.LessThan(Meshes(levels[0].renderers).Sum(Triangles)));
                Assert.That(levels[1].screenRelativeTransitionHeight, Is.LessThan(levels[0].screenRelativeTransitionHeight));
            }
            var groundMaterials = Materials(groundRocks.GetComponentsInChildren<Renderer>(true));
            Assert.That(Materials(flightRocks.GetComponentsInChildren<Renderer>(true)), Is.EquivalentTo(groundMaterials));
            var surfaceMaterials = Materials(terrain.GroundVisual.GetComponentsInChildren<Renderer>(true));
            Assert.That(surfaceMaterials.Count, Is.EqualTo(1));
            AssertSupported(surfaceMaterials.Concat(groundMaterials));
            var surfaceMaterial = surfaceMaterials.Single();
            Assert.That(surfaceMaterial.shader.name, Is.EqualTo("LunarEscape/Detailed Continuous Moon"));
            foreach (string property in new[] { "_DetailAlbedo", "_DetailNormal" })
            {
                Assert.That(surfaceMaterial.HasProperty(property), Is.True, "The imported surface shader is missing " + property);
                Assert.That(surfaceMaterial.GetTexture(property), Is.Not.Null, "The saved lunar surface must bind " + property);
            }
            Assert.That(surfaceMaterial.GetFloat("_DetailScale"), Is.GreaterThan(0));
            Assert.That(surfaceMaterial.GetFloat("_NormalStrength"), Is.GreaterThan(0));
            Assert.That(groundMaterials.Count, Is.EqualTo(1));
            Assert.That(groundMaterials.Single().GetTexture("_BaseMap"), Is.SameAs(surfaceMaterial.GetTexture("_DetailAlbedo")));
            Assert.That(groundMaterials.Single().GetTexture("_BumpMap"), Is.SameAs(surfaceMaterial.GetTexture("_DetailNormal")));
            var textures = Textures(surfaceMaterials.Concat(groundMaterials));
            Assert.That(textures.Sum(texture => (long)texture.width * texture.height), Is.LessThanOrEqualTo(12L * 1024 * 1024));

            session.BeginMission(); session.Advance(session.Mission.Config.RepairWindowSeconds);
            Physics.SyncTransforms();
            // Always produce surface previews before a physical-route diagnostic can stop the test.
            yield return Capture("surface-base-overview", new Vector3(23, 3.5f, -17), new Vector3(3, 1.2f, 2), true, 65);
            // Outside the flat route, establish both preview heights from the baked collision surface.
            Assert.That(terrain.WalkableSurface.Raycast(new Ray(new Vector3(28, 100, -8), Vector3.down), out var previewEyeHit, 200), Is.True);
            Assert.That(terrain.WalkableSurface.Raycast(new Ray(new Vector3(30, 100, -11), Vector3.down), out var previewAimHit, 200), Is.True);
            yield return Capture("surface-regolith-close", previewEyeHit.point + Vector3.up * 1.3f,
                previewAimHit.point + Vector3.up * .1f, true, 70);
            // Follow the existing marked lane; then approach the hatch from beside the intentional ladder collider.
            var route = new[] { new Vector3(8.3f, 0, 1.7f), new Vector3(38, 0, 1.7f), new Vector3(38, 0, -4.3f),
                new Vector3(46.8f, 0, -4.3f), new Vector3(46.8f, 0, -6), new Vector3(48, 0, -6) };
            for (int segment = 1; segment < route.Length; segment++)
            {
                var from = route[segment - 1]; var vector = route[segment] - from;
                var hits = Physics.CapsuleCastAll(from + Vector3.up * .35f, from + Vector3.up * 1.6f, .18f,
                    vector.normalized, vector.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Assert.That(hits, Is.Empty, "Evacuation route obstructed: " + string.Join(", ", hits.Select(hit => hit.collider.name)));
                for (float distance = 0; distance <= vector.magnitude; distance += .5f)
                {
                    var point = from + vector.normalized * distance;
                    Assert.That(terrain.WalkableSurface.Raycast(new Ray(point + Vector3.up * 1, Vector3.down), out var hit, 2), Is.True,
                        "Missing route surface at " + point.ToString("F3"));
                    string hitDetail = hit.normal.y < .9f || Mathf.Abs(hit.point.y) > .08f ? TerrainHitDetail(terrain.WalkableSurface, point, hit) : point.ToString("F3");
                    Assert.That(hit.point.y, Is.InRange(-.08f, .08f), hitDetail);
                    Assert.That(Vector3.Angle(hit.normal, Vector3.up), Is.LessThanOrEqualTo(session.Exit.PlayerBody.slopeLimit + .1f),
                        "Actual route contact must be walkable with the player's slope setting. " + hitDetail);
                    if (Mathf.Abs(point.z + 6) < .001f)
                    {
                        // The original boarding point is exactly on the southern 1 m mesh edge. PhysX may return
                        // its OUTER slope (also present before the art upgrade). Check strict flatness 1 cm inside
                        // the route while retaining the exact-edge height, player slope and full-body capsule checks.
                        var inside = point + Vector3.forward * .01f;
                        Assert.That(terrain.WalkableSurface.Raycast(new Ray(inside + Vector3.up, Vector3.down), out var innerHit, 2), Is.True);
                        Assert.That(terrain.WalkableSurface.Raycast(new Ray(point + Vector3.back * .01f + Vector3.up, Vector3.down), out var outerHit, 2), Is.True);
                        Debug.Log("BOARDING_MESH_EDGE " + hitDetail + "; inside=" + innerHit.point.ToString("F3") +
                            ", n=" + innerHit.normal.ToString("F4") + ", triangle=" + innerHit.triangleIndex +
                            "; outside=" + outerHit.point.ToString("F3") + ", n=" + outerHit.normal.ToString("F4") + ", triangle=" + outerHit.triangleIndex);
                        Assert.That(innerHit.point.y, Is.InRange(-.01f, .01f), hitDetail);
                        Assert.That(innerHit.normal.y, Is.GreaterThan(.9f), hitDetail);
                    }
                    else Assert.That(hit.normal.y, Is.GreaterThan(.9f), hitDetail);
                }
            }
            foreach (var foot in layout.Lander.GetComponentsInChildren<Transform>(true).Where(child => child.name.StartsWith("Anchor_Foot")))
            {
                Assert.That(terrain.WalkableSurface.Raycast(new Ray(foot.position + Vector3.up * 5, Vector3.down), out var hit, 10), Is.True);
                Assert.That(hit.point.y, Is.InRange(-.08f, .08f), "Landing pad height changed under " + foot.name);
            }
            session.RetryMission(); Board(); yield return null;
            Assert.That(groundRocks.gameObject.activeInHierarchy, Is.False);
            Assert.That(flightRocks.gameObject.activeInHierarchy, Is.True);
            Launch();
            foreach (float time in new[] { 3f, 25, 60, 85 })
            {
                flight.Tick(Mathf.Max(0, time - flight.AirborneSeconds)); yield return null;
                Assert.That(flightRocks.gameObject.activeInHierarchy, Is.True);
                Assert.That(terrain.FlightVisual.activeInHierarchy, Is.True);
            }
            session.RetryMission(); yield return null; yield return null;
            Assert.That(groundRocks.gameObject.activeInHierarchy, Is.True);
            Assert.That(flightRocks.gameObject.activeInHierarchy, Is.False);
        }

        private void StartAt(Vector3 position, Vector3 velocity = default)
        {
            var config = Object.Instantiate(docking.Config); temporary.Add(config);
            config.ConfigureStart(position, velocity, Vector3.zero); docking.Configure(flight, config);
            Board(); Launch();
            flight.Tick(flight.OrbitAscentSeconds - flight.AirborneSeconds); flight.Circularize();
            flight.Tick(flight.Config.CircularizationSeconds);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Rendezvous));
        }
        private void Board()
        {
            session.BeginMission(); session.Advance(session.Mission.Config.RepairWindowSeconds);
            session.Advance(session.Mission.RemainingSeconds - 60);
            var body = session.Exit.PlayerBody; var center = body.transform.TransformPoint(body.center);
            body.enabled = false; body.transform.position += new Vector3(48 - center.x, 0, -6 - center.z); body.enabled = true;
            Physics.SyncTransforms();
            var hatch = Object.FindAnyObjectByType<HatchBoardingController>();
            Assert.That(hatch.CanBoard, Is.True); hatch.RequestBoarding(); session.Advance(0);
            flight.Tick(flight.Config.FadeOutSeconds + flight.Config.BlackSeconds + flight.Config.FadeInSeconds + .001f);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Startup));
        }
        private void Launch()
        {
            flight.PowerOn(); flight.StartNavigation(); flight.Tick(flight.Config.NavigationSeconds);
            flight.PrepareEngine(); flight.Tick(flight.Config.EngineSeconds);
            flight.Ignite(); flight.Tick(flight.Config.IgnitionSeconds);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Ascent));
        }
        private void Drive(DockCommand command, float duration)
        { docking.SetCommand(command, true); flight.Tick(duration); docking.SetCommand(command, false); }
        private Renderer CaptureLamp() => NamedChild(view.Target, "Capture Indicator").GetComponent<Renderer>();
        private static Transform NamedChild(Transform root, string name)
        {
            var matches = root.GetComponentsInChildren<Transform>(true).Where(child => child.name == name).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), root.name + " must have exactly one " + name);
            return matches[0];
        }
        private static IEnumerable<Mesh> Meshes(IEnumerable<Renderer> renderers) => renderers.Where(renderer => renderer != null)
            .Select(renderer => renderer.GetComponent<MeshFilter>()?.sharedMesh).Where(mesh => mesh != null);
        private static HashSet<Material> Materials(IEnumerable<Renderer> renderers) => renderers
            .SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).ToHashSet();
        private static HashSet<Texture> Textures(IEnumerable<Material> materials) => materials
            .SelectMany(material => material.GetTexturePropertyNames().Select(material.GetTexture)).Where(texture => texture != null).ToHashSet();
        private static void AssertSupported(IEnumerable<Material> materials) => Assert.That(materials.All(material =>
            material.shader != null && material.shader.isSupported && material.shader.name != "Hidden/InternalErrorShader"), Is.True);
        private static long Triangles(Mesh mesh)
        {
            long count = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                if (mesh.GetTopology(submesh) == MeshTopology.Triangles) count += (long)mesh.GetIndexCount(submesh) / 3;
            return count;
        }
        private static string TerrainHitDetail(MeshCollider collider, Vector3 sample, RaycastHit hit)
        {
            var mesh = collider.sharedMesh; var vertices = mesh.vertices; var indices = mesh.triangles;
            int start = hit.triangleIndex * 3;
            if (start < 0 || start + 2 >= indices.Length) return $"point={sample:F3}; mesh={mesh.name}; triangle={hit.triangleIndex}; normal={hit.normal:F4}";
            var a = collider.transform.TransformPoint(vertices[indices[start]]);
            var b = collider.transform.TransformPoint(vertices[indices[start + 1]]);
            var c = collider.transform.TransformPoint(vertices[indices[start + 2]]);
            var faceNormal = Vector3.Cross(b - a, c - a).normalized;
            return $"point={sample:F3}; hit={hit.point:F3}; normal={hit.normal:F4}; collider={collider.name}; mesh={mesh.name}; " +
                $"triangle={hit.triangleIndex} [{indices[start]},{indices[start + 1]},{indices[start + 2]}]; " +
                $"world vertices={a:F4}/{b:F4}/{c:F4}; face normal={faceNormal:F4}; " +
                $"collider position={collider.transform.position:F3}, rotation={collider.transform.eulerAngles:F3}, scale={collider.transform.lossyScale:F3}";
        }
        private static Bounds LocalBounds(Transform root, IEnumerable<Renderer> renderers)
        {
            var bounds = new Bounds(); bool initialized = false;
            foreach (var renderer in renderers)
            {
                var local = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = root.InverseTransformPoint(renderer.transform.TransformPoint(p));
                    if (!initialized) { bounds = new Bounds(p, Vector3.zero); initialized = true; } else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        private static IEnumerator Capture(string name, Vector3 eye, Vector3 target, bool artPreview, float fieldOfView = 70)
        {
            var mainCamera = Camera.main;
            var obj = new GameObject("Orbiter and surface verification camera");
            var camera = obj.AddComponent<Camera>(); camera.CopyFrom(mainCamera); camera.enabled = false;
            if (artPreview) { camera.fieldOfView = fieldOfView; camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye)); }
            else camera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
            var canvases = new Dictionary<Canvas, bool>(); var renderers = new Dictionary<Renderer, bool>();
            var rt = new RenderTexture(1600, 1100, 24); var pixels = new Texture2D(1600, 1100, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                if (artPreview)
                {
                    foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    { canvases[canvas] = canvas.enabled; canvas.enabled = false; }
                    var player = Object.FindAnyObjectByType<StationMissionSession>().Player;
                    var avatar = player.GetComponentInChildren<TrackedCrewSuit>(true);
                    if (avatar != null && avatar.Suit != null)
                        foreach (var renderer in avatar.Suit.GetComponentsInChildren<Renderer>(true)) renderers[renderer] = renderer.enabled;
                    var pouch = Object.FindAnyObjectByType<CargoPackZone>();
                    if (pouch != null)
                        foreach (var renderer in pouch.GetComponentsInChildren<Renderer>(true)) renderers[renderer] = renderer.enabled;
                    foreach (var renderer in renderers.Keys) renderer.enabled = false;
                }
                rt.Create(); camera.targetTexture = rt; camera.enabled = true;
                for (int frame = 0; frame < 8; frame++)
                {
                    if (!artPreview) camera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
                    yield return null;
                }
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 1600, 1100), 0, 0); pixels.Apply();
                var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Previews")); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                foreach (var state in canvases) if (state.Key != null) state.Key.enabled = state.Value;
                foreach (var state in renderers) if (state.Key != null) state.Key.enabled = state.Value;
                RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
                Object.Destroy(pixels); Object.Destroy(rt); Object.Destroy(obj);
            }
        }
    }
}
