using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LunarEscape.Tests
{
    public sealed class ArtemisTimelineTests
    {
        private LaunchCinematic film;

        [UnitySetUp] public IEnumerator LoadDeparture()
        {
            yield return SceneManager.LoadSceneAsync(ArtemisPrologue.SceneName);
            yield return null;
            film = Object.FindAnyObjectByType<LaunchCinematic>();
            Assert.That(film, Is.Not.Null);
        }

        [UnityTest] public IEnumerator ThreeCrewWalkThroughTheHatchAndTheVehicleShedsSeparateAssemblies()
        {
            film.Evaluate(1);
            Assert.That(film.Actors.Length, Is.EqualTo(3));
            Assert.That(film.Actors.All(a => !a.Boarded), Is.True);
            var start = film.Actors.Select(a => a.transform.position).ToArray();
            var stage = film.Rocket;
            var mounted = film.RocketView.position;
            film.Evaluate(8);
            for(int i=0; i<3; i++) Assert.That(Vector3.Distance(start[i],film.Actors[i].transform.position), Is.GreaterThan(3));
            film.Evaluate(12);
            Assert.That(film.Actors.Count(a => a.Boarded), Is.EqualTo(1));
            film.Evaluate(17);
            Assert.That(film.Actors.All(a => a.Boarded && a.Seated.gameObject.activeInHierarchy), Is.True);
            Assert.That(Vector3.Distance(film.CrewView.position, stage.Seat.position), Is.LessThan(.02f));
            Assert.That(stage.HatchDoor.activeInHierarchy, Is.True);
            film.SetCrewView(true);
            var commander = film.Actors[2].Seated;
            Assert.That(commander.Find("Helmet").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(commander.Find("Suit Body").GetComponent<Renderer>().enabled, Is.False);
            Assert.That(commander.Find("First-person lap").GetComponent<Renderer>().enabled, Is.True);
            film.SetCrewView(false);
            Assert.That(commander.Find("Suit Body").GetComponent<Renderer>().enabled, Is.True);
            film.Evaluate(24);
            Assert.That(stage.transform.position.y, Is.GreaterThan(5));
            Assert.That(stage.Core.parent, Is.SameAs(stage.transform));
            film.Evaluate(33);
            Assert.That(stage.LeftBooster.parent, Is.Not.SameAs(stage.transform));
            Assert.That(stage.RightBooster.parent, Is.Not.SameAs(stage.transform));
            Assert.That(Vector3.Distance(stage.LeftBooster.position,stage.RightBooster.position),Is.GreaterThan(20));
            Assert.That(stage.Core.parent, Is.SameAs(stage.transform));
            film.Evaluate(39);
            Assert.That(stage.Core.parent, Is.Not.SameAs(stage.transform));
            Assert.That(stage.Core.gameObject.activeInHierarchy, Is.True);
            Assert.That(stage.UpperStage.parent, Is.SameAs(stage.transform));
            film.Evaluate(65);
            Assert.That(stage.UpperStage.parent, Is.Not.SameAs(stage.transform));
            Assert.That(stage.Orion.gameObject.activeInHierarchy, Is.True);
            film.Evaluate(1);
            Assert.That(Vector3.Distance(film.RocketView.position,mounted), Is.LessThan(.01f));
            Assert.That(stage.LeftBooster.parent, Is.SameAs(stage.transform));
            Assert.That(stage.Core.parent, Is.SameAs(stage.transform));
            Assert.That(stage.HatchDoor.activeSelf, Is.False);
            for (int i=0; i<3; i++) Assert.That(Vector3.Distance(start[i],film.Actors[i].transform.position), Is.LessThan(.01f));
            yield return null;
        }

        [UnityTest] public IEnumerator RotatingWorldsChangeApparentSizeAndTheArrivalTravelsAroundTheMoon()
        {
            film.Evaluate(48);
            Assert.That(film.Phase, Is.EqualTo(LaunchPhase.EarthOrbit));
            float earthAtStart = AngularRadius(film.Earth), moonAtStart = AngularRadius(film.Moon);
            var earthStart = film.Earth.rotation; var moonStart = film.Moon.rotation;
            Assert.That(earthAtStart, Is.GreaterThan(18)); Assert.That(moonAtStart, Is.LessThan(1));
            film.Evaluate(61);
            Assert.That(film.Phase, Is.EqualTo(LaunchPhase.LunarTransfer));
            float earthOnCoast = AngularRadius(film.Earth), moonOnCoast = AngularRadius(film.Moon);
            Assert.That(earthOnCoast, Is.LessThan(earthAtStart));
            Assert.That(moonOnCoast, Is.GreaterThan(moonAtStart));
            float worldSeparation = Vector3.Angle(film.Earth.position - film.RocketView.position,
                film.Moon.position - film.RocketView.position);
            Assert.That(worldSeparation, Is.GreaterThan(earthOnCoast + moonOnCoast));
            film.Evaluate(70);
            var lunarBearing = film.CrewView.InverseTransformDirection((film.Moon.position-film.CrewView.position).normalized);
            film.Evaluate(78);
            Assert.That(film.Phase, Is.EqualTo(LaunchPhase.LunarOrbit));
            Assert.That(AngularRadius(film.Earth), Is.LessThan(earthOnCoast));
            Assert.That(AngularRadius(film.Moon), Is.GreaterThan(moonOnCoast));
            Assert.That(Quaternion.Angle(earthStart,film.Earth.rotation), Is.GreaterThan(20));
            Assert.That(Quaternion.Angle(moonStart,film.Moon.rotation), Is.GreaterThan(20));
            var arrival = film.CrewView.InverseTransformDirection((film.Moon.position-film.CrewView.position).normalized);
            Assert.That(Vector3.Angle(lunarBearing,arrival), Is.GreaterThan(8));
            foreach (var world in new[]{film.Earth,film.Moon})
            {
                Assert.That(world.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(world.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                Assert.That(Vector3.Dot(film.CrewView.forward,(world.position-film.CrewView.position).normalized), Is.GreaterThan(.85f));
            }
            yield return null;
        }

        private float AngularRadius(Transform world)
        {
            float radius = world.localScale.x * .5f;
            return Mathf.Asin(radius / Vector3.Distance(film.CrewView.position,world.position)) * Mathf.Rad2Deg;
        }
    }
}
