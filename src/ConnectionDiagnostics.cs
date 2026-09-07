namespace CodexMonitor;

internal sealed record DiagnosticStep(string Name, bool? Ready, string Detail, string Advice = "");
internal sealed record DiagnosticReport(bool Busy, DateTimeOffset? At, DiagnosticStep[] Steps)
{
    internal string CopyText() => $"Codex Monitor {PluginVersion.Current} · diagnostic {At:O}\n" + string.Join("\n", Steps.Select(s =>
        $"{s.Name}: {(s.Ready is true ? "OK" : s.Ready is false ? "À vérifier" : "Inconnu")} · {s.Detail}{(s.Advice.Length > 0 ? " · " + s.Advice : "")}"));
}

internal sealed class ConnectionDiagnostics : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private DiagnosticReport report = new(false, null, []);
    internal DiagnosticReport Report => Volatile.Read(ref report);
    internal void Check(int port, string configuredNode)
    {
        if (Report.Busy || lifetime.IsCancellationRequested) return;
        Volatile.Write(ref report, new(true, null, []));
        _ = Task.Run(async () =>
        {
            var steps = new List<DiagnosticStep>(); var token = lifetime.Token;
            try
            {
                try { var node = RelayLauncher.FindNode(configuredNode); await RelayLauncher.ValidateNode(node, token); steps.Add(new("Node.js", true, "Version compatible (22.22.2 minimum)")); }
                catch (Exception e) when (e is not OperationCanceledException) { steps.Add(new("Node.js", false, "Introuvable ou incompatible", "Installer Node.js 22.22.2+ ou renseigner node.exe")); }
                var portState = await RelayLauncher.Probe(port, token);
                steps.Add(new("Relais", portState == RelayLauncher.PortState.Relay, portState switch
                { RelayLauncher.PortState.Free => "Aucun relais sur le port configuré", RelayLauncher.PortState.Relay => "Relais reconnu", _ => "Port occupé ou réponse invalide" },
                    portState == RelayLauncher.PortState.Free ? "Utiliser Lancer le relais" : portState == RelayLauncher.PortState.Occupied ? "Vérifier le port dans Connexion" : ""));
                MonitorSnapshot? snapshot = null;
                if (portState == RelayLauncher.PortState.Relay)
                {
                    try
                    {
                        using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(4), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
                        snapshot = MonitorContract.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/api/threads", token), DateTimeOffset.UtcNow);
                    }
                    catch (Exception e) when (e is not OperationCanceledException) { steps.Add(new("Données", false, "Réponse absente, périmée ou incompatible", "Vérifier la version du relais")); }
                }
                steps.Add(new("Codex", snapshot?.Connected, snapshot?.Connected == true ? "Application connectée" : "Connexion non confirmée", snapshot?.Connected == true ? "" : "Ouvrir Codex puis relancer la vérification"));
                var quota = snapshot?.CurrentUsage is not null;
                steps.Add(new("Quota / CLI", snapshot is null ? null : quota, quota ? "Quota disponible" : snapshot?.QuotaDiagnostic?.Message ?? "Lecture non vérifiable sans relais connecté",
                    quota ? "" : snapshot?.QuotaDiagnostic?.Status == "authRequired" ? "Se connecter avec codex login dans un terminal" : "Vérifier le CLI Codex connecté au compte souhaité"));
                Volatile.Write(ref report, new(false, DateTimeOffset.UtcNow, steps.ToArray()));
            }
            catch (OperationCanceledException) { Volatile.Write(ref report, new(false, DateTimeOffset.UtcNow, [new("Vérification", null, "Vérification interrompue ou délai dépassé", "Réessayer")])); }
            catch { Volatile.Write(ref report, new(false, DateTimeOffset.UtcNow, [new("Vérification", false, "Vérification indisponible", "Réessayer")])); }
        });
    }
    public void Dispose() => lifetime.Cancel();
}
