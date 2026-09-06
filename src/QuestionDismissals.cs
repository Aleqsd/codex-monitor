namespace CodexMonitor;

public sealed record DismissedQuestions(string ThreadId, string[] QuestionIds);

/// <summary>Local display choices, keyed to exact questions. Never answers a Codex request.</summary>
public sealed class QuestionDismissals
{
    private readonly Dictionary<string, string[]> dismissed;
    private MonitorSnapshot? source, projected;
    public QuestionDismissals(IEnumerable<DismissedQuestions>? saved = null) => dismissed = Clean(saved).ToDictionary(row => row.ThreadId, row => row.QuestionIds);
    public static List<DismissedQuestions> Clean(IEnumerable<DismissedQuestions>? rows) => (rows ?? [])
        .Where(row => row is not null && row.ThreadId is { Length: > 0 and <= 128 })
        .GroupBy(row => row.ThreadId).TakeLast(200)
        .Select(group => new DismissedQuestions(group.Key, group.SelectMany(row => row.QuestionIds ?? [])
            .Where(id => id is { Length: 32 } && id.All(Uri.IsHexDigit)).Distinct().TakeLast(100).ToArray()))
        .Where(row => row.QuestionIds.Length > 0).ToList();
    public List<DismissedQuestions> Export() => dismissed.Select(row => new DismissedQuestions(row.Key, row.Value.ToArray())).ToList();
    public bool Dismiss(MonitoredThread task)
    {
        if (!task.HasQuestion) return false;
        dismissed[task.Id] = (task.HiddenQuestionIds ?? []).Concat(task.QuestionIds).Distinct().TakeLast(100).ToArray();
        if (dismissed.Count > 200) dismissed.Remove(dismissed.Keys.First());
        source = null;
        return true;
    }
    public bool Restore(string threadId)
    {
        if (!dismissed.Remove(threadId)) return false;
        source = null;
        return true;
    }
    public MonitorSnapshot Apply(MonitorSnapshot snapshot)
    {
        if (ReferenceEquals(source, snapshot)) return projected!;
        source = snapshot;
        if (!snapshot.Connected || dismissed.Count == 0) return projected = snapshot;
        return projected = snapshot with { Threads = snapshot.Threads.Select(task =>
        {
            var hidden = task.QuestionIds.Intersect(dismissed.GetValueOrDefault(task.Id) ?? []).ToArray();
            return hidden.Length == 0 ? task : task with { PendingQuestionIds = task.QuestionIds.Except(hidden).ToArray(), HiddenQuestionIds = hidden };
        }).ToArray() };
    }
}
