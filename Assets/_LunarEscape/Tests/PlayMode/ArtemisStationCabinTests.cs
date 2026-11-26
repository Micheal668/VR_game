using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LunarEscape.Tests
{
    public class ArtemisStationCabinTests
    {
        [UnityTest]
        public IEnumerator LandingCockpitSharesTheStationShellSeatsMaterialsAndPilotSeat()
        {
            yield return SceneManager.LoadSceneAsync(ArtemisLanding.SceneName);
            yield return null;
            var instruments = Object.FindAnyObjectByType<ArtemisLandingCabinInstruments>(FindObjectsInactive.Include);
            Assert.That(instruments, Is.Not.Null);
            Assert.That(instruments.gameObject.activeSelf, Is.False);
            var landingEye = instruments.StationShell.InverseTransformPoint(instruments.PilotEye.position);
            var landingGaze = instruments.StationShell.InverseTransformDirection(instruments.PilotEye.forward);
            Assert.That(landingEye.y, Is.EqualTo(1.65f).Within(.001f));
            var parts = instruments.StationShell.GetComponentsInChildren<MeshFilter>(true)
                .ToDictionary(mesh => mesh.name, mesh => (mesh.sharedMesh, mesh.GetComponent<Renderer>().sharedMaterials));
            Dictionary<string, Vector3> surfaces = new();
            foreach (var screen in instruments.GetComponentsInChildren<Canvas>(true))
                surfaces.Add(screen.name, instruments.StationShell.InverseTransformPoint(screen.transform.position));
            // Load the authored station scene and compare actual native meshes and materials by identity.
            const string station = "11_LunarStation_LifeSupport";
            yield return SceneManager.LoadSceneAsync(station, LoadSceneMode.Additive);
            yield return null;
            var layout = Object.FindAnyObjectByType<LifeSupportSceneLayout>();
            var cabin = layout.Cockpit.transform;
            var originals = cabin.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(originals.Length, Is.EqualTo(parts.Count));
            foreach (var original in originals)
            {
                Assert.That(parts[original.name].sharedMesh, Is.SameAs(original.sharedMesh), original.name);
                CollectionAssert.AreEqual(original.GetComponent<Renderer>().sharedMaterials,
                    parts[original.name].sharedMaterials, original.name);
            }
            var seat = Object.FindAnyObjectByType<FlightScenePresenter>().Seat;
            var anchor = cabin.InverseTransformPoint(seat.position);
            Assert.That(landingEye.x, Is.EqualTo(anchor.x).Within(.001f));
            Assert.That(landingEye.z, Is.EqualTo(anchor.z).Within(.001f));
            Assert.That(Vector3.Angle(landingGaze, cabin.InverseTransformDirection(seat.forward)), Is.LessThan(.01f));
            foreach (var screen in new[] { layout.OperationsScreen, layout.NavigationScreen, layout.ResourceScreen })
                Assert.That(Vector3.Distance(surfaces[screen.name], cabin.InverseTransformPoint(screen.transform.position)), Is.LessThan(.002f));
            yield return SceneManager.UnloadSceneAsync(station);
        }
    }
}
