using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 防止练习工具意外掉出场景，初学者不必为了找回物体重新运行游戏。
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class ReturnFallenTool : MonoBehaviour
    {
        // 单位是米。物体低于地板 2 米时，视为意外掉出了房间。
        [SerializeField] private float minimumHeight = -2f;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Rigidbody body;
        private XRGrabInteractable grab;
        private bool restoreThrow;
        private bool originalThrowOnDetach;
        private int resetFrame;

        private void Awake()
        {
            // 记住本次运行开始时的位置，作为找回工具时的安全落点。
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
        }

        private void FixedUpdate()
        {
            // FixedUpdate 按物理时间步执行，适合处理刚体的位置和速度。
            // 正被手柄抓着时不强行移动物体，避免物体和手争夺控制权。
            if (grab.isSelected || body.position.y >= minimumHeight) return;
            ResetToStart();
        }

        public void ResetToStart()
        {
            if (body == null || grab == null)
                throw new InvalidOperationException("工具复位前需要完成 Awake 初始化。");

            if (grab.isSelected && grab.interactionManager == null)
                throw new InvalidOperationException("工具正被持有，但没有交互管理器可取消抓取。");

            // XRI 取消抓取后仍会在 Late 阶段写入投掷速度。
            // 即使此帧刚放手，也可能还没处理该步骤，因此每次复位都短暂暂停投掷。
            if (!restoreThrow) originalThrowOnDetach = grab.throwOnDetach;
            restoreThrow = true;
            resetFrame = Time.frameCount;
            grab.throwOnDetach = false;
            if (grab.isSelected)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

            // 同时清除移动、旋转速度，防止复位后仍沿着掉落方向飞走。
            // 运动学刚体不接受速度赋值；取消抓取已恢复它原有的刚体模式。
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.position = initialPosition;
            body.rotation = initialRotation;
        }

        private void LateUpdate()
        {
            // 交互管理器的 Late 执行顺序早于默认脚本；多等一帧也覆盖 Late 中触发的重试。
            if (!restoreThrow || Time.frameCount <= resetFrame) return;
            grab.throwOnDetach = originalThrowOnDetach;
            restoreThrow = false;
        }
    }
}
