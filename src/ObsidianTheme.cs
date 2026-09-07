using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace CodexMonitor;

// Historical type name; all skins share semantic roles, never job colors for task states.
internal static class ObsidianTheme
{
    // Fixed shell for navigation and settings. Saved data/HUD/toast styles never change it.
    internal static readonly SurfaceAppearance Chrome = CreateChrome();
    private static SurfaceAppearance CreateChrome()
    {
        var appearance = new SurfaceAppearance(); appearance.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Window);
        appearance.Text.Font = MonitorFont.Dalamud; appearance.Text.Edge = TextEdge.Shadow;
        return appearance;
    }
    private static readonly SurfaceAppearance Legacy = SurfaceAppearance.Legacy(AppearanceTarget.Window);
    private static SurfaceAppearance current = Legacy;
    internal static bool Compact => current.Skin != MonitorSkin.Obsidienne;
    internal static float UiScale => ImGuiHelpers.GlobalScale * Math.Max(1, ImGui.GetFontSize() / (17 * ImGuiHelpers.GlobalScale));
    internal static Vector4 Ink => new(current.Red, current.Green, current.Blue, 1);
    internal static Vector4 Surface => Compact ? new(0.125f, 0.125f, 0.125f, 1) : new(0.11f, 0.13f, 0.13f, 1);
    internal static Vector4 Raised => Compact ? new(0.22f, 0.22f, 0.22f, 1) : new(0.15f, 0.18f, 0.17f, 1);
    internal static Vector4 Line => Compact ? new(0.23f, 0.23f, 0.23f, 1) : new(0.22f, 0.27f, 0.25f, 1);
    internal static Vector4 Text => current.Text.Color;
    internal static Vector4 Muted => Compact ? new(0.72f, 0.72f, 0.72f, 1) : new(0.58f, 0.65f, 0.62f, 1);
    internal static Vector4 Mint => current.Accent;
    internal static Vector4 Blue => Compact ? new(0.20f, 0.68f, 0.94f, 1) : new(0.48f, 0.72f, 0.83f, 1);
    internal static Vector4 Green => Compact ? new(0.42f, 0.82f, 0.52f, 1) : new(0.65f, 0.83f, 0.72f, 1);
    internal static Vector4 Amber => Compact ? new(1, 0.73f, 0.27f, 1) : new(0.92f, 0.74f, 0.45f, 1);
    internal static Vector4 Red => Compact ? new(0.96f, 0.35f, 0.35f, 1) : new(0.92f, 0.57f, 0.56f, 1);
    internal static uint U(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color);
    internal static Vector4 State(string state) => state switch
    { "active" or "summary" => Blue, "idle" => Green, "needsInput" or "needsApproval" or "question" or "quota" => Amber, "error" => Red, _ => Muted };
    private sealed class PaletteScope(SurfaceAppearance previous) : IDisposable { public void Dispose() => current = previous; }
    internal static IDisposable Palette(SurfaceAppearance? appearance)
    { var previous = current; current = appearance ?? Legacy; return new PaletteScope(previous); }
    private static readonly Stack<IDisposable> scopes = new();
    private const int ColorCount = 27;
    internal static void Push(SurfaceAppearance? appearance = null)
    {
        scopes.Push(Palette(appearance));
        var s = UiScale;
        (ImGuiCol, Vector4)[] colors =
        [
            (ImGuiCol.WindowBg, current.Color), (ImGuiCol.ChildBg, Vector4.Zero), (ImGuiCol.PopupBg, Surface),
            (ImGuiCol.Text, Text), (ImGuiCol.TextDisabled, Muted), (ImGuiCol.Border, Line),
            (ImGuiCol.TitleBg, Surface), (ImGuiCol.TitleBgActive, Surface), (ImGuiCol.FrameBg, Surface),
            (ImGuiCol.FrameBgHovered, Raised), (ImGuiCol.FrameBgActive, Raised),
            (ImGuiCol.Button, Surface), (ImGuiCol.ButtonHovered, Raised), (ImGuiCol.ButtonActive, Line),
            (ImGuiCol.Header, Raised), (ImGuiCol.HeaderHovered, Raised), (ImGuiCol.HeaderActive, Line),
            (ImGuiCol.CheckMark, Mint), (ImGuiCol.SliderGrab, Mint), (ImGuiCol.SliderGrabActive, Blue),
            (ImGuiCol.Separator, Line), (ImGuiCol.ScrollbarBg, Vector4.Zero), (ImGuiCol.ScrollbarGrab, Line),
            (ImGuiCol.ScrollbarGrabHovered, Muted), (ImGuiCol.ScrollbarGrabActive, Mint),
            (ImGuiCol.TableHeaderBg, Surface), (ImGuiCol.TableRowBgAlt, new(1, 1, 1, 0.02f)),
        ];
        foreach (var (slot, value) in colors) ImGui.PushStyleColor(slot, value);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, (Compact ? 3 : 12) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, (Compact ? 2 : 9) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, (Compact ? 2 : 6) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, (Compact ? 3 : 8) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, (new Vector2(Compact ? 12 : 20, 12) + current.Padding) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 5) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8, 6) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, current.Border ? 1 : 0);
    }
    internal static void Pop() { ImGui.PopStyleVar(9); ImGui.PopStyleColor(ColorCount); scopes.Pop().Dispose(); }
    internal static void Section(string title, string? description = null)
    { ImGui.Spacing(); ImGui.TextColored(Mint, title); if (description != null) ImGui.TextWrapped(description); ImGui.Spacing(); }
    internal static bool Tab(string text, bool selected, float width = 0)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, selected ? Raised : Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, selected ? Text : Muted);
        var clicked = ImGui.Button(text, new Vector2(width, 30 * UiScale));
        if (selected)
        {
            var a = ImGui.GetItemRectMin(); var b = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(new(a.X + 8, b.Y), new(b.X - 8, b.Y), U(Mint), 2);
        }
        ImGui.PopStyleColor(2); return clicked;
    }
    internal static void DrawText(ImDrawListPtr draw, string text, Vector2 position, Vector4 color, float size, TextAppearance? appearance = null)
        => EmojiText.Draw(draw, text, position, color, size, appearance ?? current.Text);
    internal static float Measure(string text, float fontSize = 0) => EmojiText.Measure(text, fontSize > 0 ? fontSize : ImGui.GetFontSize());
    internal static void DrawPlainText(ImDrawListPtr draw, string text, Vector2 position, Vector4 color, float size, TextAppearance style)
    {
        var font = ImGui.GetFont(); var edge = new Vector4(0, 0, 0, color.W * 0.90f);
        var pixel = Math.Max(1, size / 17);
        if (style.Edge == TextEdge.Shadow) draw.AddText(font, size, position + new Vector2(pixel), U(edge), text);
        else if (style.Edge == TextEdge.Outline)
        {
            draw.AddText(font, size, position + new Vector2(-pixel, 0), U(edge), text);
            draw.AddText(font, size, position + new Vector2(pixel, 0), U(edge), text);
            draw.AddText(font, size, position + new Vector2(0, -pixel), U(edge), text);
            draw.AddText(font, size, position + new Vector2(0, pixel), U(edge), text);
        }
        draw.AddText(font, size, position, U(color), text);
    }
    private static readonly BoundedCache<(ImFontPtr Font, float Size, float Width, string Text), string> fitted = new(2048);
    internal static void ResetTextCache() => fitted.Clear();
    internal static string Fit(string value, float width, float fontSize = 0)
    {
        var key = (ImGui.GetFont(), fontSize > 0 ? fontSize : ImGui.GetFontSize(), width, value);
        return fitted.TryGetValue(key, out var result) ? result : fitted.Add(key, FitUncached(value, width, fontSize));
    }
    private static string FitUncached(string value, float width, float fontSize)
    {
        if (width <= 0) return "";
        if (Measure(value, fontSize) <= width) return value;
        if (Measure("…", fontSize) > width) return "";
        var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(value);
        var low = 0; var high = boundaries.Length - 1;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (Measure(value[..boundaries[mid]] + "…", fontSize) <= width) low = mid; else high = mid - 1;
        }
        return value[..boundaries[low]] + "…";
    }
}
