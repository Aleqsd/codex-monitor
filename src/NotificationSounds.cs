using System.Runtime.InteropServices;

namespace CodexMonitor;

internal sealed class NotificationSounds : IDisposable
{
    private readonly object gate = new();
    private readonly SoundGate policy = new();
    private CancellationTokenSource? playing;
    private bool quiet;
    private bool disposed;
    internal string? Error { get; private set; }

    internal void SetQuiet(bool value)
    {
        lock (gate) { quiet = value; if (value) playing?.Cancel(); }
    }

    internal void Play(string state, SoundOptions options, bool preview = false)
    {
        var (tone, file) = options.Select(state);
        var volume = options.Volume;
        CancellationTokenSource cancellation;
        lock (gate)
        {
            if (disposed || (playing != null && !preview) || !policy.Accept(options.Enabled, quiet, volume, tone, DateTimeOffset.UtcNow, preview)) return;
            playing?.Cancel();
            playing = cancellation = new CancellationTokenSource();
            Error = null;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                SoundClip clip;
                if (tone == SoundTone.Custom)
                {
                    if (string.IsNullOrWhiteSpace(file) || !Path.IsPathFullyQualified(file) || file.StartsWith(@"\\") || !file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Choisir le chemin absolu d’un fichier WAV sur ce PC.");
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length > SoundClip.MaxBytes) throw new InvalidDataException("Le fichier WAV dépasse 2 Mo.");
                    var bytes = new byte[(int)stream.Length];
                    await stream.ReadExactlyAsync(bytes, cancellation.Token);
                    clip = SoundClip.ReadWave(bytes);
                }
                else clip = SoundClip.Synthesize(tone);
                cancellation.Token.ThrowIfCancellationRequested();
                await PlayClip(clip.AtVolume(volume), cancellation.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Error = MonitorContract.Clean(ex.Message, "Lecture audio impossible.", 180); }
            finally
            {
                lock (gate) { if (ReferenceEquals(playing, cancellation)) playing = null; }
                cancellation.Dispose();
            }
        });
    }

    private static async Task PlayClip(SoundClip clip, CancellationToken cancellation)
    {
        var format = new WaveFormat { Format = 1, Channels = clip.Channels, Samples = (uint)clip.SampleRate,
            BytesPerSecond = (uint)(clip.SampleRate * clip.Channels * 2), BlockAlign = (ushort)(clip.Channels * 2), Bits = 16 };
        Check(waveOutOpen(out var device, uint.MaxValue, ref format, 0, 0, 0));
        nint data = 0, header = 0;
        var prepared = false;
        var size = (uint)Marshal.SizeOf<WaveHeader>();
        try
        {
            data = Marshal.AllocHGlobal(clip.Pcm.Length);
            Marshal.Copy(clip.Pcm, 0, data, clip.Pcm.Length);
            header = Marshal.AllocHGlobal((int)size);
            Marshal.StructureToPtr(new WaveHeader { Data = data, Length = (uint)clip.Pcm.Length }, header, false);
            Check(waveOutPrepareHeader(device, header, size)); prepared = true;
            cancellation.ThrowIfCancellationRequested();
            Check(waveOutWrite(device, header, size));
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            while ((Marshal.PtrToStructure<WaveHeader>(header).Flags & 1) == 0) await Task.Delay(15, deadline.Token);
        }
        finally
        {
            waveOutReset(device);
            if (prepared) waveOutUnprepareHeader(device, header, size);
            waveOutClose(device);
            if (header != 0) Marshal.FreeHGlobal(header);
            if (data != 0) Marshal.FreeHGlobal(data);
        }
    }

    private static void Check(uint result) { if (result != 0) throw new IOException($"Sortie audio indisponible (code {result})."); }
    public void Dispose() { lock (gate) { disposed = true; playing?.Cancel(); } }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat { public ushort Format, Channels; public uint Samples, BytesPerSecond; public ushort BlockAlign, Bits, Extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader { public nint Data; public uint Length, Recorded; public nuint User; public uint Flags, Loops; public nint Next; public nuint Reserved; }
    [DllImport("winmm.dll")] private static extern uint waveOutOpen(out nint device, uint id, ref WaveFormat format, nint callback, nint instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(nint device, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutWrite(nint device, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutReset(nint device);
    [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(nint device, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutClose(nint device);
}
