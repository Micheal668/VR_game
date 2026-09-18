using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace LunarEscape
{
    // 场景桥接层：读取真实接触和出口区域，把一次 Update 交给规则层计时。
    public sealed class StationMissionSession : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private RepairContact contact;
        [SerializeField] private EvacuationZone exit;
        [SerializeField] private XROrigin player;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private ReturnFallenTool tool;
        [SerializeField] private bool requireExitConfirmation;
        [SerializeField] private AscentMission ascent;
        private bool exitConfirmed;
        private GravityProvider gravity;
        private MissionTeleportationProvider teleport;
        private TimedRepairTask contactTask;

        public StationMission Mission => mission;
        public EvacuationZone Exit => exit;
        public XROrigin Player => player;
        public Transform SpawnPoint => spawnPoint;
        public bool RequiresExitConfirmation => requireExitConfirmation;

        // 第四步需要进舱后确认，旧教学场景仍然保持走到出口就完成。
        public void ConfigureExitConfirmation(bool required)
        {
            requireExitConfirmation = required;
            exitConfirmed = false;
        }

        public bool ConfirmExit()
        {
            if (mission == null || exit == null || mission.Phase != StationMissionPhase.Evacuation
                || !exit.ContainsPlayer || mission.RemainingSeconds <= 0f) return false;
            exitConfirmed = true;
            teleport?.CancelPendingTeleport();
            return true;
        }

        private void Awake()
        {
            PrepareSceneReferences();
        }

        public void Configure(StationMission task, RepairContact repairContact, EvacuationZone exitZone,
            XROrigin xrPlayer, Transform spawn, ReturnFallenTool resettableTool)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (repairContact == null) throw new ArgumentNullException(nameof(repairContact));
            if (exitZone == null) throw new ArgumentNullException(nameof(exitZone));
            if (xrPlayer == null) throw new ArgumentNullException(nameof(xrPlayer));
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));
            if (resettableTool == null) throw new ArgumentNullException(nameof(resettableTool));

            mission = task;
            contact = repairContact;
            exit = exitZone;
            player = xrPlayer;
            spawnPoint = spawn;
            tool = resettableTool;
            PrepareSceneReferences();
        }

        private void PrepareSceneReferences()
        {
            // 禁用旧组件的 Update，但保留其只读接触检测，避免维修每帧推进两次。
            if (contact != null) contact.enabled = false;
            contactTask = contact != null ? contact.GetComponent<TimedRepairTask>() : null;
            gravity = player != null ? player.GetComponentInChildren<GravityProvider>(true) : null;
            teleport = player != null ? player.GetComponentInChildren<MissionTeleportationProvider>(true) : null;
        }

        public void ConfigureAscent(AscentMission flight)
        {
            if (flight == null || flight.Station != mission) throw new ArgumentException("上升任务必须连接同一基地任务。");
            ascent = flight;
        }
        private void Update()
        {
            // 地面与登舱后的阶段共用一条帧时钟，转场首帧不会重复扣时间。
            if (ascent != null && ascent.IsLocked) ascent.Tick(Time.deltaTime);
            else Advance(Time.deltaTime);
        }

        public void BeginMission()
        {
            ValidateReferences();
            mission.Begin();
        }

        public void Advance(float dt)
        {
            ValidateReferences();
            // 点击时已核对身体区域；合法确认后锁存这次请求，避免下一帧小幅移动使登舱卡住。
            // Tick 仍先结算截止时间，因此本帧到期不会被点击绕过。
            mission.Tick(dt, contact.HasValidContact(),requireExitConfirmation ? exitConfirmed : exit.ContainsPlayer);
        }

        public void RetryMission()
        {
            ValidateReferences();
            teleport?.CancelPendingTeleport();
            Vector3 forward = Vector3.ProjectOnPlane(spawnPoint.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                throw new InvalidOperationException("重试出生点需要有效的水平朝向。");

            CharacterController body = exit.PlayerBody;
            bool bodyWasEnabled = body.enabled;
            // 追踪高度包括 Floor/Device 模式的相机偏移；重试不改变玩家真实站立或蹲下的高度。
            float headHeight = player.CameraInOriginSpaceHeight * player.Origin.transform.lossyScale.y;
            tool.ResetToStart();
            body.enabled = false;
            try
            {
                bool rotated = player.MatchOriginUpCameraForward(Vector3.up, forward.normalized);
                bool moved = player.MoveCameraToWorldLocation(spawnPoint.position + Vector3.up * headHeight);
                if (!rotated || !moved)
                    throw new InvalidOperationException("XR Origin 未能完成重试位置复位。");

                // 立即把身体水平中心对齐头部，避免等到下一帧重力组件更新才回到出生点。
                Vector3 localHead = body.transform.InverseTransformPoint(player.Camera.transform.position);
                Vector3 center = body.center;
                center.x = localHead.x;
                center.z = localHead.z;
                body.center = center;
            }
            finally
            {
                body.enabled = bodyWasEnabled;
            }

            if (gravity != null) gravity.ResetFallForce();
            exitConfirmed = false;
            Physics.SyncTransforms();
            // 世界先复位，再让阶段事件关闭撤离门、更新界面；语言由独立服务保留。
            mission.ResetMission();
        }

        private void ValidateReferences()
        {
            if (mission == null || contact == null || exit == null || player == null ||
                spawnPoint == null || tool == null || exit.Volume == null || exit.PlayerBody == null ||
                player.Camera == null || player.Origin == null || mission.Config == null || mission.RepairTask == null)
                throw new InvalidOperationException("任务场景引用不完整：需要任务、维修接触、出口、XR 玩家、出生点及可复位工具。");
            if (!exit.PlayerBody.transform.IsChildOf(player.Origin.transform))
                throw new InvalidOperationException("出口区域必须检测当前 XR 玩家的身体。");
            if (contactTask != mission.RepairTask)
                throw new InvalidOperationException("维修接触和任务必须使用同一个 TimedRepairTask。");
        }
    }
}
