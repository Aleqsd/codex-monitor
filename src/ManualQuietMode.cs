namespace CodexMonitor;

// Session state: intentionally absent from persisted configuration.
public sealed class ManualQuietMode
{
    public bool Enabled { get; private set; }
    public DateTimeOffset? Until { get; private set; }
    public void Start(int? minutes, DateTimeOffset now)
    { Enabled = true; Until = minutes is { } m ? now.AddMinutes(Math.Clamp(m, 1, 1440)) : null; }
    public void Stop() { Enabled = false; Until = null; }
    public bool Update(DateTimeOffset now)
    { if (Enabled && Until <= now) Stop(); return Enabled; }
    public string Label(DateTimeOffset now) => !Enabled ? "Alertes actives" : Until is { } until
        ? $"Alertes en pause · {Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes))} min" : "Alertes en pause · cette session";
    public bool Command(string argument, DateTimeOffset now)
    {
        if (argument is "off" or "reprendre") { Stop(); return true; }
        if (argument is "on" or "session") { Start(null, now); return true; }
        if (argument.Length == 0) { if (Update(now)) Stop(); else Start(30, now); return true; }
        if (int.TryParse(argument, out var minutes) && minutes is >= 1 and <= 1440) { Start(minutes, now); return true; }
        return false;
    }
}
