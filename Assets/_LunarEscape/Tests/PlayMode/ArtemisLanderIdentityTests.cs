using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LunarEscape.Tests
{
    public sealed class ArtemisLanderIdentityTests
    {
        [UnityTest]
        public IEnumerator FlyableExteriorSharesBothStationDaysMeshesMaterialsAndLandmarks()
        {
            yield return SceneManager.LoadSceneAsync(ArtemisLanding.SceneName);
            var model = Object.FindAnyObjectByType<ArtemisLanderVisual>(FindObjectsInactive.Include);
            Assert.That(model, Is.Not.Null);
            var exterior = model.Exterior.transform;
            var geometry = exterior.Find("Geometry");
            var parts = geometry.GetComponentsInChildren<MeshRenderer>(true).ToDictionary(r => r.name);
            Assert.That(parts.Count, Is.EqualTo(9));
            Assert.That(model.Exterior.GetComponentsInChildren<Collider>(true), Is.Empty);
            CollectionAssert.AreEquivalent(new[] { "Ascentstructure", "CabinPanels", "Hardware" }, model.AscentStage.Select(r => r.name));
            CollectionAssert.AreEquivalent(new[] { "Golddescentstage", "Frontlandinggear", "Rearlandinggear",
                "Leftlandinggear", "Rightlandinggear", "EVAAccess" }, model.DescentStage.Select(r => r.name));
            var originalSurface = parts.Values.SelectMany(r => r.sharedMaterials).First(m => m.name == "OriginalStructure");
            foreach (string map in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                Assert.That(originalSurface.GetTexture(map), Is.Not.Null, map);

            foreach (string day in new[] { "12_LunarStation_DayOne", "11_LunarStation_LifeSupport" })
            {
                yield return SceneManager.LoadSceneAsync(day, LoadSceneMode.Additive);
                var original = SceneManager.GetSceneByName(day).GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Single(t => t.name == "Apollo Exterior - Blender" && t.parent != null && t.parent.name == "Apollo Lander Exterior");
                Assert.That(original, Is.Not.Null, day);
                var originals = original.GetComponentsInChildren<MeshRenderer>(true);
                CollectionAssert.AreEquivalent(parts.Keys, originals.Select(r => r.name), day);
                foreach (var renderer in originals)
                {
                    var instance = parts[renderer.name];
                    var sourceMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    // The scene builder compares every native mesh reference in both authored base scenes.
                    // In a running base Unity can replace the stationary part's mesh with a static batch.
                    if (renderer.isPartOfStaticBatch)
                    {
                        Assert.That(instance.isPartOfStaticBatch, Is.False, "The flying lander must be movable.");
                        Assert.That(sourceMesh.subMeshCount, Is.GreaterThan(renderer.subMeshStartIndex), renderer.name);
                    }
                    else Assert.That(instance.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(sourceMesh), renderer.name);
                    CollectionAssert.AreEqual(renderer.sharedMaterials, instance.sharedMaterials, renderer.name);
                    Assert.That(instance.enabled, Is.EqualTo(renderer.enabled), renderer.name);
                }
                var authoredGeometry = original.Find("Geometry");
                CompareTransform(geometry, authoredGeometry);
                Assert.That(geometry.childCount, Is.EqualTo(authoredGeometry.childCount));
                foreach (Transform child in authoredGeometry)
                    CompareTransform(geometry.Find(child.name), child);
                Assert.That((exterior.lossyScale - original.lossyScale).magnitude, Is.LessThan(.001f));
                yield return SceneManager.UnloadSceneAsync(day);
            }

            // Dimensions of the whole station mesh include its antennas and the full footpad span.
            var bounds = parts.Values.First().bounds;
            foreach (var part in parts.Values) bounds.Encapsulate(part.bounds);
            Assert.That(bounds.size.y, Is.EqualTo(7f).Within(.02f));
            Assert.That(bounds.size.x, Is.EqualTo(11.245f).Within(.03f));
            Assert.That(bounds.size.z, Is.EqualTo(11.332f).Within(.03f));
            Assert.That(bounds.min.y, Is.EqualTo(model.transform.position.y).Within(.015f));

            // The collared docking surface sits below the antenna tip. The station windows
            // and ladder face forward in the pilot's frame after the root yaw adjustment.
            var craft = model.transform;
            Assert.That(craft.InverseTransformPoint(model.DockingPort.position).y,
                Is.EqualTo(EarthReturnPresenter.AscentPortHeight).Within(.001f));
            Assert.That(Vector3.Angle(model.DockingPort.up, craft.up), Is.LessThan(.02f));
            Assert.That(Vector3.Angle(model.Nozzle.forward, -craft.up), Is.LessThan(.02f));
            Assert.That(craft.InverseTransformPoint(model.Nozzle.position).magnitude, Is.LessThan(.05f));
            var orion = model.OrbiterDockingPort.parent;
            var face = orion.Find("Docking contact face");
            Assert.That(Vector3.Distance(model.OrbiterDockingPort.position, face.TransformPoint(Vector3.up)), Is.LessThan(.001f));
            Assert.That(Vector3.Angle(model.OrbiterDockingPort.up, face.up), Is.LessThan(.02f));
            var front = geometry.Find("Anchor_Front").position - geometry.Find("Anchor_Origin").position;
            Assert.That(Vector3.Angle(model.PilotEye.forward, front), Is.LessThan(.02f));
            var eye = craft.InverseTransformPoint(model.PilotEye.position);
            Assert.That(Vector3.Distance(eye, new Vector3(-1.1179f, 4.7468f, 2.2557f)), Is.LessThan(.005f));
            Assert.That(model.Feet.Length, Is.EqualTo(4));
            for (int i = 0; i < model.Feet.Length; i++)
            {
                var anchor = model.FootAnchors[i];
                Assert.That(anchor, Is.SameAs(geometry.Find(anchor.name)));
                var foot = craft.InverseTransformPoint(model.Feet[i].position);
                var axis = craft.InverseTransformPoint(anchor.position);
                Assert.That(Vector3.ProjectOnPlane(foot - axis, Vector3.up).magnitude, Is.LessThan(.11f));
                Assert.That(foot.y, Is.InRange(.016f, .021f));
            }
        }

        private static void CompareTransform(Transform instance, Transform original)
        {
            Assert.That(instance, Is.Not.Null, original.name);
            Assert.That(Vector3.Distance(instance.localPosition, original.localPosition), Is.LessThan(.0001f), original.name);
            Assert.That(Quaternion.Angle(instance.localRotation, original.localRotation), Is.LessThan(.02f), original.name);
            Assert.That(Vector3.Distance(instance.localScale, original.localScale), Is.LessThan(.0001f), original.name);
        }
    }
}
