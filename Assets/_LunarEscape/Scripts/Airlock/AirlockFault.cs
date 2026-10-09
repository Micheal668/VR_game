using System;
using UnityEngine;

namespace LunarEscape
{
    // 气闸 A 的一处故障。每处故障用不同的手部操作修复，只报告“是否已修好”，
    // 由 AirlockRepair 汇总并决定能否手动开门。故障自己不读任务时间，也不改界面。
    public abstract class AirlockFault : MonoBehaviour
    {
        [SerializeField] private AirlockRepair owner;

        public abstract bool IsFixed { get; }

        // 任务开始前和结束后不允许修理，避免在倒计时外提前完成。
        protected bool CanWork => owner != null && owner.Active;

        public event Action Changed;

        public void ConfigureOwner(AirlockRepair repair) => owner = repair;

        // 重新开始时恢复到损坏状态，包括物体位置。
        public abstract void ResetFault();

        protected void RaiseChanged() => Changed?.Invoke();
    }
}
