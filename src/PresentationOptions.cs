namespace CodexMonitor;

/// <summary>Hide immediately; resume only after a stable interval without combat/cutscenes.</summary>
public sealed class QuietModeGate
{
    public static readonly TimeSpan ReleaseDelay = TimeSpan.FromSeconds(2);
    private bool quiet;
    private DateTimeOffset? clearSince;
    public bool Update(bool requested, DateTimeOffset now)
    {
        if (requested) { quiet = true; clearSince = null; return true; }
        if (!quiet) return false;
        clearSince ??= now;
        if (now - clearSince.Value >= ReleaseDelay) { quiet = false; clearSince = null; }
        return quiet;
    }
}

public enum MiniHudStyle { Fil, Capsule, Balise, Lisere, Totem, ObsidienneFine, Ruban, Focus, TacheEpinglee }
public static class MiniHudOptions
{
    public static float FitScale(System.Numerics.Vector2 size, float requested, System.Numerics.Vector2 viewport) =>
        Math.Min(requested,Math.Min((viewport.X-24)/size.X,(viewport.Y-24)/size.Y));
    public static readonly string[] Names = ["Fil", "Capsule", "Balise", "Liseré", "Totem", "Panneau fin", "Ruban", "Focus", "Tâche épinglée"];
    public static System.Numerics.Vector2 Size(MiniHudStyle style, bool quota, HudAppearance? appearance = null)
    {
        var size = BaseSize(style, quota);
        if (appearance is null) return size;
        if (appearance.PadsMinimal(style)) size += new System.Numerics.Vector2(16, 8);
        size += 2 * (appearance.Padding + System.Numerics.Vector2.Abs(appearance.Text.Offset));
        return size * (appearance.Text.Size / 14);
    }
    private static System.Numerics.Vector2 BaseSize(MiniHudStyle style, bool quota) => style switch
    {
        MiniHudStyle.Fil => new(quota ? 218 : 164, 28),
        MiniHudStyle.Balise => new(58, 58),
        MiniHudStyle.Lisere => new(quota ? 260 : 206, 34),
        MiniHudStyle.Totem => new(58, quota ? 132 : 100),
        MiniHudStyle.ObsidienneFine => new(310, 60),
        MiniHudStyle.Ruban => new(quota ? 460 : 400, 34),
        MiniHudStyle.Focus => new(320, 86),
        MiniHudStyle.TacheEpinglee => new(350, 114),
        _ => new(quota ? 216 : 162, 36),
    };
}

public enum IndicatorMode { Text, MiniHud, Hidden }
public static class IndicatorOptions
{
    public static IndicatorMode Resolve(IndicatorMode? mode, bool legacyDtr, bool legacyHud) =>
        mode is { } selected && Enum.IsDefined(selected) ? selected
            : legacyDtr ? IndicatorMode.Text : legacyHud ? IndicatorMode.MiniHud : IndicatorMode.Hidden;
}

public enum SoundTone { Silent, Glass, Droplet, Velvet, Custom }
public sealed class SoundOptions
{
    public bool Enabled { get; set; } = true;
    public float Volume { get; set; } = 0.18f;
    public SoundTone Completion { get; set; } = SoundTone.Glass;
    public SoundTone Attention { get; set; } = SoundTone.Droplet;
    public SoundTone Error { get; set; } = SoundTone.Velvet;
    public string CompletionFile { get; set; } = "";
    public string AttentionFile { get; set; } = "";
    public string ErrorFile { get; set; } = "";

    public (SoundTone Tone, string File) Select(string state) => state switch
    {
        "idle" => (Completion, CompletionFile),
        "needsInput" or "needsApproval" or "question" => (Attention, AttentionFile),
        "error" => (Error, ErrorFile), _ => (SoundTone.Silent, ""),
    };
    public void Normalize()
    {
        Volume = NotificationGeometry.FiniteClamp(Volume, 0, 1, 0.18f);
        if (!Enum.IsDefined(Completion)) Completion = SoundTone.Glass;
        if (!Enum.IsDefined(Attention)) Attention = SoundTone.Droplet;
        if (!Enum.IsDefined(Error)) Error = SoundTone.Velvet;
        CompletionFile ??= ""; AttentionFile ??= ""; ErrorFile ??= "";
    }
}

public sealed class SoundGate
{
    private DateTimeOffset last = DateTimeOffset.MinValue;
    public bool Accept(bool enabled, bool quiet, float volume, SoundTone tone, DateTimeOffset now, bool preview = false)
    {
        if (!enabled || quiet || !float.IsFinite(volume) || volume <= 0 || tone == SoundTone.Silent) return false;
        if (!preview && now - last < TimeSpan.FromSeconds(2)) return false;
        last = now;
        return true;
    }
}
