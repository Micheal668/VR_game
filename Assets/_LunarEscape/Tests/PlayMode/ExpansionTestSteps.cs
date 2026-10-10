using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    internal static class ExpansionTestSteps
    {
        public static IEnumerator Wake(StationMissionSession session)
        {
            yield return null; // Let a pending retry reset the opening sequence.
            var story = session.GetComponent<StationExpansionMission>();
            for (int i = 0; i < 180 && !story.IntroComplete; i++) yield return null;
            Assert.That(story.IntroComplete, Is.True);
            Assert.That(session.Mission.Phase, Is.EqualTo(StationMissionPhase.Repair));
        }
        public static void Move(StationMissionSession session, Vector3 point)
        {
            var body = session.Exit.PlayerBody; var center = body.transform.TransformPoint(body.center);
            body.enabled = false; body.transform.position += new Vector3(point.x-center.x,0,point.z-center.z);
            body.enabled = true; Physics.SyncTransforms();
        }
        public static IEnumerator Solve(StationPatchPuzzle puzzle)
        {
            var manager = Object.FindAnyObjectByType<XRInteractionManager>();
            var hand = new GameObject("Circuit insertion test hand").AddComponent<PilotTestHand>(); hand.interactionLayers = -1;
            foreach (var module in puzzle.Modules)
            {
                var grab = module.GetComponent<XRGrabInteractable>();
                var socket = puzzle.Sockets.Single(s => s.Identity == module.Identity).Socket;
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                hand.transform.SetPositionAndRotation(grab.transform.position,grab.transform.rotation); yield return null;
                manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)grab); yield return null; yield return null;
                hand.transform.position += socket.attachTransform.position - module.transform.position;
                for(int i=0;i<8;i++)yield return new WaitForFixedUpdate();
                manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)grab);
                for(int i=0;i<50&&!socket.IsSelecting(grab);i++)yield return new WaitForFixedUpdate();
                Assert.That(socket.IsSelecting(grab),Is.True,module.name);
            }
            Object.Destroy(hand.gameObject);
            Assert.That(puzzle.IsSolved,Is.True);
        }
        public static IEnumerator Power(StationMissionSession session)
        {
            var story = session.GetComponent<StationExpansionMission>();
            if (!story.Circuit.IsSolved) yield return Solve(story.Circuit);
            Object.FindAnyObjectByType<HabitatBreaker>().SwitchOn(); yield return null;
            Assert.That(Object.FindAnyObjectByType<HabitatBreaker>().IsOn,Is.True);
        }
        public static IEnumerator Rescue(StationMissionSession session)
        {
            var story = session.GetComponent<StationExpansionMission>();
            Move(session,new Vector3(-7.5f,0,1.7f)); session.Advance(2.01f);
            yield return Power(session); yield return Solve(story.Laboratory);
            Move(session,story.ControlPoint.position + Vector3.right*.6f);
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Unlock Bedroom Door").onClick.Invoke();
            Assert.That(story.DoorUnlocked,Is.True);
            session.Advance(25); yield return null;
            Assert.That(story.CommanderSuited,Is.True);
        }
    }
}
