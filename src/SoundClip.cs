using System.Text;

namespace CodexMonitor;

public sealed record SoundClip(int SampleRate, ushort Channels, byte[] Pcm)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public double Seconds => Pcm.Length / (double)(SampleRate * Channels * 2);

    public static SoundClip Synthesize(SoundTone tone)
    {
        const int rate = 44100;
        var notes = tone switch
        {
            SoundTone.Glass => new[] { (0d, 880d, 0.38d), (0.12d, 1320d, 0.24d) },
            SoundTone.Droplet => new[] { (0d, 660d, 0.28d), (0.18d, 990d, 0.36d) },
            SoundTone.Velvet => new[] { (0d, 440d, 0.42d), (0.16d, 330d, 0.4d) },
            _ => Array.Empty<(double, double, double)>(),
        };
        var pcm = new byte[(int)(0.65 * rate) * 2];
        for (var i = 0; i < pcm.Length / 2; i++)
        {
            var time = i / (double)rate;
            double sample = 0;
            foreach (var (start, frequency, duration) in notes)
            {
                var t = time - start;
                if (t < 0 || t > duration) continue;
                var envelope = Math.Min(t / 0.012, 1) * Math.Exp(-t * 10) * Math.Min((duration - t) / 0.04, 1);
                sample += Math.Sin(2 * Math.PI * frequency * t) * envelope * 0.32;
            }
            var value = (short)(Math.Clamp(sample, -0.8, 0.8) * short.MaxValue);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), value);
        }
        return new SoundClip(rate, 1, pcm);
    }

    public SoundClip AtVolume(float volume)
    {
        var gain = NotificationGeometry.FiniteClamp(volume, 0, 1, 0);
        var copy = new byte[Pcm.Length];
        for (var i = 0; i < copy.Length; i += 2)
        {
            var value = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(Pcm.AsSpan(i));
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(copy.AsSpan(i), (short)(value * gain));
        }
        return this with { Pcm = copy };
    }

    public static SoundClip ReadWave(byte[] wav)
    {
        if (wav.Length < 44 || wav.Length > MaxBytes || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF"
            || Encoding.ASCII.GetString(wav, 8, 4) != "WAVE") throw new InvalidDataException("Fichier WAV invalide ou trop volumineux (2 Mo maximum).");
        using var reader = new BinaryReader(new MemoryStream(wav));
        reader.BaseStream.Position = 12;
        int rate = 0; ushort channels = 0; byte[]? pcm = null;
        while (reader.BaseStream.Position + 8 <= wav.Length)
        {
            var tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var length = reader.ReadUInt32();
            var start = reader.BaseStream.Position;
            if (length > wav.Length - start) throw new InvalidDataException("Bloc WAV tronqué.");
            if (tag == "fmt ")
            {
                if (length < 16 || reader.ReadUInt16() != 1) throw new InvalidDataException("Utiliser un WAV PCM 16 bits, mono ou stéréo.");
                channels = reader.ReadUInt16(); rate = reader.ReadInt32();
                var bytesPerSecond = reader.ReadInt32(); var align = reader.ReadUInt16(); var bits = reader.ReadUInt16();
                if (channels is < 1 or > 2 || rate is < 8000 or > 96000 || bits != 16 || align != channels * 2 || bytesPerSecond != rate * channels * 2)
                    throw new InvalidDataException("Utiliser un WAV PCM 16 bits, mono ou stéréo (8 à 96 kHz).");
            }
            else if (tag == "data") pcm = reader.ReadBytes((int)length);
            reader.BaseStream.Position = start + length + (length % 2);
        }
        if (rate == 0 || pcm is null || pcm.Length == 0 || pcm.Length % (channels * 2) != 0)
            throw new InvalidDataException("Données audio WAV manquantes ou incomplètes.");
        var clip = new SoundClip(rate, channels, pcm);
        if (clip.Seconds > 3) throw new InvalidDataException("Le son doit durer au maximum 3 secondes.");
        return clip;
    }

    public byte[] ToWave()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + Pcm.Length); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((ushort)1); writer.Write(Channels); writer.Write(SampleRate); writer.Write(SampleRate * Channels * 2);
        writer.Write((ushort)(Channels * 2)); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(Pcm.Length); writer.Write(Pcm);
        return stream.ToArray();
    }
}
