using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SurvivalRts.Editor
{
    // Tổng hợp âm thanh tạm (WAV 16-bit mono) bằng code để test hệ thống âm thanh khi chưa có âm thanh thật.
    // File nằm ngoài Assets (thư mục FMOD/SourceAudio cạnh project Unity) để FMOD Studio import, Unity không import.
    public static class PlaceholderAudioGenerator
    {
        const int SampleRate = 44100;
        const float PeakLevel = 0.89f; // khoảng -1 dBFS

        public static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "FMOD", "SourceAudio"));

        [MenuItem("SurvivalRts/Âm thanh/Tạo âm thanh tạm (WAV)")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutputDirectory);
            int count = 0;

            for (int i = 0; i < 4; i++) count += Save("Enemy", $"Enemy_Hit_{i + 1:00}", Hit(0.9f + i * 0.07f, 100 + i));
            for (int i = 0; i < 3; i++) count += Save("Enemy", $"Enemy_Death_{i + 1:00}", Death(0.9f + i * 0.1f, 200 + i));
            for (int i = 0; i < 3; i++) count += Save("Tower", $"Tower_Shot_{i + 1:00}", Shot(0.92f + i * 0.08f, 300 + i));
            count += Save("Building", "Building_Collapse_01", Collapse(400));
            count += Save("Ambience", "Crowd_March_Loop", MarchLoop(500));
            count += Save("Ambience", "Battle_Loop", BattleLoop(600));
            count += Save("UI", "UI_Click", Click());
            count += Save("UI", "UI_Place", Place(700));
            count += Save("UI", "UI_Open", Chime(true));
            count += Save("UI", "UI_Close", Chime(false));
            count += Save("UI", "Alert_HQ", Alert());
            count += Save("Music", "Music_Calm_Loop", MusicCalm());
            count += Save("Music", "Music_Battle_Loop", MusicBattle(800));
            for (int i = 0; i < 3; i++) count += Save("Building", $"Builder_Hammer_{i + 1:00}", Hammer(0.92f + i * 0.08f, 900 + i));

            Debug.Log($"Đã tạo {count} file WAV tại {OutputDirectory}");
        }

        // ---------- Các âm thanh ----------

        // Gậy đập trúng: tiếng thịch trầm + tiếng va chạm ngắn.
        static float[] Hit(float pitch, int seed)
        {
            var rng = new System.Random(seed);
            var buffer = new float[Samples(0.22f)];
            float lowpass = 0f;
            for (int n = 0; n < buffer.Length; n++)
            {
                float t = n / (float)SampleRate;
                float freq = Mathf.Lerp(120f, 55f, Mathf.Clamp01(t / 0.12f)) * pitch;
                float thump = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 28f);
                lowpass = OnePole(lowpass, Noise(rng), 0.25f);
                float crack = lowpass * Mathf.Exp(-t * 55f);
                buffer[n] = thump * 0.8f + crack * 0.9f;
            }
            return buffer;
        }

        // Tiếng kêu rồi ngã: âm "ặc" trượt xuống + tiếng thân người chạm đất.
        static float[] Death(float pitch, int seed)
        {
            var rng = new System.Random(seed);
            var buffer = new float[Samples(0.75f)];
            float phase = 0f;
            float lowpass = 0f;
            for (int n = 0; n < buffer.Length; n++)
            {
                float t = n / (float)SampleRate;
                float freq = Mathf.Lerp(190f, 75f, Mathf.Clamp01(t / 0.4f)) * pitch * (1f + 0.03f * Mathf.Sin(t * 40f));
                phase += freq / SampleRate;
                float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
                float voice = saw * Envelope(t, 0.015f, 0.4f) * 0.45f;

                float tt = t - 0.48f;
                lowpass = OnePole(lowpass, Noise(rng), 0.06f);
                float thud = tt > 0f ? (lowpass * 2.5f + Mathf.Sin(2f * Mathf.PI * 60f * tt) * 0.7f) * Mathf.Exp(-tt * 22f) : 0f;
                buffer[n] = voice + thud;
            }
            return buffer;
        }

        // Tháp bắn: tiếng nổ đanh có lực trầm + tiếng vút đạn rõ ràng.
        static float[] Shot(float pitch, int seed)
        {
            var rng = new System.Random(seed);
            var buffer = new float[Samples(0.42f)];
            float lowpass = 0f;
            float tail = 0f;
            for (int n = 0; n < buffer.Length; n++)
            {
                float t = n / (float)SampleRate;
                float noise = Noise(rng);
                lowpass = OnePole(lowpass, noise, 0.35f);
                tail = OnePole(tail, noise, 0.09f);
                float bodyFreq = Mathf.Lerp(280f, 75f, Mathf.Clamp01(t / 0.14f)) * pitch;
                float body = Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * Mathf.Exp(-t * 18f);
                float snapFreq = Mathf.Lerp(1200f, 260f, Mathf.Clamp01(t / 0.06f)) * pitch;
                float snap = Mathf.Sin(2f * Mathf.PI * snapFreq * t) * Mathf.Exp(-t * 35f);
                buffer[n] = body * 1.1f + snap * 0.8f + lowpass * Mathf.Exp(-t * 28f) * 0.9f + tail * Mathf.Exp(-t * 9f) * 0.55f;
            }
            return buffer;
        }

        // Thợ xây gõ búa đóng gỗ/đinh: tiếng búa gõ đanh trên thớt gỗ kèm nhịp dội ngắn.
        static float[] Hammer(float pitch, int seed)
        {
            var rng = new System.Random(seed);
            var buffer = new float[Samples(0.32f)];
            float wood = 0f;
            for (int n = 0; n < buffer.Length; n++)
            {
                float t = n / (float)SampleRate;
                wood = OnePole(wood, Noise(rng), 0.28f);
                float woodTone = Mathf.Sin(2f * Mathf.PI * 340f * pitch * t) * Mathf.Exp(-t * 26f);
                float metalTap = Mathf.Sin(2f * Mathf.PI * 1450f * pitch * t) * Mathf.Exp(-t * 55f);
                float knock = wood * Mathf.Exp(-t * 42f);
                buffer[n] = woodTone * 0.95f + metalTap * 0.65f + knock * 0.85f;
            }
            return buffer;
        }

        // Công trình sập: tiếng ầm trầm kéo dài + tiếng gãy lách cách.
        static float[] Collapse(int seed)
        {
            var rng = new System.Random(seed);
            var buffer = new float[Samples(1.8f)];
            float rumble = 0f;
            for (int n = 0; n < buffer.Length; n++)
            {
                float t = n / (float)SampleRate;
                rumble = OnePole(rumble, Noise(rng), 0.02f);
                float body = rumble * 3.5f * Envelope(t, 0.05f, 1.6f) * (0.7f + 0.3f * Mathf.Sin(t * 9f));
                buffer[n] = body;
            }
            // Tiếng gãy rải ngẫu nhiên trong 1 giây đầu.
            for (int k = 0; k < 18; k++)
            {
                int start = Samples((float)rng.NextDouble() * 1.0f);
                AddImpulse(buffer, start, 0.06f, 0.35f + (float)rng.NextDouble() * 0.4f, rng, 0.5f, false);
            }
            return buffer;
        }

        // Loop bước chân đám đông: nhiều tiếng thịch nhỏ rải ngẫu nhiên, nối vòng liền mạch.
        static float[] MarchLoop(int seed)
        {
            var rng = new System.Random(seed);
            var buffer = new float[Samples(2f)];
            for (int k = 0; k < 70; k++)
            {
                int start = rng.Next(buffer.Length);
                AddImpulse(buffer, start, 0.09f, 0.25f + (float)rng.NextDouble() * 0.35f, rng, 0.05f, true);
            }
            return buffer;
        }

        // Loop giao tranh: tiếng đập dày đặc + nền ầm ì, nối vòng liền mạch.
        static float[] BattleLoop(int seed)
        {
            var rng = new System.Random(seed);
            var buffer = new float[Samples(3f)];
            float rumble = 0f;
            for (int n = 0; n < buffer.Length; n++)
            {
                rumble = OnePole(rumble, Noise(rng), 0.03f);
                buffer[n] = rumble * 0.8f;
            }
            for (int k = 0; k < 90; k++)
            {
                int start = rng.Next(buffer.Length);
                AddImpulse(buffer, start, 0.15f, 0.3f + (float)rng.NextDouble() * 0.5f, rng, 0.2f, true);
            }
            // Nối hai đầu nền ầm ì cho khớp khi lặp.
            CrossfadeEnds(buffer, Samples(0.1f));
            return buffer;
        }

        static float[] Click()
        {
            var buffer = new float[Samples(0.06f)];
            for (int n = 0; n < buffer.Length; n++)
            {
                float t = n / (float)SampleRate;
                buffer[n] = Mathf.Sin(2f * Mathf.PI * 1300f * t) * Mathf.Exp(-t * 80f);
            }
            return buffer;
        }

        // Đặt công trình: tiếng gõ gỗ hai nhịp.
        static float[] Place(int seed)
        {
            var rng = new System.Random(seed);
            var buffer = new float[Samples(0.3f)];
            for (int n = 0; n < buffer.Length; n++)
            {
                float t = n / (float)SampleRate;
                float a = Mathf.Sin(2f * Mathf.PI * 320f * t) * Mathf.Exp(-t * 35f);
                float tb = t - 0.09f;
                float b = tb > 0f ? Mathf.Sin(2f * Mathf.PI * 230f * tb) * Mathf.Exp(-tb * 30f) : 0f;
                buffer[n] = a * 0.7f + b * 0.7f + Noise(rng) * Mathf.Exp(-t * 120f) * 0.3f;
            }
            return buffer;
        }

        // Cảnh báo Nhà chính bị tấn công: hai nốt luân phiên.
        static float[] Alert()
        {
            var buffer = new float[Samples(0.9f)];
            for (int n = 0; n < buffer.Length; n++)
            {
                float t = n / (float)SampleRate;
                int step = (int)(t / 0.22f);
                float local = t - step * 0.22f;
                float freq = step % 2 == 0 ? 880f : 660f;
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                buffer[n] = tone * Envelope(local, 0.01f, 0.2f) * 0.6f;
            }
            return buffer;
        }

        // Mở/đóng menu: hai nốt ngắn đi lên (mở) hoặc đi xuống (đóng).
        static float[] Chime(bool rising)
        {
            var buffer = new float[Samples(0.35f)];
            float first = rising ? 660f : 990f;
            float second = rising ? 990f : 660f;
            AddNote(buffer, 0f, 0.2f, first, 0.5f, false);
            AddNote(buffer, 0.09f, 0.25f, second, 0.5f, false);
            return buffer;
        }

        // Nhạc yên lúc xây dựng: 4 hợp âm (Am - F - C - G), mỗi hợp âm 4 giây, nền pad êm và chuỗi nốt rải nhẹ. Lặp liền 16 giây.
        static float[] MusicCalm()
        {
            const float chordLength = 4f;
            var buffer = new float[Samples(chordLength * 4f)];
            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f },
                new[] { 174.61f, 220.00f, 261.63f },
                new[] { 130.81f, 164.81f, 196.00f },
                new[] { 196.00f, 246.94f, 293.66f },
            };

            for (int c = 0; c < chords.Length; c++)
            {
                float start = c * chordLength;
                foreach (float f in chords[c])
                {
                    AddPad(buffer, start, chordLength + 0.8f, f, 0.12f);
                    AddPad(buffer, start, chordLength + 0.8f, f * 1.003f, 0.08f);
                }
                AddPad(buffer, start, chordLength + 0.8f, chords[c][0] * 0.5f, 0.14f);

                // Rải nốt theo nhịp nửa giây: 1-3-5-8 của hợp âm.
                float[] arpeggio = { chords[c][0] * 2f, chords[c][1] * 2f, chords[c][2] * 2f, chords[c][0] * 4f };
                for (int step = 0; step < 8; step++)
                    AddNote(buffer, start + step * 0.5f, 0.9f, arpeggio[step % 4], 0.07f, true);
            }
            return buffer;
        }

        // Nhạc trận: nhịp 120 BPM, 4 ô nhịp (8 giây): trống trầm, trống lẫy, tiếng bass lặp và nền u ám. Lặp liền.
        static float[] MusicBattle(int seed)
        {
            var rng = new System.Random(seed);
            const float beat = 0.5f;
            var buffer = new float[Samples(beat * 16f)];

            for (int b = 0; b < 16; b++)
            {
                float t = b * beat;
                AddKick(buffer, t, 0.9f);
                if (b % 2 == 1) AddKick(buffer, t + beat * 0.5f, 0.5f);
                if (b % 4 == 2) AddSnare(buffer, t, 0.6f, rng);
                // Dồn trống ở ô nhịp cuối để báo hiệu vòng lặp mới.
                if (b >= 14) AddSnare(buffer, t + beat * 0.5f, 0.45f, rng);
            }

            // Bass lặp theo nốt móc: D - D - F - D - C - D - A - D (quãng thấp).
            float[] bass = { 73.42f, 73.42f, 87.31f, 73.42f, 65.41f, 73.42f, 110.00f, 73.42f };
            for (int i = 0; i < 32; i++)
                AddBass(buffer, i * beat * 0.5f, beat * 0.45f, bass[i % bass.Length], 0.35f);

            // Nền u ám: quãng năm D - A kéo dài suốt đoạn.
            AddPad(buffer, 0f, beat * 16f, 146.83f, 0.08f);
            AddPad(buffer, 0f, beat * 16f, 220.00f, 0.06f);
            return buffer;
        }

        // Nốt có tiếng gảy (pluck) hoặc tiếng chuông đơn giản; wrap = true thì đuôi nốt vượt cuối buffer quay về đầu.
        static void AddNote(float[] buffer, float start, float length, float freq, float gain, bool wrap)
        {
            int begin = Samples(start);
            int count = Samples(length);
            for (int i = 0; i < count; i++)
            {
                int index = begin + i;
                if (index >= buffer.Length)
                {
                    if (!wrap) break;
                    index -= buffer.Length;
                }
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 6f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                buffer[index] += tone * env * gain;
            }
        }

        // Pad êm: vào và ra chậm, luôn quay vòng về đầu buffer để nối lặp liền mạch.
        static void AddPad(float[] buffer, float start, float length, float freq, float gain)
        {
            int begin = Samples(start);
            int count = Samples(length);
            const float attack = 0.8f;
            const float release = 1.2f;
            for (int i = 0; i < count; i++)
            {
                int index = (begin + i) % buffer.Length;
                float t = i / (float)SampleRate;
                float env = Mathf.Clamp01(t / attack) * Mathf.Clamp01((length - t) / release);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t + 0.5f);
                buffer[index] += tone * env * gain;
            }
        }

        static void AddKick(float[] buffer, float start, float gain)
        {
            int begin = Samples(start);
            int count = Samples(0.35f);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                int index = (begin + i) % buffer.Length;
                float t = i / (float)SampleRate;
                float freq = Mathf.Lerp(150f, 45f, Mathf.Clamp01(t / 0.08f));
                phase += freq / SampleRate;
                buffer[index] += Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 12f) * gain;
            }
        }

        static void AddSnare(float[] buffer, float start, float gain, System.Random rng)
        {
            int begin = Samples(start);
            int count = Samples(0.25f);
            float previous = 0f;
            for (int i = 0; i < count; i++)
            {
                int index = (begin + i) % buffer.Length;
                float t = i / (float)SampleRate;
                float noise = Noise(rng);
                float bright = noise - previous * 0.6f;
                previous = noise;
                float body = Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t * 30f);
                buffer[index] += (bright * Mathf.Exp(-t * 18f) * 0.7f + body * 0.5f) * gain;
            }
        }

        static void AddBass(float[] buffer, float start, float length, float freq, float gain)
        {
            int begin = Samples(start);
            int count = Samples(length);
            float phase = 0f;
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                int index = (begin + i) % buffer.Length;
                float t = i / (float)SampleRate;
                phase += freq / SampleRate;
                float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
                filtered = OnePole(filtered, saw, 0.12f);
                float env = Mathf.Min(1f, t / 0.005f) * Mathf.Clamp01((length - t) / 0.03f);
                buffer[index] += filtered * env * gain;
            }
        }

        // ---------- Tiện ích ----------

        // Tiếng va chạm ngắn tại vị trí start; wrap = true thì phần vượt quá cuối buffer quay về đầu (để loop liền).
        static void AddImpulse(float[] buffer, int start, float length, float gain, System.Random rng, float lowpassAmount, bool wrap)
        {
            int count = Samples(length);
            float filtered = 0f;
            float freq = 60f + (float)rng.NextDouble() * 60f;
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index >= buffer.Length)
                {
                    if (!wrap) break;
                    index -= buffer.Length;
                }
                float t = i / (float)SampleRate;
                filtered = OnePole(filtered, Noise(rng), lowpassAmount + 0.05f);
                float env = Mathf.Exp(-t * 40f);
                buffer[index] += (filtered * 1.5f + Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f) * env * gain;
            }
        }

        static void CrossfadeEnds(float[] buffer, int length)
        {
            for (int i = 0; i < length; i++)
            {
                float u = i / (float)length;
                int tail = buffer.Length - length + i;
                float mixed = buffer[tail] * (1f - u) + buffer[i] * u;
                buffer[i] = mixed;
            }
        }

        static float Envelope(float t, float attack, float decay)
        {
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / decay * 3f);
        }

        static float OnePole(float state, float input, float amount)
        {
            return state + (input - state) * amount;
        }

        static float Noise(System.Random rng)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        static int Samples(float seconds)
        {
            return Mathf.CeilToInt(seconds * SampleRate);
        }

        static int Save(string folder, string fileName, float[] samples)
        {
            float peak = 0f;
            foreach (var s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            float gain = peak > 0f ? PeakLevel / peak : 1f;

            // Fade 2 ms ở hai đầu cho âm thanh không lặp, tránh tiếng tách.
            bool isLoop = fileName.EndsWith("_Loop", StringComparison.Ordinal);
            int fade = isLoop ? 0 : Samples(0.002f);

            var dir = Path.Combine(OutputDirectory, folder);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName + ".wav");
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                for (int i = 0; i < samples.Length; i++)
                {
                    float s = samples[i] * gain;
                    if (fade > 0)
                    {
                        if (i < fade) s *= i / (float)fade;
                        int fromEnd = samples.Length - 1 - i;
                        if (fromEnd < fade) s *= fromEnd / (float)fade;
                    }
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), short.MinValue, short.MaxValue));
                }
            }
            return 1;
        }
    }
}
