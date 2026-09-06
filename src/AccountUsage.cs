using System.Text.Json;

namespace CodexMonitor;

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
    public UsageWindow? Current(DateTimeOffset now) => (now - FetchedAt).Duration() > TimeSpan.FromSeconds(120)
        ? null : Windows.OrderByDescending(window => window.WindowDurationMins ?? 0)
            .FirstOrDefault(window => window.ResetsAt is null || window.ResetsAt > now.ToUnixTimeSeconds());

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
