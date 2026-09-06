using CodexMonitor;

internal static class QuietReturnChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
        var now = DateTimeOffset.UtcNow;
        var queue = new NotificationQueue();
        var center = new NotificationCenter(new NotificationHistory(), queue);
        var active = new MonitorSnapshot(true, now, [new("task", "Example", "", "", "active")], null);
        var idle = active with { Threads = [active.Threads[0] with { State = "idle" }] };
        center.Update(active, false, true, true, 7, now);
        center.Update(idle, false, true, true, 7, now);
        queue.Advance(6.8f, 3);
        center.Update(idle, true, true, true, 7, now.AddSeconds(10));
        center.Update(idle, false, true, true, 7, now.AddSeconds(20));
        var remaining = queue.Visible()[0].Duration - queue.Visible()[0].Age;
        Check(remaining >= 6.99f, $"Interrupted toast returns with a full 7s, not just its last fraction ({remaining:0.00}s)");
        queue.Advance(3, 3);
        center.Update(idle with { ReceivedAt = now.AddSeconds(21) }, false, true, true, 7, now.AddSeconds(21));
        Check(queue.Visible()[0].Age == 3, "Routine snapshots do not keep restarting notification timers");
        queue.Advance(4, 3);
        Check(queue.Count == 0, "Resumed toast still expires after its full visible duration");

        var mode = new QuietModeGate();
        Check(!mode.Update(false, now), "No delay when the plugin starts outside combat");
        var sounds = new List<string>();
        queue = new NotificationQueue(); center = new NotificationCenter(new NotificationHistory(), queue, sounds.Add);
        void Tick(MonitorSnapshot snapshot, bool combat, double seconds) => center.Update(snapshot,
            mode.Update(combat, now.AddSeconds(seconds)), true, true, 7, now.AddSeconds(seconds));
        Tick(active, false, 0); Tick(idle, true, 1);
        Check(center.IsQuiet && center.DeferredCount == 1 && queue.Count == 0 && sounds.Count == 0, "Combat immediately defers popup and sound");
        Tick(idle, false, 10); Tick(idle, false, 10.1); Tick(idle, true, 10.2);
        Check(center.IsQuiet && queue.Count == 0 && sounds.Count == 0 && center.DeferredCount == 1, "Brief combat-flag release creates neither a flashing summary nor a sound");
        Tick(idle, false, 20); Tick(idle, false, 21.99);
        Check(center.IsQuiet && queue.Count == 0, "A new combat/cutscene restarts the entire two-second release delay");
        Tick(idle, false, 22);
        Check(!center.IsQuiet && queue.Count == 1 && queue.Visible()[0].Task.State == "summary"
            && queue.Visible()[0].Age == 0 && sounds.Count == 1, "Stable return creates one fresh summary and one sound");
        queue.Advance(6.8f, 3); Tick(idle, true, 30); Tick(idle, false, 40); Tick(idle, false, 42);
        Check(queue.Visible()[0].Age == 0 && sounds.Count == 1, "An interrupted summary gets its full timer back without repeating the sound");
        queue.Advance(6, 3); Tick(idle, false, 48);
        Check(queue.Count == 1 && queue.Visible()[0].Age == 6, "Resumed summary remains present for six visible seconds");
        queue.Advance(1, 3); Check(queue.Count == 0, "Resumed summary expires at seven visible seconds");

        Tick(active, false, 50); Tick(idle, true, 51); Tick(idle, false, 52);
        Tick(MonitorSnapshot.Offline("offline"), false, 52.1); Tick(idle, false, 54);
        Check(queue.Count == 0 && center.DeferredCount == 0 && sounds.Count == 1, "Disconnect during release delay cannot resurrect old alerts");
        return checks;
    }
}
