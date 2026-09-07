using CodexMonitor;

internal static class VisibilityChecks
{
    internal static int Run()
    {
        var count = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
        var options = new VisibilityOptions();
        Check(options.IsHidden(new(false)), "Title screen must hide by default.");
        Check(!options.IsHidden(new(true)), "Logged-in normal gameplay must remain visible.");
        foreach (var context in new[] { new GameContext(true, Loading: true), new(true, Cutscene: true), new(true, Gpose: true), new(true, UiHidden: true) })
            Check(options.IsHidden(context), "Default cinematic/loading/UI/photo visibility failed.");
        Check(!options.IsHidden(new(true, Combat: true, Duty: true)), "Combat and duty hiding must be opt-in.");
        options.HideInCombat = true;
        Check(options.IsHidden(new(true, Combat: true)), "Combat option failed.");
        options.HideInDuty = true;
        Check(options.IsHidden(new(true, Duty: true)), "Duty option failed.");
        options.HideOutsideGame = options.HideInCutscenes = options.HideInGpose = options.HideWhileLoading = false;
        Check(!options.IsHidden(new(false, Loading: true, Cutscene: true, Gpose: true)), "Visibility overrides failed.");
        Check(options.IsHidden(new(true, UiHidden: true)), "Manual UI hiding must always be respected.");
        Check(new GameContext(false).CanOpenManually && !new GameContext(true, Cutscene: true).CanOpenManually, "Manual settings access must not interrupt cinematics.");
        var now = DateTimeOffset.UtcNow;
        var active = new MonitorSnapshot(true, now, [new("fixture", "Exemple", "Démo", "", "active")], null);
        var idle = active with { Threads = [active.Threads[0] with { State = "idle" }] };
        var queue = new NotificationQueue(); var history = new NotificationHistory(); var sounds = 0;
        var center = new NotificationCenter(history, queue, _ => sounds++);
        center.Update(active, false, true, true, 7, now);
        center.Update(idle, true, true, true, 7, now);
        Check(center.DeferredCount == 1, "Hidden gameplay must defer notifications.");
        center.Suspend(idle);
        Check(center.DeferredCount == 0 && queue.Count == 0, "Logout must clear queued and deferred toasts.");
        center.Suspend(active); center.Suspend(idle);
        center.Update(idle, false, true, true, 7, now);
        Check(queue.Count == 0 && sounds == 0, "Login must not replay title-screen changes.");
        center.Update(active, false, true, true, 7, now);
        center.Update(idle, false, true, true, 7, now);
        Check(queue.Count == 1 && sounds == 1, "Fresh post-login changes must still notify.");
        queue.Advance(6.8f, 3); center.Update(idle, true, true, true, 7, now);
        center.Update(idle, false, true, true, 7, now);
        Check(queue.Visible()[0].Age == 0 && queue.Visible()[0].Duration == 7, "Hidden toast must regain a full reading interval.");
        return count;
    }
}
