using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexMonitor;

public sealed record QuestionPreview(string Id, string Text);

public sealed record MonitoredThread(string Id, string Title, string Project, string Model, string State, string[]? PendingQuestionIds = null, string[]? HiddenQuestionIds = null,
    string? ReasoningEffort = null, bool? HasUnreadTurn = null, string? LatestTurnStatus = null, string? ProjectId = null, bool IsFavorite = false, string? ModelSource = null,
    QuestionPreview[]? QuestionPreviews = null)
{
    public string? QuestionExcerpt => QuestionPreviews?.FirstOrDefault(q => QuestionIds.Contains(q.Id))?.Text;
    public string? InterventionLabel => HasQuestion && State is "active" or "idle" ? "Question posée" : null;
    public string ProjectKey => ProjectId ?? Project;
    public bool ResponseReady => State == "idle" && HasUnreadTurn == true;
    public string ModelLabel => Model.Length == 0 ? "Modèle non fourni" : Model + " · " + (ReasoningEffort ?? "effort non fourni");
    public string[] QuestionIds => PendingQuestionIds ?? [];
    public bool HasQuestion => IsObserved && QuestionIds.Length > 0;
    public bool NeedsAttention => State is "needsInput" or "needsApproval" or "error" or "question" || HasQuestion;
    public bool IsObserved => State is "active" or "idle" or "needsInput" or "needsApproval" or "error";
    public string Label => State switch
    {
        "active" => "En cours",
        "idle" => ResponseReady ? "Réponse prête" : LatestTurnStatus == "completed" ? "Réponse terminée" : "Sans activité",
        "needsInput" => "Réponse attendue",
        "needsApproval" => "Approbation",
        "error" => "Erreur",
        "question" => "Question posée",
        "quota" => "Quota bas",
        "disconnected" => "Déconnectée",
        "incompatible" => "Version incompatible",
        "resyncing" => "Synchronisation…",
        _ => "Non observée",
    };
}

public sealed record MonitorSnapshot(bool Connected, DateTimeOffset ReceivedAt,
    IReadOnlyList<MonitoredThread> Threads, string? Error, bool QuestionTrackingSupported = false, AccountUsage? Usage = null, bool UsageTrackingSupported = false,
    string? RelayVersion = null, QuotaDiagnostic? QuotaDiagnostic = null, bool TaskMetadataSupported = false)
{
    public static MonitorSnapshot Offline(string error) => new(false, DateTimeOffset.UtcNow, [], error);
    public int Active => Threads.Count(thread => thread.State == "active");
    public int Attention => Threads.Count(thread => thread.NeedsAttention);
    public int Idle => Threads.Count(thread => thread.State == "idle");
    public int Ready => Threads.Count(thread => thread.ResponseReady);
    public bool ReadStateSupported => TaskMetadataSupported || Threads.Any(thread => thread.HasUnreadTurn is not null);
    public string ReadyCount => Connected && ReadStateSupported ? Ready.ToString() : "—";
    public UsageWindow? CurrentUsage => Connected ? Usage?.Current(DateTimeOffset.UtcNow) : null;
    public UsageWindow? SelectedUsage(UsagePreference preference) => Connected ? Usage?.Current(DateTimeOffset.UtcNow, preference) : null;
    public int Questions => Threads.Where(thread => thread.IsObserved).Sum(thread => thread.QuestionIds.Length);
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
        var relayVersion = response.RelayVersion.ValueKind == JsonValueKind.String && Version.TryParse(response.RelayVersion.GetString(), out var parsedVersion) ? parsedVersion.ToString() : null;
        var diagnostic = response.QuotaDiagnostic.ValueKind == JsonValueKind.Object && response.QuotaDiagnostic.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
            ? new QuotaDiagnostic(Clean(status.GetString(), "unknown", 40)) : null;
        if (!response.Connected) return new(false, now, [], "Codex est déconnecté du relais.", response.QuestionTrackingSupported,
            null, response.Usage.ValueKind != JsonValueKind.Undefined, relayVersion, diagnostic);
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
                ProjectName(row.Project), Clean(row.Model, "", 100), state, questions, ReasoningEffort: Effort(row.ReasoningEffort),
                HasUnreadTurn: row.Availability == "live" && state != "unobserved" && row.HasUnreadTurn.ValueKind is JsonValueKind.True or JsonValueKind.False ? row.HasUnreadTurn.GetBoolean() : null,
                LatestTurnStatus: row.LatestTurnStatus.ValueKind == JsonValueKind.String && row.LatestTurnStatus.GetString() is "completed" or "inProgress" or "interrupted" or "failed" ? row.LatestTurnStatus.GetString() : null,
                ProjectId: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes((row.Project ?? "").Replace('\\', '/').TrimEnd('/').ToUpperInvariant()))),
                ModelSource: row.ModelSource.ValueKind == JsonValueKind.String && row.ModelSource.GetString() == "turn" ? "turn" : "thread",
                QuestionPreviews: ReadQuestionPreviews(row.QuestionPreviews, questions)));
        }
        return new MonitorSnapshot(true, now, threads.AsReadOnly(), null, response.QuestionTrackingSupported, AccountUsage.Parse(response.Usage), response.Usage.ValueKind != JsonValueKind.Undefined,
            relayVersion, diagnostic, response.TaskMetadataSupported.ValueKind == JsonValueKind.True);
    }

    public static string Clean(string? value, string fallback, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var cleaned = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return UnicodeText.Truncate(cleaned, maxLength);
    }
    private static QuestionPreview[] ReadQuestionPreviews(JsonElement value, string[] ids)
    {
        if (value.ValueKind != JsonValueKind.Array || ids.Length == 0) return [];
        return value.EnumerateArray().Take(100).Where(q => q.ValueKind == JsonValueKind.Object
            && q.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && ids.Contains(id.GetString())
            && q.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            .Select(q => new QuestionPreview(q.GetProperty("id").GetString()!, Clean(q.GetProperty("text").GetString(), "", 240)))
            .Where(q => q.Text.Length > 0).DistinctBy(q => q.Id).ToArray();
    }
    private static string? Effort(JsonElement value) => value.ValueKind == JsonValueKind.String
        && value.GetString() is "none" or "minimal" or "low" or "medium" or "high" or "xhigh" or "max" or "ultra" ? value.GetString() : null;

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
        public JsonElement TaskMetadataSupported { get; set; }
        public JsonElement Usage { get; set; }
        public JsonElement RelayVersion { get; set; }
        public JsonElement QuotaDiagnostic { get; set; }
        public List<BridgeThread?>? Threads { get; set; }
    }

    private sealed class BridgeThread
    {
        public string Id { get; set; } = "";
        public string? Title { get; set; }
        public string? Project { get; set; }
        public string? Model { get; set; }
        public JsonElement ReasoningEffort { get; set; }
        public JsonElement HasUnreadTurn { get; set; }
        public JsonElement LatestTurnStatus { get; set; }
        public JsonElement ModelSource { get; set; }
        public string State { get; set; } = "unobserved";
        public string Availability { get; set; } = "unobserved";
        public DateTimeOffset? LastConfirmedAt { get; set; }
        public string[]? PendingQuestionIds { get; set; }
        public JsonElement QuestionPreviews { get; set; }
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
