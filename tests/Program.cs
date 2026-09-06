using System.Net;
using System.Text.Json;
using CodexMonitor;

var id = Guid.NewGuid().ToString();
var now = DateTimeOffset.UtcNow;
var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine($"PASS {message}"); }
string Payload(string state = "active", bool connected = true, int version = 1, int generatedAge = 0, int confirmedAge = 0) => JsonSerializer.Serialize(new
{
    schemaVersion = version, generatedAt = now.AddSeconds(-generatedAge), connected,
    threads = new[] { new { id, title = "Une tâche\ntrès longue", project = "C:\\Work\\Meet", model = "gpt-6-astra",
        state, availability = "live", lastConfirmedAt = now.AddSeconds(-confirmedAge) } },
});
var live = MonitorContract.Parse(Payload(), now);
Check(live.Connected && live.Active == 1 && live.Threads[0].Project == "Meet", "Live bridge contract and project name");
Check(live.Threads[0].Title == "Une tâche très longue", "Titles render as plain single-line text");
Check(MonitorContract.Parse(Payload(connected: false), now).Threads.Count == 0, "Disconnected bridge clears all task states");
Check(MonitorContract.Parse(Payload(confirmedAge: 40), now).Threads[0].State == "unobserved", "Stale owner state is never shown as active");
try { MonitorContract.Parse(Payload(version: 2), now); throw new Exception("Schema check failed"); } catch (InvalidDataException) { checks++; Console.WriteLine("PASS Unsupported schema rejected"); }
try { MonitorContract.Parse(Payload(generatedAge: 20), now); throw new Exception("Age check failed"); } catch (InvalidDataException) { checks++; Console.WriteLine("PASS Stale entire response rejected"); }
var idle = MonitorContract.Parse(Payload("idle"), now);
Check(TransitionDetector.Find(live, idle, true, true).Count == 1, "Active-to-idle emits one completion");
Check(TransitionDetector.Find(idle, idle, true, true).Count == 0, "Repeated polling does not duplicate alerts");
Check(TransitionDetector.Find(MonitorSnapshot.Offline("offline"), idle, true, true).Count == 0, "Reconnect never announces initial idle tasks as completed");
Check(TransitionDetector.Find(live, MonitorContract.Parse(Payload("needsInput"), now), true, true).Count == 1, "Request for input emits an attention notification");
Check(TransitionDetector.Find(live, idle, false, false).Count == 0, "Disabled notifications remain disabled");

var handler = new ScriptedHandler(() => Payload());
using (var client = new BridgeClient(43187, handler))
{
    await Until(() => client.Current.Connected);
    Check(client.Current.Active == 1, "Background HTTP client consumes a response");
    handler.Fail = true;
    await Until(() => !client.Current.Connected);
    Check(client.Current.Threads.Count == 0, "HTTP failure clears previous active states");
    handler.Fail = false;
    await Until(() => client.Current.Connected);
    Check(client.Current.Active == 1, "HTTP client recovers automatically");
}
using (var http = new HttpClient())
{
    var response = await http.GetStringAsync("http://127.0.0.1:43187/api/threads");
    var snapshot = MonitorContract.Parse(response, DateTimeOffset.UtcNow);
    Check(snapshot.Connected && snapshot.Threads.Any(thread => thread.IsObserved), "Actual running relay accepted by the plugin's parser");
    Console.WriteLine($"LIVE {snapshot.Active} active, {snapshot.Attention} attention, {snapshot.Idle} idle");
}
Console.WriteLine($"{checks} checks passed.");

static async Task Until(Func<bool> condition)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
    while (!condition()) await Task.Delay(20, timeout.Token);
}

sealed class ScriptedHandler(Func<string> payload) : HttpMessageHandler
{
    public volatile bool Fail;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Host != "127.0.0.1") throw new Exception("Unexpected remote host");
        if (Fail) throw new HttpRequestException("Simulated connection loss");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload()) });
    }
}
