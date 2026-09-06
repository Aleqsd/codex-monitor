using System.Numerics;

namespace CodexMonitor;

public enum HudBackgroundMode { StyleDefault, Visible, Hidden }

public sealed class HudAppearance : SurfaceAppearance
{
    internal static readonly (string Name, Vector3 Color)[] Presets =
    [
        ("Obsidienne", new(0.11f, 0.13f, 0.13f)), ("Nuit", new(0.07f, 0.12f, 0.23f)),
        ("Prune", new(0.20f, 0.09f, 0.22f)), ("Forêt", new(0.07f, 0.19f, 0.14f)),
    ];
    public HudBackgroundMode Background { get; set; }
    internal bool HasBackground(MiniHudStyle style) => Background switch
    {
        HudBackgroundMode.Visible => true, HudBackgroundMode.Hidden => false,
        _ => style is not (MiniHudStyle.Fil or MiniHudStyle.Lisere),
    };
    internal bool PadsMinimal(MiniHudStyle style) => HasBackground(style) && style is MiniHudStyle.Fil or MiniHudStyle.Lisere;
    public override void Normalize()
    {
        base.Normalize();
        if (!Enum.IsDefined(Background)) Background = HudBackgroundMode.StyleDefault;
    }
}
