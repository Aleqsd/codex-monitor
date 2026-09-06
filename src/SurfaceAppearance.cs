using System.Numerics;

namespace CodexMonitor;

public enum MonitorSkin { Obsidienne, LMeter, Nuit }
public enum MonitorFont { Dalamud, Expressway, SegoeUi, LocalFile }
public enum TextEdge { None, Shadow, Outline }
public enum ContentAlignment { Left, Center, Right }
public enum AppearanceTarget { Window, Hud, Notification }

public sealed class TextAppearance
{
    public MonitorFont Font { get; set; }
    public string FontFile { get; set; } = "";
    public float Size { get; set; } = 14;
    public float Red { get; set; } = 0.91f;
    public float Green { get; set; } = 0.94f;
    public float Blue { get; set; } = 0.91f;
    public TextEdge Edge { get; set; } = TextEdge.Shadow;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    internal Vector4 Color => new(Red, Green, Blue, 1);
    internal Vector2 Offset => new(OffsetX, OffsetY);
    public void Normalize()
    {
        if (!Enum.IsDefined(Font)) Font = MonitorFont.Dalamud;
        if (!Enum.IsDefined(Edge)) Edge = TextEdge.Shadow;
        FontFile ??= "";
        Size = NotificationGeometry.FiniteClamp(Size, 12, 24, 14);
        Red = NotificationGeometry.FiniteClamp(Red, 0, 1, 1);
        Green = NotificationGeometry.FiniteClamp(Green, 0, 1, 1);
        Blue = NotificationGeometry.FiniteClamp(Blue, 0, 1, 1);
        OffsetX = NotificationGeometry.FiniteClamp(OffsetX, -20, 20, 0);
        OffsetY = NotificationGeometry.FiniteClamp(OffsetY, -12, 12, 0);
    }
}

// Public scalar properties keep the existing Dalamud/Newtonsoft configuration format portable.
public class SurfaceAppearance
{
    public MonitorSkin Skin { get; set; }
    public float Red { get; set; } = 0.11f;
    public float Green { get; set; } = 0.13f;
    public float Blue { get; set; } = 0.13f;
    public float Opacity { get; set; } = 0.94f;
    public bool Border { get; set; } = true;
    public float? ToastCornerRadius { get; set; }
    public bool ToastShowIcon { get; set; } = true;
    public bool ToastShowTimer { get; set; } = true;
    public float AccentRed { get; set; } = 0.65f;
    public float AccentGreen { get; set; } = 0.83f;
    public float AccentBlue { get; set; } = 0.72f;
    public float PaddingX { get; set; }
    public float PaddingY { get; set; }
    public float RowSpacing { get; set; } = 4;
    public ContentAlignment Alignment { get; set; }
    public TextAppearance Text { get; set; } = new();
    internal Vector4 Color => new(Red, Green, Blue, Opacity);
    internal Vector4 Accent => new(AccentRed, AccentGreen, AccentBlue, 1);
    internal Vector2 Padding => new(PaddingX, PaddingY);
    public virtual void Normalize()
    {
        if (!Enum.IsDefined(Skin)) Skin = MonitorSkin.Obsidienne;
        if (!Enum.IsDefined(Alignment)) Alignment = ContentAlignment.Left;
        Red = Clamp(Red, 0, 1, 0.11f); Green = Clamp(Green, 0, 1, 0.13f); Blue = Clamp(Blue, 0, 1, 0.13f);
        Opacity = Clamp(Opacity, 0, 1, 0.94f);
        if (ToastCornerRadius.HasValue) ToastCornerRadius = Clamp(ToastCornerRadius.Value, 0, 24, 2);
        AccentRed = Clamp(AccentRed, 0, 1, 0.65f); AccentGreen = Clamp(AccentGreen, 0, 1, 0.83f); AccentBlue = Clamp(AccentBlue, 0, 1, 0.72f);
        PaddingX = Clamp(PaddingX, 0, 24, 0); PaddingY = Clamp(PaddingY, 0, 16, 0);
        RowSpacing = Clamp(RowSpacing, 0, 16, 4);
        Text ??= new(); Text.Normalize();
    }
    private static float Clamp(float value, float min, float max, float fallback) => NotificationGeometry.FiniteClamp(value, min, max, fallback);
    public void ApplyPreset(MonitorSkin skin, AppearanceTarget target)
    {
        Skin = skin; Border = true; Alignment = ContentAlignment.Left; PaddingX = PaddingY = 0; RowSpacing = 4;
        ToastCornerRadius = null; ToastShowIcon = ToastShowTimer = true;
        Text = new TextAppearance { Size = target == AppearanceTarget.Hud ? 14 : 17 };
        if (skin == MonitorSkin.Obsidienne)
        {
            Red = target == AppearanceTarget.Window ? 0.075f : 0.11f;
            Green = Blue = target == AppearanceTarget.Window ? 0.09f : 0.13f;
            AccentRed = 0.65f; AccentGreen = 0.83f; AccentBlue = 0.72f;
            Opacity = target == AppearanceTarget.Window ? 1 : 0.94f;
        }
        else
        {
            Red = skin == MonitorSkin.Nuit ? 0.04f : target == AppearanceTarget.Window ? 17f / 255 : 0;
            Green = skin == MonitorSkin.Nuit ? 0.07f : Red; Blue = skin == MonitorSkin.Nuit ? 0.14f : Red;
            AccentRed = 0; AccentGreen = 185f / 255; AccentBlue = 247f / 255;
            Opacity = target == AppearanceTarget.Window ? 0.95f : 0.70f;
            Text.Red = Text.Green = Text.Blue = 1; Text.Edge = TextEdge.Outline; Text.Font = MonitorFont.Expressway;
        }
    }
    internal static SurfaceAppearance Legacy(AppearanceTarget target)
    { var result = new SurfaceAppearance(); result.ApplyPreset(MonitorSkin.Obsidienne, target); return result; }
}
