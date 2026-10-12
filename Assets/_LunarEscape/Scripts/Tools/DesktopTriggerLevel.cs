using UnityEngine;
using UnityEngine.InputSystem;

namespace LunarEscape
{
    // 键鼠测试用：XRI 模拟器的扳机（T 键）只有按下/松开，相当于一直扣到底，取芯钻会立刻过热。
    // 挂在“XR Interaction Simulator - Editor Only”上：- / = 键以 10% 为一档调节按下 T 时的扳机深度，左下角显示当前值。
    // 真机模式下模拟器对象关闭，本组件不起作用。
    public sealed class DesktopTriggerLevel : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float level = 0.6f;

        public static DesktopTriggerLevel Active { get; private set; }
        public float Level => level;

        private void OnEnable() => Active = this;
        private void OnDisable() { if (Active == this) Active = null; }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.equalsKey.wasPressedThisFrame || keys.numpadPlusKey.wasPressedThisFrame) level = Mathf.Min(1f, Mathf.Round(level * 10f + 1f) / 10f);
            if (keys.minusKey.wasPressedThisFrame || keys.numpadMinusKey.wasPressedThisFrame) level = Mathf.Max(0.1f, Mathf.Round(level * 10f - 1f) / 10f);
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12, Screen.height - 34, 640, 28), $"Курок (T): {Mathf.RoundToInt(level * 100f)}%   сила: [-] / [=]   ·   Взять: G");
        }
    }
}
