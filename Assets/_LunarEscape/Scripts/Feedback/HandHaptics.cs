using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LunarEscape
{
    // 统一的手柄震动入口：从交互器找到对应手柄上的官方 HapticImpulsePlayer 并发送脉冲。
    // 键鼠模拟器和没有震动硬件的设备上，发送会安静地失败，不影响游戏规则。
    public static class HandHaptics
    {
        private static float strength = 1f;

        // 全局强度倍率，便于在舒适度测试时整体调低；0 表示关闭全部玩法震动。
        public static float Strength { get => strength; set => strength = Mathf.Clamp01(value); }

        // 交互器（近远交互器、UI 射线等）位于手柄物体之下，向上查找即可得到该手的震动组件。
        public static HapticImpulsePlayer FromInteractor(object interactor)
        {
            return interactor is Component component && component != null
                ? component.GetComponentInParent<HapticImpulsePlayer>()
                : null;
        }

        public static HapticImpulsePlayer FromGrab(XRGrabInteractable grab)
        {
            return grab != null && grab.isSelected ? FromInteractor(grab.firstInteractorSelecting) : null;
        }

        public static void Pulse(HapticImpulsePlayer hand, float amplitude, float seconds)
        {
            if (hand == null || amplitude <= 0f || seconds <= 0f || strength <= 0f) return;
            hand.SendHapticImpulse(Mathf.Clamp01(amplitude * strength), seconds);
        }

        // “嗡-嗡”两下：表示操作被拒绝，与成功时的单次轻触区分开。
        public static IEnumerator DenyPattern(HapticImpulsePlayer hand, float amplitude)
        {
            Pulse(hand, amplitude, 0.06f);
            yield return new WaitForSeconds(0.12f);
            Pulse(hand, amplitude, 0.06f);
        }
    }

    // 连续震动：每隔约 0.08 秒重发一个 0.12 秒脉冲，前后重叠成持续震动；
    // 调用方停止调用后，震动在一个脉冲内自然结束，不需要额外的“停止”命令。
    public sealed class HapticRumble
    {
        private const float Interval = 0.08f;
        private readonly Dictionary<HapticImpulsePlayer, float> nextPulse = new();

        public void Drive(HapticImpulsePlayer hand, float amplitude)
        {
            if (hand == null || amplitude <= 0.001f) return;
            float now = Time.time;
            if (nextPulse.TryGetValue(hand, out float next) && now < next) return;
            nextPulse[hand] = now + Interval;
            HandHaptics.Pulse(hand, amplitude, Interval * 1.5f);
        }

        public void Clear() => nextPulse.Clear();
    }
}
