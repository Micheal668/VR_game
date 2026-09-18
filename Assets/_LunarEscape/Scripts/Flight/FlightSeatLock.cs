using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace LunarEscape
{
    // 登舱后固定玩家身体，保留自然头手追踪与操作面板。
    // 不使用 Time.timeScale=0，避免把 XR 输入和重新开始一起暂停。
    [DefaultExecutionOrder(10000)]
    public sealed class FlightSeatLock : MonoBehaviour
    {
        [SerializeField] private AscentMission mission;
        [SerializeField] private GameObject flightPanel;
        [SerializeField] private XROrigin player;
        [SerializeField] private GameObject summaryRoot;
        private readonly List<(Behaviour component, bool enabled)> behaviours = new();
        private readonly List<(Selectable control, bool interactable)> controls = new();
        private readonly List<(XRBaseInteractor interactor, int layers)> interactors = new();
        private readonly List<(Rigidbody body, RigidbodyConstraints constraints)> bodies = new();
        private readonly List<(CollisionAwareSimulator simulator, bool movement, bool pointClick)> simulators = new();
        private readonly List<(MissionTeleportationProvider provider, bool allowed)> teleports = new();
        private Vector3 stoppedPosition;
        private Quaternion stoppedRotation;

        public bool IsLocked { get; private set; }

        public void Configure(AscentMission task, XROrigin origin, GameObject summary, GameObject controlsPanel)
        {
            if (task == null || origin == null || summary == null)
                throw new ArgumentException("失败锁定需要任务、玩家和结算窗口。");
            if (mission != null) mission.PhaseChanged -= Refresh;
            Restore();
            mission = task;
            player = origin;
            summaryRoot = summary;
            flightPanel = controlsPanel;
            if (isActiveAndEnabled) mission.PhaseChanged += Refresh;
            Refresh(mission.Phase);
        }

        private void OnEnable()
        {
            if (mission == null) return;
            mission.PhaseChanged += Refresh;
            Refresh(mission.Phase);
        }

        private void OnDisable()
        {
            if (mission != null) mission.PhaseChanged -= Refresh;
            // 卸载场景也会调用这里，不能重新启用正在销毁的 XR 组件。
            // 只有任务重置才解锁；临时停用后 OnEnable 会按当前阶段补同步。
        }

        private void Refresh(AscentPhase phase)
        {
            if (phase != AscentPhase.AwaitingBoarding && !IsLocked) Lock();
            else if (phase == AscentPhase.AwaitingBoarding && IsLocked) Restore();
        }

        private void Lock()
        {
            IsLocked = true;
            stoppedPosition = player.Origin.transform.position;
            stoppedRotation = player.Origin.transform.rotation;
            foreach (var provider in player.GetComponentsInChildren<MissionTeleportationProvider>(true))
            {
                teleports.Add((provider, provider.CanTeleport));
                provider.CanTeleport = false;
                provider.CancelPendingTeleport();
            }
            foreach (var provider in player.GetComponentsInChildren<LocomotionProvider>(true))
                Disable(provider);
            foreach (var simulator in SceneComponents<CollisionAwareSimulator>())
            {
                simulators.Add((simulator, simulator.BodyMovementEnabled, simulator.usePointAndClick));
                simulator.BodyMovementEnabled = false;
                // 归还普通鼠标给 UI：死亡时即使仍处于手柄模式，也能直接点击重新开始。
                simulator.usePointAndClick = false;
            }

            foreach (var interactor in player.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                interactors.Add((interactor, interactor.interactionLayers.value));
                // 仅清除世界物体交互层，不关闭射线组件或它的 UI 点选能力。
                interactor.interactionLayers = 0;
            }
            foreach (var grab in SceneComponents<XRGrabInteractable>())
            {
                // 停用会由 XRI 取消正在进行的抓取；追踪手柄继续移动也带不走物体。
                Disable(grab);
                if (!grab.TryGetComponent<Rigidbody>(out var body)) continue;
                bodies.Add((body, body.constraints));
                ClearVelocity(body);
                body.constraints = RigidbodyConstraints.FreezeAll;
            }
            foreach (var raycaster in SceneComponents<BaseRaycaster>())
                if (!Allowed(raycaster.transform)) Disable(raycaster);
            foreach (var control in SceneComponents<Selectable>())
            {
                if (Allowed(control.transform)) continue;
                controls.Add((control, control.interactable));
                control.interactable = false;
            }
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private bool Allowed(Transform target) => target.IsChildOf(summaryRoot.transform)
            || (flightPanel != null && target.IsChildOf(flightPanel.transform));

        public void Relocate(Vector3 position, Vector3 forward)
        {
            if (!IsLocked) return;
            var body = player.GetComponent<CharacterController>();
            bool enabled = body != null && body.enabled;
            if (body != null) body.enabled = false;
            player.MatchOriginUpCameraForward(Vector3.up, forward);
            player.MoveCameraToWorldLocation(position + Vector3.up * player.CameraInOriginSpaceHeight);
            stoppedPosition = player.Origin.transform.position;
            stoppedRotation = player.Origin.transform.rotation;
            if (body != null) body.enabled = enabled;
        }

        private void LateUpdate()
        {
            if (!IsLocked) return;
            // XRBodyTransformer 继续消化失败帧已排队的变换；渲染前固定身体，
            // 避免停用变换器后旧位移积压到重新开始。头显的局部追踪姿态不受影响。
            player.Origin.transform.SetPositionAndRotation(stoppedPosition, stoppedRotation);
        }

        private void Restore()
        {
            if (!IsLocked) return;
            IsLocked = false;
            foreach (var entry in bodies)
            {
                if (entry.body == null) continue;
                ClearVelocity(entry.body);
                entry.body.constraints = entry.constraints;
            }
            foreach (var entry in teleports)
            {
                if (entry.provider == null) continue;
                entry.provider.CancelPendingTeleport();
                entry.provider.CanTeleport = entry.allowed;
            }
            foreach (var entry in interactors)
                if (entry.interactor != null) entry.interactor.interactionLayers = entry.layers;
            foreach (var entry in behaviours)
                if (entry.component != null) entry.component.enabled = entry.enabled;
            foreach (var entry in controls)
                if (entry.control != null) entry.control.interactable = entry.interactable;
            foreach (var entry in simulators)
            {
                if (entry.simulator == null) continue;
                entry.simulator.BodyMovementEnabled = entry.movement;
                entry.simulator.usePointAndClick = entry.pointClick;
            }
            bodies.Clear(); teleports.Clear(); interactors.Clear();
            behaviours.Clear(); controls.Clear(); simulators.Clear();
        }

        private void Disable(Behaviour component)
        {
            behaviours.Add((component, component.enabled));
            component.enabled = false;
        }

        private static void ClearVelocity(Rigidbody body)
        {
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private IEnumerable<T> SceneComponents<T>() where T : Component =>
            gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
