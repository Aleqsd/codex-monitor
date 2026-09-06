using System.Text.Json;
using CodexMonitor;

internal static class QuestionDismissalChecks
{
    internal static int Run()
    {
        var count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        var q1 = new string('a', 32); var q2 = new string('b', 32);
        var task = new MonitoredThread("demo", "Tâche exemple", "Projet", "", "idle", [q1]);
        MonitorSnapshot Snapshot(MonitoredThread row) => new(true, DateTimeOffset.UtcNow, [row], null, true);
        var source = Snapshot(task); var choices = new QuestionDismissals();
        Check(choices.Dismiss(task), "Current asynchronous question can be dismissed");
        var hidden = choices.Apply(source);
        Check(hidden.Attention == 0 && hidden.Idle == 1 && source.Attention == 1, "Display dismissal preserves raw source and idle status");
        Check(ReferenceEquals(hidden, choices.Apply(source)), "Per-frame projection is cached");
        var history = new NotificationHistory(); var entry = history.Add(task with { State = "question" }, DateTimeOffset.UtcNow);
        Check(history.CurrentLabel(entry, hidden) == "Question masquée dans FF14", "Dismissal is never described as an answer");
        foreach (var state in new[] { "needsInput", "needsApproval", "error" })
            Check(choices.Apply(Snapshot(task with { State = state })).Attention == 1, "Blocking intervention preserved: " + state);
        var json = JsonSerializer.Serialize(choices.Export());
        var reloaded = new QuestionDismissals(JsonSerializer.Deserialize<List<DismissedQuestions>>(json));
        Check(reloaded.Apply(source).Attention == 0, "Exact dismissals survive JSON reload");
        reloaded.Apply(MonitorSnapshot.Offline("offline"));
        Check(reloaded.Apply(source).Attention == 0, "Disconnect does not erase choices");
        var next = choices.Apply(Snapshot(task with { PendingQuestionIds = [q1, q2] }));
        Check(next.Attention == 1 && next.Threads[0].QuestionIds.SequenceEqual(new[] { q2 }), "A new question on the same task remains visible");
        Check(TransitionDetector.Find(hidden, next, true, true).Single().QuestionIds.SequenceEqual(new[] { q2 }), "Only new question generates an alert");
        Check(choices.Apply(Snapshot(task with { Id = "another" })).Attention == 1, "Other tasks remain visible even with matching question IDs");
        var queue = new NotificationQueue(); queue.Add(task with { State = "question" }, 7, entry.Id); queue.Reconcile(hidden);
        Check(queue.Count == 0, "Dismissing removes the current question toast");
        Check(NotificationCenter.Summarize(history.Entries, hidden).Questions == 0, "Quiet-mode digest excludes dismissed question");
        var sounds = 0; var center = new NotificationCenter(new(), queue, _ => sounds++);
        center.Update(hidden, false, true, true, 7, DateTimeOffset.UtcNow);
        choices.Restore(task.Id); center.RestoreQuestions(task.Id, [q1]);
        center.Update(choices.Apply(source), false, true, true, 7, DateTimeOffset.UtcNow);
        Check(choices.Apply(source).Attention == 1 && sounds == 0 && queue.Count == 0, "Restore shows existing question without replaying a toast or sound");
        // A new question arriving between frames must not be swallowed by restoration.
        choices.Dismiss(task); hidden = choices.Apply(source);
        center.Update(hidden, false, true, true, 7, DateTimeOffset.UtcNow);
        choices.Restore(task.Id); center.RestoreQuestions(task.Id, [q1]);
        center.Update(choices.Apply(Snapshot(task with { State = "needsApproval", PendingQuestionIds = [q1, q2] })), false, true, true, 7, DateTimeOffset.UtcNow);
        Check(sounds == 1 && queue.Visible().Single().Task.State == "needsApproval", "Restoring preserves simultaneous blocking state transition");
        var clean = QuestionDismissals.Clean([new("", [q1]), new("demo", ["bad", q1]), new("demo", [q1, q2])]);
        Check(clean.Count == 1 && clean[0].QuestionIds.Length == 2, "Invalid saved data is removed and duplicates merged");
        Console.WriteLine($"{count} question dismissal checks passed.");
        return count;
    }
}
