using System.Text.Json;

namespace CodexMonitor;

public enum UsagePreference { Weekly, ShortWindow, Limiting }

public sealed record UsageWindow(double RemainingPercent = double.NaN, int? WindowDurationMins = null, long? ResetsAt = null)
{
    public string Period => WindowDurationMins switch
    {
        10080 => "Semaine", 300 => "5 heures", 1440 => "24 heures",
        > 0 => $"{WindowDurationMins} min", _ => "Période Codex",
    };
    public string Percent => $"{Math.Floor(RemainingPercent):0}%";
}

public sealed record AccountUsage(DateTimeOffset FetchedAt, IReadOnlyList<UsageWindow> Windows)
{
    // Prefer the weekly allowance; installations with only a short window still work.
    public IEnumerable<UsageWindow> ValidWindows(DateTimeOffset now) => (now - FetchedAt).Duration() > TimeSpan.FromSeconds(120)
        ? [] : Windows.Where(window => window.ResetsAt is null || window.ResetsAt > now.ToUnixTimeSeconds());
    public UsageWindow? Current(DateTimeOffset now, UsagePreference preference = UsagePreference.Weekly)
    {
        var valid = ValidWindows(now);
        return preference switch
        {
            UsagePreference.ShortWindow => valid.OrderBy(window => window.WindowDurationMins ?? int.MaxValue).FirstOrDefault(),
            UsagePreference.Limiting => valid.OrderBy(window => window.RemainingPercent).FirstOrDefault(),
            _ => valid.OrderByDescending(window => window.WindowDurationMins ?? 0).FirstOrDefault(),
        };
    }
    public bool OtherExhausted(DateTimeOffset now, UsageWindow? selected) => selected is not null
        && ValidWindows(now).Any(window => window != selected && window.RemainingPercent <= 0);

    public static AccountUsage? Parse(JsonElement element)
    {
        try
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            var usage = element.Deserialize<AccountUsage>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (usage?.Windows is null || usage.Windows.Count is < 1 or > 2) return null;
            var windows = usage.Windows.Where(window => window is not null && double.IsFinite(window.RemainingPercent)
                && window.RemainingPercent is >= 0 and <= 100
                && (window.WindowDurationMins is null or > 0)
                && (window.ResetsAt is null or > 0 and < 253402300800)).ToArray();
            return windows.Length == 0 ? null : usage with { Windows = windows };
        }
        catch (JsonException) { return null; }
    }
}

public sealed record QuotaDiagnostic(string Status)
{
    public string Message => Status switch
    {
        "ready" => "Quota actualisé", "loading" => "Lecture du quota…", "cliMissing" => "CLI Codex introuvable · installer le CLI ou configurer son chemin dans le relais",
        "authRequired" => "Connexion au compte requise dans le CLI Codex", "timeout" => "Le CLI ne répond pas · nouvelle tentative automatique",
        "unsupported" => "Limites Codex indisponibles pour ce compte ou cette version du CLI",
        "protocolError" => "Réponse du CLI incompatible · vérifier sa version", "processError" => "Le CLI Codex ne démarre pas ou s’est arrêté",
        "stale" => "Dernier quota périmé · en attente d’une actualisation", "stopped" => "Lecture du quota arrêtée", _ => "Quota indisponible · vérifier le CLI Codex",
    };
}
