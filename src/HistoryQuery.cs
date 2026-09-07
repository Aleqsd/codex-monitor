namespace CodexMonitor;

internal enum HistoryFilter { All, Questions, Errors, Responses, Quota }
internal static class HistoryQuery
{
    internal static HistoryEntry[] Select(IEnumerable<HistoryEntry> entries, string search, HistoryFilter filter, bool pending, NotificationHistory history, MonitorSnapshot snapshot)
        => entries.Where(e => (search.Length == 0 || e.Task.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || e.Task.Project.Contains(search, StringComparison.OrdinalIgnoreCase))
            && (filter switch { HistoryFilter.Questions => e.Task.State is "question" or "needsInput" or "needsApproval", HistoryFilter.Errors => e.Task.State == "error", HistoryFilter.Responses => e.Task.State == "idle", HistoryFilter.Quota => e.Task.State == "quota", _ => true })
            && (!pending || history.CurrentLabel(e, snapshot) == "À traiter")).ToArray();
}
