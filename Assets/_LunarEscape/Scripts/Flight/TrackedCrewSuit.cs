using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace LunarEscape
{
    // 仅驱动视觉骨骼，绝不移动头显、手柄、碰撞体或交互射线。
    // 三点追踪估算身体及双肘；没有腿部传感器时保留站姿，不伪称全身追踪。
    [DefaultExecutionOrder(10010)]
    public sealed class TrackedCrewSuit : MonoBehaviour
    {
        [SerializeField] private XROrigin player;
        [SerializeField] private Transform leftController, rightController;
        [SerializeField] private CrewSuitRig suit;
        [SerializeField] private Vector3 gripOffset = new(0, -.025f, -.035f);
        [SerializeField] private Vector3 handRotationOffset = new(-72, 0, 0);
        private Quaternion[] restRotations;
        private Vector3[] restPositions;
        private Quaternion[] previousArmRotations;
        [SerializeField, Min(1)] private float armSmoothing = 18;
        [SerializeField, Range(0, 120)] private float wristRotationLimit = 80;
        private float heightScale = 1;
        private bool calibrated;
        public CrewSuitRig Suit => suit;
        public Transform LeftController => leftController;
        public Transform RightController => rightController;

        public void Configure(XROrigin origin, Transform left, Transform right, CrewSuitRig model)
        { player = origin; leftController = left; rightController = right; suit = model; }
        private void Start()
        {
            if (suit == null || player == null) { enabled = false; return; }
            var bones = suit.Bones;
            restRotations = new Quaternion[bones.Length];restPositions = new Vector3[bones.Length];
            previousArmRotations = new Quaternion[bones.Length];
            for (int i=0;i<bones.Length;i++) { restRotations[i]=bones[i].localRotation;restPositions[i]=bones[i].localPosition; }
            System.Array.Copy(restRotations, previousArmRotations, bones.Length);
        }
        private void LateUpdate()
        {
            if (restRotations == null) return;
            var head = player.Camera.transform;
            // 设备初始化后只校准一次身高，低头和蹲下不会让全身反复缩放。
            if (!calibrated && Time.timeSinceLevelLoad > 1 && HasPose(InputSystem.GetDevice<XRHMD>()))
            { heightScale = Mathf.Clamp(player.CameraInOriginSpaceHeight / 1.64f, .8f, 1.2f); calibrated = true; }
            var up=player.Origin.transform.up;
            var forward=Vector3.ProjectOnPlane(head.forward,up);
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(suit.transform.forward,up);
            var yaw=Quaternion.LookRotation(forward.normalized,up);
            var floor=player.Origin.transform.position;
            var feet=head.position-up*Vector3.Dot(head.position-floor,up)-yaw*Vector3.forward*.06f;
            suit.transform.SetPositionAndRotation(feet,yaw);suit.transform.localScale=Vector3.one*heightScale;
            var bones=suit.Bones;
            for(int i=0;i<bones.Length;i++){bones[i].localRotation=restRotations[i];bones[i].localPosition=restPositions[i];}
            // 头盔只供旁观视角与阴影使用；本地相机通过独立层隐藏它。
            bones[2].SetPositionAndRotation(head.position-head.forward*.04f,head.rotation);
            // 丢失位置或旋转追踪时使用自然站姿，不能把残留坐标当成有效手腕位置。
            if (HasPose(XRController.leftHand)) SolveArm(3,leftController,-1);
            if (HasPose(XRController.rightHand)) SolveArm(6,rightController,1);
            float blend = 1 - Mathf.Exp(-armSmoothing * Time.deltaTime);
            for (int i = 3; i < bones.Length; i++)
            {
                bones[i].localRotation = Quaternion.Slerp(previousArmRotations[i], bones[i].localRotation, blend);
                previousArmRotations[i] = bones[i].localRotation;
            }
        }

        private static bool HasPose(TrackedDevice device) => device != null && device.added
            && device.isTracked.isPressed && (device.trackingState.ReadValue() & 3) == 3;
        private void SolveArm(int start,Transform controller,float side)
        {
            if(controller==null)return;
            var upper=suit.Bones[start];var fore=suit.Bones[start+1];var hand=suit.Bones[start+2];
            var shoulder=upper.position;var elbow=fore.position;var wrist=hand.position;
            float lengthA=Vector3.Distance(shoulder,elbow),lengthB=Vector3.Distance(elbow,wrist);
            var target=controller.TransformPoint(gripOffset);
            var delta=target-shoulder;float distance=delta.magnitude;
            if(distance<.001f)return;
            var direction=delta/distance;
            float reach=Mathf.Clamp(distance,Mathf.Abs(lengthA-lengthB)+.001f,lengthA+lengthB-.001f);
            // 超出臂长时只限制视觉手腕，不再拉长骨骼或袖口；真实交互位置保持不变。
            target = shoulder + direction * reach;
            float along=(lengthA*lengthA-lengthB*lengthB+reach*reach)/(2*reach);
            float outward=Mathf.Sqrt(Mathf.Max(0,lengthA*lengthA-along*along));
            // 肘部提示主要朝外和向后，避免手臂自然下垂时提示向量与手臂平行而翻肘。
            var hint=suit.transform.TransformDirection(new Vector3(side*.85f,-.15f,-1));
            var bend=Vector3.ProjectOnPlane(hint,direction).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(suit.transform.forward,direction).normalized;
            var elbowTarget=shoulder+direction*along+bend*outward;
            upper.rotation=Quaternion.FromToRotation(elbow-shoulder,elbowTarget-shoulder)*upper.rotation;
            fore.rotation=Quaternion.FromToRotation(hand.position-fore.position,target-fore.position)*fore.rotation;
            // 限制手腕相对前臂的扭角，避免不同手柄握姿让手掌反拧。保持绑定骨骼长度。
            var neutral = fore.rotation * restRotations[start + 2];
            hand.rotation = Quaternion.RotateTowards(neutral,
                controller.rotation * Quaternion.Euler(handRotationOffset), wristRotationLimit);
        }
    }
}
