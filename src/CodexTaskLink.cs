using System.Diagnostics;

namespace CodexMonitor;

internal sealed class CodexTaskLink(Action<Uri>? launch = null, Func<DateTimeOffset>? clock = null)
{
    private int busy;
    private DateTimeOffset lastAttempt;
    private sealed record Failure(string TaskId, string Message, DateTimeOffset Until);
    private Failure? error;
    private DateTimeOffset Now => clock?.Invoke() ?? DateTimeOffset.UtcNow;
    internal string? Error => Volatile.Read(ref error) is { } failure && Now < failure.Until ? failure.Message : null;
    internal string? ErrorFor(string id) => Volatile.Read(ref error) is { } failure && failure.TaskId == id && Now < failure.Until ? failure.Message : null;
    internal void DismissError(string id) { var current = Volatile.Read(ref error); if (current?.TaskId == id) Interlocked.CompareExchange(ref error, null, current); }
    internal bool Busy => Volatile.Read(ref busy) != 0;
    internal static Uri? Build(string? id) => Guid.TryParseExact(id, "D", out var guid) && guid != Guid.Empty
        ? new Uri($"codex://threads/{guid:D}") : null;
    internal Task Open(string id)
    {
        var uri = Build(id);
        if (uri is null || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return Task.CompletedTask;
        if (Now - lastAttempt < TimeSpan.FromSeconds(1)) { Volatile.Write(ref busy, 0); return Task.CompletedTask; }
        lastAttempt = Now; Volatile.Write(ref error, null);
        return Task.Run(() =>
        {
            try
            {
                if (launch is not null) launch(uri);
                else Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception)
            {
                Volatile.Write(ref error, new Failure(id, "Impossible d’ouvrir Codex. Vérifiez que l’application est installée et que les liens codex:// sont associés à Codex dans Windows.", Now.AddSeconds(20)));
            }
            finally { Volatile.Write(ref busy, 0); }
        });
    }
}
