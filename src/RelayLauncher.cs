using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace CodexMonitor;

internal enum RelayPhase { Ready, Starting, Running, External, Stopping, Error }
internal sealed record RelayLaunchState(RelayPhase Phase, string Message, bool Retryable = false)
{
    internal bool Busy => Phase is RelayPhase.Starting or RelayPhase.Stopping;
}

/// <summary>Local launch after a click or configured opt-in. All I/O runs off the game's frame thread.</summary>
internal sealed class RelayLauncher(string storageRoot) : IDisposable
{
    private readonly object gate = new();
    private CancellationTokenSource? session;
    private bool disposed;
    private RelayLaunchState state = new(RelayPhase.Ready, "Relais inclus · lancement manuel en arrière-plan.");
    internal RelayLaunchState State => Volatile.Read(ref state);
    internal void Start(int port, string nodePath)
    {
        lock (gate)
        {
            if (disposed || session != null) return;
            session = new CancellationTokenSource();
            Set(RelayPhase.Starting, "Vérification du port et de Node.js…");
            var current = session;
            _ = Task.Run(() => Run(port, nodePath, current));
        }
    }

    internal void Stop()
    {
        lock (gate)
        {
            if (session is null) return;
            Set(RelayPhase.Stopping, "Arrêt du relais lancé par ce plugin…");
            session.Cancel();
        }
    }

    private void Set(RelayPhase phase, string message, bool retryable = false) => Volatile.Write(ref state, new(phase, message, retryable));

    private async Task Run(int port, string nodePath, CancellationTokenSource current)
    {
        Process? child = null;
        string? stopFile = null;
        string? runtime = null;
        Task? output = null, errors = null;
        var stopped = false;
        var token = current.Token;
        try
        {
            if (port is < 1 or > 65535) throw new InvalidDataException("Port local invalide.");
            var existing = await Probe(port, token);
            if (existing != PortState.Free)
            {
                Set(existing == PortState.Relay ? RelayPhase.External : RelayPhase.Error,
                    existing == PortState.Relay ? "Un relais déjà lancé a été détecté sur ce port. Il reste géré séparément."
                    : "Ce port est occupé par un autre service ou un relais qui ne répond pas. Aucun processus lancé.");
                return;
            }
            var node = FindNode(nodePath);
            await ValidateNode(node, token);
            var directory = ExtractBundle(storageRoot);
            RelayRuntimeStorage.Prune(storageRoot);
            runtime = Path.Combine(storageRoot, "runtime", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runtime);
            stopFile = Path.Combine(runtime, "stop");
            // Recheck after preparation: another launch may have taken the port in the meantime.
            if (await Probe(port, token) != PortState.Free)
                throw new InvalidDataException("Le port vient d’être occupé. Aucun nouveau relais lancé.");
            var start = CreateStartInfo(node, Path.Combine(directory, "bridge.mjs"), runtime, port);
            token.ThrowIfCancellationRequested();
            child = Process.Start(start) ?? throw new IOException("Impossible de démarrer Node.js.");
            output = Drain(child.StandardOutput);
            errors = Drain(child.StandardError);
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (child.HasExited) throw new IOException("Le relais s’est arrêté au démarrage. Vérifier que Codex a déjà été lancé sur ce PC.");
                if (await Probe(port, token) == PortState.Relay) break;
                if (DateTime.UtcNow >= deadline) throw new IOException("Le relais ne répond pas. Vérifier Codex et le port local.");
                await Task.Delay(200, token);
            }
            Set(RelayPhase.Running, "Relais lancé par ce plugin. Il sera arrêté avec le plugin.");
            await child.WaitForExitAsync(token);
            throw new IOException("Le relais s’est arrêté. Il peut être relancé avec le bouton ci-dessus.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { stopped = true; Set(RelayPhase.Stopping, "Arrêt du relais lancé par ce plugin…"); }
        catch (OperationCanceledException) { Set(RelayPhase.Error, "Délai de démarrage dépassé · nouvelle tentative possible.", true); }
        catch (Exception error) { Set(RelayPhase.Error, MonitorContract.Clean(error.Message, "Lancement impossible.", 240),
            child is not null && error is IOException && error is not InvalidDataException); }
        finally
        {
            if (child is not null)
            {
                try
                {
                    if (!child.HasExited)
                    {
                        // A unique runtime directory belongs only to this child; never stop an external relay.
                        await File.WriteAllTextAsync(stopFile!, "stop");
                        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await child.WaitForExitAsync(deadline.Token);
                    }
                    if (output is not null && errors is not null) await Task.WhenAll(output, errors);
                    if (child.HasExited && runtime is not null)
                    {
                        try { RelayRuntimeStorage.MarkStopped(runtime); }
                        catch (IOException) { /* Retain the unmarked directory if bookkeeping fails. */ }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                catch (Exception) { stopped = false; Set(RelayPhase.Error, "Arrêt non confirmé. Vérifier le relais avant de le relancer."); }
                finally { child.Dispose(); }
            }
            lock (gate)
            {
                if (ReferenceEquals(session, current)) session = null;
                if (stopped) Set(RelayPhase.Ready, "Relais arrêté. Utiliser le bouton pour le relancer.");
                current.Dispose();
            }
        }
    }

    // Drain output without retaining conversation metadata or unbounded logs in the game process.
    private static async Task Drain(StreamReader reader)
    {
        var buffer = new char[2048];
        try { while (await reader.ReadAsync(buffer) != 0) { } }
        catch (Exception error) when (error is IOException or ObjectDisposedException) { }
    }

    internal static ProcessStartInfo CreateStartInfo(string node, string script, string runtime, int port)
    {
        var start = new ProcessStartInfo(node)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(script)!, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var arg in new[] { "--disable-warning=ExperimentalWarning", script, "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--output", runtime, "--parent-pid", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            start.ArgumentList.Add(arg);
        // Node startup hooks from the host are irrelevant to the bundled relay.
        start.Environment.Remove("NODE_OPTIONS"); start.Environment.Remove("NODE_PATH");
        return start;
    }

    internal static string FindNode(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var candidate = configured.Trim().Trim('"');
            if (!IsLocalExecutable(candidate) || !File.Exists(candidate))
                throw new InvalidDataException("Choisir le chemin complet d’un node.exe installé sur ce PC.");
            return candidate;
        }
        var directories = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs") }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (var directory in directories)
        {
            var candidate = Path.Combine(directory.Trim().Trim('"'), "node.exe");
            if (IsLocalExecutable(candidate) && File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Node.js introuvable. Installer Node.js 22.22.2 ou plus récent, ou indiquer node.exe ci-dessous.");
    }

    internal static bool IsLocalExecutable(string path) => Path.IsPathFullyQualified(path)
        && path.Length > 3 && char.IsLetter(path[0]) && path[1] == ':' && !path[2..].Contains(':')
        && Path.GetFileName(path).Equals("node.exe", StringComparison.OrdinalIgnoreCase);

    private static async Task ValidateNode(string node, CancellationToken token)
    {
        var start = new ProcessStartInfo(node) { UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--version"); start.Environment.Remove("NODE_OPTIONS"); start.Environment.Remove("NODE_PATH");
        using var process = Process.Start(start) ?? throw new IOException("Node.js ne démarre pas.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var result = process.StandardOutput.ReadLineAsync(deadline.Token);
        await process.WaitForExitAsync(deadline.Token);
        if (process.ExitCode != 0 || !Version.TryParse((await result)?.Trim().TrimStart('v'), out var version) || version < new Version(22, 22, 2))
            throw new InvalidDataException("Mettre à jour Node.js : version 22.22.2 minimum.");
    }

    internal static string ExtractBundle(string root)
    {
        var assembly = typeof(RelayLauncher).Assembly;
        string[] names = ["bridge.mjs", "observer.mjs", "questions.mjs", "usage.mjs", "files.mjs"];
        var content = names.Select(name =>
        {
            using var stream = assembly.GetManifestResourceStream("CodexMonitor.Relay." + name)
                ?? throw new IOException("Relais intégré absent de cette DLL.");
            using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray();
        }).ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(content.SelectMany(bytes => bytes).ToArray()))[..16];
        var directory = Path.Combine(root, "bridge", fingerprint);
        Directory.CreateDirectory(directory);
        for (var i = 0; i < names.Length; i++)
        {
            var file = Path.Combine(directory, names[i]);
            if (File.Exists(file))
            {
                if (!File.ReadAllBytes(file).AsSpan().SequenceEqual(content[i]))
                    throw new IOException("La copie du relais a été modifiée. Supprimer son dossier bridge avant de réessayer.");
            }
            else { using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write); stream.Write(content[i]); }
        }
        return directory;
    }

    internal enum PortState { Free, Relay, Occupied }
    internal static async Task<PortState> Probe(int port, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            token.ThrowIfCancellationRequested();
            // Windows may retry a refused TCP connect past our HTTP deadline. Inspect listeners
            // first so a closed port is not mistaken for an occupied but unresponsive service.
            if (!IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == port))
                return PortState.Free;
            using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { MaxResponseContentBufferSize = 4096 };
            using var response = await http.GetAsync($"http://127.0.0.1:{port}/health", deadline.Token);
            if (!response.IsSuccessStatusCode) return PortState.Occupied;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
            var root = json.RootElement;
            return root.TryGetProperty("schemaVersion", out var schema) && schema.TryGetInt32(out var version) && version == 1
                && root.TryGetProperty("connected", out var connected) && connected.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? PortState.Relay : PortState.Occupied;
        }
        catch (Exception error) when (error is HttpRequestException or SocketException or NetworkInformationException or JsonException or OperationCanceledException or InvalidOperationException)
        { token.ThrowIfCancellationRequested(); return PortState.Occupied; }
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; session?.Cancel(); }
    }
}
