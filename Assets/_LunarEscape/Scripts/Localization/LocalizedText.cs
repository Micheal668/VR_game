using System;
using TMPro;
using UnityEngine;

namespace LunarEscape
{
    // 将一个文字组件绑定到文案键；切换语言时自动保留并重新格式化进度参数。
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private LocalizationService localization;
        [SerializeField] private string key;
        private object[] arguments = Array.Empty<object>();
        private TMP_Text label;
        public string Key => key;

        private void OnEnable()
        {
            if (localization != null) localization.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (localization != null) localization.LanguageChanged -= Refresh;
        }

        public void Configure(LocalizationService service, string textKey)
        {
            if (isActiveAndEnabled && localization != null) localization.LanguageChanged -= Refresh;
            localization = service;
            if (isActiveAndEnabled && localization != null) localization.LanguageChanged += Refresh;
            SetKey(textKey);
        }

        public void SetKey(string textKey, params object[] values)
        {
            key = textKey;
            arguments = values;
            Refresh();
        }

        private void Refresh()
        {
            if (localization == null || string.IsNullOrEmpty(key)) return;
            if (label == null) label = GetComponent<TMP_Text>();
            label.text = localization.Format(key, arguments);
        }
    }
}
