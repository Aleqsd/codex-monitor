using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace CodexMonitor;

internal sealed class NotificationOverlay(Configuration config, Action save, Action<NotificationItem> openMonitor)
{
    internal readonly NotificationQueue Queue = new();
    internal bool Preview;
    internal string PreviewState = "idle";
    private bool anchorDragging;
    private long? pausedId;
    private Vector2 dragOrigin;
    private Vector2 dragMouseOrigin;

    internal static MonitoredThread Example(string state) => new("preview", "Notification d’exemple", "Aperçu Codex", "", state);
    internal void Test(string state) => Queue.Add(Example(state), config.NotificationSeconds);
    internal void SetPreview(bool enabled)
    {
        Preview = enabled;
        anchorDragging = false;
        pausedId = null;
        save();
    }

    internal void Align(int horizontal, int vertical)
    {
        var viewport = ImGui.GetMainViewport();
        var scale = Scale(viewport.Size);
        if (horizontal >= 0) config.NotificationAnchorX = horizontal switch
        {
            0 => (BaseSize.X / 2 * scale + 12) / viewport.Size.X,
            2 => 1 - (BaseSize.X / 2 * scale + 12) / viewport.Size.X, _ => 0.5f,
        };
        if (vertical >= 0) config.NotificationAnchorY = vertical switch
        {
            0 => 12 / viewport.Size.Y, 2 => 1 - (BaseSize.Y * scale + 12) / viewport.Size.Y, _ => 0.5f,
        };
        config.NotificationOffsetX = config.NotificationOffsetY = 0;
        SetPreview(true);
    }

    internal static Vector2 LogicalSize(SurfaceAppearance appearance) =>
        new Vector2(440, appearance.Skin == MonitorSkin.Obsidienne ? 112 : 92)
        + 2 * (appearance.Padding + Vector2.Abs(appearance.Text.Offset));
    private Vector2 BaseSize => LogicalSize(config.ToastAppearance!);
    private float Scale(Vector2 viewport) => Math.Min(config.NotificationScale * ImGuiHelpers.GlobalScale * (config.ToastAppearance!.Text.Size / 17),
        Math.Min((viewport.X - 24) / BaseSize.X, (viewport.Y - 24) / BaseSize.Y));

    internal void Draw(bool quiet)
    {
        using var fontScope = UiFonts.Push(config.ToastAppearance!.Text);
        using var palette = ObsidianTheme.Palette(config.ToastAppearance);
        if (quiet) { pausedId = null; return; }
        var viewport = ImGui.GetMainViewport();
        var scale = Scale(viewport.Size);
        if (scale < 0.25f) return;
        var size = BaseSize * scale;
        var gap = 10 * scale;
        var count = Math.Clamp((int)((viewport.Size.Y - 24 + gap) / (size.Y + gap)), 1, 3);
        var anchor = new Vector2(config.NotificationAnchorX, config.NotificationAnchorY);
        var offset = new Vector2(config.NotificationOffsetX, config.NotificationOffsetY);
        if (Preview)
        {
            if (config.PreviewGrid) DrawGrid(viewport.Pos, viewport.Size, 40 * scale);
            var previewCount = Math.Min(count, config.PreviewCount);
            for (var i = 0; i < previewCount; i++)
            {
                var position = NotificationGeometry.Place(anchor, viewport.Pos, viewport.Size, size, i, previewCount, gap,
                    direction: config.NotificationDirection, offset: offset);
                var example = Example(i == 0 ? PreviewState : i == 1 ? "needsInput" : "error");
                DrawToast(new NotificationItem(-1 - i, example, 1, config.NotificationSeconds), position, size, scale, true, i == 0);
            }
            if (anchorDragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { anchorDragging = false; save(); }
            return;
        }
        Queue.Advance(Math.Min(ImGui.GetIO().DeltaTime, 0.25f), count, pausedId);
        pausedId = null;
        var items = Queue.Visible(count);
        for (var i = 0; i < items.Length; i++)
        {
            var position = NotificationGeometry.Place(anchor, viewport.Pos, viewport.Size, size, i, items.Length, gap,
                direction: config.NotificationDirection, offset: offset);
            DrawToast(items[i], position, size, scale, false);
        }
    }

    private void DrawToast(NotificationItem item, Vector2 position, Vector2 size, float scale, bool preview, bool draggable = true)
    {
        var remaining = item.Duration - item.Age;
        var enter = Math.Clamp(item.Age / 0.26f, 0, 1);
        var alpha = preview || config.NotificationReducedMotion ? 1 : Math.Min(enter, Math.Clamp(remaining / 0.22f, 0, 1));
        if (!preview && !config.NotificationReducedMotion)
        {
            var viewport = ImGui.GetMainViewport();
            var inward = position.X + size.X / 2 > viewport.Pos.X + viewport.Size.X / 2 ? -1 : 1;
            position.X += inward * 16 * scale * MathF.Pow(1 - enter, 3);
        }
        ImGui.SetNextWindowPos(position);
        ImGui.SetNextWindowSize(size);
        ImGui.SetNextWindowViewport(ImGui.GetMainViewport().ID);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground;
        if (preview && !draggable) flags |= ImGuiWindowFlags.NoInputs;
        var visible = ImGui.Begin($"###CodexObsidian{item.Id}", flags);
        try
        {
            if (visible)
            {
                var draw = ImGui.GetWindowDrawList();
                var p = ImGui.GetWindowPos();
                uint Color(float r, float g, float b, float opacity = 1) => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, alpha * opacity));
                DrawFace(draw, p, size, scale, item, config.ToastAppearance!, alpha, preview, draggable);

                ImGui.SetCursorPos(new Vector2(4, 4) * scale);
                ImGui.InvisibleButton("body", new Vector2(size.X - 37 * scale, size.Y - 8 * scale));
                if (ImGui.IsItemHovered())
                {
                    if (!preview) pausedId = item.Id;
                    ImGui.SetMouseCursor(preview ? ImGuiMouseCursor.ResizeAll : ImGuiMouseCursor.Hand);
                    if (!preview)
                    {
                        ImGui.BeginTooltip();
                        ImGui.PushTextWrapPos(440 * ImGuiHelpers.GlobalScale);
                        ImGui.TextUnformatted(item.Task.Title);
                        ImGui.PopTextWrapPos();
                        ImGui.EndTooltip();
                    }
                }
                if (preview && draggable && ImGui.IsItemActivated())
                {
                    dragOrigin = p;
                    dragMouseOrigin = ImGui.GetIO().MousePos;
                }
                if (preview && draggable && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var viewport = ImGui.GetMainViewport();
                    var anchor = NotificationGeometry.AnchorAfterDrag(dragOrigin, size, ImGui.GetIO().MousePos - dragMouseOrigin, viewport.Pos, viewport.Size);
                    if (config.PreviewSnap) anchor = NotificationGeometry.Snap(anchor, viewport.Size, size, config.PreviewGrid ? 40 * scale : 0);
                    config.NotificationAnchorX = anchor.X;
                    config.NotificationAnchorY = anchor.Y;
                    config.NotificationOffsetX = config.NotificationOffsetY = 0;
                    anchorDragging = true;
                }
                if (!preview && ImGui.IsItemClicked()) openMonitor(item);

                if (preview && !draggable) return;
                var close = p + new Vector2(size.X - 20 * scale, 18 * scale);
                draw.AddLine(close + new Vector2(-3, -3) * scale, close + new Vector2(3, 3) * scale, Color(0.68f, 0.71f, 0.68f), scale);
                draw.AddLine(close + new Vector2(-3, 3) * scale, close + new Vector2(3, -3) * scale, Color(0.68f, 0.71f, 0.68f), scale);
                ImGui.SetCursorPos(new Vector2(size.X - 34 * scale, 3 * scale));
                if (ImGui.InvisibleButton("close", new Vector2(30, 30) * scale))
                {
                    if (preview) SetPreview(false);
                    else Queue.Dismiss(item.Id);
                }
            }
        }
        finally { ImGui.End(); ImGui.PopStyleVar(2); }
    }

    internal static void DrawFace(ImDrawListPtr draw, Vector2 p, Vector2 size, float scale, NotificationItem item,
        SurfaceAppearance appearance, float alpha = 1, bool preview = false, bool draggable = false)
    {
        using var fontScope = UiFonts.Push(appearance.Text);
        using var palette = ObsidianTheme.Palette(appearance);
        var compact = appearance.Skin != MonitorSkin.Obsidienne;
        var background = appearance.Color; background.W *= alpha;
        var accent = ObsidianTheme.State(item.Task.State); accent.W = alpha;
        var foreground = appearance.Text.Color; foreground.W = alpha;
        var secondary = ObsidianTheme.Muted; secondary.W = alpha;
        var radius = (compact ? 2 : 14) * scale;
        draw.AddRectFilled(p, p + size, ObsidianTheme.U(background), radius);
        if (appearance.Border && background.W > 0) draw.AddRect(p, p + size, ObsidianTheme.U(new Vector4(ObsidianTheme.Line.X, ObsidianTheme.Line.Y, ObsidianTheme.Line.Z, background.W)), radius);
        var content = p + (appearance.Padding + Vector2.Abs(appearance.Text.Offset)) * scale;
        if (compact) draw.AddRectFilled(p, p + new Vector2(3 * scale, size.Y), ObsidianTheme.U(accent));
        var center = content + new Vector2(compact ? 25 : 40, compact ? 27 : 48) * scale;
        if (!compact) draw.AddCircleFilled(center, 20 * scale, ObsidianTheme.U(new Vector4(accent.X * .2f, accent.Y * .2f, accent.Z * .2f, background.W)));
        DrawSymbol(draw, center, scale * (compact ? .7f : 1), ObsidianTheme.U(accent), item.Task.State);
        var heading = item.Task.State switch
        {
            "idle" => "Tour terminé", "needsInput" => "Réponse requise", "question" => "Question posée",
            "summary" => "Pendant votre absence", "needsApproval" => "Approbation requise", _ => "Une erreur est survenue",
        };
        var text = content + (new Vector2(compact ? 46 : 75, compact ? 14 : 18) + appearance.Text.Offset) * scale;
        var fontSize = 17 * scale;
        var width = size.X - (compact ? 80 : 110) * scale - 2 * (appearance.PaddingX + Math.Abs(appearance.Text.OffsetX)) * scale;
        void Label(string value, float y, Vector4 color, float factor)
        {
            var fitted = ObsidianTheme.Fit(value, width, fontSize * factor);
            var measured = ImGui.CalcTextSize(fitted).X * fontSize * factor / ImGui.GetFontSize();
            var shift = Math.Max(0, width - measured) * (int)appearance.Alignment / 2;
            ObsidianTheme.DrawText(draw, fitted, text + new Vector2(shift, y * scale), color, fontSize * factor, appearance.Text);
        }
        Label(heading, 0, accent, 1);
        Label(item.Task.Title, compact ? 23 : 26, foreground, .94f);
        var meta = preview ? (draggable ? "APERÇU · Glisser pour placer l’ancre" : "APERÇU · Données fictives")
            : item.Task.State == "summary" ? item.Task.Project : $"Codex · {item.Task.Project}";
        Label(meta, compact ? 46 : 53, secondary, .8f);
        var fraction = preview ? 1 : Math.Clamp((item.Duration - item.Age) / item.Duration, 0, 1);
        draw.AddRectFilled(p + new Vector2(10 * scale, size.Y - 4 * scale), p + new Vector2(10 * scale + (size.X - 20 * scale) * fraction, size.Y - 2 * scale), ObsidianTheme.U(accent));
        if (preview && draggable) draw.AddRect(p, p + size, ObsidianTheme.U(accent), radius);
    }

    private static void DrawGrid(Vector2 origin, Vector2 size, float grid)
    {
        var draw = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());
        var subtle = ImGui.ColorConvertFloat4ToU32(new Vector4(0.47f, 0.72f, 0.83f, 0.16f));
        var guide = ImGui.ColorConvertFloat4ToU32(new Vector4(0.47f, 0.72f, 0.83f, 0.55f));
        for (var x = grid; x < size.X; x += grid) draw.AddLine(origin + new Vector2(x, 0), origin + new Vector2(x, size.Y), subtle);
        for (var y = grid; y < size.Y; y += grid) draw.AddLine(origin + new Vector2(0, y), origin + new Vector2(size.X, y), subtle);
        draw.AddLine(origin + new Vector2(size.X / 2, 0), origin + new Vector2(size.X / 2, size.Y), guide);
        draw.AddLine(origin + new Vector2(0, size.Y / 2), origin + new Vector2(size.X, size.Y / 2), guide);
        draw.AddRect(origin + new Vector2(12), origin + size - new Vector2(12), guide);
    }

    private static string Fit(string text, float width, float fontSize)
    {
        var ratio = fontSize / ImGui.GetFontSize();
        if (ImGui.CalcTextSize(text).X * ratio <= width) return text;
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(text[..mid] + "…").X * ratio <= width) low = mid;
            else high = mid - 1;
        }
        if (low > 0 && char.IsHighSurrogate(text[low - 1])) low--;
        return text[..low] + "…";
    }

    private static void DrawSymbol(ImDrawListPtr draw, Vector2 center, float scale, uint color, string state)
    {
        if (state == "idle")
        {
            draw.AddLine(center + new Vector2(-7, 0) * scale, center + new Vector2(-2, 5) * scale, color, 2 * scale);
            draw.AddLine(center + new Vector2(-2, 5) * scale, center + new Vector2(8, -6) * scale, color, 2 * scale);
        }
        else if (state == "question")
        {
            var size = ImGui.GetFontSize() * scale / ImGuiHelpers.GlobalScale * 1.4f;
            draw.AddText(ImGui.GetFont(), size, center - new Vector2(size * 0.23f, size * 0.56f), color, "?");
        }
        else if (state == "error")
        {
            draw.AddLine(center + new Vector2(-5, -5) * scale, center + new Vector2(5, 5) * scale, color, 2 * scale);
            draw.AddLine(center + new Vector2(-5, 5) * scale, center + new Vector2(5, -5) * scale, color, 2 * scale);
        }
        else
        {
            draw.AddLine(center + new Vector2(0, -8) * scale, center + new Vector2(0, 2) * scale, color, 2 * scale);
            draw.AddCircleFilled(center + new Vector2(0, 7) * scale, 1.6f * scale, color);
        }
    }
}
