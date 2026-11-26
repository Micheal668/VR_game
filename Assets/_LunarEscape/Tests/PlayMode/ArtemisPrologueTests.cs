using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace LunarEscape.Tests
{
    public sealed class ArtemisPrologueTests
    {
        private ArtemisPrologue film;
        private XRHMD hmd;
        private HTCViveControllerProfile.ViveController wand;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode inputMode;

        [UnitySetUp] public IEnumerator Load()
        {
            background = InputSystem.settings.backgroundBehavior;
            inputMode = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync(ArtemisPrologue.SceneName);
            yield return null;
            film = Object.FindAnyObjectByType<ArtemisPrologue>();
            Assert.That(film, Is.Not.Null);
        }

        [TearDown] public void Restore()
        {
            if (wand != null && wand.added) InputSystem.RemoveDevice(wand);
            if (hmd != null && hmd.added) InputSystem.RemoveDevice(hmd);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = inputMode;
            LocalizationService.CarriedLanguage = null;
        }

        [UnityTest] public IEnumerator TriggerTogglesViewsAndHeadTrackingSurvivesShotChanges()
        {
            Assert.That(SceneUtility.GetScenePathByBuildIndex(0), Does.EndWith(ArtemisPrologue.SceneName + ".unity"));
            Assert.That(SceneUtility.GetScenePathByBuildIndex(1), Does.EndWith(ArtemisLanding.SceneName + ".unity"));
            Assert.That(SceneUtility.GetScenePathByBuildIndex(2), Does.EndWith(ArtemisPrologue.DayOneScene + ".unity"));
            Assert.That(Camera.allCamerasCount, Is.EqualTo(1));
            Assert.That(film.Eye.stereoTargetEye, Is.EqualTo(StereoTargetEyeMask.Both));
            Assert.That(film.CrewPerspective, Is.False);
            hmd = InputSystem.AddDevice<XRHMD>();
            var initial = new Vector3(.2f, 1.65f, -.1f);
            SetHead(initial, Quaternion.identity);
            yield return null;
            yield return null;
            NewWand();
            Trigger(true);
            Assert.That(film.CrewPerspective, Is.True);
            yield return null;
            Assert.That(film.CrewPerspective, Is.True, "Holding the analog and click controls together must change the view only once.");
            var offset = new Vector3(.12f, -.15f, .05f);
            var turn = Quaternion.Euler(4, 35, 0);
            SetHead(initial + offset, turn);
            Trigger(false);
            Trigger(true);
            Assert.That(film.CrewPerspective, Is.False);
            yield return null;
            yield return null;
            var frame = film.Eye.transform.parent.parent;
            Assert.That(Vector3.Distance(frame.InverseTransformPoint(film.Eye.transform.position), offset), Is.LessThan(.015f));
            Assert.That(Quaternion.Angle(Quaternion.Inverse(frame.rotation) * film.Eye.transform.rotation, turn), Is.LessThan(.2f));
        }

        [UnityTest] public IEnumerator LunarOrbitHandsOffToDockedPlayableLander()
        {
            film.Timeline.Evaluate(film.Timeline.Duration - .01f);
            float end = Time.realtimeSinceStartup + 45f;
            while (SceneManager.GetActiveScene().name == ArtemisPrologue.SceneName && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(ArtemisLanding.SceneName));
            var landing = Object.FindAnyObjectByType<ArtemisLanding>();
            Assert.That(landing.Phase, Is.EqualTo(LandingPhase.Docked));
            Assert.That(Object.FindAnyObjectByType<DayOneMission>(), Is.Null);
        }

        [UnityTest] public IEnumerator HoldingGripSkipsOnlyTheFilm()
        {
            NewWand("LeftHand");
            using (StateEvent.From(wand, out var state))
            {
                wand.gripPressed.WriteValueIntoEvent(1f, state);
                wand.grip.WriteValueIntoEvent(1f, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
            Assert.That(film.Leaving, Is.False, "A tap must not skip the prologue.");
            float end = Time.realtimeSinceStartup + 45f;
            while (SceneManager.GetActiveScene().name == ArtemisPrologue.SceneName && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(ArtemisLanding.SceneName));
            yield return null;
            Assert.That(Object.FindAnyObjectByType<ArtemisLanding>().Phase, Is.EqualTo(LandingPhase.Docked));
        }

        [UnityTest] public IEnumerator RenderOpeningAndPlayableApproach()
        {
            var language = Object.FindAnyObjectByType<LocalizationService>();
            language.SetLanguage(GameLanguage.Chinese);
            foreach (var sample in new[] { (2f,"pad"), (5f,"boarding"), (13f,"hatch"), (23f,"launch"), (32.5f,"boosters"), (39.5f,"core"),
                (48f,"earth-orbit"), (61f,"transfer"), (75.5f,"lunar-orbit") })
            {
                film.Timeline.Evaluate(sample.Item1);
                foreach (bool inside in new[] { false, true })
                {
                    film.SelectView(inside);
                    yield return null;
                    yield return null;
                    film.SelectView(inside);
                    Capture(film.Eye, sample.Item2 + (inside ? "-crew" : "-exterior"));
                }
            }
            yield return SceneManager.LoadSceneAsync(ArtemisLanding.SceneName);
            yield return null;
            var flight = Object.FindAnyObjectByType<ArtemisLanding>();
            var eye = Camera.main;
            Object.FindAnyObjectByType<LocalizationService>().SetLanguage(GameLanguage.Chinese);
            flight.Advance(1, Vector2.zero, Vector2.zero);
            yield return new WaitForSecondsRealtime(.15f);
            yield return null;
            yield return null;
            Capture(eye, "orbit-docked");
            flight.SeparateOrRetry();
            flight.Advance(4, Vector2.zero, Vector2.zero);
            yield return new WaitForSecondsRealtime(.15f);
            yield return null;
            yield return null;
            Capture(eye, "orbit-separated");
            flight.ToggleEngine();
            flight.Advance(5, Vector2.zero, Vector2.zero);
            yield return new WaitForSecondsRealtime(.15f);
            yield return null;
            yield return null;
            Capture(eye, "landing-approach");
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Approach));
            NewWand();
            using (StateEvent.From(wand, out var grip))
            {
                wand.gripPressed.WriteValueIntoEvent(1f, grip);
                InputSystem.QueueEvent(grip);
            }
            InputSystem.Update();
            yield return null;
            yield return null;
            Capture(eye, "landing-cockpit");
            for (int step = 0; step < 3600 && flight.Height > 5 && flight.Phase == LandingPhase.Approach; step++)
            {
                var delta = flight.OffsetToPad;
                var stick = Vector2.ClampMagnitude(new Vector2(delta.x - 2 * flight.Velocity.x, delta.z - 2 * flight.Velocity.z) / 9, 1);
                flight.Advance(.04f, stick, new Vector2(0, delta.magnitude < 10 ? -.22f : -.12f));
            }
            yield return new WaitForSecondsRealtime(.15f);
            yield return null;
            yield return null;
            Capture(eye, "landing-final");
            var pilot = Object.FindAnyObjectByType<ArtemisLandingPilot>();
            pilot.SetCockpit(false);
            yield return null;
            yield return null;
            Capture(eye, "landing-final-exterior");
            for (int step = 0; step < 1200 && flight.Phase == LandingPhase.Approach; step++)
                flight.Advance(.05f, Vector2.zero, new Vector2(0, -.12f));
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Landed));
            var inspection = new GameObject("Touchdown inspection camera").AddComponent<Camera>();
            try
            {
                inspection.CopyFrom(eye);
                inspection.enabled = false;
                inspection.stereoTargetEye = StereoTargetEyeMask.None;
                inspection.cullingMask &= ~LayerMask.GetMask("UI", "Cockpit Interior");
                var at = flight.Craft.TransformPoint(new Vector3(0, 3, 0));
                var position = flight.Craft.TransformPoint(new Vector3(-10, 5.3f, 12));
                inspection.transform.SetPositionAndRotation(position, Quaternion.LookRotation(at - position));
                Capture(inspection, "landing-touchdown");
            }
            finally { Object.Destroy(inspection.gameObject); }
        }

        private static void Capture(Camera eye, string name)
        {
            var rt = RenderTexture.GetTemporary(1280, 800, 24);
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(eye, new RenderPipeline.StandardRequest { destination = rt });
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/ArtemisPreviews");
                File.WriteAllBytes("Logs/ArtemisPreviews/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(image);
            }
        }

        private void NewWand(string hand = "RightHand")
        {
            InputSystem.RegisterLayout<HTCViveControllerProfile.ViveController>();
            wand = InputSystem.AddDevice<HTCViveControllerProfile.ViveController>();
            InputSystem.SetDeviceUsage(wand, hand);
        }

        private void Trigger(bool down)
        {
            using (StateEvent.From(wand, out var state))
            {
                wand.triggerPressed.WriteValueIntoEvent(down ? 1f : 0f, state);
                wand.trigger.WriteValueIntoEvent(down ? 1f : 0f, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }

        private void SetHead(Vector3 position, Quaternion rotation)
        {
            using (StateEvent.From(hmd, out var state))
            {
                hmd.centerEyePosition.WriteValueIntoEvent(position, state);
                hmd.centerEyeRotation.WriteValueIntoEvent(rotation, state);
                hmd.isTracked.WriteValueIntoEvent(1f, state);
                hmd.trackingState.WriteValueIntoEvent(3, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }
    }
}
