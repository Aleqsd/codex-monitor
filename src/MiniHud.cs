using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace CodexMonitor;

internal enum HudTarget { All, Active, Ready, Attention, Quota }
internal sealed record HudHit(HudTarget Target, Vector2 Min, Vector2 Max)
{ internal bool Contains(Vector2 p) => p.X >= Min.X && p.X < Max.X && p.Y >= Min.Y && p.Y < Max.Y; }

internal sealed class MiniHud(Plugin plugin, Action openMonitor)
{
    internal bool Editing;
    private bool dragging;
    private Vector2 dragOrigin, mouseOrigin;
    private readonly HudMotion motion = new();
    internal readonly List<HudHit> Hits = [];
    internal void SetEditing(bool enabled) { Editing = enabled; dragging = false; plugin.Save(); }

    internal static void DrawFace(Vector2 p, Vector2 size, MonitorSnapshot snapshot, bool quiet, bool editing, float opacity,
        MiniHudStyle style = MiniHudStyle.Capsule, bool showUsage = true, HudMotionFrame? motion = null, HudAppearance? appearance = null,
        UsagePreference usagePreference = UsagePreference.Weekly, List<HudHit>? hits = null)
    {
        using var palette = ObsidianTheme.Palette(appearance);
        using var fontScope = UiFonts.Push(appearance?.Text);
        var draw = ImGui.GetWindowDrawList();
        var baseSize = MiniHudOptions.Size(style, showUsage);
        var s = size.Y / MiniHudOptions.Size(style, showUsage, appearance).Y * ((appearance?.Text.Size ?? 14) / 14);
        var font = ImGui.GetFont();
        var usage = snapshot.SelectedUsage(usagePreference);
        var muted = ObsidianTheme.Muted;
        var activeColor = snapshot.Connected && snapshot.Active > 0 ? ObsidianTheme.Blue : muted;
        var quotaColor = QuotaColor(usage?.RemainingPercent);
        var otherExhausted = snapshot.Connected && snapshot.Usage?.OtherExhausted(DateTimeOffset.UtcNow, usage) == true;
        if (otherExhausted) quotaColor = ObsidianTheme.Red;
        var attentionColor = snapshot.Connected && snapshot.Attention > 0 ? ObsidianTheme.Amber : muted;
        var transparent = style is MiniHudStyle.Fil or MiniHudStyle.Lisere;
        var background = appearance?.Color ?? ObsidianTheme.Surface;
        if (appearance is null) background.W = opacity;
        var hasBackground = appearance?.HasBackground(style) ?? !transparent;
        var border = appearance?.Border ?? true;
        var contentOrigin = p + ((appearance?.PadsMinimal(style) == true ? new Vector2(8, 4) : Vector2.Zero)
            + (appearance?.Padding ?? Vector2.Zero) + Vector2.Abs(appearance?.Text.Offset ?? Vector2.Zero)) * s;
        string Count(int n) => n > 99 ? "99+" : n.ToString();
        var active = snapshot.Connected ? Count(snapshot.Active) : "—";
        var alert = snapshot.Connected ? Count(snapshot.Attention) : "—";
        var ready = snapshot.Connected && snapshot.ReadStateSupported ? Count(snapshot.Ready) : "—";
        var readyColor = snapshot.Connected && snapshot.Ready > 0 ? ObsidianTheme.Blue : muted;
        var percent = usage?.Percent ?? "—";
        if (otherExhausted) percent += "*";
        var animation = motion ?? new HudMotionFrame(0, 0, 0, usage is null ? null : (float)usage.RemainingPercent / 100);
        var quotaFraction = usage is null ? 0 : animation.QuotaFraction ?? (float)usage.RemainingPercent / 100;
        Vector4 Emphasis(Vector4 color, float pulse) => Vector4.Lerp(color, ObsidianTheme.Text, pulse * 0.7f);
        uint Color(Vector4 c) { c.W *= opacity; return ObsidianTheme.U(c); }
        Vector2 At(float x, float y) => contentOrigin + new Vector2(x, y) * s;
        void Hit(HudTarget target, float x, float y, float w, float h) => hits?.Add(new(target,
            At(x,y) + (appearance?.Text.Offset ?? Vector2.Zero) * s, At(x+w,y+h) + (appearance?.Text.Offset ?? Vector2.Zero) * s));
        void Ready(float x, float y, bool label = false)
        {
            draw.AddCircleFilled(At(x+3, y+7) + (appearance?.Text.Offset ?? Vector2.Zero)*s, 2.5f*s, Color(readyColor));
            Text(ready + (label ? snapshot.Ready == 1 ? " prête" : " prêtes" : ""), x+10, y, readyColor, label ? 12 : 14);
            Hit(HudTarget.Ready,x-2,y-3,label ? 76 : 38,23);
        }
        float Width(string text, float fs = 14) => ImGui.CalcTextSize(text).X * fs / ImGui.GetFontSize();
        void Text(string text, float x, float y, Vector4 color, float fs = 14)
        {
            color.W *= opacity;
            ObsidianTheme.DrawText(draw, text, At(x, y) + (appearance?.Text.Offset ?? Vector2.Zero) * s, color, fs * s, appearance?.Text);
        }
        void Center(string text, float x, float y, Vector4 color, float fs = 14) => Text(text, x - Width(text, fs) / 2, y, color, fs);
        void Animated(string text, float x, float y, Vector4 color, float pulse, float fs = 14) =>
            Text(text, x, y - 2 * pulse, Emphasis(color, pulse), fs);
        void AnimatedCenter(string text, float x, float y, Vector4 color, float pulse, float fs = 14) =>
            Animated(text, x - Width(text, fs) / 2, y, color, pulse, fs);
        void Line(float x1, float y1, float x2, float y2, Vector4 color, float thickness = 1) =>
            draw.AddLine(At(x1, y1), At(x2, y2), Color(color), thickness * s);
        void Terminal(float x, float y)
        {
            var color = snapshot.Connected ? ObsidianTheme.Text : muted;
            Line(x - 6, y - 4, x - 2, y, color, 1.4f); Line(x - 2, y, x - 6, y + 4, color, 1.4f);
            Line(x + 1, y + 4, x + 6, y + 4, color, 1.4f);
        }
        void Bar(float x, float y, float width)
        {
            if (!showUsage) return;
            Line(x, y, x + width, y, ObsidianTheme.Line, 2);
            if (usage is not null && quotaFraction > 0) Line(x, y, x + width * quotaFraction, y, Emphasis(quotaColor, animation.UsagePulse), 2);
        }
        if (style != MiniHudStyle.Balise)
        {
            var radius = ObsidianTheme.Compact ? 2 : style == MiniHudStyle.Capsule ? 18 : 7;
            if (hasBackground) draw.AddRectFilled(p, p + size, ObsidianTheme.U(background), radius * s);
            if (border && hasBackground && background.W > 0) draw.AddRect(p, p + size, Color(ObsidianTheme.Line), radius * s);
        }
        switch (style)
        {
            case MiniHudStyle.Fil:
                Text("Codex", 1, 6, ObsidianTheme.Text); Animated(active, 51, 6, activeColor, animation.ActivePulse);
                Hit(HudTarget.Active,48,2,32,24); Ready(84,6);
                if (snapshot.Attention > 0 || !snapshot.Connected) { Animated(alert, 124, 6, attentionColor, animation.AttentionPulse); Hit(HudTarget.Attention,122,2,31,24); }
                if (showUsage) { Line(158, 8, 158, 20, muted); Animated(percent, 170, 6, quotaColor, animation.UsagePulse); Hit(HudTarget.Quota,164,2,54,24); }
                break;
            case MiniHudStyle.Balise:
                var center = At(29, 29);
                if (hasBackground) draw.AddCircleFilled(center, 24 * s, ObsidianTheme.U(background), 48);
                if (showUsage || (border && hasBackground && background.W > 0)) draw.AddCircle(center, 24 * s, Color(ObsidianTheme.Line), 48, 2 * s);
                if (showUsage && usage is not null && quotaFraction > 0)
                {
                    draw.PathArcTo(center, 24 * s, -MathF.PI / 2, -MathF.PI / 2 + MathF.Tau * quotaFraction, 64);
                    draw.PathStroke(Color(Emphasis(quotaColor, animation.UsagePulse)), ImDrawFlags.None, 2 * s);
                }
                AnimatedCenter(showUsage ? percent : "C", 29, 20, showUsage ? quotaColor : ObsidianTheme.Text, animation.UsagePulse, 15);
                if (showUsage) Hit(HudTarget.Quota,8,17,43,25);
                draw.AddRectFilled(At(0,0),At(27,17),Color(readyColor),4*s); Center(ready,13,1,ObsidianTheme.Ink,11);
                Hit(HudTarget.Ready,0,0,27,17);
                if (snapshot.Attention > 0)
                {
                    draw.AddRectFilled(At(34, 0), At(58, 17), Color(Emphasis(ObsidianTheme.Amber, animation.AttentionPulse)), 7 * s);
                    Center(alert, 46, 1, ObsidianTheme.Ink, 11);
                    Hit(HudTarget.Attention,34,0,24,17);
                }
                draw.AddCircleFilled(At(11, 49), (4 + animation.ActivePulse) * s, Color(Emphasis(activeColor, animation.ActivePulse)), 16);
                Text(active,19,43,activeColor,11); Hit(HudTarget.Active,0,42,39,16);
                break;
            case MiniHudStyle.Lisere:
                Text("C", 1, 6, ObsidianTheme.Text); Animated($"{active} en cours", 22, 6, activeColor, animation.ActivePulse);
                Hit(HudTarget.Active,20,2,103,24); Ready(126,6);
                if (snapshot.Attention > 0 || !snapshot.Connected) { Animated(alert, 167, 6, attentionColor, animation.AttentionPulse); Hit(HudTarget.Attention,165,2,37,24); }
                if (showUsage) { Animated(percent, 216, 6, quotaColor, animation.UsagePulse); Hit(HudTarget.Quota,208,2,52,24); }
                Bar(1, 29, baseSize.X - 2);
                break;
            case MiniHudStyle.Totem:
                if (border) Line(1, 10, 1, baseSize.Y - 10, snapshot.Connected ? ObsidianTheme.Mint : muted, 2);
                AnimatedCenter(active, 29, 10, activeColor, animation.ActivePulse, 17);
                Hit(HudTarget.Active,5,4,48,27);
                Line(12, 34, 46, 34, ObsidianTheme.Line);
                Ready(13,43); Line(12,66,46,66,ObsidianTheme.Line);
                AnimatedCenter(alert, 29, 75, attentionColor, animation.AttentionPulse); Hit(HudTarget.Attention,5,69,48,27);
                if (showUsage) { Line(12, 98, 46, 98, ObsidianTheme.Line); AnimatedCenter(percent, 29, 109, quotaColor, animation.UsagePulse, 13); Hit(HudTarget.Quota,5,101,48,29); }
                break;
            case MiniHudStyle.ObsidienneFine:
                if (ObsidianTheme.Compact && hasBackground)
                {
                    var header = ObsidianTheme.Surface; header.W = background.W;
                    draw.AddRectFilled(p, new Vector2(p.X + size.X, At(0, 28).Y), ObsidianTheme.U(header), 2 * s);
                }
                Terminal(18, 16); Text("Codex", 32, 8, ObsidianTheme.Text, 14);
                Ready(110,9,true);
                var status = snapshot.Connected ? $"{active} en cours" : "Hors ligne";
                Animated(status, baseSize.X - 12 - Width(status), 8, activeColor, animation.ActivePulse);
                Hit(HudTarget.Active,baseSize.X - 16 - Width(status),3,Width(status)+16,24);
                Animated(!snapshot.Connected ? "Relais absent" : snapshot.Attention > 0 ? $"{alert} à voir" : "Aucune alerte", 12, 33, attentionColor, animation.AttentionPulse, 12);
                Hit(HudTarget.Attention,8,29,108,26);
                if (showUsage) { Animated(percent + " restants", baseSize.X - 12 - Width(percent + " restants", 12), 33, quotaColor, animation.UsagePulse, 12); Hit(HudTarget.Quota,baseSize.X-120,29,116,26); }
                break;
            default:
                Terminal(17, 18); Animated(active, 32, 10, activeColor, animation.ActivePulse);
                Hit(HudTarget.Active,30,5,30,26); Ready(64,10);
                if (snapshot.Attention > 0 || !snapshot.Connected) { Animated(alert, 112, 10, attentionColor, animation.AttentionPulse); Hit(HudTarget.Attention,109,5,32,26); }
                if (showUsage) { Line(156, 11, 156, 25, ObsidianTheme.Line); Animated(percent, 167, 10, quotaColor, animation.UsagePulse); Hit(HudTarget.Quota,161,5,55,26); }
                break;
        }
        if (editing) draw.AddRect(p, p + size, Color(ObsidianTheme.Blue), 4 * s);
    }

    internal static Vector4 QuotaColor(double? remaining) => remaining is null ? ObsidianTheme.Muted
        : Math.Floor(remaining.Value) < 20 ? ObsidianTheme.Red : Math.Floor(remaining.Value) <= 50 ? ObsidianTheme.Amber : ObsidianTheme.Green;

    internal void Draw()
    {
        var config = plugin.Config;
        if (config.Indicator != IndicatorMode.MiniHud && !Editing) { motion.Reset(); return; }
        var snapshot = plugin.Snapshot;
        var viewport = ImGui.GetMainViewport();
        var faceBase = MiniHudOptions.Size(config.HudStyle, config.ShowUsage, config.HudAppearance);
        var baseSize = faceBase + new Vector2(30, 0);
        var scale = Math.Min(config.MiniHudScale * ImGuiHelpers.GlobalScale, (viewport.Size.X - 24) / baseSize.X);
        var size = baseSize * scale;
        if (scale < 0.25f || viewport.Size.Y < size.Y + 24) return;
        var position = NotificationGeometry.Place(new Vector2(config.MiniHudAnchorX, config.MiniHudAnchorY), viewport.Pos, viewport.Size, size, 0, 1, 0);
        ImGui.SetNextWindowPos(position); ImGui.SetNextWindowSize(size); ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var visible = ImGui.Begin("###CodexMiniHud", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoBackground);
        try
        {
            if (!visible) return;
            var p = ImGui.GetWindowPos();
            var faceSize = faceBase * scale;
            var frame = motion.Update(snapshot, config.AnimateHudChanges && !Editing, ImGui.GetIO().DeltaTime, config.UsagePeriod);
            Hits.Clear();
            DrawFace(p, faceSize, snapshot, plugin.Center.IsQuiet, Editing, config.MiniHudOpacity, config.HudStyle, config.ShowUsage, frame, config.HudAppearance, config.UsagePeriod, Hits);
            var target = Hits.LastOrDefault(h => h.Contains(ImGui.GetIO().MousePos))?.Target ?? HudTarget.All;
            ImGui.SetCursorPos(Vector2.Zero);
            var clicked = ImGui.InvisibleButton("hud", Editing ? size : faceSize);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(Editing ? ImGuiMouseCursor.ResizeAll : ImGuiMouseCursor.Hand);
                ImGui.BeginTooltip(); ImGui.PushTextWrapPos(430 * ImGuiHelpers.GlobalScale);
                if (Editing) ImGui.TextUnformatted("Glisser pour placer · Clic droit pour verrouiller");
                else
                {
                    ImGui.TextColored(ObsidianTheme.Text, "Codex Monitor");
                    ImGui.TextUnformatted(target switch { HudTarget.Active => "Clic : tâches en cours", HudTarget.Ready => "Clic : réponses prêtes · points bleus Codex", HudTarget.Attention => "Clic : demandes à voir", HudTarget.Quota => "Clic : détails et alertes du quota", _ => "Clic : toutes les tâches suivies" });
                    ImGui.TextUnformatted($"{snapshot.Active} en cours · {snapshot.ReadyCount} réponses prêtes");
                    if (!snapshot.ReadStateSupported) ImGui.TextWrapped("Point bleu non fourni · lancer le relais 0.10.0 ou plus récent depuis Connexion");
                    DrawUsageDetails(snapshot, config.UsagePeriod);
                    ImGui.TextUnformatted($"{snapshot.Attention} tâches à voir · {snapshot.Questions} questions suivies");
                    if (!snapshot.Connected) ImGui.TextWrapped(snapshot.Error ?? "Relais déconnecté");
                    else
                    {
                        var tasks = snapshot.Threads.Where(task => task.IsObserved).OrderBy(task => task.NeedsAttention ? 0 : task.State == "active" ? 1 : 2).ToArray();
                        foreach (var task in tasks.Take(8)) EmojiText.Wrapped($"{task.Label}{(task.HasQuestion ? " · ?" : "")} · {UnicodeText.Truncate(task.Title, 100)}", 430 * ImGuiHelpers.GlobalScale);
                        if (tasks.Length > 8) ImGui.TextDisabled($"+ {tasks.Length - 8} tâches");
                        if (tasks.Length == 0) ImGui.TextDisabled("Aucune tâche observée");
                    }
                    ImGui.TextDisabled("Cliquer pour ouvrir les tâches");
                    ImGui.TextWrapped(plugin.PauseDescription);
                }
                ImGui.PopTextWrapPos(); ImGui.EndTooltip();
            }
            if (Editing && ImGui.IsItemActivated()) { dragOrigin = p; mouseOrigin = ImGui.GetIO().MousePos; }
            if (Editing && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                var anchor = NotificationGeometry.AnchorAfterDrag(dragOrigin, size, ImGui.GetIO().MousePos - mouseOrigin, viewport.Pos, viewport.Size);
                config.MiniHudAnchorX = anchor.X; config.MiniHudAnchorY = anchor.Y; dragging = true;
            }
            if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { dragging = false; plugin.Save(); }
            if (Editing && ImGui.IsItemClicked(ImGuiMouseButton.Right)) SetEditing(false);
            else if (!Editing)
            {
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) ImGui.OpenPopup("pause-menu");
                if (clicked)
                {
                    if (target == HudTarget.Quota) plugin.OpenQuota();
                    else if (target == HudTarget.All) openMonitor();
                    else plugin.OpenTasks(target == HudTarget.Active ? 1 : target == HudTarget.Ready ? 4 : 2);
                }
                PauseControls.Icon(plugin, p + new Vector2(faceSize.X + 4 * scale, Math.Max(0, (size.Y - 24 * scale) / 2)), 24 * scale);
            }
        }
        finally { ImGui.End(); ImGui.PopStyleVar(3); }
    }

    internal static void DrawUsageDetails(MonitorSnapshot snapshot, UsagePreference preference = UsagePreference.Weekly)
    {
        if (snapshot.SelectedUsage(preference) is { } usage)
        {
            foreach (var window in snapshot.Usage!.ValidWindows(DateTimeOffset.UtcNow).OrderByDescending(w => w.WindowDurationMins))
            {
                ImGui.TextColored(QuotaColor(window.RemainingPercent), $"{window.Percent} restants · {window.Period}{(window == usage ? " · HUD" : "")}");
                if (window.ResetsAt is { } reset) ImGui.TextDisabled($"Renouvellement : {DateTimeOffset.FromUnixTimeSeconds(reset).LocalDateTime:ddd dd/MM à HH:mm}");
            }
            ImGui.TextDisabled($"Actualisé il y a {Math.Max(0, (int)(DateTimeOffset.UtcNow - snapshot.Usage.FetchedAt).TotalSeconds)} s · compte du CLI Codex");
            if (snapshot.Usage.OtherExhausted(DateTimeOffset.UtcNow, usage)) ImGui.TextColored(ObsidianTheme.Red, "* Une autre période est épuisée.");
        }
        else ImGui.TextDisabled(!snapshot.Connected ? "Quota indisponible · relais déconnecté"
            : !snapshot.UsageTrackingSupported && snapshot.Usage is null ? "Quota indisponible · ancien relais détecté"
            : snapshot.Usage is not null ? "Dernier quota périmé · actualisation en attente" : snapshot.QuotaDiagnostic?.Message ?? "Quota momentanément indisponible · —");
    }
}
