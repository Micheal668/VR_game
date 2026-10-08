using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 同一个百分比仪表用于基地、宇航服和飞船；读数始终来自对应资源的真实消耗率。
    public sealed class ResourceReadout : MonoBehaviour
    {
        [SerializeField] private TMP_Text value, remaining;
        [SerializeField] private Image fill;
        [SerializeField] private Color normal = new(.33f, .88f, .89f);
        public TMP_Text ValueLabel => value;
        public TMP_Text RemainingLabel => remaining;
        public float DisplayedPercent { get; private set; }
        public float DisplayedSeconds { get; private set; }
        public void Configure(TMP_Text number, TMP_Text time, Image bar, Color color)
        { value = number; remaining = time; fill = bar; normal = color; }
        public void Set(float percent, float seconds)
        {
            DisplayedPercent = Mathf.Clamp(percent, 0, 100); DisplayedSeconds = Mathf.Max(0, seconds);
            value.text = DisplayedPercent.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
            remaining.text = Clock(DisplayedSeconds);
            var color = percent <= 10 ? new Color(1, .28f, .2f) : percent < 25 ? new Color(1, .70f, .26f) : normal;
            value.color = color; fill.color = color; fill.fillAmount = DisplayedPercent * .01f;
        }
        public static string Clock(float seconds)
        {
            if (!float.IsFinite(seconds)) return "--:--";
            int whole = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }
    }
}
