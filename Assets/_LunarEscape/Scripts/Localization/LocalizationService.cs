using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LunarEscape
{
    public enum GameLanguage { Chinese, English, Russian }

    // 场景共用的文字字典，只管理语言；不保存或修改任何游戏任务进度。
    [DefaultExecutionOrder(-100)]
    public sealed class LocalizationService : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string key, zh, en, ru;
            public string Get(GameLanguage language) => language switch
            {
                GameLanguage.English => en,
                GameLanguage.Russian => ru,
                _ => zh
            };
        }

        [Serializable]
        public sealed class Table { public Entry[] entries; }

        [SerializeField] private TextAsset catalog;
        [SerializeField] private GameLanguage currentLanguage = GameLanguage.Chinese;
        private readonly Dictionary<string, Entry> entries = new();
        private bool loaded;
        public TextAsset Catalog => catalog;
        public GameLanguage CurrentLanguage => currentLanguage;
        public event Action LanguageChanged;

        private void Awake() => Load();

        public void Configure(TextAsset source, GameLanguage initialLanguage = GameLanguage.Chinese)
        {
            catalog = source;
            loaded = false;
            currentLanguage = initialLanguage;
            Load();
            LanguageChanged?.Invoke();
        }

        public void SetLanguage(GameLanguage language)
        {
            if (!Enum.IsDefined(typeof(GameLanguage), language)) throw new ArgumentOutOfRangeException(nameof(language));
            if (currentLanguage == language) return;
            currentLanguage = language;
            LanguageChanged?.Invoke();
        }

        // Unity 按钮可在 Inspector 中传整数，因此提供这一层简单转换。
        public void SetLanguageIndex(int index) => SetLanguage((GameLanguage)index);

        public string Format(string key, params object[] arguments)
        {
            Load();
            if (!entries.TryGetValue(key, out var entry)) return "[" + key + "]";
            var value = entry.Get(currentLanguage);
            return arguments == null || arguments.Length == 0
                ? value : string.Format(CultureInfo.InvariantCulture, value, arguments);
        }

        private void Load()
        {
            if (loaded) return;
            entries.Clear();
            if (catalog == null) return;
            var table = JsonUtility.FromJson<Table>(catalog.text);
            if (table?.entries == null) throw new InvalidOperationException("语言文件缺少 entries 数组。");
            foreach (var entry in table.entries)
            {
                // 错误文案在导入和测试时直接暴露，避免发布后悄悄显示空白。
                if (string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.zh)
                    || string.IsNullOrWhiteSpace(entry.en) || string.IsNullOrWhiteSpace(entry.ru))
                    throw new InvalidOperationException("语言文件存在空键或缺少翻译。");
                if (!entries.TryAdd(entry.key, entry)) throw new InvalidOperationException("重复语言键：" + entry.key);
            }
            loaded = true;
        }
    }
}
