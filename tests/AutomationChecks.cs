using CodexMonitor;

internal static class AutomationChecks
{
    internal static async Task<int> Run()
    {
        var checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var now = DateTimeOffset.UtcNow;
        var manual = new ManualQuietMode();
        Check(!manual.Update(now), "DND starts disabled");
        manual.Start(30, now);
        Check(manual.Update(now.AddMinutes(29)) && manual.Label(now.AddMinutes(29)).Contains("1 min"), "DND duration and label");
        Check(!manual.Update(now.AddMinutes(30)), "DND expires exactly at its deadline");
        manual.Start(null, now); Check(manual.Update(now.AddDays(2)), "Session pause has no timer");
        manual.Stop(); Check(!manual.Enabled && manual.Until is null, "Logout/reset clears manual pause");
        Check(manual.Command("15", now) && manual.Enabled && manual.Command("off", now) && !manual.Enabled, "DND commands start and resume");
        Check(!manual.Command("-3", now) && !manual.Command("wat", now) && !manual.Enabled, "Invalid DND command has no side effect");
        var quiet = new QuietModeGate(); manual.Start(1, now);
        Check(quiet.Update(manual.Update(now), now), "Manual pause enters shared quiet gate");
        Check(quiet.Update(manual.Update(now.AddMinutes(2)) || true, now.AddMinutes(2)), "Combat keeps quiet after timer expires");
        Check(quiet.Update(false, now.AddMinutes(3)) && !quiet.Update(false, now.AddMinutes(3).AddSeconds(2)), "Shared calm delay still applies");

        var task = new MonitoredThread("11111111-2222-4333-8444-555555555555", "Trois questions", "Projet fictif", "", "idle", [new('a', 32), new('b', 32), new('c', 32)]);
        var snapshot = new MonitorSnapshot(true, now, [task], null, true);
        var projection = new TaskListProjection();
        Check(projection.Get(snapshot, false, false, 2, "").Length == 1, "Attention view includes idle question despite idle preference");
        Check(projection.Get(snapshot, false, false, 0, "").Length == 1, "Default view keeps actionable idle task");
        var rows = projection.Get(snapshot, false, false, 0, "");
        Check(ReferenceEquals(rows, projection.Get(snapshot, false, false, 0, "")), "Stable list projection is reused");
        Check(projection.Get(snapshot, false, false, 0, "absent").Length == 0, "Search remains intentional");
        Check(snapshot.Attention == 1 && snapshot.Questions == 3, "Task and question counts are distinct");
        var payload = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion=1, connected=true, generatedAt=now,
            threads = new[] { new { id=task.Id, title=task.Title, state="idle", availability="live", lastConfirmedAt=now } }, relayVersion=42, quotaDiagnostic=false });
        Check(MonitorContract.Parse(payload, now).Threads.Count == 1, "Malformed optional diagnostics cannot hide task states");
        var cleared = snapshot with { Threads = [task with { PendingQuestionIds = [] }] };
        Check(projection.Get(cleared, false, false, 0, "").Length == 0, "Resolved idle question follows idle preference again");

        var queue = new NotificationQueue(); var history = new NotificationHistory(); var sounds = new List<string>();
        var center = new NotificationCenter(history, queue, sounds.Add);
        var active = snapshot with { Threads = [task with { State = "active", PendingQuestionIds = [] }] };
        center.Update(active, false, true, true, 7, now);
        center.Update(snapshot, false, true, true, 7, now.AddSeconds(1));
        Check(queue.Count == 1 && sounds.Count == 1, "Initial response and question coalesce per task");
        center.BeginManualPause(); Check(queue.Count == 0 && center.IsQuiet, "Manual pause collects queued alerts immediately");
        center.Update(cleared, true, true, true, 7, now.AddSeconds(2));
        Check(queue.Count == 0 && sounds.Count == 1 && history.Entries.Count > 0, "Pause keeps history and suppresses alerts and sounds");
        center.Update(cleared, false, true, true, 7, now.AddSeconds(3));
        Check(queue.Count == 1 && queue.Visible()[0].Task.State == "summary" && !queue.Visible()[0].Task.Title.Contains("question"), "One summary excludes resolved questions");
        center.Update(cleared, false, true, true, 7, now.AddSeconds(4)); Check(queue.Count == 1, "Summary is not replayed");

        var ready = new RelayLaunchState(RelayPhase.Ready, "Ready");
        var auto = new RelayAutoStart();
        Check(!auto.ShouldStart(false, true, false, false, ready, now), "Automatic relay is opt-in");
        Check(!auto.ShouldStart(true, false, false, false, ready, now), "No launch at title screen");
        Check(!auto.ShouldStart(true, true, true, false, ready, now), "Loading delays automatic launch");
        Check(auto.ShouldStart(true, true, false, false, ready, now), "Login starts relay once");
        Check(!auto.ShouldStart(true, true, false, false, ready, now.AddSeconds(1)), "Frame updates cannot duplicate launch");
        Check(!auto.ShouldStart(true, true, false, true, ready, now.AddMinutes(1)), "Connected external relay is reused");
        Check(!auto.ShouldStart(true, true, false, false, new(RelayPhase.Error, "Node missing"), now.AddMinutes(1)), "Configuration error is not retried automatically");
        var transient = new RelayLaunchState(RelayPhase.Error, "Timeout", true);
        Check(auto.ShouldStart(true, true, false, false, transient, now.AddMinutes(1)), "Transient error can retry");
        Check(auto.ShouldStart(true, true, false, false, transient, now.AddMinutes(2)), "Last bounded retry");
        Check(!auto.ShouldStart(true, true, false, false, transient, now.AddMinutes(3)), "Retry budget exhausted");
        auto.ManualStart(); auto.ManualStop();
        Check(!auto.ShouldStart(true, true, false, false, ready, now.AddDays(1)), "Manual stop survives frame updates");
        auto.ShouldStart(true, false, false, false, ready, now);
        Check(auto.ShouldStart(true, true, false, false, ready, now), "Next login resets manual stop");

        var weekly = new UsageWindow(78, 10080); var shortWindow = new UsageWindow(0, 300);
        var usage = new AccountUsage(now, [weekly, shortWindow]);
        Check(usage.Current(now) == weekly && usage.Current(now, UsagePreference.ShortWindow) == shortWindow && usage.Current(now, UsagePreference.Limiting) == shortWindow, "All quota period choices work");
        Check(usage.OtherExhausted(now, weekly) && !usage.OtherExhausted(now, shortWindow), "Other exhausted period warning preserves selected value");
        Check(usage.Current(now.AddSeconds(121)) is null && !usage.OtherExhausted(now.AddSeconds(121), weekly), "Stale quota never gives an exhaustion warning");

        var retry = new RetrySchedule(); retry.Fail(now);
        Check(!retry.CanRetry(now.AddSeconds(4)) && retry.CanRetry(now.AddSeconds(5)), "Emoji first retry is delayed");
        retry.Begin(); Check(!retry.CanRetry(now.AddMinutes(1)), "Only one emoji attempt can be queued");
        retry.Fail(now); Check(!retry.CanRetry(now.AddSeconds(29)) && retry.CanRetry(now.AddSeconds(30)), "Emoji retry backs off");
        retry.Begin(); retry.Fail(now); Check(!retry.CanRetry(now.AddDays(1)), "Emoji retries stop after three failures");
        var cache = new BoundedCache<int, string>(2); cache.Add(1, "one"); cache.Add(2, "two"); cache.Add(3, "three");
        Check(cache.Count == 2 && cache.TryGetValue(2, out _) && !cache.TryGetValue(1, out _), "Cache evicts progressively instead of clearing everything");

        var clock = now;
        var link = new CodexTaskLink(_ => throw new InvalidOperationException(), () => clock);
        await link.Open(task.Id);
        Check(link.ErrorFor(task.Id) is not null && link.ErrorFor("22222222-3333-4444-8555-666666666666") is null, "Open failure belongs to one task");
        clock = now.AddSeconds(21); Check(link.ErrorFor(task.Id) is null, "Open failure expires");
        await link.Open(task.Id); link.DismissError(task.Id); Check(link.Error is null, "Open failure can be dismissed");
        return checks;
    }
}
