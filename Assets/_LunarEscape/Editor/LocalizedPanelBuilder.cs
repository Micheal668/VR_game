using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace LunarEscape.Editor
{
    // 编辑器共用的面板搭建工具。场景只描述布局，三语绑定与按钮基础配置集中在这里。
    public sealed class LocalizedPanelBuilder
    {
        private readonly TMP_FontAsset font;
        private readonly LocalizationService localization;

        public LocalizedPanelBuilder(TMP_FontAsset font, LocalizationService localization)
        {
            this.font = font;
            this.localization = localization;
        }

        public Transform Panel(string name, Vector3 position, Vector2 size, float scale, Quaternion rotation)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.SetPositionAndRotation(position, rotation);
            rect.localScale = Vector3.one * scale;
            panel.AddComponent<GraphicRaycaster>();
            panel.AddComponent<TrackedDeviceGraphicRaycaster>();
            Rectangle(panel.transform, "Panel Background", Vector2.zero, size, new Color(0.025f, 0.055f, 0.075f));
            return panel.transform;
        }

        public LocalizedText Label(Transform parent, string name, string key, Vector2 position,
            Vector2 size, float fontSize, Color color)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            Place(label.rectTransform, parent, position, size);
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.color = color;
            var localized = label.gameObject.AddComponent<LocalizedText>();
            localized.Configure(localization, key);
            return localized;
        }

        public LocalizedText WorldLabel(string name, string key, Vector3 position,
            Vector2 size, float fontSize, Quaternion rotation)
        {
            var label = new GameObject(name).AddComponent<TextMeshPro>();
            label.transform.SetPositionAndRotation(position, rotation);
            label.rectTransform.sizeDelta = size;
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            var localized = label.gameObject.AddComponent<LocalizedText>();
            localized.Configure(localization, key);
            return localized;
        }

        public Image Rectangle(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            Place(image.rectTransform, parent, position, size);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public Button Button(Transform parent, string name, string key, Vector2 position, Vector2 size, float fontSize = 28)
        {
            var image = Rectangle(parent, name, position, size, new Color(0.1f, 0.24f, 0.3f));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // 鼠标与 VR 射线共用，键盘方向键留给模拟器。
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(image.transform, name + " Text", key, Vector2.zero, size - new Vector2(18, 8), fontSize, Color.white);
            return button;
        }

        private static void Place(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
