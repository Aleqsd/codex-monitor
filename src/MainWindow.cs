using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace CodexMonitor;

internal sealed class MainWindow : Window
{
    private readonly Plugin plugin;
    private readonly SettingsPanel settings;
    private string filter = "";
    private int stateFilter;
    private bool pendingOnly;
    internal bool ShowSettings, ShowHistory;

    public MainWindow(Plugin plugin) : base("Codex Monitor###CodexMonitorMain")
    {
        this.plugin = plugin;
        settings = new SettingsPanel(plugin);
        Size = new Vector2(760, 610); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(560, 440), MaximumSize = new Vector2(float.MaxValue) };
    }

    public override void Draw()
    {
        var snapshot = plugin.Snapshot;
        var scale = ObsidianTheme.UiScale;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var draw = ImGui.GetWindowDrawList();
        if (ObsidianTheme.Compact)
        {
            var statusLabel = !snapshot.Connected ? "Hors ligne" : plugin.Center.IsQuiet ? "Discret" : "En direct";
            var right = ImGui.CalcTextSize(statusLabel).X;
            ObsidianTheme.DrawText(draw, "CODEX", start, ObsidianTheme.Mint, ImGui.GetFontSize());
            ObsidianTheme.DrawText(draw, statusLabel, start + new Vector2(width - right, 0), snapshot.Connected ? ObsidianTheme.Muted : ObsidianTheme.Amber, ImGui.GetFontSize());
            ImGui.Dummy(new Vector2(0, 25 * scale));
        }
        else
        {
        var logo = start + new Vector2(14, 19) * scale;
        draw.AddQuadFilled(logo + new Vector2(0, -9) * scale, logo + new Vector2(9, 0) * scale,
            logo + new Vector2(0, 9) * scale, logo + new Vector2(-9, 0) * scale, ObsidianTheme.U(ObsidianTheme.Mint));
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 1.2f, start + new Vector2(37, 1) * scale, ObsidianTheme.U(ObsidianTheme.Text), "Codex Monitor");
        draw.AddText(start + new Vector2(38, 28) * scale, ObsidianTheme.U(ObsidianTheme.Muted), "Suivi des tâches de ce PC");
        var status = !snapshot.Connected ? "HORS LIGNE" : plugin.Center.IsQuiet ? "MODE DISCRET" : "EN DIRECT";
        var statusWidth = ImGui.CalcTextSize(status).X;
        var statusPos = start + new Vector2(width - statusWidth, 4 * scale);
        draw.AddText(statusPos, ObsidianTheme.U(snapshot.Connected ? ObsidianTheme.Mint : ObsidianTheme.Amber), status);
        ImGui.Dummy(new Vector2(0, 62 * scale));
        }
        if (ObsidianTheme.Tab("Tâches", !ShowSettings && !ShowHistory)) { ShowSettings = false; ShowHistory = false; }
        ImGui.SameLine();
        if (ObsidianTheme.Tab("Historique", ShowHistory)) { ShowSettings = false; ShowHistory = true; }
        ImGui.SameLine();
        if (ObsidianTheme.Tab("Réglages", ShowSettings)) { ShowSettings = true; ShowHistory = false; }
        ImGui.Separator();
        if (ShowSettings) { settings.Draw(); return; }
        if (ShowHistory) { DrawHistory(); return; }
        DrawTasks(snapshot);
    }

    private void DrawTasks(MonitorSnapshot snapshot)
    {
        var s = ObsidianTheme.UiScale;
        if (!snapshot.Connected)
        {
            ObsidianTheme.Section("Connexion interrompue", snapshot.Error ?? "Le relais local ne répond pas.");
            ImGui.TextWrapped("Les tâches réapparaîtront automatiquement dès la reconnexion.");
            if (ImGui.Button("Vérifier la connexion")) { ShowSettings = true; settings.Category = 3; }
            return;
        }
        var width = ImGui.GetContentRegionAvail().X;
        var gap = 8 * s;
        var cell = (width - 2 * gap) / 3;
        Summary("En cours", snapshot.Active, ObsidianTheme.Blue, cell, stateFilter == 1, () => stateFilter = stateFilter == 1 ? 0 : 1);
        ImGui.SameLine(0, gap);
        Summary("Interventions", snapshot.Attention, ObsidianTheme.Amber, cell, stateFilter == 2, () => stateFilter = stateFilter == 2 ? 0 : 2);
        ImGui.SameLine(0, gap);
        Summary("Au repos", snapshot.Idle, ObsidianTheme.Green, cell, stateFilter == 3, () => stateFilter = stateFilter == 3 ? 0 : 3);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##search", "Rechercher une tâche ou un projet…", ref filter, 150);
        if (stateFilter != 0)
        {
            ImGui.TextDisabled("Vue filtrée"); ImGui.SameLine();
            if (ImGui.SmallButton("Tout afficher")) stateFilter = 0;
        }
        var tasks = snapshot.Threads.Where(task => (plugin.Config.ShowIdle || task.State != "idle") && (plugin.Config.ShowUnobserved || task.IsObserved)
            && (stateFilter == 0 || stateFilter == 1 && task.State == "active" || stateFilter == 2 && task.NeedsAttention || stateFilter == 3 && task.State == "idle")
            && (filter.Length == 0 || task.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) || task.Project.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(task => task.NeedsAttention ? 0 : task.State == "active" ? 1 : task.State == "idle" ? 2 : 3)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (ImGui.BeginChild("task-list", new Vector2(0, Math.Max(80 * s, ImGui.GetContentRegionAvail().Y - 29 * s)), false))
        {
            if (tasks.Length == 0) { ImGui.Spacing(); ImGui.TextDisabled("Aucune tâche dans cette vue."); }
            foreach (var task in tasks) TaskRow(task);
        }
        ImGui.EndChild();
        ImGui.TextDisabled($"{tasks.Length} tâches affichées · Actualisation toutes les 2 s");
    }

    private static void Summary(string label, int count, Vector4 color, float width, bool selected, Action click)
    {
        var s = ObsidianTheme.UiScale;
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, (ObsidianTheme.Compact ? 35 : 67) * s);
        ImGui.InvisibleButton(label, size);
        var draw = ImGui.GetWindowDrawList();
        if (ObsidianTheme.Compact)
        {
            if (selected || ImGui.IsItemHovered()) draw.AddRectFilled(p, p + size, ObsidianTheme.U(ObsidianTheme.Surface), 2 * s);
            var value = count.ToString(); var countWidth = ImGui.CalcTextSize(value).X;
            ObsidianTheme.DrawText(draw, ObsidianTheme.Fit(label, width - countWidth - 24 * s), p + new Vector2(6, 8) * s, color, ImGui.GetFontSize());
            ObsidianTheme.DrawText(draw, value, p + new Vector2(width - countWidth - 6 * s, 8 * s), ObsidianTheme.Text, ImGui.GetFontSize());
            if (ImGui.IsItemClicked()) click(); return;
        }
        if (selected || ImGui.IsItemHovered()) draw.AddRectFilled(p, p + size, ObsidianTheme.U(ObsidianTheme.Surface), 8 * s);
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 1.6f, p + new Vector2(12, 2) * s, ObsidianTheme.U(color), count.ToString());
        draw.AddText(p + new Vector2(13, 38) * s, ObsidianTheme.U(ObsidianTheme.Muted), label);
        if (ImGui.IsItemClicked()) click();
    }

    private void TaskRow(MonitoredThread task) => CompactTaskRow(task);

    private void CompactTaskRow(MonitoredThread task)
    {
        var appearance = plugin.Config.WindowAppearance!;
        var s = ObsidianTheme.UiScale; var p = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
        var inset = Vector2.Abs(appearance.Text.Offset) * s;
        var rowHeight = ImGui.GetFontSize() * 2 + (10 + appearance.RowSpacing) * s + inset.Y * 2;
        ImGui.PushID(task.Id); ImGui.InvisibleButton("row", new Vector2(width, rowHeight));
        DrawTaskFace(ImGui.GetWindowDrawList(), p, width, rowHeight, task, appearance, ImGui.IsItemHovered());
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip(); ImGui.PushTextWrapPos(470 * s); ImGui.TextUnformatted(task.Title); ImGui.TextDisabled(task.Project);
            if (task.HasQuestion) ImGui.TextColored(ObsidianTheme.Amber, "Question posée · Répondre dans Codex");
            ImGui.PopTextWrapPos(); ImGui.EndTooltip();
        }
        ImGui.PopID();
    }

    internal static void DrawTaskFace(ImDrawListPtr draw, Vector2 p, float width, float rowHeight, MonitoredThread task, SurfaceAppearance appearance, bool hovered = false)
    {
        using var font = UiFonts.Push(appearance.Text); using var palette = ObsidianTheme.Palette(appearance);
        var s = ObsidianTheme.UiScale; var inset = Vector2.Abs(appearance.Text.Offset) * s;
        var color = ObsidianTheme.State(task.State);
        draw.AddRectFilled(p, p + new Vector2(width, rowHeight - 2 * s), ObsidianTheme.U(new Vector4(color.X, color.Y, color.Z, (hovered ? 0.22f : 0.09f) * appearance.Opacity)));
        draw.AddRectFilled(p, p + new Vector2(3 * s, rowHeight - 2 * s), ObsidianTheme.U(color));
        var text = p + new Vector2(10, 5) * s + inset + appearance.Text.Offset * s;
        var status = task.HasQuestion ? task.Label + " · ?" : task.Label;
        var statusWidth = Math.Min(width * 0.38f, ImGui.CalcTextSize(status).X);
        var titleWidth = Math.Max(1, width - statusWidth - 30 * s - 2 * inset.X);
        var title = ObsidianTheme.Fit(task.Title, titleWidth);
        var shift = (titleWidth - ImGui.CalcTextSize(title).X) * (int)appearance.Alignment / 2;
        ObsidianTheme.DrawText(draw, title, text + new Vector2(shift, 0), ObsidianTheme.Text, ImGui.GetFontSize());
        ObsidianTheme.DrawText(draw, ObsidianTheme.Fit(status, statusWidth), new Vector2(p.X + width - statusWidth - 8 * s, text.Y), color, ImGui.GetFontSize());
        ObsidianTheme.DrawText(draw, ObsidianTheme.Fit(task.Project, width - 26 * s - 2 * inset.X), text + new Vector2(0, ImGui.GetFontSize() + 4 * s), ObsidianTheme.Muted, ImGui.GetFontSize() * 0.9f);
    }

    private void DrawHistory()
    {
        var history = plugin.History;
        ImGui.Checkbox("Interventions en cours seulement", ref pendingOnly);
        ImGui.SameLine();
        if (ImGui.SmallButton("Effacer")) { history.Clear(); plugin.Save(); }
        var rows = history.Entries.Where(entry => !pendingOnly || history.CurrentLabel(entry, plugin.Snapshot) == "Intervention en cours").ToArray();
        var s = ObsidianTheme.UiScale;
        if (ImGui.BeginChild("history-list", new Vector2(0, Math.Max(70 * s, ImGui.GetContentRegionAvail().Y - 28 * s)), false))
        {
            if (rows.Length == 0) ImGui.TextDisabled("Aucun événement à afficher.");
            foreach (var entry in rows)
            {
                ImGui.PushID(entry.Id.ToString());
                ImGui.TextColored(ObsidianTheme.Muted, entry.At.ToLocalTime().ToString("dd/MM · HH:mm"));
                ImGui.SameLine();
                ImGui.TextColored(ObsidianTheme.State(entry.Task.State), entry.Task.State == "idle" ? "Tour terminé" : entry.Task.Label);
                ImGui.TextWrapped(entry.Task.Title);
                var current = history.CurrentLabel(entry, plugin.Snapshot);
                ImGui.TextColored(current == "Intervention en cours" ? ObsidianTheme.Amber : ObsidianTheme.Muted, current);
                ImGui.Separator(); ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.TextDisabled("100 derniers événements · Conservés sur ce PC");
    }
}
