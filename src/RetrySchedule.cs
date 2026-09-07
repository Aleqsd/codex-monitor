namespace CodexMonitor;

internal sealed class RetrySchedule
{
    internal int Failures { get; private set; }
    private DateTimeOffset next;
    private bool pending = true;
    internal bool CanRetry(DateTimeOffset now) => !pending && Failures is > 0 and < 3 && now >= next;
    internal void Begin() => pending = true;
    internal void Fail(DateTimeOffset now) { pending = false; Failures++; next = now.AddSeconds(Failures == 1 ? 5 : 30); }
}
