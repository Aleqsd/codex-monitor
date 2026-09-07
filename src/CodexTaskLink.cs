using System.Diagnostics;

namespace CodexMonitor;

internal sealed class CodexTaskLink(Action<Uri>? launch = null)
{
    private int busy;
    private DateTimeOffset lastAttempt;
    private string? error;
    internal string? Error => Volatile.Read(ref error);
    internal bool Busy => Volatile.Read(ref busy) != 0;
    internal static Uri? Build(string? id) => Guid.TryParseExact(id, "D", out var guid) && guid != Guid.Empty
        ? new Uri($"codex://threads/{guid:D}") : null;
    internal Task Open(string id)
    {
        var uri = Build(id);
        if (uri is null || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return Task.CompletedTask;
        if (DateTimeOffset.UtcNow - lastAttempt < TimeSpan.FromSeconds(1)) { Volatile.Write(ref busy, 0); return Task.CompletedTask; }
        lastAttempt = DateTimeOffset.UtcNow; Volatile.Write(ref error, null);
        return Task.Run(() =>
        {
            try
            {
                if (launch is not null) launch(uri);
                else Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception)
            {
                Volatile.Write(ref error, "Impossible d’ouvrir Codex. Vérifiez que l’application est installée et que les liens codex:// sont associés à Codex dans Windows.");
            }
            finally { Volatile.Write(ref busy, 0); }
        });
    }
}
