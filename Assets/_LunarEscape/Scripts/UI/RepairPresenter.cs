using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 只把任务状态显示出来：规则层不知道面板、颜色和当前语言的存在。
    public sealed class RepairPresenter : MonoBehaviour
    {
        [SerializeField] private TimedRepairTask task;
        [SerializeField] private LocalizedText status;
        [SerializeField] private LocalizedText progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Renderer indicator;
        private readonly Color waiting = new(1f, 0.62f, 0.2f);
        private readonly Color working = new(0.22f, 0.85f, 1f);
        private readonly Color complete = new(0.2f, 0.95f, 0.65f);
        private MaterialPropertyBlock indicatorProperties;
        private int lastPercent = -1;
        private RepairState lastState;

        public void Configure(TimedRepairTask source, LocalizedText statusText,
            LocalizedText percentage, Image fill, Renderer targetIndicator)
        {
            if (isActiveAndEnabled && task != null) task.Changed -= Refresh;
            task = source;
            status = statusText;
            progressText = percentage;
            progressFill = fill;
            indicator = targetIndicator;
            if (isActiveAndEnabled && task != null) task.Changed += Refresh;
            lastPercent = -1;
            Refresh();
        }

        private void OnEnable()
        {
            if (task != null) task.Changed += Refresh;
            lastPercent = -1;
            Refresh();
        }

        private void OnDisable()
        {
            if (task != null) task.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (task == null || status == null) return;
            if (progressFill != null) progressFill.fillAmount = task.Progress;
            // 只在显示值发生变化时更新文字；100% 只在任务真正完成后显示。
            int percent = task.State == RepairState.Complete ? 100 : Mathf.FloorToInt(task.Progress * 100f);
            if (percent == lastPercent && task.State == lastState) return;
            lastPercent = percent;
            lastState = task.State;
            string key = task.State switch
            {
                RepairState.Working => "repair.working",
                RepairState.Paused => "repair.paused",
                RepairState.Complete => "repair.complete",
                _ => "repair.ready"
            };
            status.SetKey(key, percent);
            if (progressText != null) progressText.SetKey("repair.progress", percent);
            var color = task.State == RepairState.Complete ? complete
                : task.State == RepairState.Working ? working : waiting;
            status.GetComponent<TMP_Text>().color = color;
            if (progressFill != null) progressFill.color = color;
            if (indicator == null) return;
            // 属性块只改变这一个物体的颜色，不修改共用材质或生成材质副本。
            indicatorProperties ??= new MaterialPropertyBlock();
            indicator.GetPropertyBlock(indicatorProperties);
            indicatorProperties.SetColor("_BaseColor", color);
            indicator.SetPropertyBlock(indicatorProperties);
        }
    }
}
