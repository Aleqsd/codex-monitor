namespace CodexMonitor;

internal sealed class RelayAutoStart
{
    private int attempts;
    private bool stopped, wasEnabled;
    private DateTimeOffset nextAttempt;
    internal void ManualStop() => stopped = true;
    internal void ManualStart() { stopped = false; attempts = 0; }
    internal bool ShouldStart(bool enabled, bool loggedIn, bool loading, bool connected, RelayLaunchState state, DateTimeOffset now)
    {
        if (!loggedIn) { attempts = 0; stopped = false; nextAttempt = default; }
        if (enabled && !wasEnabled) { attempts = 0; stopped = false; nextAttempt = default; }
        wasEnabled = enabled;
        if (!enabled || !loggedIn || loading || connected || stopped || attempts >= 3 || now < nextAttempt) return false;
        if (state.Phase is RelayPhase.Starting or RelayPhase.Running or RelayPhase.Stopping) return false;
        if (state.Phase == RelayPhase.Error && !state.Retryable) return false;
        attempts++; nextAttempt = now.AddSeconds(attempts == 1 ? 20 : 60); return true;
    }
    internal string Status => stopped ? "Arrêt manuel · reprise automatique à la prochaine connexion"
        : attempts >= 3 ? "Tentatives terminées · utiliser Lancer le relais pour réessayer" : "Automatique activé · après connexion au personnage";
}
