using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexMonitor;

public sealed record MonitoredThread(string Id, string Title, string Project, string Model, string State, string[]? PendingQuestionIds = null, string[]? HiddenQuestionIds = null)
{
    public string[] QuestionIds => PendingQuestionIds ?? [];
    public bool HasQuestion => IsObserved && QuestionIds.Length > 0;
    public bool NeedsAttention => State is "needsInput" or "needsApproval" or "error" or "question" || HasQuestion;
    public bool IsObserved => State is "active" or "idle" or "needsInput" or "needsApproval" or "error";
    public string Label => State switch
    {
        "active" => "En cours",
        "idle" => "Au repos",
        "needsInput" => "Réponse attendue",
        "needsApproval" => "Approbation",
        "error" => "Erreur",
        "question" => "Question posée",
        "disconnected" => "Déconnectée",
        "incompatible" => "Version incompatible",
        "resyncing" => "Synchronisation…",
        _ => "Non observée",
    };
}

public sealed record MonitorSnapshot(bool Connected, DateTimeOffset ReceivedAt,
    IReadOnlyList<MonitoredThread> Threads, string? Error, bool QuestionTrackingSupported = false, AccountUsage? Usage = null, bool UsageTrackingSupported = false)
{
    public static MonitorSnapshot Offline(string error) => new(false, DateTimeOffset.UtcNow, [], error);
    public int Active => Threads.Count(thread => thread.State == "active");
    public int Attention => Threads.Count(thread => thread.NeedsAttention);
    public int Idle => Threads.Count(thread => thread.State == "idle");
    public UsageWindow? CurrentUsage => Connected ? Usage?.Current(DateTimeOffset.UtcNow) : null;
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
            var questions = state is "active" or "idle" or "needsInput" or "needsApproval" or "error"
                && response.QuestionTrackingSupported ? (row.PendingQuestionIds ?? []).Where(id => id is { Length: 32 } && id.All(Uri.IsHexDigit)).Distinct().Take(100).ToArray() : [];
            threads.Add(new MonitoredThread(row.Id, Clean(row.Title, "Tâche sans titre", 1500),
                ProjectName(row.Project), Clean(row.Model, "", 100), state, questions));
        }
        return new MonitorSnapshot(true, now, threads.AsReadOnly(), null, response.QuestionTrackingSupported, AccountUsage.Parse(response.Usage), response.Usage.ValueKind != JsonValueKind.Undefined);
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
        public bool QuestionTrackingSupported { get; set; }
        public JsonElement Usage { get; set; }
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
        public string[]? PendingQuestionIds { get; set; }
    }
}

public static class TransitionDetector
{
    public static IReadOnlyList<MonitoredThread> Find(MonitorSnapshot previous, MonitorSnapshot current, bool idle, bool attention)
    {
        if (!previous.Connected || !current.Connected) return [];
        var oldStates = previous.Threads.ToDictionary(thread => thread.Id);
        var events = new List<MonitoredThread>();
        foreach (var thread in current.Threads)
        {
            if (!oldStates.TryGetValue(thread.Id, out var old) || !old.IsObserved || !thread.IsObserved) continue;
            if (idle && old.State == "active" && thread.State == "idle") events.Add(thread with { PendingQuestionIds = null });
            if (attention && old.State != thread.State && thread.State is "needsInput" or "needsApproval" or "error")
                events.Add(thread);
            else if (attention && previous.QuestionTrackingSupported && current.QuestionTrackingSupported)
            {
                var added = thread.QuestionIds.Except(old.QuestionIds).ToArray();
                if (added.Length > 0) events.Add(thread with { State = "question", PendingQuestionIds = added });
            }
        }
        return events;
    }
}
