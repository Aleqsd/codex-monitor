using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexMonitor;

public sealed record MonitoredThread(string Id, string Title, string Project, string Model, string State)
{
    public bool NeedsAttention => State is "needsInput" or "needsApproval" or "error";
    public bool IsObserved => State is "active" or "idle" or "needsInput" or "needsApproval" or "error";
    public string Label => State switch
    {
        "active" => "En cours",
        "idle" => "Au repos",
        "needsInput" => "Réponse attendue",
        "needsApproval" => "Approbation",
        "error" => "Erreur",
        "disconnected" => "Déconnectée",
        "incompatible" => "Version incompatible",
        "resyncing" => "Synchronisation…",
        _ => "Non observée",
    };
}

public sealed record MonitorSnapshot(bool Connected, DateTimeOffset ReceivedAt,
    IReadOnlyList<MonitoredThread> Threads, string? Error)
{
    public static MonitorSnapshot Offline(string error) => new(false, DateTimeOffset.UtcNow, [], error);
    public int Active => Threads.Count(thread => thread.State == "active");
    public int Attention => Threads.Count(thread => thread.NeedsAttention);
    public int Idle => Threads.Count(thread => thread.State == "idle");
}

public static class MonitorContract
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, MaxDepth = 16 };

    public static MonitorSnapshot Parse(string json, DateTimeOffset now)
    {
        var response = JsonSerializer.Deserialize<BridgeResponse>(json, JsonOptions)
            ?? throw new InvalidDataException("Réponse vide du relais.");
        if (response.SchemaVersion != 1) throw new InvalidDataException("Version du relais incompatible.");
        if ((now - response.GeneratedAt).Duration() > TimeSpan.FromSeconds(15))
            throw new InvalidDataException("Le relais renvoie un état périmé.");
        if (!response.Connected) return MonitorSnapshot.Offline("Codex est déconnecté du relais.");
        if (response.Threads is null || response.Threads.Count > 200)
            throw new InvalidDataException("Liste de tâches invalide.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var threads = new List<MonitoredThread>();
        foreach (var row in response.Threads)
        {
            if (row is null || !Guid.TryParse(row.Id, out _) || !ids.Add(row.Id)) continue;
            var state = row.Availability == "live" ? row.State : row.Availability;
            if (row.Availability == "live" && (row.LastConfirmedAt is null || now - row.LastConfirmedAt > TimeSpan.FromSeconds(30)))
                state = "unobserved";
            if (state is not ("active" or "idle" or "needsInput" or "needsApproval" or "error" or "disconnected" or "incompatible" or "resyncing"))
                state = "unobserved";
            threads.Add(new MonitoredThread(row.Id, Clean(row.Title, "Tâche sans titre", 1500),
                ProjectName(row.Project), Clean(row.Model, "", 100), state));
        }
        return new MonitorSnapshot(true, now, threads.AsReadOnly(), null);
    }

    public static string Clean(string? value, string fallback, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var cleaned = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength] + "…";
    }

    private static string ProjectName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Sans projet";
        return Clean(value.TrimEnd('/', '\\').Replace('\\', '/').Split('/').LastOrDefault(), "Sans projet", 100);
    }

    private sealed class BridgeResponse
    {
        public int SchemaVersion { get; set; }
        public bool Connected { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
        public List<BridgeThread?>? Threads { get; set; }
    }

    private sealed class BridgeThread
    {
        public string Id { get; set; } = "";
        public string? Title { get; set; }
        public string? Project { get; set; }
        public string? Model { get; set; }
        public string State { get; set; } = "unobserved";
        public string Availability { get; set; } = "unobserved";
        public DateTimeOffset? LastConfirmedAt { get; set; }
    }
}

public static class TransitionDetector
{
    public static IReadOnlyList<MonitoredThread> Find(MonitorSnapshot previous, MonitorSnapshot current, bool idle, bool attention)
    {
        if (!previous.Connected || !current.Connected) return [];
        var oldStates = previous.Threads.ToDictionary(thread => thread.Id, thread => thread.State);
        return current.Threads.Where(thread => oldStates.TryGetValue(thread.Id, out var old)
            && old is "active" or "idle" or "needsInput" or "needsApproval" or "error"
            && old != thread.State
            && ((idle && old == "active" && thread.State == "idle") || (attention && thread.NeedsAttention))).ToArray();
    }
}
