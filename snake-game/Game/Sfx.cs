using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace snake_game.Game
{
    public enum SoundId
    {
        Eat,
        Golden,
        Shrink,
        Dash,
        Death,
        LevelUp,
        Start
    }

    /// <summary>
    /// Tiny synthesized sound effects, generated as PCM wave data at startup. Every
    /// playback path is wrapped so a missing audio device can never break the game.
    /// </summary>
    internal static class Sfx
    {
        public static bool Muted;

        private static readonly byte[] EatSound = Build(new double[] { 880, 1180 }, 0.055, 0.45);
        private static readonly byte[] GoldenSound = Build(new double[] { 660, 880, 1320, 1760 }, 0.055, 0.40);
        private static readonly byte[] ShrinkSound = Build(new double[] { 520, 380 }, 0.08, 0.40);
        private static readonly byte[] DashSound = Build(new double[] { 320, 480, 720 }, 0.045, 0.36);
        private static readonly byte[] DeathSound = Build(new double[] { 420, 330, 250, 170, 110 }, 0.08, 0.45);
        private static readonly byte[] LevelUpSound = Build(new double[] { 784, 988, 1319 }, 0.06, 0.36);
        private static readonly byte[] StartSound = Build(new double[] { 523, 659, 784, 1047 }, 0.055, 0.40);

        public static void Play(SoundId id)
        {
            if (Muted) return;

            byte[] data = Select(id);
            if (data == null) return;

            try
            {
                Task.Run(delegate
                {
                    try
                    {
                        using (MemoryStream stream = new MemoryStream(data))
                        using (SoundPlayer player = new SoundPlayer(stream))
                        {
                            player.PlaySync();
                        }
                    }
                    catch
                    {
                        // Audio is optional polish.
                    }
                });
            }
            catch
            {
                // Ignore scheduler failures.
            }
        }

        private static byte[] Select(SoundId id)
        {
            switch (id)
            {
                case SoundId.Eat: return EatSound;
                case SoundId.Golden: return GoldenSound;
                case SoundId.Shrink: return ShrinkSound;
                case SoundId.Dash: return DashSound;
                case SoundId.Death: return DeathSound;
                case SoundId.LevelUp: return LevelUpSound;
                case SoundId.Start: return StartSound;
                default: return null;
            }
        }

        private static byte[] Build(double[] notes, double noteSeconds, double volume)
        {
            const int sampleRate = 22050;
            int perNote = (int)(sampleRate * noteSeconds);
            int totalSamples = perNote * notes.Length;
            int dataSize = totalSamples * 2;
            byte[] wav = new byte[44 + dataSize];

            WriteAscii(wav, 0, "RIFF");
            WriteInt(wav, 4, 36 + dataSize);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteInt(wav, 16, 16);
            WriteShort(wav, 20, 1);
            WriteShort(wav, 22, 1);
            WriteInt(wav, 24, sampleRate);
            WriteInt(wav, 28, sampleRate * 2);
            WriteShort(wav, 32, 2);
            WriteShort(wav, 34, 16);
            WriteAscii(wav, 36, "data");
            WriteInt(wav, 40, dataSize);

            int index = 44;
            for (int n = 0; n < notes.Length; n++)
            {
                double frequency = notes[n];
                for (int s = 0; s < perNote; s++)
                {
                    double t = (double)s / sampleRate;
                    double envelope = 1.0;
                    double attack = 0.004;
                    if (t < attack) envelope = t / attack;
                    double remaining = noteSeconds - t;
                    if (remaining < 0.012) envelope *= Math.Max(0.0, remaining / 0.012);

                    double wave = Math.Sin(2 * Math.PI * frequency * t) * 0.7
                                + Math.Sin(4 * Math.PI * frequency * t) * 0.18;
                    short value = (short)(wave * envelope * volume * 32000);
                    wav[index++] = (byte)(value & 0xFF);
                    wav[index++] = (byte)((value >> 8) & 0xFF);
                }
            }
            return wav;
        }

        private static void WriteAscii(byte[] buffer, int offset, string text)
        {
            for (int i = 0; i < text.Length; i++) buffer[offset + i] = (byte)text[i];
        }

        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteShort(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
