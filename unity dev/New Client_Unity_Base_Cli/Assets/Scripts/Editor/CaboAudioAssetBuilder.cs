using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cabo.Client.Editor
{
    public static class CaboAudioAssetBuilder
    {
        public const string AudioFolder = "Assets/Art/Audio/SFX";
        public const string BgmFolder = "Assets/Art/Audio/BGM";
        public const string BgmPath = BgmFolder + "/sweet_shop_loop.wav";
        const int SampleRate = 44100;

        static readonly string[] ClipNames =
        {
            "draw", "flip", "discard", "swap", "skill", "cabo", "eat", "penalty", "victory"
        };

        [MenuItem("Cabo/Art/Generate Original SFX")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(ToFullPath(AudioFolder));
            WriteDraw();
            WriteFlip();
            WriteDiscard();
            WriteSwap();
            WriteSkill();
            if (!File.Exists(ToFullPath(PathFor("cabo"))))
                WriteCabo();
            WriteEat();
            WritePenalty();
            WriteVictory();
            EnsureBgmAsset();
            AssetDatabase.Refresh();
            ConfigureImporters();
            Debug.Log("[CaboAudio] Generated 9 original kitchen-table SFX clips and one original BGM loop.");
        }

        public static void EnsureAudioAssets()
        {
            for (int i = 0; i < ClipNames.Length; i++)
            {
                if (!File.Exists(ToFullPath(PathFor(ClipNames[i]))))
                {
                    GenerateAll();
                    return;
                }
            }
            EnsureBgmAsset();
            ConfigureImporters();
        }

        public static void EnsureBgmAsset()
        {
            Directory.CreateDirectory(ToFullPath(BgmFolder));
            if (!File.Exists(ToFullPath(BgmPath)))
                WriteSweetShopBgm();
        }

        public static string PathFor(string clipName)
        {
            return $"{AudioFolder}/{clipName}.wav";
        }

        static void WriteDraw()
        {
            const float duration = 0.30f;
            Write("draw", duration, t =>
            {
                float p = t / duration;
                float f = Mathf.Lerp(420f, 920f, p);
                return Envelope(t, duration, 0.012f, 0.11f) *
                    (0.48f * Sine(f, t) + 0.20f * Sine(f * 1.52f, t));
            });
        }

        static void WriteFlip()
        {
            const float duration = 0.16f;
            var noise = new System.Random(1402);
            Write("flip", duration, t =>
            {
                float click = (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 46f);
                float paper = Sine(1150f - t * 1600f, t) * Mathf.Exp(-t * 24f);
                return 0.42f * click + 0.38f * paper;
            });
        }

        static void WriteDiscard()
        {
            const float duration = 0.26f;
            var noise = new System.Random(2704);
            Write("discard", duration, t =>
            {
                float thud = Sine(150f - t * 180f, t) * Mathf.Exp(-t * 14f);
                float paper = (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 28f);
                return 0.62f * thud + 0.20f * paper;
            });
        }

        static void WriteSwap()
        {
            const float duration = 0.46f;
            Write("swap", duration, t =>
            {
                float p = t / duration;
                float left = Sine(Mathf.Lerp(760f, 330f, p), t);
                float right = Sine(Mathf.Lerp(330f, 820f, p), t);
                return Envelope(t, duration, 0.02f, 0.12f) * (0.28f * left + 0.28f * right);
            });
        }

        static void WriteSkill()
        {
            const float duration = 0.66f;
            float[] notes = { 660f, 830f, 990f, 1320f };
            Write("skill", duration, t =>
            {
                int index = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / 0.13f));
                float local = t - index * 0.13f;
                float bell = Sine(notes[index], t) + 0.32f * Sine(notes[index] * 2f, t);
                return 0.30f * bell * Mathf.Exp(-local * 7f) * FadeOut(t, duration, 0.16f);
            });
        }

        static void WriteCabo()
        {
            const float duration = 0.82f;
            float[] notes = { 392f, 523.25f, 659.25f };
            Write("cabo", duration, t =>
            {
                int index = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / 0.22f));
                float local = t - index * 0.22f;
                float note = Sine(notes[index], t) + 0.35f * Sine(notes[index] * 1.5f, t);
                return 0.40f * note * Mathf.Exp(-local * 3.4f) * FadeOut(t, duration, 0.20f);
            });
        }

        static void WriteEat()
        {
            const float duration = 0.30f;
            var noise = new System.Random(8811);
            Write("eat", duration, t =>
            {
                float crunch = (float)(noise.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 18f);
                float bite = Sine(330f + 180f * Mathf.Sin(t * 30f), t) * Mathf.Exp(-t * 10f);
                return 0.28f * crunch + 0.34f * bite;
            });
        }

        static void WritePenalty()
        {
            const float duration = 0.58f;
            Write("penalty", duration, t =>
            {
                float p = t / duration;
                float frequency = Mathf.Lerp(430f, 145f, p);
                float wobble = 1f + 0.08f * Mathf.Sin(t * 42f);
                return 0.48f * Sine(frequency * wobble, t) * Envelope(t, duration, 0.01f, 0.14f);
            });
        }

        static void WriteVictory()
        {
            const float duration = 1.28f;
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            Write("victory", duration, t =>
            {
                int index = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / 0.24f));
                float local = t - index * 0.24f;
                float chord = Sine(notes[index], t)
                    + 0.30f * Sine(notes[index] * 1.25f, t)
                    + 0.22f * Sine(notes[index] * 2f, t);
                return 0.34f * chord * Mathf.Exp(-local * 2.2f) * FadeOut(t, duration, 0.28f);
            });
        }

        static void WriteSweetShopBgm()
        {
            const float duration = 16f;
            float[] melody =
            {
                659.25f, 783.99f, 880f, 783.99f, 659.25f, 587.33f, 523.25f, 587.33f,
                659.25f, 783.99f, 987.77f, 880f, 783.99f, 659.25f, 587.33f, 523.25f,
                587.33f, 659.25f, 783.99f, 659.25f, 587.33f, 523.25f, 493.88f, 523.25f,
                659.25f, 587.33f, 523.25f, 493.88f, 523.25f, 587.33f, 659.25f, 523.25f
            };
            float[] bass = { 130.81f, 110f, 146.83f, 98f, 130.81f, 110f, 146.83f, 98f };

            WritePath(BgmPath, duration, t =>
            {
                int melodyIndex = Mathf.Min(melody.Length - 1, Mathf.FloorToInt(t / 0.5f));
                float melodyLocal = t - melodyIndex * 0.5f;
                float melodyEnvelope = NoteEnvelope(melodyLocal, 0.5f, 0.035f, 0.12f);
                float mallet = Sine(melody[melodyIndex], t)
                    + 0.24f * Sine(melody[melodyIndex] * 2f, t)
                    + 0.10f * Sine(melody[melodyIndex] * 3f, t);

                int bassIndex = Mathf.Min(bass.Length - 1, Mathf.FloorToInt(t / 2f));
                float bassLocal = t - bassIndex * 2f;
                float bassEnvelope = NoteEnvelope(bassLocal, 1.7f, 0.08f, 0.32f);
                float warmBass = Sine(bass[bassIndex], t) + 0.18f * Sine(bass[bassIndex] * 2f, t);

                float beatLocal = t % 0.5f;
                float softBeat = Sine(92f - beatLocal * 36f, t) * Mathf.Exp(-beatLocal * 17f);
                return 0.19f * mallet * melodyEnvelope
                    + 0.12f * warmBass * bassEnvelope
                    + 0.035f * softBeat;
            });
        }

        static void Write(string clipName, float duration, Func<float, float> signal)
        {
            WritePath(PathFor(clipName), duration, signal);
        }

        static void WritePath(string assetPath, float duration, Func<float, float> signal)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            var samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = Mathf.Clamp(signal(i / (float)SampleRate), -0.94f, 0.94f);
                samples[i] = (short)Mathf.RoundToInt(sample * short.MaxValue);
            }

            string fullPath = ToFullPath(assetPath);
            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * sizeof(short);
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * sizeof(short));
                writer.Write((short)sizeof(short));
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                for (int i = 0; i < samples.Length; i++)
                    writer.Write(samples[i]);
            }
        }

        static void ConfigureImporters()
        {
            AssetDatabase.Refresh();
            for (int i = 0; i < ClipNames.Length; i++)
            {
                var importer = AssetImporter.GetAtPath(PathFor(ClipNames[i])) as AudioImporter;
                if (importer == null)
                    continue;

                importer.forceToMono = true;
                importer.loadInBackground = false;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            var bgmImporter = AssetImporter.GetAtPath(BgmPath) as AudioImporter;
            if (bgmImporter != null)
            {
                bgmImporter.forceToMono = true;
                bgmImporter.loadInBackground = true;
                var settings = bgmImporter.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.72f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                bgmImporter.defaultSampleSettings = settings;
                bgmImporter.SaveAndReimport();
            }
        }

        static float Sine(float frequency, float time)
        {
            return Mathf.Sin(Mathf.PI * 2f * frequency * time);
        }

        static float Envelope(float time, float duration, float attack, float release)
        {
            float attackGain = Mathf.Clamp01(time / Mathf.Max(0.001f, attack));
            float releaseGain = Mathf.Clamp01((duration - time) / Mathf.Max(0.001f, release));
            return attackGain * releaseGain;
        }

        static float FadeOut(float time, float duration, float release)
        {
            return Mathf.Clamp01((duration - time) / Mathf.Max(0.001f, release));
        }

        static float NoteEnvelope(float time, float duration, float attack, float release)
        {
            if (time < 0f || time >= duration)
                return 0f;
            return Envelope(time, duration, attack, release);
        }

        static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
