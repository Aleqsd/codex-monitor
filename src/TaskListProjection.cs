namespace CodexMonitor;

internal sealed class TaskListProjection
{
    private (MonitorSnapshot Snapshot, bool Idle, bool Unobserved, int State, string Search)? key;
    private MonitoredThread[] rows = [];
    internal MonitoredThread[] Get(MonitorSnapshot snapshot, bool idle, bool unobserved, int state, string search)
    {
        if (key is { } old && ReferenceEquals(old.Snapshot, snapshot) && old.Idle == idle && old.Unobserved == unobserved && old.State == state && old.Search == search) return rows;
        key = (snapshot, idle, unobserved, state, search);
        return rows = snapshot.Threads.Where(task => (idle || state is 3 or 4 || task.State != "idle" || task.NeedsAttention || task.ResponseReady)
            && (unobserved || task.IsObserved || task.NeedsAttention)
            && (state == 0 || state == 1 && task.State == "active" || state == 2 && task.NeedsAttention || state == 3 && task.State == "idle" || state == 4 && task.ResponseReady || state == 5 && task.IsFavorite)
            && (search.Length == 0 || task.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || task.Project.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(task => task.IsFavorite).ThenBy(task => task.NeedsAttention ? 0 : task.ResponseReady ? 1 : task.State == "active" ? 2 : task.State == "idle" ? 3 : 4)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
