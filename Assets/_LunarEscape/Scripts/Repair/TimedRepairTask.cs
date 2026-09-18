using System;
using UnityEngine;

namespace LunarEscape
{
    public enum RepairState { Ready, Working, Paused, Complete }

    // 只管理维修进度，不读取手柄、不检测距离，也不直接修改界面。
    // 检测组件传入“现在能否维修”，因此同一任务可以复用于不同设备。
    public sealed class TimedRepairTask : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float durationSeconds = 2f;
        private float elapsedSeconds;

        public float DurationSeconds => durationSeconds;
        public float RemainingSeconds => Mathf.Max(0f, durationSeconds - elapsedSeconds);
        public float Progress => Mathf.Clamp01(elapsedSeconds / durationSeconds);
        public RepairState State { get; private set; } = RepairState.Ready;

        // 界面或设备通过事件响应变化；任务本身无需认识这些接收者。
        public event Action Changed;
        public event Action Completed;

        public void Configure(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds), "维修时长必须为有限正数。");

            durationSeconds = seconds;
            ResetTask();
        }

        public void Tick(bool canWork, float deltaTime)
        {
            // 完成后不再累计，保证一次练习只发出一次完成事件。
            // 无效时间不能倒扣进度，也不能让设备瞬间修好。
            if (State == RepairState.Complete || deltaTime < 0f ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;

            var previousState = State;
            float previousElapsed = elapsedSeconds;

            if (canWork)
            {
                elapsedSeconds = Mathf.Min(elapsedSeconds + deltaTime, durationSeconds);
                State = elapsedSeconds >= durationSeconds ? RepairState.Complete : RepairState.Working;
            }
            else
            {
                // 工具移开只暂停；重新接触时，从保留的进度继续。
                State = elapsedSeconds > 0f ? RepairState.Paused : RepairState.Ready;
            }

            // 先记下本次完成事实，避免事件接收者重置任务后遗漏完成通知。
            bool completedNow = State == RepairState.Complete;
            if (State != previousState || elapsedSeconds != previousElapsed)
                Changed?.Invoke();
            if (completedNow)
                Completed?.Invoke();
        }

        public void ResetTask()
        {
            elapsedSeconds = 0f;
            State = RepairState.Ready;
            Changed?.Invoke();
        }

        private void OnValidate()
        {
            // 同时保护 Inspector 中手动填写的参数。
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds))
                durationSeconds = 2f;
            durationSeconds = Mathf.Max(0.1f, durationSeconds);
        }
    }
}
