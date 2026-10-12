using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LunarEscape
{
    // 头显前的黑幕与章节标题（“第一天”“第二天”）：入睡时淡出，醒来时淡入。
    // 标题文字随语言切换；黑幕可与其他效果共用同一块画布。
    public sealed class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Image cover;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [Tooltip("场景开始时显示的标题键；为空则不自动显示。")]
        [SerializeField] private string startTitleKey;
        [SerializeField] private string startSubtitleKey;
        [Tooltip("场景开始时是否由本组件从黑幕淡入（第二天的醒来由剧情组件负责黑幕，只叠加标题）。")]
        [SerializeField] private bool fadeInOnStart;
        private LocalizationService language;

        public bool Busy { get; private set; }
        public float CoverAlpha => cover != null ? cover.color.a : 0f;

        public void Configure(Image black, TMP_Text heading, TMP_Text caption, string titleKey, string subtitleKey, bool fadeIn)
        { cover = black; title = heading; subtitle = caption; startTitleKey = titleKey; startSubtitleKey = subtitleKey; fadeInOnStart = fadeIn; }

        private void Awake()
        {
            language = FindAnyObjectByType<LocalizationService>();
            SetTitleAlpha(0f);
            if (fadeInOnStart && cover != null) cover.color = Color.black;
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(startTitleKey)) yield break;
            yield return Title(startTitleKey, startSubtitleKey, 3.2f);
            if (fadeInOnStart) yield return Fade(0f, 2f);
        }

        // 标题：0.6 秒淡入、停留、0.8 秒淡出。
        public IEnumerator Title(string key, string subtitleKey, float hold)
        {
            Busy = true;
            if (title != null) title.text = language != null ? language.Format(key) : key;
            if (subtitle != null) subtitle.text = string.IsNullOrEmpty(subtitleKey) ? "" : language != null ? language.Format(subtitleKey) : subtitleKey;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f) { SetTitleAlpha(t); yield return null; }
            SetTitleAlpha(1f);
            for (float t = 0f; t < hold; t += Time.deltaTime) yield return null;
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.8f) { SetTitleAlpha(t); yield return null; }
            SetTitleAlpha(0f);
            Busy = false;
        }

        public IEnumerator Fade(float alpha, float seconds)
        {
            if (cover == null) yield break;
            float start = cover.color.a;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                cover.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, alpha, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            cover.color = new Color(0f, 0f, 0f, alpha);
        }

        private void SetTitleAlpha(float alpha)
        {
            if (title != null) title.alpha = alpha;
            if (subtitle != null) subtitle.alpha = alpha * 0.8f;
        }
    }
}
