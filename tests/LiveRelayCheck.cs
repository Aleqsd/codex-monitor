using CodexMonitor;

// Explicit opt-in only: actual embedded scripts, actual local Codex, own bounded relay lifetime.
internal sealed class LiveRelayCheck : IAsyncDisposable
{
    private readonly RelayLauncher launcher = new(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "live-relay-" + Guid.NewGuid().ToString("N")));
    internal static async Task<LiveRelayCheck> Start()
    {
        if (await RelayLauncher.Probe(43187, default) != RelayLauncher.PortState.Free)
            throw new Exception("Live launch test requires a free port 43187; existing services are left alone.");
        var check = new LiveRelayCheck();
        try
        {
            check.launcher.Start(43187, "");
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (check.launcher.State.Phase == RelayPhase.Starting && DateTime.UtcNow < deadline) await Task.Delay(100);
            if (check.launcher.State.Phase != RelayPhase.Running) throw new Exception(check.launcher.State.Message);
            Console.WriteLine("LIVE: actual embedded relay launched locally for this check only.");
            return check;
        }
        catch { await check.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        launcher.Stop();
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (launcher.State.Busy && DateTime.UtcNow < deadline) await Task.Delay(100);
        launcher.Dispose();
        if (await RelayLauncher.Probe(43187, default) != RelayLauncher.PortState.Free) throw new Exception("Live relay shutdown not confirmed.");
        Console.WriteLine("LIVE: owned relay stopped; port 43187 released.");
    }
}
