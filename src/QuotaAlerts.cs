namespace CodexMonitor;

public sealed record QuotaCheckpoint(string Key, int[] Triggered);
public sealed class QuotaAlertOptions
{
    public bool Enabled { get; set; } = true;
    public List<int> Thresholds { get; set; } = [20, 10, 5];
    public List<QuotaCheckpoint> Checkpoints { get; set; } = [];
    public void Normalize()
    {
        Thresholds = (Thresholds ?? [20, 10, 5]).Where(n => n is >= 1 and <= 99).Distinct().OrderDescending().Take(5).ToList();
        Checkpoints = (Checkpoints ?? []).Where(c => c is not null && c.Key is { Length: > 0 and < 80 })
            .DistinctBy(c => c.Key).TakeLast(8).Select(c => c with { Triggered = (c.Triggered ?? []).Where(n => n is >= 1 and <= 99).Distinct().ToArray() }).ToList();
    }
}

internal sealed record QuotaNotice(string Key, int Threshold, UsageWindow Window)
{
    internal MonitoredThread Task(DateTimeOffset now) => new("quota-" + Key, $"{Window.Percent} restants · {Window.Period}",
        ResetLabel(Window.ResetsAt, now), "", "quota");
    internal static string ResetLabel(long? reset, DateTimeOffset now)
    {
        if (reset is null) return "Renouvellement non fourni par Codex";
        var delay = DateTimeOffset.FromUnixTimeSeconds(reset.Value) - now;
        return delay.TotalDays >= 1 ? $"Renouvellement dans {(int)delay.TotalDays} j {delay.Hours} h"
            : delay.TotalHours >= 1 ? $"Renouvellement dans {(int)delay.TotalHours} h {delay.Minutes} min"
            : $"Renouvellement dans {Math.Max(1, (int)Math.Ceiling(delay.TotalMinutes))} min";
    }
}

internal sealed class QuotaAlerts
{
    private readonly Dictionary<string, double> previous = new();
    private readonly Dictionary<string, QuotaNotice> deferred = new();
    internal bool Changed { get; private set; }
    internal IReadOnlyList<QuotaNotice> Update(MonitorSnapshot snapshot, QuotaAlertOptions options, bool quiet, bool outside, DateTimeOffset now)
    {
        Changed = false;
        if (!snapshot.Connected || snapshot.Usage is null) { deferred.Clear(); previous.Clear(); return []; }
        var windows = snapshot.Usage.ValidWindows(now).ToArray();
        var fresh = new List<QuotaNotice>();
        var validKeys = new HashSet<string>();
        foreach (var window in windows)
        {
            // Without a reset identity there is no reliable once-per-period guarantee.
            if (window.ResetsAt is null || window.WindowDurationMins is null) continue;
            var key = $"{window.WindowDurationMins}:{window.ResetsAt}"; validKeys.Add(key);
            var checkpoint = options.Checkpoints.FirstOrDefault(c => c.Key == key);
            var fired = checkpoint?.Triggered.ToHashSet() ?? [];
            var initial = !previous.TryGetValue(key, out var before);
            var crossed = options.Thresholds.Where(t => window.RemainingPercent <= t && !fired.Contains(t) && (initial || before > t)).ToArray();
            previous[key] = window.RemainingPercent;
            if (crossed.Length == 0) continue;
            fired.UnionWith(crossed);
            options.Checkpoints.RemoveAll(c => c.Key == key); options.Checkpoints.Add(new(key, fired.ToArray()));
            if (options.Checkpoints.Count > 8) options.Checkpoints.RemoveAt(0);
            Changed = true;
            if (!initial && options.Enabled && !outside) fresh.Add(new(key, crossed.Min(), window));
        }
        foreach (var key in previous.Keys.Where(k => !validKeys.Contains(k)).ToArray()) previous.Remove(key);
        foreach (var notice in fresh) deferred[notice.Key] = notice;
        foreach (var key in deferred.Keys.ToArray())
        {
            var current = windows.FirstOrDefault(w => $"{w.WindowDurationMins}:{w.ResetsAt}" == key);
            if (outside || !options.Enabled || current is null || current.RemainingPercent > deferred[key].Threshold) deferred.Remove(key);
            else deferred[key] = deferred[key] with { Window = current };
        }
        if (quiet || outside) return [];
        var result = deferred.Values.OrderBy(n => n.Window.RemainingPercent).ToArray(); deferred.Clear(); return result;
    }
}
