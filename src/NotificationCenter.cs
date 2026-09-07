namespace CodexMonitor;

public sealed record HistoryEntry(long Id, MonitoredThread Task, DateTimeOffset At);

public sealed class NotificationHistory
{
    public const int Capacity = 100;
    private readonly List<HistoryEntry> entries;
    private readonly object gate = new();
    private long sequence;

    public NotificationHistory(IEnumerable<HistoryEntry>? saved = null)
    {
        entries = (saved ?? []).Where(item => item is { Task: not null } && item.Id > 0 && item.Id < long.MaxValue - Capacity)
            .DistinctBy(item => item.Id).OrderByDescending(item => item.Id).Take(Capacity).ToList();
        sequence = entries.Count == 0 ? 0 : entries[0].Id;
    }

    public IReadOnlyList<HistoryEntry> Entries { get { lock (gate) return entries.ToArray(); } }
    public HistoryEntry Add(MonitoredThread task, DateTimeOffset now)
    {
        lock (gate)
        {
            var entry = new HistoryEntry(++sequence, task, now);
            entries.Insert(0, entry);
            if (entries.Count > Capacity) entries.RemoveAt(Capacity);
            return entry;
        }
    }
    public void Clear() { lock (gate) entries.Clear(); }
    public string CurrentLabel(HistoryEntry entry, MonitorSnapshot snapshot)
    {
        if (!entry.Task.NeedsAttention) return "Événement passé";
        if (entry.Task.State == "question")
        {
            var task = snapshot.Threads.FirstOrDefault(task => task.Id == entry.Task.Id);
            if (!snapshot.Connected || !snapshot.QuestionTrackingSupported || task is null || !task.IsObserved) return "État actuel inconnu";
            if (entry.Task.QuestionIds.Intersect(task.QuestionIds).Any()) return "Intervention en cours";
            return entry.Task.QuestionIds.Intersect(task.HiddenQuestionIds ?? []).Any() ? "Question masquée dans FF14" : "Question traitée ou dépassée";
        }
        lock (gate) if (entries.Any(item => item.Task.Id == entry.Task.Id && item.Id > entry.Id)) return "Ancienne alerte";
        var current = snapshot.Threads.FirstOrDefault(task => task.Id == entry.Task.Id);
        if (!snapshot.Connected || current is null || !current.IsObserved) return "État actuel inconnu";
        return current.State == entry.Task.State ? "Intervention en cours" : "Intervention terminée";
    }
}

public sealed record QuietDigest(int Completed, int Input, int Approval, int Errors, int Events, int Questions = 0)
{
    public string Title => string.Join(" · ", new[]
    {
        Completed > 0 ? $"{Completed} " + (Completed == 1 ? "tour terminé" : "tours terminés") : null,
        Input > 0 ? $"{Input} " + (Input == 1 ? "réponse attendue" : "réponses attendues") : null,
        Approval > 0 ? $"{Approval} " + (Approval == 1 ? "approbation" : "approbations") : null,
        Errors > 0 ? $"{Errors} " + (Errors == 1 ? "erreur" : "erreurs") : null,
        Questions > 0 ? $"{Questions} " + (Questions == 1 ? "tâche avec une question" : "tâches avec des questions") : null,
    }.Where(part => part != null));
    public string DisplayTitle => Title.Length > 0 ? Title : $"{Events} " + (Events == 1 ? "alerte" : "alertes") + " · aucune intervention en attente";
}

/// <summary>Runs on the framework/UI thread; persistence belongs to the plugin.</summary>
public sealed class NotificationCenter(NotificationHistory history, NotificationQueue queue, Action<string>? signalSound = null)
{
    private MonitorSnapshot previous = MonitorSnapshot.Offline("Initialisation");
    private readonly List<HistoryEntry> deferred = [];
    private bool wasQuiet;
    public bool IsQuiet { get; private set; }
    public int DeferredCount => deferred.Count;

    public void Suspend(MonitorSnapshot snapshot)
    {
        previous = snapshot;
        deferred.Clear();
        queue.Clear();
        IsQuiet = wasQuiet = true;
    }

    // Restoring a local display choice must not replay an old question or hide other transitions.
    public void RestoreQuestions(string threadId, string[] restoredIds)
    {
        previous = previous with { Threads = previous.Threads.Select(row => row.Id == threadId
            ? row with { PendingQuestionIds = row.QuestionIds.Concat(restoredIds).Distinct().ToArray() } : row).ToArray() };
    }

    public bool Update(MonitorSnapshot snapshot, bool quiet, bool notifyIdle, bool notifyAttention, float duration, DateTimeOffset now, bool notifyQuestions = true)
    {
        IsQuiet = quiet;
        var changed = false;
        string? sound = null;
        static int Priority(string state) => state == "error" ? 3 : state is "needsInput" or "needsApproval" or "question" ? 2 : 1;
        if (!snapshot.Connected && previous.Connected) { queue.Clear(); deferred.Clear(); }
        if (!ReferenceEquals(previous, snapshot))
        {
            foreach (var task in TransitionDetector.Find(previous, snapshot, true, true))
            {
                var entry = history.Add(task, now);
                changed = true;
                if (!(task.State == "idle" ? notifyIdle : task.State == "question" ? notifyQuestions : notifyAttention)) continue;
                if (quiet)
                {
                    deferred.Add(entry);
                    if (deferred.Count > NotificationHistory.Capacity) deferred.RemoveAt(0);
                }
                else
                {
                    queue.Add(task, duration, entry.Id);
                    if (sound == null || Priority(task.State) > Priority(sound)) sound = task.State;
                }
            }
            queue.Reconcile(snapshot);
        }
        // A toast interrupted near its expiry needs a full reading interval on return.
        if (wasQuiet && !quiet) queue.RestartTimers();
        if (wasQuiet && !quiet && snapshot.Connected && deferred.Count > 0)
        {
            var digest = Summarize(deferred, snapshot);
            queue.Add(new MonitoredThread("quiet-summary", digest.DisplayTitle, "Cliquer pour consulter l’historique", "", "summary"), duration, first: true);
            var digestSound = digest.Errors > 0 ? "error" : digest.Input + digest.Approval + digest.Questions > 0 ? "needsInput" : "idle";
            if (sound == null || Priority(digestSound) > Priority(sound)) sound = digestSound;
            deferred.Clear();
        }
        previous = snapshot;
        wasQuiet = quiet;
        if (sound != null) signalSound?.Invoke(sound);
        return changed;
    }

    public static QuietDigest Summarize(IEnumerable<HistoryEntry> events, MonitorSnapshot snapshot)
    {
        var rows = events.ToArray();
        var affected = rows.Where(entry => entry.Task.NeedsAttention).Select(entry => entry.Task.Id).ToHashSet();
        var current = snapshot.Connected ? snapshot.Threads.Where(task => affected.Contains(task.Id)).ToArray() : [];
        return new QuietDigest(rows.Count(entry => entry.Task.State == "idle"), current.Count(task => task.State == "needsInput"),
            current.Count(task => task.State == "needsApproval"), current.Count(task => task.State == "error"), rows.Length,
            current.Count(task => task.State is "active" or "idle" && task.HasQuestion
                && rows.Any(entry => entry.Task.Id == task.Id && entry.Task.State == "question" && entry.Task.QuestionIds.Intersect(task.QuestionIds).Any())));
    }
}
