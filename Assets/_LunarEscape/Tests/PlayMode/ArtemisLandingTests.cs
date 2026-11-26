using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace LunarEscape.Tests
{
    public sealed class ArtemisLandingTests
    {
        private ArtemisLanding flight;
        private ArtemisLandingPilot pilot;
        private float originalDelta;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private HTCViveControllerProfile.ViveController left, right;

        [UnitySetUp] public IEnumerator LoadFlight()
        {
            originalDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync(ArtemisLanding.SceneName);
            yield return null;
            flight = Object.FindAnyObjectByType<ArtemisLanding>();
            pilot = Object.FindAnyObjectByType<ArtemisLandingPilot>();
            Assert.That(flight, Is.Not.Null);
            Assert.That(pilot, Is.Not.Null);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Docked));
        }

        [TearDown] public void Cleanup()
        {
            if (left != null && left.added) InputSystem.RemoveDevice(left);
            if (right != null && right.added) InputSystem.RemoveDevice(right);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.captureDeltaTime = originalDelta;
        }

        private void Coast(float seconds) => flight.Advance(seconds, Vector2.zero, Vector2.zero);

        private void Approach()
        {
            flight.SeparateOrRetry();
            flight.ToggleEngine();
            Coast(4.1f);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Approach));
            Physics.SyncTransforms();
        }

        // Flies the vehicle using only the pitch/roll inputs that the pilot can command.
        private void SteerAbovePad()
        {
            for (int step = 0; step < 1800; step++)
            {
                var delta = flight.OffsetToPad;
                var steering = new Vector2(delta.x * .14f - flight.Velocity.x * .32f,
                    delta.z * .14f - flight.Velocity.z * .32f);
                flight.Advance(.05f, Vector2.ClampMagnitude(steering, 1), Vector2.zero);
                if (flight.Range < 1.3f && flight.Drift < .42f && flight.Tilt < 3) break;
            }
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Approach));
            Assert.That(flight.Range, Is.LessThan(1.35f));
            Assert.That(flight.Drift, Is.LessThan(.45f));
        }

        [Test] public void DockAndSeparatedCoastBothWaitForThePilot()
        {
            var port = flight.Model.DockingPort;
            var mating = flight.Model.OrbiterDockingPort;
            Assert.That(Vector3.Distance(port.position, mating.position), Is.LessThan(.001f));
            Assert.That(Vector3.Angle(port.up, -mating.up), Is.LessThan(.1f));
            float dockedGap = Vector3.Distance(flight.Craft.position, flight.Orbiter.position);
            flight.ToggleEngine();
            Coast(45);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Docked));
            Assert.That(flight.EngineOn, Is.False);
            Assert.That(flight.Thrust, Is.Zero);
            Assert.That(Vector3.Distance(flight.Craft.position, flight.Orbiter.position), Is.EqualTo(dockedGap).Within(.001f));
            flight.SeparateOrRetry();
            Coast(.35f);
            var clearance = mating.position - port.position;
            Assert.That(Vector3.Dot(clearance, port.up), Is.GreaterThan(.5f));
            Assert.That(Vector3.ProjectOnPlane(clearance, port.up).magnitude, Is.LessThan(.01f));
            Coast(15);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Separated));
            Assert.That(Vector3.Distance(flight.Craft.position, flight.Orbiter.position), Is.GreaterThan(dockedGap + 5));
            var before = flight.Craft.position;
            flight.ToggleEngine();
            Coast(1);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.DepartureBurn));
            Assert.That(flight.Thrust, Is.GreaterThan(.5f));
            Assert.That(Vector3.Distance(before, flight.Craft.position), Is.GreaterThan(.4f));
            Coast(3.2f);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Approach));
            Assert.That(flight.Height, Is.GreaterThan(20));
            Assert.That(flight.Target.gameObject.activeInHierarchy, Is.True);
            Assert.That(flight.Orbiter.gameObject.activeInHierarchy, Is.False);
        }

        [Test] public void PilotCanBankTranslateClimbDescendAndReverseHeading()
        {
            Approach();
            var start = flight.Craft.position;
            flight.Advance(2, new Vector2(.7f,.7f), Vector2.zero);
            Assert.That(flight.Craft.up.x, Is.GreaterThan(.12f));
            Assert.That(flight.Craft.up.z, Is.GreaterThan(.12f));
            Assert.That(flight.Velocity.x, Is.GreaterThan(.6f));
            Assert.That(flight.Craft.position.z, Is.GreaterThan(start.z+1));
            flight.Advance(4, new Vector2(-.7f,-.7f), Vector2.zero);
            Assert.That(flight.Velocity.x, Is.LessThan(-.6f));
            Assert.That(flight.Velocity.z, Is.LessThan(-.6f));
            Coast(1);
            Assert.That(flight.Tilt, Is.LessThan(.2f));
            flight.Advance(2, Vector2.zero, Vector2.right);
            Assert.That(Mathf.DeltaAngle(0,flight.Heading), Is.GreaterThan(40));
            flight.Advance(2, Vector2.zero, Vector2.left);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0,flight.Heading)), Is.LessThan(.2f));
            float height = flight.Height;
            flight.Advance(3, Vector2.zero, Vector2.up);
            Assert.That(flight.Height, Is.GreaterThan(height+2));
            Assert.That(flight.Velocity.y, Is.GreaterThan(1));
            flight.Advance(7, Vector2.zero, Vector2.down);
            Assert.That(flight.Velocity.y, Is.LessThan(-1));
            Assert.That(flight.Height, Is.LessThan(height+2));
        }

        [Test] public void CuttingEngineCausesFreeFallAndFaultsRestoreTheApproachCheckpoint()
        {
            Approach();
            var start = flight.Craft.position;
            float descent = flight.Velocity.y;
            flight.ToggleEngine();
            Coast(1);
            Assert.That(flight.Thrust, Is.Zero);
            Assert.That(flight.Velocity.y, Is.EqualTo(descent - 1.62f).Within(.04f));
            Assert.That(flight.Height, Is.LessThan(start.y-flight.Target.position.y-1));
            Coast(45);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Retry));
            Assert.That(flight.Fault, Is.EqualTo(LandingFault.OutsideSite));
            Coast(30);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Retry));
            flight.SeparateOrRetry();
            Assert.That(flight.Fault, Is.EqualTo(LandingFault.None));
            Assert.That(flight.EngineOn, Is.True);
            Assert.That(Vector3.Distance(start,flight.Craft.position), Is.LessThan(.4f));
            SteerAbovePad();
            flight.ToggleEngine();
            Coast(45);
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Retry));
            Assert.That(flight.Fault, Is.EqualTo(LandingFault.FastDescent));
            Assert.That(flight.Range, Is.LessThan(ArtemisLanding.TargetRadius));
            flight.SeparateOrRetry();
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Approach));
            Assert.That(flight.Fault, Is.EqualTo(LandingFault.None));
        }

        [UnityTest] public IEnumerator ControlledSoftLandingHandsOverToDayOne()
        {
            Approach();
            SteerAbovePad();
            for (int i = 0; i < 2500 && flight.Phase == LandingPhase.Approach; i++)
                flight.Advance(.05f, Vector2.zero, new Vector2(0,-.11f));
            Assert.That(flight.Phase, Is.EqualTo(LandingPhase.Landed), flight.Fault.ToString());
            Assert.That(flight.Range, Is.LessThanOrEqualTo(ArtemisLanding.TargetRadius));
            Assert.That(flight.Height, Is.EqualTo(0).Within(.01f));
            Assert.That(flight.Model.Feet.Length, Is.EqualTo(4));
            foreach (var foot in flight.Model.Feet)
            {
                Assert.That(foot.position.y, Is.EqualTo(flight.Target.position.y).Within(.012f));
                Assert.That(Vector3.Distance(foot.position, flight.Target.position), Is.LessThan(ArtemisLanding.TargetRadius));
            }
            Assert.That(flight.EngineOn, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(ArtemisLanding.SceneName));
            Coast(4);
            for (int frame=0; frame<450 && SceneManager.GetActiveScene().name!=ArtemisPrologue.DayOneScene; frame++) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(ArtemisPrologue.DayOneScene));
            Assert.That(Object.FindAnyObjectByType<DayOneMission>(), Is.Not.Null);
        }

        private static void Send(HTCViveControllerProfile.ViveController device, Vector2 axis, bool grip=false, bool trigger=false)
        {
            using (StateEvent.From(device,out var state))
            {
                device.isTracked.WriteValueIntoEvent(1f,state);
                device.trackingState.WriteValueIntoEvent(3,state);
                device.trackpad.WriteValueIntoEvent(axis,state);
                device.trackpadTouched.WriteValueIntoEvent(axis.sqrMagnitude>0 ? 1f : 0f,state);
                device.trackpadClicked.WriteValueIntoEvent(axis.sqrMagnitude>0 ? 1f : 0f,state);
                device.grip.WriteValueIntoEvent(grip?1f:0f,state);
                device.gripPressed.WriteValueIntoEvent(grip?1f:0f,state);
                device.trigger.WriteValueIntoEvent(trigger?1f:0f,state);
                device.triggerPressed.WriteValueIntoEvent(trigger?1f:0f,state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }

        [UnityTest] public IEnumerator ViveGripsTriggerAndBothTrackpadsFlyAndRetry()
        {
            InputSystem.RegisterLayout<HTCViveControllerProfile.ViveController>();
            left=InputSystem.AddDevice<HTCViveControllerProfile.ViveController>();
            right=InputSystem.AddDevice<HTCViveControllerProfile.ViveController>();
            InputSystem.SetDeviceUsage(left,"LeftHand");
            InputSystem.SetDeviceUsage(right,"RightHand");
            yield return null;
            Send(left,Vector2.zero,grip:true);
            Assert.That(flight.Phase,Is.EqualTo(LandingPhase.Separated));
            Send(left,Vector2.zero);
            Send(right,Vector2.zero,trigger:true);
            Assert.That(flight.Phase,Is.EqualTo(LandingPhase.DepartureBurn));
            Send(right,Vector2.zero);
            Coast(4.1f);
            Assert.That(flight.Phase,Is.EqualTo(LandingPhase.Approach));
            Physics.SyncTransforms();
            bool originalView=pilot.Cockpit;
            Send(right,Vector2.zero,grip:true);
            Assert.That(pilot.Cockpit,Is.Not.EqualTo(originalView));
            Send(right,Vector2.zero);
            Send(left,new Vector2(.72f,.72f));
            Send(right,new Vector2(.7f,.7f));
            for(int i=0;i<35;i++) yield return null;
            Assert.That(flight.Tilt,Is.GreaterThan(8));
            var localUp=Quaternion.Inverse(Quaternion.Euler(0,flight.Heading,0))*flight.Craft.up;
            Assert.That(localUp.x,Is.GreaterThan(.1f));
            Assert.That(localUp.z,Is.GreaterThan(.1f));
            Assert.That(Mathf.DeltaAngle(0,flight.Heading),Is.GreaterThan(10));
            Assert.That(flight.Velocity.y,Is.GreaterThan(0));
            Assert.That(flight.Thrust,Is.GreaterThan(.6f));
            Send(left,Vector2.zero);
            Send(right,Vector2.zero,trigger:true);
            Assert.That(flight.EngineOn,Is.False);
            Send(right,Vector2.zero);
            Coast(60);
            Assert.That(flight.Phase,Is.EqualTo(LandingPhase.Retry));
            Send(left,Vector2.zero,grip:true);
            Assert.That(flight.Phase,Is.EqualTo(LandingPhase.Approach));
            Assert.That(flight.Fault,Is.EqualTo(LandingFault.None));
        }
    }
}
