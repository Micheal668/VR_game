using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    // 最简交互器：始终处于“握住”状态，位置由测试直接设置，经真实 XRInteractionManager 选择操纵件。
    internal sealed class PilotTestHand : XRBaseInteractor { }

    // 生命保障主场景的通用步骤，做法与 LifeSupportTests 相同：穿航天服、解除气闸、开门、登舱。
    internal sealed class LifeSupportSteps
    {
        private readonly StationMissionSession session;
        private readonly LifeSupportMission life;
        private readonly XRInteractionManager manager;
        private readonly XRRayInteractor hand;

        public LifeSupportSteps(StationMissionSession source)
        {
            session = source;
            life = session.GetComponent<LifeSupportMission>();
            manager = Object.FindAnyObjectByType<XRInteractionManager>();
            hand = new GameObject("Life support step hand").AddComponent<XRRayInteractor>();
            hand.interactionManager = manager;
            hand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
        }

        public void Dispose() { if (hand != null) Object.Destroy(hand.gameObject); }

        public void MoveBody(Vector3 destination)
        {
            var body = session.Exit.PlayerBody; var center = body.transform.TransformPoint(body.center); body.enabled = false;
            body.transform.position += new Vector3(destination.x - center.x, 0, destination.z - center.z); body.enabled = true; Physics.SyncTransforms();
        }

        public void Don()
        {
            var layout = session.GetComponent<LifeSupportSceneLayout>();
            session.BeginMission(); MoveBody(life.SuitRack.position + Vector3.left * .75f);
            var grip = layout.SuitHandle.Physical;
            hand.transform.position = grip.transform.position; hand.selectInput.manualPerformed = true; hand.selectInput.manualValue = 1;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grip);
            session.Advance(life.Config.DonSeconds);
            hand.selectInput.manualPerformed = false; hand.selectInput.manualValue = 0;
            if (grip.isSelected) manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grip);
            Assert.That(life.SuitWorn, Is.True);
        }

        // 穿好航天服 → 气闸放行（捷径）→ 用压力屏开门 → 走到梯旁登舱，进入上升器启动阶段。
        public void Board(AscentMission flight)
        {
            Don();
            life.AirlockRepair.SkipRepair();
            MoveBody(life.DoorControl.position + Vector3.left * .8f);
            Assert.That(life.TryOpenDoor(), Is.True);
            MoveBody(new Vector3(49.4f, 0, -4.5f));
            var hatch = Object.FindAnyObjectByType<HatchBoardingController>();
            Assert.That(hatch.CanBoard, Is.True);
            hatch.RequestBoarding(); session.Advance(0); flight.Tick(.951f);
            Assert.That(flight.Phase, Is.EqualTo(AscentPhase.Startup));
        }
    }
}
