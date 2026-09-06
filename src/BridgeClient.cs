using System.Net;

namespace CodexMonitor;

public sealed class BridgeClient : IDisposable
{
    private readonly HttpClient http;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task worker;
    private MonitorSnapshot current = MonitorSnapshot.Offline("Connexion au relais local…");
    private int port;
    private int disposed;
    public MonitorSnapshot Current => Volatile.Read(ref current);

    public BridgeClient(int port, HttpMessageHandler? handler = null)
    {
        this.port = Math.Clamp(port, 1, 65535);
        http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        {
            Timeout = TimeSpan.FromSeconds(2),
            MaxResponseContentBufferSize = 1024 * 1024,
        };
        worker = Task.Run(RunAsync);
    }

    public void SetPort(int newPort) => Volatile.Write(ref port, Math.Clamp(newPort, 1, 65535));

    private async Task RunAsync()
    {
        var token = cancellation.Token;
        while (!token.IsCancellationRequested)
        {
            MonitorSnapshot next;
            try
            {
                using var response = await http.GetAsync($"http://127.0.0.1:{Volatile.Read(ref port)}/api/threads", token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK) throw new HttpRequestException($"Relais HTTP {(int)response.StatusCode}.");
                var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                next = MonitorContract.Parse(body, DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidDataException)
            {
                next = MonitorSnapshot.Offline(error is InvalidDataException ? error.Message : "Relais indisponible. Lance le relais Codex sur ce PC.");
            }
            Interlocked.Exchange(ref current, next);
            try { await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        cancellation.Cancel();
        _ = worker.ContinueWith(_ => { http.Dispose(); cancellation.Dispose(); }, TaskScheduler.Default);
    }
}
