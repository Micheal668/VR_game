using System;

namespace LunarEscape
{
    public enum MusicTrack { None, Calm, Outside, Tension, Urgent, Triumph }

    // 背景音乐用代码合成（与音效一致，不依赖外部素材）：每首都是可无缝循环的短乐段。
    // 纯数学计算，不调用 Unity API，可在后台线程生成，避免载入时卡顿。
    public static class MusicSynth
    {
        public const int Rate = 22050;
        private const double Tau = Math.PI * 2.0;

        public static float Seconds(MusicTrack track) => track switch
        {
            MusicTrack.Calm => 24f,
            MusicTrack.Outside => 32f,
            MusicTrack.Tension => 16f,
            MusicTrack.Urgent => 8f,
            MusicTrack.Triumph => 7f,
            _ => 1f
        };

        public static bool Loops(MusicTrack track) => track != MusicTrack.Triumph;

        public static float[] Render(MusicTrack track) => track switch
        {
            MusicTrack.Calm => Seamless(Calm()),
            MusicTrack.Outside => Seamless(Outside()),
            MusicTrack.Tension => Seamless(Tension()),
            MusicTrack.Urgent => Seamless(Urgent()),
            MusicTrack.Triumph => Triumph(),
            _ => new float[Rate]
        };

        // 循环乐段：把末尾 0.25 秒交叉淡化进开头再截掉，首尾样本连续，循环点没有“咔哒”。
        private static float[] Seamless(float[] data)
        {
            int fade = Rate / 4, length = data.Length - fade;
            var result = new float[length];
            for (int i = 0; i < length; i++)
            {
                float value = data[i];
                if (i < fade) { float w = (float)i / fade; value = data[i] * w + data[length + i] * (1f - w); }
                result[i] = Math.Max(-1f, Math.Min(1f, value));
            }
            return result;
        }

        private static double Hz(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        // 舱内的平静段落：Am9 – Fmaj7 – Cadd9 – G6，柔和的合成弦乐加高处零星的钟声琶音。
        private static float[] Calm()
        {
            int[][] chords = { new[] { 45, 52, 57, 59, 60, 64 }, new[] { 41, 48, 52, 57, 60 }, new[] { 48, 55, 62, 64 }, new[] { 43, 50, 59, 64 } };
            double length = Seconds(MusicTrack.Calm), chordSeconds = length / chords.Length;
            var data = new float[(int)(length * Rate)];
            for (int i = 0; i < data.Length; i++)
            {
                double t = (double)i / Rate, sum = 0;
                for (int c = 0; c < chords.Length; c++)
                {
                    // 相邻和弦交叉淡化；首尾和弦按周期绕回，保证循环点连续。
                    double center = (c + 0.5) * chordSeconds, d = Math.Abs(t - center);
                    d = Math.Min(d, length - d);
                    double weight = Math.Max(0, 1 - Math.Max(0, d - chordSeconds * 0.35) / (chordSeconds * 0.4));
                    if (weight <= 0) continue;
                    foreach (int note in chords[c]) sum += weight * Pad(t, Hz(note), length) / chords[c].Length;
                }
                data[i] = (float)(sum * 0.5);
            }
            var random = new Random(3);
            for (double start = 0.75; start < length - 0.5; start += 1.5)
            {
                int c = (int)(start / chordSeconds) % chords.Length;
                if (random.NextDouble() < 0.35) continue;
                int note = chords[c][random.Next(chords[c].Length)] + 24;
                Bell(data, start, Hz(note), 0.06, 1.6);
            }
            return data;
        }

        // 舱外：极低的持续音（A 与 E），缓慢起伏；偶尔几声带回声的五声音阶钟音，空旷而安静。
        private static float[] Outside()
        {
            double length = Seconds(MusicTrack.Outside);
            var data = new float[(int)(length * Rate)];
            for (int i = 0; i < data.Length; i++)
            {
                double t = (double)i / Rate;
                double swell = 0.6 + 0.4 * Math.Sin(Tau * t / length);
                data[i] = (float)((Math.Sin(Tau * Hz(33) * t) * 0.18 + Math.Sin(Tau * Hz(40) * t) * 0.12 + Math.Sin(Tau * Hz(45) * t) * 0.05) * swell);
            }
            int[] pentatonic = { 69, 72, 74, 76, 79, 81, 84 };
            var random = new Random(11);
            for (double start = 1.0; start < length - 3; start += 3 + random.NextDouble() * 2.5)
                Bell(data, start, Hz(pentatonic[random.Next(pentatonic.Length)]), 0.07, 2.5);
            Echo(data, 0.45, 0.4);
            return data;
        }

        // 紧张：小二度相撞的低音持续音（D 与 E♭）拍频起伏，加上双击的心跳鼓。
        private static float[] Tension()
        {
            double length = Seconds(MusicTrack.Tension), beat = 0.8;
            var data = new float[(int)(length * Rate)];
            var random = new Random(5);
            double low = 0;
            for (int i = 0; i < data.Length; i++)
            {
                double t = (double)i / Rate;
                double drone = Math.Sin(Tau * Hz(38) * t) * 0.16 + Math.Sin(Tau * Hz(39) * t) * 0.12 + Math.Sin(Tau * Hz(50) * t) * 0.04;
                low += 0.01 * ((random.NextDouble() * 2 - 1) - low);
                double swell = 0.5 + 0.5 * Math.Sin(Tau * t / (length / 2));
                double local = t % (beat * 2);
                double heart = Kick(local, 52) * 0.5 + Kick(local - 0.22, 48) * 0.35;
                data[i] = (float)(drone + low * 1.5 * swell + heart);
            }
            return data;
        }

        // 紧急撤离：120 拍的 D 小调八分音符低音动机，底鼓踩在拍上，弱拍上是短促的镲声。
        private static float[] Urgent()
        {
            double length = Seconds(MusicTrack.Urgent), eighth = 0.25;
            int[] line = { 38, 38, 41, 38, 45, 38, 48, 46, 38, 38, 41, 38, 45, 43, 41, 40 };
            var data = new float[(int)(length * Rate)];
            var random = new Random(9);
            double filtered = 0, phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                double t = (double)i / Rate;
                int step = (int)(t / eighth) % line.Length;
                double local = t % eighth;
                phase += Hz(line[step]) / Rate;
                double saw = 2 * (phase % 1) - 1;
                filtered += 0.12 * (saw - filtered);
                double bass = filtered * Math.Exp(-local / 0.18) * 0.32;
                double beatLocal = t % 0.5;
                double kick = Kick(beatLocal, 50) * 0.45;
                double hat = (t % 0.5) > 0.25 ? (random.NextDouble() * 2 - 1) * Math.Exp(-((t % 0.25)) / 0.02) * 0.08 : 0;
                double pad = (Math.Sin(Tau * Hz(62) * t) + Math.Sin(Tau * Hz(65) * t) + Math.Sin(Tau * Hz(69) * t)) * 0.025 * (0.6 + 0.4 * Math.Sin(Tau * t / length));
                data[i] = (float)(bass + kick + hat + pad);
            }
            return data;
        }

        // 升空成功：定音鼓一击后，D 大三和弦由弱渐强，管乐般明亮。
        private static float[] Triumph()
        {
            double length = Seconds(MusicTrack.Triumph);
            int[] chord = { 50, 54, 57, 62, 66, 69 };
            var data = new float[(int)(length * Rate)];
            double[] filters = new double[chord.Length], phases = new double[chord.Length];
            for (int i = 0; i < data.Length; i++)
            {
                double t = (double)i / Rate;
                double env = Math.Min(1, t / 1.6) * Math.Min(1, (length - t) / 2.2);
                double sum = 0;
                for (int n = 0; n < chord.Length; n++)
                {
                    phases[n] += Hz(chord[n]) * (1 + 0.002 * Math.Sin(Tau * 5 * t + n)) / Rate;
                    double saw = 2 * (phases[n] % 1) - 1;
                    filters[n] += (0.04 + 0.12 * env) * (saw - filters[n]);
                    sum += filters[n];
                }
                data[i] = (float)(sum / chord.Length * env * 0.6 + Kick(t, 45) * 0.6);
            }
            return data;
        }

        // 合成弦乐：一对略微失谐的正弦加二次谐波，频率取为循环长度内的整数周期，循环无断点。
        private static double Pad(double t, double frequency, double loopSeconds)
        {
            double f1 = Math.Round(frequency * loopSeconds) / loopSeconds, f2 = Math.Round(frequency * 1.003 * loopSeconds) / loopSeconds;
            return Math.Sin(Tau * f1 * t) * 0.5 + Math.Sin(Tau * f2 * t) * 0.35 + Math.Sin(Tau * f1 * 2 * t) * 0.12;
        }

        private static void Bell(float[] data, double start, double frequency, double amplitude, double decay)
        {
            int begin = (int)(start * Rate), count = (int)(decay * 3 * Rate);
            for (int i = 0; i < count; i++)
            {
                int index = (begin + i) % data.Length;
                double t = (double)i / Rate;
                double env = Math.Min(1, t / 0.004) * Math.Exp(-t / decay);
                data[index] += (float)((Math.Sin(Tau * frequency * t) + 0.3 * Math.Sin(Tau * frequency * 2.76 * t) * Math.Exp(-t / 0.3)) * env * amplitude);
            }
        }

        // 底鼓：频率快速下滑的正弦。
        private static double Kick(double local, int midi)
        {
            if (local < 0 || local > 0.35) return 0;
            double f = Hz(midi) * (1 + 3 * Math.Exp(-local / 0.03));
            return Math.Sin(Tau * f * local) * Math.Exp(-local / 0.12);
        }

        // 环形回声：延迟线首尾相接，循环点不会截断尾音。
        private static void Echo(float[] data, double delay, double feedback)
        {
            int d = (int)(delay * Rate);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < data.Length; i++)
                    data[i] += (float)(data[(i - d + data.Length) % data.Length] * feedback * 0.5);
        }
    }
}
