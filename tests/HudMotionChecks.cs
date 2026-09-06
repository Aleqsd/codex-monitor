using CodexMonitor;

internal static class HudMotionChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
        var snapshot = new MonitorSnapshot(true, DateTimeOffset.UtcNow, [new("task", "Example", "", "", "active")], null,
            true, new AccountUsage(DateTimeOffset.UtcNow, [new(48, 10080, null)]));
        var motion = new HudMotion();
        var first = motion.Update(snapshot, true, 0);
        Check(first.ActivePulse == 0 && first.UsagePulse == 0 && first.QuotaFraction == 0.48f, "First HUD display has no false activity animation");
        var changed = snapshot with { Threads = [snapshot.Threads[0] with { State = "idle", PendingQuestionIds = ["question"] }],
            Usage = new AccountUsage(DateTimeOffset.UtcNow, [new(46, 10080, null)]) };
        var start = motion.Update(changed, true, 0);
        Check(start.ActivePulse == 1 && start.AttentionPulse == 1 && start.UsagePulse == 1, "Only real HUD value changes start visual transitions");
        var middle = motion.Update(changed with { ReceivedAt = DateTimeOffset.UtcNow }, true, 0.2f);
        Check(middle.QuotaFraction > 0.46f && middle.QuotaFraction < 0.48f && middle.ActivePulse < 1, "Gauge eases toward current quota and identical polls do not restart the pulse");
        var finish = motion.Update(changed, true, 0.25f);
        Check(Math.Abs(finish.QuotaFraction!.Value - 0.46f) < 0.0001f, "Gauge reaches its target in 450ms");
        finish = motion.Update(changed, true, 0.25f);
        Check(finish.ActivePulse == 0 && finish.AttentionPulse == 0 && finish.UsagePulse == 0, "HUD settles completely without looping or blinking");
        var disabled = motion.Update(snapshot, false, 0.1f);
        Check(disabled.ActivePulse == 0 && disabled.QuotaFraction == 0.48f && motion.Update(snapshot, true, 0.1f).ActivePulse == 0,
            "Disabling motion snaps to real values and re-enabling does not replay changes");
        motion.Update(changed, true, 0.1f);
        var offline = motion.Update(MonitorSnapshot.Offline("offline"), true, 0.1f);
        Check(offline.QuotaFraction is null && offline.ActivePulse == 0, "Disconnect immediately removes the animated quota");
        Check(motion.Update(snapshot, true, 0.1f).UsagePulse == 0, "Reconnect displays real quota without an old transition");
        var missing = motion.Update(snapshot with { Usage = null }, true, 0.1f);
        Check(missing.QuotaFraction is null && missing.UsagePulse == 0, "Missing quota cannot leave a lingering gauge");
        motion.Update(snapshot, true, 0.1f);
        var shortWindow = motion.Update(snapshot with { Usage = new AccountUsage(DateTimeOffset.UtcNow, [new(90, 300, null)]) }, true, 0.1f);
        Check(shortWindow.QuotaFraction == 0.9f, "Changing quota period never interpolates two different allowances");
        motion.Highlight(); var invalid = motion.Update(snapshot, true, float.NaN);
        Check(float.IsFinite(invalid.ActivePulse) && invalid.ActivePulse is >= 0 and <= 1, "Invalid frame time cannot corrupt HUD motion");
        motion.Reset(); Check(motion.Update(snapshot, true, 0).ActivePulse == 0, "Reopening a hidden HUD starts from its current state");
        return checks;
    }
}
