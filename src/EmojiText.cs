using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CodexMonitor;

internal static class EmojiText
{
    internal static Func<string, ImTextureID?>? Resolve;
    private static readonly Dictionary<string, UnicodeText.Run[]> Runs = new(StringComparer.Ordinal);
    internal static UnicodeText.Run[] Split(string text)
    {
        if (Runs.TryGetValue(text, out var found)) return found;
        if (Runs.Count >= 512) Runs.Clear();
        return Runs[text] = UnicodeText.Runs(text);
    }
    internal static float Measure(string text, float size)
    {
        var ratio = size / ImGui.GetFontSize();
        return Split(text).Sum(run => run.Emoji ? size * 1.12f : ImGui.CalcTextSize(run.Text).X * ratio);
    }
    internal static void Draw(ImDrawListPtr draw, string text, Vector2 position, Vector4 color, float size, TextAppearance style)
    {
        foreach (var run in Split(text))
        {
            if (!run.Emoji)
            {
                ObsidianTheme.DrawPlainText(draw, run.Text, position, color, size, style);
                position.X += ImGui.CalcTextSize(run.Text).X * size / ImGui.GetFontSize();
                continue;
            }
            if (Resolve?.Invoke(run.Text) is { } texture)
            {
                // The 96px bitmap has 64px text centered inside it; preserve those safe margins.
                var extent = new Vector2(size * 1.5f);
                var top = position + new Vector2((size * 1.12f - extent.X) / 2, (size - extent.Y) / 2);
                draw.AddImage(texture, top, top + extent, Vector2.Zero, Vector2.One, ObsidianTheme.U(new Vector4(1, 1, 1, color.W)));
            }
            else ObsidianTheme.DrawPlainText(draw, "◇", position, color, size, style);
            position.X += size * 1.12f;
        }
    }
    internal static void Label(string text, float width = 0)
    {
        var size = ImGui.GetFontSize();
        if (width <= 0) width = ImGui.GetContentRegionAvail().X;
        var fitted = ObsidianTheme.Fit(text, width);
        ObsidianTheme.DrawText(ImGui.GetWindowDrawList(), fitted, ImGui.GetCursorScreenPos(), ObsidianTheme.Text, size);
        ImGui.Dummy(new Vector2(Math.Min(width, Measure(fitted, size)), size));
    }
    internal static void Wrapped(string text, float width)
    {
        var line = "";
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var next = elements.GetTextElement();
            if (line.Length > 0 && Measure(line + next, ImGui.GetFontSize()) > width)
            { Label(line, width); line = ""; }
            line += next;
        }
        if (line.Length > 0) Label(line, width);
    }
    internal static void Reset() { Resolve = null; Runs.Clear(); }
}
