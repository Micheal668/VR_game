using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape.Tests
{
    // 用一只“测试手”经真实 XRInteractionManager 完成气闸抢修的每个动作：
    // 掀盖、拔插保险丝、转手轮、用扳手拧螺栓、拉杆。手的位置逐帧移动，操纵件按真实手部规则响应。
    internal sealed class AirlockTestDriver
    {
        private readonly AirlockRepair airlock;
        private readonly XRInteractionManager manager;
        public readonly PilotTestHand Hand;

        public AirlockTestDriver(AirlockRepair repair)
        {
            airlock = repair;
            manager = Object.FindAnyObjectByType<XRInteractionManager>();
            Hand = new GameObject("Airlock Test Hand").AddComponent<PilotTestHand>();
            Hand.interactionLayers = -1;
        }

        public FuseFault Fuse => (FuseFault)airlock.Faults[0];
        public ValveFault Valve => (ValveFault)airlock.Faults[1];
        public LatchFault Latches => (LatchFault)airlock.Faults[2];

        public void Dispose() { if (Hand != null) Object.Destroy(Hand.gameObject); }

        public IEnumerator Grab(IXRSelectInteractable target, Vector3 handPosition)
        {
            Hand.transform.position = handPosition;
            yield return null;
            manager.SelectEnter((IXRSelectInteractor)Hand, target);
            yield return Frames(2);
        }

        public IEnumerator Release(IXRSelectInteractable target)
        {
            if (Hand.IsSelecting(target)) manager.SelectExit((IXRSelectInteractor)Hand, target);
            yield return Frames(2);
        }

        public IEnumerator Press(CockpitControl control)
        {
            yield return Grab(control, control.transform.position);
            yield return Release(control);
        }

        // 掀盖 → 拔出烧坏的 → 等插座冷却 → 拿备用的放到插座处松手。
        public IEnumerator ReplaceFuse()
        {
            var fuse = Fuse;
            if (!fuse.CoverOpen) yield return Press(fuse.Cover);
            Assert.That(fuse.CoverOpen, "配电盒盖应已掀开");
            yield return Grab(fuse.Burnt.Grab, fuse.Burnt.transform.position);
            Assert.That(Hand.IsSelecting(fuse.Burnt.Grab), "盖子打开后可以拔出烧坏的保险丝");
            Hand.transform.position += Vector3.left * 0.4f;
            yield return Frames(3);
            yield return Release(fuse.Burnt.Grab);
            yield return Frames(45);
            yield return Grab(fuse.Spare.Grab, fuse.Spare.transform.position);
            Assert.That(Hand.IsSelecting(fuse.Spare.Grab));
            // 动态抓取：手移动多少，保险丝就移动多少。
            Hand.transform.position += fuse.Socket.attachTransform.position - fuse.Spare.transform.position;
            for (int i = 0; i < 10; i++) { yield return new WaitForFixedUpdate(); }
            yield return Release(fuse.Spare.Grab);
            for (int i = 0; i < 30 && !fuse.IsFixed; i++) yield return new WaitForFixedUpdate();
        }

        // 握住轮缘，沿轮面画圆。
        public IEnumerator TurnValve(float degrees)
        {
            var wheel = Valve.Valve;
            const float radius = 0.12f;
            float sign = Mathf.Sign(degrees);
            float start = Mathf.Atan2(0f, 1f);
            yield return Grab(wheel, wheel.transform.TransformPoint(new Vector3(radius, 0f, 0f)));
            for (float turned = 0f; turned < Mathf.Abs(degrees); turned += 8f)
            {
                float a = start + sign * Mathf.Min(turned + 8f, Mathf.Abs(degrees)) * Mathf.Deg2Rad;
                Hand.transform.position = wheel.transform.TransformPoint(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
                yield return null;
            }
            yield return Release(wheel);
        }

        // 握住扳手，把工具头套在每颗螺栓上，绕螺栓轴转半圈多。
        public IEnumerator UnboltAll(RepairTool tool)
        {
            var grab = tool.GetComponent<XRGrabInteractable>();
            // 测试中让扳手严格跟手（不经物理追赶），避免穿越房间时被家具卡住；规则判定不受影响。
            var movement = grab.movementType;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            yield return Grab(grab, tool.transform.position);
            yield return Frames(10);
            Quaternion relRotation = Quaternion.Inverse(Hand.transform.rotation) * tool.transform.rotation;
            Vector3 relPosition = Quaternion.Inverse(Hand.transform.rotation) * (tool.transform.position - Hand.transform.position);
            Vector3 tipLocal = Quaternion.Inverse(tool.transform.rotation) * (tool.Tip.position - tool.transform.position);
            foreach (var bolt in Latches.Bolts)
            {
                Vector3 axis = bolt.transform.forward;
                for (float angle = 0f; angle <= bolt.ReleaseDegrees + 40f && !bolt.Released; angle += 4f)
                {
                    Vector3 forward = Quaternion.AngleAxis(angle, axis) * Vector3.up;
                    var toolRotation = Quaternion.LookRotation(forward, axis);
                    Vector3 toolPosition = bolt.transform.position - toolRotation * tipLocal;
                    Hand.transform.rotation = toolRotation * Quaternion.Inverse(relRotation);
                    Hand.transform.position = toolPosition - Hand.transform.rotation * relPosition;
                    yield return new WaitForFixedUpdate();
                    yield return null;
                }
                Assert.That(bolt.Released, bolt.name + " 应已被扳手拧松：工具头距螺栓 "
                    + Vector3.Distance(tool.Tip.position, bolt.transform.position).ToString("0.000") + " m，已转 " + bolt.Turned.ToString("0") + "°");
            }
            yield return Release(grab);
            grab.movementType = movement;
        }

        // 握住拉杆手柄，沿弧线向下、向舱内拉到底并保持 seconds 秒。
        public IEnumerator PullLever(float holdSeconds)
        {
            var lever = airlock.Lever;
            const float radius = 0.27f;
            yield return Grab(lever, lever.transform.TransformPoint(new Vector3(0f, radius, 0f)));
            for (float a = 0f; a <= 115f; a += 5f)
            {
                float r = a * Mathf.Deg2Rad;
                Hand.transform.position = lever.transform.TransformPoint(new Vector3(0f, Mathf.Cos(r) * radius, Mathf.Sin(r) * radius));
                yield return null;
            }
            for (float t = 0f; t < holdSeconds; t += Time.deltaTime) yield return null;
            yield return Release(lever);
        }

        public static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    }
}
