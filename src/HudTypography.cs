namespace CodexMonitor;

internal static class HudTypography
{
    internal static readonly float[] RoleSizes = [13, 14, 15, 17];
    internal static float Pixels(float roleSize, float scale) => Math.Max(1, MathF.Round(Math.Max(13, roleSize) * scale));
    internal static void Apply(TextAppearance text)
    {
        text.Font = MonitorFont.Expressway;
        text.FontFile = "";
        text.Size = 16;
        text.Red = text.Green = text.Blue = 1;
        text.Edge = TextEdge.Outline;
        text.EdgeOpacity = 128f / 255;
    }
    internal static void Upgrade(TextAppearance text)
    {
        // Upgrade the old stock typography, retaining explicit custom font/size/color choices.
        if (text.Font is not (MonitorFont.Dalamud or MonitorFont.Expressway)) return;
        text.Font = MonitorFont.Expressway;
        if (text.Size <= 14) text.Size = 16;
        if (Math.Abs(text.Red - .91f) < .001f && Math.Abs(text.Green - .94f) < .001f && Math.Abs(text.Blue - .91f) < .001f)
            text.Red = text.Green = text.Blue = 1;
        if (text.Edge == TextEdge.Shadow || text.Edge == TextEdge.Outline)
        { text.Edge = TextEdge.Outline; text.EdgeOpacity = 128f / 255; }
    }
}
