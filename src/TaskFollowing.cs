namespace CodexMonitor;

public sealed record MutedTask(string Id, DateTimeOffset Until);

public sealed class FollowingOptions
{
    public bool AllProjects { get; set; } = true;
    public List<string> Projects { get; set; } = [];
    public List<string> Favorites { get; set; } = [];
    public List<MutedTask> Muted { get; set; } = [];
    public void Normalize()
    {
        Projects = (Projects ?? []).Where(x => x is { Length: > 0 and <= 200 }).Distinct().Take(200).ToList();
        Favorites = (Favorites ?? []).Where(x => Guid.TryParse(x, out _)).Distinct().Take(200).ToList();
        Muted = (Muted ?? []).Where(x => x is not null && Guid.TryParse(x.Id, out _) && x.Until > DateTimeOffset.UtcNow)
            .DistinctBy(x => x.Id).Take(200).ToList();
    }
    public bool Allows(MonitoredThread task, DateTimeOffset now) => (AllProjects || Projects.Contains(task.ProjectKey)) && !Muted.Any(m => m.Id == task.Id && m.Until > now);
    public void ToggleFavorite(string id) { if (!Favorites.Remove(id)) Favorites.Add(id); }
    public void Mute(string id, int minutes, DateTimeOffset now) { Muted.RemoveAll(x => x.Id == id); if (minutes > 0) Muted.Add(new(id, now.AddMinutes(minutes))); }
}

internal sealed class TaskFollowing
{
    private MonitorSnapshot? source, projected;
    public void Invalidate() => source = null;
    public MonitorSnapshot Apply(MonitorSnapshot snapshot, FollowingOptions options)
    {
        if (ReferenceEquals(source, snapshot)) return projected!;
        source = snapshot;
        var projects = options.Projects.ToHashSet(); var favorites = options.Favorites.ToHashSet();
        return projected = snapshot with { Threads = snapshot.Threads.Where(t => options.AllProjects || projects.Contains(t.ProjectKey))
            .Select(t => t with { IsFavorite = favorites.Contains(t.Id) }).ToArray() };
    }
}
