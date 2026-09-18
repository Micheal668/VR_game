using System;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 失败结算只呈现结果；行动封锁与重新开始仍由任务会话负责。
    // 此组件挂在始终启用的 Mission Flow 上，不能挂在会被隐藏的结算窗口上。
    public sealed class MissionFailurePresenter : MonoBehaviour
    {
        [SerializeField] private StationMission mission;
        [SerializeField] private AscentMission flight;
        [SerializeField] private LocalizedText reason;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject root;
        [SerializeField] private Button restartButton;
        [SerializeField] private LocalizedText repairSummary;
        [SerializeField, Min(1f)] private float viewingDistance = 1.8f;

        private bool showing;
        private Vector3 cameraRelativePosition;

        public GameObject Root => root;
        public Button RestartButton => restartButton;
        public bool IsVisible => root != null && root.activeInHierarchy;

        public void Configure(StationMission task, Camera camera, GameObject panel,
            Button restart, LocalizedText summary)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            if (restart == null) throw new ArgumentNullException(nameof(restart));
            if (isActiveAndEnabled && mission != null) mission.Changed -= Refresh;
            mission = task;
            playerCamera = camera;
            root = panel;
            restartButton = restart;
            repairSummary = summary;
            showing = false;
            if (isActiveAndEnabled) mission.Changed += Refresh;
            Refresh();
        }

        public void ConfigureFlight(AscentMission ascent, LocalizedText failureReason)
        {
            if (flight != null) flight.Changed -= Refresh;
            flight = ascent; reason = failureReason;
            if (isActiveAndEnabled && flight != null) flight.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (mission != null) mission.Changed += Refresh;
            if (flight != null) flight.Changed += Refresh;
            showing = false;
            Refresh();
        }

        private void OnDisable()
        {
            if (mission != null) mission.Changed -= Refresh;
            if (flight != null) flight.Changed -= Refresh;
            if (root != null) root.SetActive(false);
            showing = false;
        }

        private void Refresh()
        {
            if (root == null) return;
            bool flightFailed = flight != null && flight.Phase == AscentPhase.Failed;
            bool failed = flightFailed || (mission != null && mission.Phase == StationMissionPhase.Failed);
            if (reason != null) reason.SetKey(flightFailed ? "flight.failure." + flight.Failure : "failure.reason");
            if (failed && !showing) PlaceInView();
            root.SetActive(failed);
            showing = failed;

            if (failed && repairSummary != null)
                repairSummary.SetKey(mission.RepairRestored
                    ? "failure.summary.repaired" : "failure.summary.unrepaired");
        }

        private void PlaceInView()
        {
            if (playerCamera == null) return;
            Transform head = playerCamera.transform;
            cameraRelativePosition = head.forward * Mathf.Max(1f, viewingDistance);
            // 以失败瞬间的视线摆放窗口，但不继承头部滚转，文字保持可读。
            Vector3 up = Mathf.Abs(Vector3.Dot(head.forward, Vector3.up)) < 0.98f
                ? Vector3.up : head.up;
            root.transform.SetPositionAndRotation(head.position + cameraRelativePosition,
                Quaternion.LookRotation(head.forward, up));
        }

        private void LateUpdate()
        {
            if (!showing || root == null || playerCamera == null) return;
            // 只跟随真实头部平移，避免前倾穿过面板；转头仍自然地改变观看方向。
            // 不改相机或 XR Origin，不冻结头显追踪，也不把窗口硬锁在每帧视线中心。
            root.transform.position = playerCamera.transform.position + cameraRelativePosition;
        }
    }
}
