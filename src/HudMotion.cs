namespace CodexMonitor;

public readonly record struct HudMotionFrame(float ActivePulse, float AttentionPulse, float UsagePulse, float? QuotaFraction);

/// <summary>Short visual transitions only: the labels always show the current values.</summary>
public sealed class HudMotion
{
    private const float PulseSeconds = 0.65f, GaugeSeconds = 0.45f;
    private bool initialized, connected;
    private int active, attention, questions;
    private int? period;
    private float activeTime, attentionTime, usageTime, gaugeTime;
    private float? fraction, target;
    private float gaugeStart;

    public void Reset() => initialized = false;
    public void Highlight() { activeTime = attentionTime = usageTime = PulseSeconds; }

    public HudMotionFrame Update(MonitorSnapshot snapshot, bool enabled, float deltaSeconds)
    {
        var usage = snapshot.CurrentUsage;
        var next = usage is null ? (float?)null : (float)usage.RemainingPercent / 100;
        var questionCount = snapshot.Threads.Count(task => task.HasQuestion);
        var reset = !initialized || !enabled || connected != snapshot.Connected;
        var dt = float.IsFinite(deltaSeconds) ? Math.Clamp(deltaSeconds, 0, 0.25f) : 0;
        activeTime = Math.Max(0, activeTime - dt); attentionTime = Math.Max(0, attentionTime - dt); usageTime = Math.Max(0, usageTime - dt);
        if (reset)
        {
            activeTime = attentionTime = usageTime = gaugeTime = 0;
            fraction = target = next;
        }
        else
        {
            if (active != snapshot.Active) activeTime = PulseSeconds;
            if (attention != snapshot.Attention || questions != questionCount) attentionTime = PulseSeconds;
            if (next != target || period != usage?.WindowDurationMins)
            {
                usageTime = next is null ? 0 : PulseSeconds;
                // Unknown/new periods must not briefly display an old allowance.
                if (next is null || fraction is null || period != usage?.WindowDurationMins)
                { fraction = next; gaugeTime = 0; }
                else { gaugeStart = fraction.Value; gaugeTime = GaugeSeconds; }
                target = next;
            }
            if (gaugeTime > 0 && target is { } end)
            {
                gaugeTime = Math.Max(0, gaugeTime - dt);
                var t = 1 - gaugeTime / GaugeSeconds;
                var eased = 1 - MathF.Pow(1 - t, 3);
                fraction = gaugeStart + (end - gaugeStart) * eased;
            }
        }
        initialized = true; connected = snapshot.Connected; active = snapshot.Active; attention = snapshot.Attention;
        questions = questionCount; period = usage?.WindowDurationMins;
        float Pulse(float time) => MathF.Pow(time / PulseSeconds, 2);
        return new(Pulse(activeTime), Pulse(attentionTime), Pulse(usageTime), fraction);
    }
}
