using System;
using System.IO;
using System.Linq;
using System.Media;

namespace LePaSentinel;

/// <summary>Chimes synthesized in memory, so the app ships no audio files.</summary>
public static class Sounds
{
    const int Rate = 44100;

    // Rising two-note chime: soft and pleasant.
    static readonly SoundPlayer Done = Player(Synth(decay: 5.5,
        (659.25, 0.00, 0.45),
        (987.77, 0.12, 0.70)));

    // Three quick higher notes: meant to be noticed.
    static readonly SoundPlayer Waiting = Player(Synth(decay: 12,
        (880.00, 0.00, 0.14),
        (880.00, 0.17, 0.14),
        (1174.66, 0.34, 0.30)));

    public static void PlayDone() => Play(Done);
    public static void PlayWaiting() => Play(Waiting);

    static void Play(SoundPlayer player)
    {
        try { player.Play(); } catch { }
    }

    static SoundPlayer Player(byte[] wav)
    {
        var player = new SoundPlayer(new MemoryStream(wav));
        player.Load();
        return player;
    }

    static byte[] Synth(double decay, params (double Freq, double Start, double Duration)[] notes)
    {
        double total = notes.Max(n => n.Start + n.Duration) + 0.05;
        var samples = new double[(int)(total * Rate)];

        foreach (var (freq, start, duration) in notes)
        {
            int offset = (int)(start * Rate);
            int length = (int)(duration * Rate);
            for (int i = 0; i < length && offset + i < samples.Length; i++)
            {
                double t = (double)i / Rate;
                double attack = Math.Min(1, t / 0.006);
                double release = Math.Min(1, (duration - t) / 0.03);
                double envelope = attack * release * Math.Exp(-t * decay);
                double wave = Math.Sin(2 * Math.PI * freq * t)
                            + 0.30 * Math.Sin(2 * Math.PI * freq * 2 * t)
                            + 0.10 * Math.Sin(2 * Math.PI * freq * 3 * t);
                samples[offset + i] += wave * envelope;
            }
        }

        double peak = samples.Max(Math.Abs);
        double gain = peak > 0 ? 0.45 / peak : 0;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataBytes = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataBytes);
        foreach (var s in samples) w.Write((short)(s * gain * short.MaxValue));
        w.Flush();
        return ms.ToArray();
    }
}
