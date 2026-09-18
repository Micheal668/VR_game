using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LunarEscape
{
    // 在官方 XRGrabInteractable 上增加物资规则，不替换其抓取、追踪和刚体配置。
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class CargoItem : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] private CargoInventory inventory;
        [SerializeField] private CargoKind kind;
        private XRGrabInteractable grab;
        private Rigidbody body;
        private bool initialized;
        private bool listening;
        private bool changingPhysicalState;
        private Transform initialParent;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialScale;
        private bool initialKinematic;
        private bool initialGravity;
        private bool initialGrabEnabled;
        private RigidbodyConstraints initialConstraints;
        private bool restoreThrow;
        private bool originalThrowOnDetach;
        private int resetFrame;

        public CargoInventory Inventory => inventory;
        public CargoKind Kind => kind;
        public CargoState State { get; private set; } = CargoState.World;
        public bool IsCarried => State == CargoState.Held || State == CargoState.Packed || State == CargoState.Loaded;
        public XRGrabInteractable Grab { get { EnsureInitialized(); return grab; } }
        public Rigidbody Body { get { EnsureInitialized(); return body; } }
        public bool canProcess => isActiveAndEnabled;

        private void Awake() => EnsureInitialized();

        public void Configure(CargoInventory owner, CargoKind cargoKind)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (!Enum.IsDefined(typeof(CargoKind), cargoKind)) throw new ArgumentOutOfRangeException(nameof(cargoKind));
            if (State != CargoState.World && (inventory != owner || kind != cargoKind))
                throw new InvalidOperationException("已携带物资不能在途中换清单或改类型。");
            inventory = owner;
            kind = cargoKind;
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (listening) return;
            grab.selectFilters.Add(this);
            grab.selectEntered.AddListener(OnSelected);
            grab.selectExited.AddListener(OnReleased);
            listening = true;
        }

        private void OnDisable()
        {
            if (!listening || grab == null) return;
            grab.selectFilters.Remove(this);
            grab.selectEntered.RemoveListener(OnSelected);
            grab.selectExited.RemoveListener(OnReleased);
            listening = false;
            // 场景卸载只解绑，不复活物品，也不把取消抓取算成有效收纳。
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (inventory == null || interactable != (IXRSelectInteractable)Grab) return false;
            bool allowed = inventory.CanAcquire(this, out CargoRejection reason);
            // 仅在真的按下抓取时给出反馈，经过或悬停不会覆盖上一条操作结果。
            // 相同拒绝原因由清单去重，不会每帧重写 UI。
            if (!allowed && interactor != null && interactor.isSelectActive) inventory.ReportRejection(reason);
            return allowed;
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            if (changingPhysicalState) return;
            // 显式 SelectEnter 可以绕过过滤器，因此真实事件必须再次通过额度检查。
            if (inventory == null || !inventory.TryHold(this)) CancelSelection();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (changingPhysicalState || grab.isSelected || inventory == null) return;
            inventory.Released(this, args.isCanceled);
        }

        internal void SetState(CargoState state) => State = state;

        internal void CancelSelection()
        {
            EnsureInitialized();
            bool wasChanging = changingPhysicalState;
            changingPhysicalState = true;
            try
            {
                SuspendThrow();
                if (grab.isSelected)
                {
                    if (grab.interactionManager == null)
                        throw new InvalidOperationException("取消物资抓取需要现有 XR 交互管理器。");
                    grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
                }
                ClearVelocity();
            }
            finally { changingPhysicalState = wasChanging; }
        }

        internal void HideForStorage()
        {
            CancelSelection();
            // 隐藏实体不会释放额度；状态已经由清单改为 Packed 或 Loaded。
            gameObject.SetActive(false);
        }

        internal void ResetToStart()
        {
            EnsureInitialized();
            RestoreAt(initialPosition, initialRotation);
        }

        internal void RestoreAt(Vector3 position, Quaternion rotation)
        {
            EnsureInitialized();
            changingPhysicalState = true;
            try
            {
                CancelSelection();
                transform.SetParent(initialParent, true);
                transform.localScale = initialScale;
                body.isKinematic = initialKinematic;
                body.useGravity = initialGravity;
                body.constraints = initialConstraints;
                ClearVelocity();
                body.position = position;
                body.rotation = rotation;
                // 活跃物体和从包里恢复的物体，都在同一调用中拥有一致的实体位置。
                transform.SetPositionAndRotation(position, rotation);
                grab.enabled = initialGrabEnabled;
                gameObject.SetActive(true);
            }
            finally { changingPhysicalState = false; }
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            if (body == null || grab == null) throw new InvalidOperationException("物资需要 Rigidbody 和 XRGrabInteractable。");
            initialParent = transform.parent;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialScale = transform.localScale;
            initialKinematic = body.isKinematic;
            initialGravity = body.useGravity;
            initialConstraints = body.constraints;
            initialGrabEnabled = grab.enabled;
            initialized = true;
        }

        private void SuspendThrow()
        {
            // 编辑器搭建/保存场景时没有 LateUpdate，不能把临时抑制写进抓取组件的序列化配置。
            if (!Application.isPlaying) return;
            // 官方取消抓取仍可能留下 Late 阶段的投掷；收纳、拒绝和复位都要屏蔽它。
            if (!restoreThrow) originalThrowOnDetach = grab.throwOnDetach;
            restoreThrow = true;
            resetFrame = Time.frameCount;
            grab.throwOnDetach = false;
        }

        private void LateUpdate()
        {
            if (!restoreThrow || Time.frameCount <= resetFrame) return;
            grab.throwOnDetach = originalThrowOnDetach;
            restoreThrow = false;
        }

        private void ClearVelocity()
        {
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
