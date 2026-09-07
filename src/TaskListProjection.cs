namespace CodexMonitor;

internal sealed class TaskListProjection
{
    private (MonitorSnapshot Snapshot, bool Idle, bool Unobserved, int State, string Search)? key;
    private MonitoredThread[] rows = [];
    internal MonitoredThread[] Get(MonitorSnapshot snapshot, bool idle, bool unobserved, int state, string search)
    {
        if (key is { } old && ReferenceEquals(old.Snapshot, snapshot) && old.Idle == idle && old.Unobserved == unobserved && old.State == state && old.Search == search) return rows;
        key = (snapshot, idle, unobserved, state, search);
        return rows = snapshot.Threads.Where(task => (idle || state == 3 || task.State != "idle" || task.NeedsAttention)
            && (unobserved || task.IsObserved || task.NeedsAttention)
            && (state == 0 || state == 1 && task.State == "active" || state == 2 && task.NeedsAttention || state == 3 && task.State == "idle")
            && (search.Length == 0 || task.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || task.Project.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(task => task.NeedsAttention ? 0 : task.State == "active" ? 1 : task.State == "idle" ? 2 : 3)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
