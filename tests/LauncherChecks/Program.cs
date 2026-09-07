using System.Net;
using System.Net.Sockets;
using CodexMonitor;

var checks = 0;
void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
async Task Until(Func<bool> condition)
{
    var deadline = DateTime.UtcNow.AddSeconds(15);
    while (!condition()) { if (DateTime.UtcNow > deadline) throw new Exception("Launcher condition timed out."); await Task.Delay(40); }
}
int FreePort() { using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port; }
var root = Path.Combine(Path.GetTempPath(), "Codex Monitor launch ' " + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var node = RelayLauncher.FindNode("");
var port = FreePort();
Check(await RelayLauncher.Probe(port, default) == RelayLauncher.PortState.Free, "Free loopback port not recognized.");
var start = RelayLauncher.CreateStartInfo(node, @"C:\A folder\literal $(echo bad)\bridge.mjs", root, port);
Check(!start.UseShellExecute && start.CreateNoWindow && start.ArgumentList[1].Contains("$(echo bad)"), "Launch must not invoke a command shell or split paths.");
Check(!RelayLauncher.IsLocalExecutable(@"\\server\node.exe") && !RelayLauncher.IsLocalExecutable("node.exe"), "Remote or relative executable accepted.");
try
{
    using (var launcher = new RelayLauncher(root))
    {
        launcher.Start(port, node); launcher.Start(port, node);
        await Until(() => launcher.State.Phase is RelayPhase.Running or RelayPhase.Error);
        Check(launcher.State.Phase == RelayPhase.Running, launcher.State.Message);
        Check(Directory.GetDirectories(Path.Combine(root, "runtime")).Length == 1, "Double click started more than one relay.");
        Check(await RelayLauncher.Probe(port, default) == RelayLauncher.PortState.Relay, "Relay with disconnected Codex must still be recognized.");
        using (var other = new RelayLauncher(Path.Combine(root, "other")))
        {
            other.Start(port, node); await Until(() => other.State.Phase == RelayPhase.External);
            Check(!Directory.Exists(Path.Combine(root, "other")), "Existing relay should not prepare or launch a duplicate.");
        }
        Check(await RelayLauncher.Probe(port, default) == RelayLauncher.PortState.Relay, "Disposal stopped someone else's relay.");
        launcher.Stop();
        await Until(() => launcher.State.Phase == RelayPhase.Ready);
        await Task.Delay(250);
        Check(await RelayLauncher.Probe(port, default) == RelayLauncher.PortState.Free, "Graceful stop failed.");
        launcher.Start(port, node); await Until(() => launcher.State.Phase == RelayPhase.Running);
        Check(Directory.GetDirectories(Path.Combine(root, "runtime")).Length == 2, "Restart did not create a fresh owned runtime.");
    }
    await Task.Delay(500);
    Check(await RelayLauncher.Probe(port, default) == RelayLauncher.PortState.Free, "Plugin unload failed to stop its relay.");
    var bundle = RelayLauncher.ExtractBundle(root);
    File.AppendAllText(Path.Combine(bundle, "bridge.mjs"), "// changed");
    try { RelayLauncher.ExtractBundle(root); throw new Exception("Modified bundle was accepted."); }
    catch (IOException) { checks++; }
    using var blocker = new TcpListener(IPAddress.Loopback, 0); blocker.Start();
    var occupied = ((IPEndPoint)blocker.LocalEndpoint).Port;
    using var blockedLauncher = new RelayLauncher(Path.Combine(root, "blocked"));
    blockedLauncher.Start(occupied, node);
    await Until(() => blockedLauncher.State.Phase == RelayPhase.Error);
    Check(!Directory.Exists(Path.Combine(root, "blocked")), "Unrelated service caused a process launch.");
    using var missing = new RelayLauncher(Path.Combine(root, "missing"));
    missing.Start(FreePort(), Path.Combine(root, "missing", "node.exe"));
    await Until(() => missing.State.Phase == RelayPhase.Error);
    Check(missing.State.Message.Contains("node.exe"), "Missing Node must have an actionable error.");
}
finally
{
    // Only this test's verified unique temporary directory is removed; never touch a live relay.
    if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
}
Console.WriteLine($"PASS {checks} native Node launcher checks; isolated fixture, no Codex access.");
