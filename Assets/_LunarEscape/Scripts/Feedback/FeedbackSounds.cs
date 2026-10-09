using System;
using System.Collections.Generic;
using UnityEngine;

namespace LunarEscape
{
    public enum FeedbackSound { Click, Deny, Grab, Release, Knock, Pack, RepairLoop, Chime, Clunk, ThrusterLoop, EngineLoop, Hiss, RadioOpen, RadioClose, Decompression }

    // 交互音效在运行时用数学方式合成，与警报音做法一致：不依赖外部录音素材，也不需要导入音频文件。
    // 每种声音只生成一次并缓存；固定随机种子保证每次运行听到的声音一致，便于调参对比。
    public static class FeedbackSounds
    {
        private const int Rate = 44100;
        private static readonly Dictionary<FeedbackSound, AudioClip> cache = new();

        public static AudioClip Get(FeedbackSound sound)
        {
            if (cache.TryGetValue(sound, out var clip) && clip != null) return clip;
            clip = Build(sound);
            cache[sound] = clip;
            return clip;
        }

        // 在宿主下创建独立子物体承载声源，不改动宿主上已有的 AudioSource 引用。
        // spatialBlend=1 时声音来自物体位置（头显内能分辨方向）；0 时为舱内环境声。
        public static AudioSource CreateSource(Transform host, string name, float spatialBlend, bool loop = false)
        {
            var child = new GameObject(name);
            child.transform.SetParent(host, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.4f;
            source.maxDistance = 8f;
            source.dopplerLevel = 0f;
            return source;
        }

        // 循环声源先以零音量播放，之后只调音量，避免反复 Play 产生的爆音。
        public static AudioSource CreateLoop(Transform host, string name, FeedbackSound sound, float spatialBlend)
        {
            var source = CreateSource(host, name, spatialBlend, true);
            source.clip = Get(sound);
            source.volume = 0f;
            return source;
        }

        public static void Play(AudioSource source, FeedbackSound sound, float volume)
        {
            if (source != null && source.isActiveAndEnabled && volume > 0f) source.PlayOneShot(Get(sound), Mathf.Clamp01(volume));
        }

        private static AudioClip Build(FeedbackSound sound)
        {
            var random = new System.Random(17 + (int)sound);
            float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);
            const float Tau = Mathf.PI * 2f;
            float low = 0f, low2 = 0f, phase = 0f;
            float[] data = sound switch
            {
                // 短促的硬质开关声：高频“嗒”加一点低频机身。
                FeedbackSound.Click => Render(0.06f, 0f, t =>
                    Mathf.Sin(Tau * 1800f * t) * Mathf.Exp(-t / 0.006f) * 0.45f +
                    Mathf.Sin(Tau * 520f * t) * Mathf.Exp(-t / 0.015f) * 0.35f),
                // 两声低沉方波“嗡嗡”，表示操作被拒绝或顺序不对。
                FeedbackSound.Deny => Render(0.3f, 0f, t =>
                {
                    float local = t % 0.14f;
                    if (local > 0.09f) return 0f;
                    float envelope = Mathf.Min(1f, local / 0.005f) * Mathf.Min(1f, (0.09f - local) / 0.01f);
                    return Mathf.Sign(Mathf.Sin(Tau * 170f * t)) * envelope * 0.22f;
                }),
                FeedbackSound.Grab => Render(0.09f, 0f, t =>
                {
                    low += 0.08f * (Noise() - low);
                    return low * Mathf.Exp(-t / 0.02f) * 0.9f + Mathf.Sin(Tau * 230f * t) * Mathf.Exp(-t / 0.025f) * 0.25f;
                }),
                FeedbackSound.Release => Render(0.08f, 0f, t => Mathf.Sin(Tau * 160f * t) * Mathf.Exp(-t / 0.03f) * 0.3f),
                // 物体碰到桌面或墙面：低频撞击加极短的噪声瞬态。
                FeedbackSound.Knock => Render(0.16f, 0f, t =>
                    Mathf.Sin(Tau * 140f * t) * Mathf.Exp(-t / 0.04f) * 0.6f + Noise() * Mathf.Exp(-t / 0.005f) * 0.35f),
                // 物资落进腰包：布料摩擦噪声加闷响。
                FeedbackSound.Pack => Render(0.26f, 0f, t =>
                {
                    low += 0.15f * (Noise() - low);
                    return low * Mathf.Exp(-t / 0.05f) * 0.7f + Mathf.Sin(Tau * 95f * t) * Mathf.Exp(-t / 0.07f) * 0.6f;
                }),
                // 维修时的电焊/电流声；50 Hz 调制在 1 秒内为整数周期，循环无断点。
                FeedbackSound.RepairLoop => Render(1f, 0.05f, t =>
                {
                    float hiss = Noise();
                    low += 0.25f * (hiss - low);
                    float buzz = 0.55f + 0.45f * Mathf.Sin(Tau * 50f * t);
                    return (hiss - low) * buzz * 0.3f + Mathf.Sin(Tau * 100f * t) * 0.08f;
                }),
                // 维修完成、对接成功：三个上行音符。
                FeedbackSound.Chime => Render(0.9f, 0f, t =>
                    Note(t, 0f, 784f) + Note(t, 0.11f, 988f) + Note(t, 0.22f, 1175f)),
                // 舱门关闭、对接锁定：沉重的金属碰撞。
                FeedbackSound.Clunk => Render(0.55f, 0f, t =>
                    Mathf.Sin(Tau * 70f * t) * Mathf.Exp(-t / 0.12f) * 0.8f +
                    Mathf.Sin(Tau * 210f * t) * Mathf.Exp(-t / 0.05f) * 0.3f +
                    Noise() * Mathf.Exp(-t / 0.01f) * 0.5f),
                // RCS 喷气：去掉低频的嘶嘶噪声。
                FeedbackSound.ThrusterLoop => Render(1.5f, 0.1f, t =>
                {
                    float hiss = Noise();
                    low += 0.1f * (hiss - low);
                    return (hiss - low) * 0.35f;
                }),
                // 主发动机：两次低通的隆隆声，40 Hz 在 2 秒内为整数周期。
                FeedbackSound.EngineLoop => Render(2f, 0.2f, t =>
                {
                    low += 0.03f * (Noise() - low);
                    low2 += 0.05f * (low - low2);
                    return Mathf.Clamp(low2 * 6f, -0.7f, 0.7f) + Mathf.Sin(Tau * 40f * t) * 0.15f;
                }),
                // 使用氧气瓶等物资：短促的阀门放气声。
                FeedbackSound.Hiss => Render(0.7f, 0f, t =>
                {
                    float hiss = Noise();
                    low += 0.2f * (hiss - low);
                    return (hiss - low) * Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t / 0.25f) * 0.4f;
                }),
                // 无线电开讲：一声“咔沙”静电，接 2525 Hz 提示音（参考 NASA 地面通话的 Quindar 音）。
                FeedbackSound.RadioOpen => Render(0.32f, 0f, t =>
                {
                    float hiss = Noise();
                    low += 0.35f * (hiss - low);
                    float crackle = t < 0.07f ? (hiss - low) * 0.35f * (1f - t / 0.07f) : 0f;
                    float beep = t >= 0.08f && t < 0.3f ? Mathf.Sin(Tau * 2525f * t) * 0.16f * Mathf.Min(1f, (t - 0.08f) / 0.005f) * Mathf.Min(1f, (0.3f - t) / 0.005f) : 0f;
                    return crackle + beep;
                }),
                // 无线电收讲：2475 Hz 提示音，尾部一小段静电。
                FeedbackSound.RadioClose => Render(0.34f, 0f, t =>
                {
                    float hiss = Noise();
                    low += 0.35f * (hiss - low);
                    float beep = t < 0.22f ? Mathf.Sin(Tau * 2475f * t) * 0.16f * Mathf.Min(1f, t / 0.005f) * Mathf.Min(1f, (0.22f - t) / 0.005f) : 0f;
                    float crackle = t >= 0.23f ? (hiss - low) * 0.3f * (1f - (t - 0.23f) / 0.11f) : 0f;
                    return beep + crackle;
                }),
                // 舱门泄压：先是一声闷爆，随后宽频气流轰鸣迅速衰减，
                // 叠加从高到低滑落的啸叫（舱压下降、气流变慢），最后归于真空的寂静。
                FeedbackSound.Decompression => Render(3.4f, 0f, t =>
                {
                    float hiss = Noise();
                    low += 0.06f * (hiss - low);
                    low2 += 0.4f * (hiss - low2);
                    float bang = Mathf.Sin(Tau * 48f * t) * Mathf.Exp(-t / 0.18f) * 0.9f + hiss * Mathf.Exp(-t / 0.012f) * 0.6f;
                    float envelope = Mathf.Min(1f, t / 0.04f) * Mathf.Exp(-t / 0.85f);
                    float roar = (low * 3.2f + (low2 - low) * 0.9f) * envelope * 0.55f;
                    float pitch = Mathf.Lerp(1500f, 380f, Mathf.Clamp01(t / 2.4f));
                    phase += Tau * pitch / Rate;
                    float whistle = Mathf.Sin(phase + Mathf.Sin(Tau * 7f * t) * 0.6f) * 0.12f * Mathf.Min(1f, t / 0.15f) * Mathf.Exp(-t / 1.1f);
                    return Mathf.Clamp(bang + roar + whistle, -1f, 1f);
                }),
                _ => throw new ArgumentOutOfRangeException(nameof(sound))
            };
            var clip = AudioClip.Create("Feedback " + sound, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Note(float t, float start, float frequency)
        {
            if (t < start) return 0f;
            float local = t - start;
            return Mathf.Sin(Mathf.PI * 2f * frequency * local) * Mathf.Min(1f, local / 0.004f) * Mathf.Exp(-local / 0.25f) * 0.22f;
        }

        // 循环声多生成 fade 秒，把尾部交叉淡化进开头，首尾样本连续，循环点不会“咔哒”。
        private static float[] Render(float seconds, float loopFade, Func<float, float> sample)
        {
            int length = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            int fade = Mathf.RoundToInt(loopFade * Rate);
            var raw = new float[length + fade];
            for (int i = 0; i < raw.Length; i++) raw[i] = sample((float)i / Rate);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float value = raw[i];
                if (i < fade)
                {
                    float weight = (float)i / fade;
                    value = raw[i] * weight + raw[length + i] * (1f - weight);
                }
                data[i] = Mathf.Clamp(value, -1f, 1f);
            }
            return data;
        }
    }
}
