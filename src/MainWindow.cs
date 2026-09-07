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
    private string historySearch = "";
    private int historyFilter;
    private bool groupHistory = true;
    private readonly TaskListProjection projection = new();
    internal bool ShowSettings, ShowHistory;
    internal void SelectTasks(int state) { stateFilter = state; filter = ""; }
    internal void SelectConnection() { settings.Category = 3; }

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
            var statusLabel = !snapshot.Connected ? "Hors ligne" : plugin.Center.IsQuiet ? "Alertes en pause" : "En direct";
            var right = ImGui.CalcTextSize(statusLabel).X;
            ObsidianTheme.DrawText(draw, "Codex", start, ObsidianTheme.Text, ImGui.GetFontSize());
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
        var status = !snapshot.Connected ? "HORS LIGNE" : plugin.Center.IsQuiet ? "ALERTES EN PAUSE" : "EN DIRECT";
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
        ImGui.SameLine(); PauseControls.Button(plugin);
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
        var cell = (width - 3 * gap) / 4;
        Summary("En cours", snapshot.Active, ObsidianTheme.Blue, cell, stateFilter == 1, () => stateFilter = stateFilter == 1 ? 0 : 1);
        ImGui.SameLine(0, gap);
        Summary("Prêtes", snapshot.Ready, ObsidianTheme.Blue, cell, stateFilter == 4, () => stateFilter = stateFilter == 4 ? 0 : 4, snapshot.ReadStateSupported);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(snapshot.ReadStateSupported ? "Réponses non lues : le point bleu de Codex. Cet indicateur se synchronise avec l’application." : "Point bleu non fourni · lancer le relais 0.10.0 ou plus récent depuis Connexion");
        ImGui.SameLine(0, gap);
        Summary("À voir", snapshot.Attention, ObsidianTheme.Amber, cell, stateFilter == 2, () => stateFilter = stateFilter == 2 ? 0 : 2);
        ImGui.SameLine(0, gap);
        Summary("Au repos", snapshot.Idle, ObsidianTheme.Muted, cell, stateFilter == 3, () => stateFilter = stateFilter == 3 ? 0 : 3);
        ImGui.SetNextItemWidth(Math.Min(220 * s, ImGui.GetContentRegionAvail().X));
        ImGui.Combo("Vue", ref stateFilter, new[] { "Toutes les tâches suivies", "En cours", "À voir", "Sans activité", "Réponses prêtes", "Favoris" }, 6);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##search", "Rechercher une tâche ou un projet…", ref filter, 150);
        if (stateFilter != 0)
        {
            ImGui.TextDisabled("Vue filtrée"); ImGui.SameLine();
            if (ImGui.SmallButton("Tout afficher")) stateFilter = 0;
        }
        var tasks = projection.Get(snapshot, plugin.Config.ShowIdle, plugin.Config.ShowUnobserved, stateFilter, filter);
        if (ImGui.BeginChild("task-list", new Vector2(0, Math.Max(80 * s, ImGui.GetContentRegionAvail().Y - 29 * s)), false))
        {
            var appearance = plugin.Config.WindowAppearance!;
            var p = ImGui.GetCursorScreenPos(); var extent = ImGui.GetContentRegionAvail(); var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(p, p + extent, ObsidianTheme.U(appearance.Color));
            if (appearance.Border && appearance.Opacity > 0) draw.AddRect(p, p + extent, ObsidianTheme.U(new Vector4(0.23f, 0.23f, 0.23f, appearance.Opacity)));
            if (tasks.Length == 0) { ImGui.Spacing(); ImGui.TextDisabled("Aucune tâche dans cette vue."); }
            var clipper = ImGui.ImGuiListClipper();
            try
            {
                // Rows have a fixed height for the current font/appearance; popups are separate windows.
                using var font = UiFonts.Push(appearance.Text);
                var rowHeight = ImGui.GetFontSize() * 2 + (10 + appearance.RowSpacing) * s
                    + (Math.Abs(appearance.Text.OffsetY) + appearance.Padding.Y) * s * 2 + ImGui.GetStyle().ItemSpacing.Y;
                clipper.Begin(tasks.Length, rowHeight);
                while (clipper.Step()) for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) TaskRow(tasks[i]);
            }
            finally { clipper.Destroy(); }
        }
        ImGui.EndChild();
        ImGui.TextDisabled($"{tasks.Length} {(tasks.Length == 1 ? "tâche affichée" : "tâches affichées")} · Actualisation toutes les 2 s");
    }

    private static void Summary(string label, int count, Vector4 color, float width, bool selected, Action click, bool known = true)
    {
        var s = ObsidianTheme.UiScale;
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, (ObsidianTheme.Compact ? 35 : 67) * s);
        ImGui.InvisibleButton(label, size);
        var draw = ImGui.GetWindowDrawList();
        if (ObsidianTheme.Compact)
        {
            if (selected || ImGui.IsItemHovered()) draw.AddRectFilled(p, p + size, ObsidianTheme.U(ObsidianTheme.Surface), 2 * s);
            var value = known ? count.ToString() : "—"; var countWidth = ImGui.CalcTextSize(value).X;
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
        using var font = UiFonts.Push(appearance.Text);
        var s = ObsidianTheme.UiScale; var p = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
        var inset = (Vector2.Abs(appearance.Text.Offset) + appearance.Padding) * s;
        var rowHeight = ImGui.GetFontSize() * 2 + (10 + appearance.RowSpacing) * s + inset.Y * 2;
        ImGui.PushID(task.Id);
        var hidden = task.HiddenQuestionIds is { Length: > 0 };
        var clickable = true;
        var openError = plugin.TaskLink.ErrorFor(task.Id);
        var openLabel = openError is null ? "Ouvrir" : "Réessayer";
        var openWidth = ImGui.CalcTextSize(openLabel).X + 20 * s;
        if (ImGui.InvisibleButton("row", new Vector2(width - openWidth - 4 * s, rowHeight)) && clickable) ImGui.OpenPopup("question-actions");
        var nextRow = ImGui.GetCursorScreenPos();
        DrawTaskFace(ImGui.GetWindowDrawList(), p, width - openWidth - 4 * s, rowHeight, task, appearance, ImGui.IsItemHovered());
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip(); ImGui.PushTextWrapPos(470 * s); EmojiText.Wrapped(UnicodeText.Truncate(task.Title, 320), 470 * s); ImGui.TextDisabled(task.Project);
            if (task.Title.Length > 320) ImGui.TextDisabled("Titre complet : ouvrir la tâche dans Codex");
            ImGui.TextWrapped(task.ModelLabel + (task.ModelSource == "turn" ? " · dernière exécution" : " · réglage de la tâche"));
            if (task.HasUnreadTurn is true) ImGui.TextColored(ObsidianTheme.Blue, "Point bleu actif dans Codex · réponse non lue");
            if (task.HasUnreadTurn is null) ImGui.TextDisabled("Indicateur de lecture non fourni par le relais");
            if (!plugin.Config.Following.Allows(task, DateTimeOffset.UtcNow)) ImGui.TextDisabled("Notifications de cette tâche en pause");
            if (task.HasQuestion) ImGui.TextColored(ObsidianTheme.Amber, $"{task.QuestionIds.Length} {(task.QuestionIds.Length == 1 ? "question suivie" : "questions suivies")} · Répondre dans Codex");
            if (hidden) ImGui.TextDisabled("Question masquée dans FF14");
            if (clickable) ImGui.TextDisabled("Cliquer pour les favoris, les alertes et les questions");
            ImGui.PopTextWrapPos(); ImGui.EndTooltip();
        }
        if (ImGui.BeginPopup("question-actions"))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 310 * s);
            EmojiText.Wrapped(UnicodeText.Truncate(task.Title, 160), 310 * s);
            ImGui.TextWrapped("Ces réglages concernent l’affichage et les alertes dans FF14.");
            ImGui.Separator();
            if (ImGui.Selectable(task.IsFavorite ? "Retirer des favoris" : "Ajouter aux favoris")) plugin.ToggleFavorite(task.Id);
            if (!plugin.Config.Following.Allows(task, DateTimeOffset.UtcNow))
            { if (ImGui.Selectable("Réactiver les alertes")) plugin.MuteTask(task.Id, 0); }
            else
            {
                if (ImGui.Selectable("Silence pendant 30 min")) plugin.MuteTask(task.Id, 30);
                if (ImGui.Selectable("Silence pendant 1 h")) plugin.MuteTask(task.Id, 60);
            }
            ImGui.Separator();
            if (task.HasQuestion && ImGui.Selectable(task.QuestionIds.Length == 1 ? "Masquer cette question" : "Masquer ces questions")) plugin.DismissQuestions(task);
            if (hidden && ImGui.Selectable("Réafficher les questions masquées")) plugin.RestoreQuestions(task.Id);
            ImGui.TextWrapped("Masquer une question ne répond pas dans Codex.");
            ImGui.PopTextWrapPos(); ImGui.EndPopup();
        }
        ImGui.SetCursorScreenPos(p + new Vector2(width - openWidth, (rowHeight - ImGui.GetFrameHeight()) / 2));
        ImGui.BeginDisabled(CodexTaskLink.Build(task.Id) is null || plugin.TaskLink.Busy);
        if (ImGui.Button(openLabel + "##codex", new Vector2(openWidth, 0))) _ = plugin.TaskLink.Open(task.Id);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(openError ?? "Ouvrir cette tâche dans l’application Codex");
        if (ImGui.BeginPopupContextItem("open-error"))
        {
            if (openError is not null) { ImGui.TextWrapped(openError); if (ImGui.Selectable("Fermer ce message")) plugin.TaskLink.DismissError(task.Id); }
            ImGui.EndPopup();
        }
        ImGui.SetCursorScreenPos(nextRow);
        ImGui.PopID();
    }

    internal static void DrawTaskFace(ImDrawListPtr draw, Vector2 p, float width, float rowHeight, MonitoredThread task, SurfaceAppearance appearance, bool hovered = false)
    {
        using var font = UiFonts.Push(appearance.Text); using var palette = ObsidianTheme.Palette(appearance);
        var s = ObsidianTheme.UiScale; var inset = (Vector2.Abs(appearance.Text.Offset) + appearance.Padding) * s;
        var color = task.ResponseReady ? ObsidianTheme.Blue : ObsidianTheme.State(task.State);
        draw.AddRectFilled(p, p + new Vector2(width, rowHeight - 2 * s), ObsidianTheme.U(new Vector4(color.X, color.Y, color.Z, (hovered ? 0.22f : 0.09f) * appearance.Opacity)));
        draw.AddRectFilled(p, p + new Vector2(3 * s, rowHeight - 2 * s), ObsidianTheme.U(color));
        var text = p + new Vector2(10, 5) * s + inset + appearance.Text.Offset * s;
        var status = task.HasQuestion ? task.Label + " · ?" : task.Label;
        var statusWidth = Math.Min(width * 0.38f, ImGui.CalcTextSize(status).X);
        if (task.HasUnreadTurn == true)
        { draw.AddCircleFilled(text + new Vector2(4 * s, ImGui.GetFontSize() / 2), 3 * s, ObsidianTheme.U(ObsidianTheme.Blue)); text.X += 12 * s; }
        var titleWidth = Math.Max(1, width - statusWidth - 30 * s - 2 * inset.X - (task.HasUnreadTurn == true ? 12 * s : 0));
        var title = ObsidianTheme.Fit(task.Title, titleWidth);
        var shift = (titleWidth - ObsidianTheme.Measure(title)) * (int)appearance.Alignment / 2;
        ObsidianTheme.DrawText(draw, title, text + new Vector2(shift, 0), ObsidianTheme.Text, ImGui.GetFontSize());
        ObsidianTheme.DrawText(draw, ObsidianTheme.Fit(status, statusWidth), new Vector2(p.X + width - statusWidth - 8 * s, text.Y), color, ImGui.GetFontSize());
        ObsidianTheme.DrawText(draw, ObsidianTheme.Fit((task.IsFavorite ? "Favori · " : "") + task.Project + " · " + task.ModelLabel, width - 26 * s - 2 * inset.X - (task.HasUnreadTurn == true ? 12 * s : 0)), text + new Vector2(0, ImGui.GetFontSize() + 4 * s), ObsidianTheme.Muted, ImGui.GetFontSize() * 0.9f);
    }

    private void DrawHistory()
    {
        var history = plugin.History;
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##history-search", "Rechercher une tâche ou un projet…", ref historySearch, 150);
        ImGui.SetNextItemWidth(185 * ObsidianTheme.UiScale);
        ImGui.Combo("Événements", ref historyFilter, new[] { "Tous", "Questions et demandes", "Erreurs", "Réponses prêtes", "Quota" }, 5);
        ImGui.Checkbox("Regrouper par tâche", ref groupHistory);
        ImGui.Checkbox("Demandes à traiter seulement", ref pendingOnly);
        ImGui.SameLine();
        if (ImGui.SmallButton("Effacer")) { history.Clear(); plugin.Save(); }
        var rows = HistoryQuery.Select(history.Entries, historySearch, (HistoryFilter)historyFilter, pendingOnly, history, plugin.AllTasks);
        var s = ObsidianTheme.UiScale;
        if (ImGui.BeginChild("history-list", new Vector2(0, Math.Max(70 * s, ImGui.GetContentRegionAvail().Y - 28 * s)), false))
        {
            if (rows.Length == 0) ImGui.TextDisabled("Aucun événement à afficher.");
            foreach (var group in rows.GroupBy(entry => groupHistory ? entry.Task.Id : entry.Id.ToString()))
            {
                var first = group.First();
                ImGui.PushID("group-" + group.Key);
                var expanded = true;
                if (groupHistory)
                {
                    expanded = ImGui.CollapsingHeader($"{group.Count()} {(group.Count()==1 ? "événement" : "événements")} · {UnicodeText.Truncate(first.Task.Project, 40)}###history-group", ImGuiTreeNodeFlags.DefaultOpen);
                    EmojiText.Wrapped(UnicodeText.Truncate(first.Task.Title, 320), ImGui.GetContentRegionAvail().X);
                }
                if (expanded) foreach (var entry in group)
                {
                ImGui.PushID(entry.Id.ToString());
                ImGui.TextColored(ObsidianTheme.Muted, entry.At.ToLocalTime().ToString("dd/MM · HH:mm"));
                ImGui.SameLine();
                ImGui.TextColored(ObsidianTheme.State(entry.Task.State), entry.Task.State == "idle" ? "Réponse prête" : entry.Task.Label);
                if (!groupHistory) EmojiText.Wrapped(entry.Task.Title, ImGui.GetContentRegionAvail().X);
                if (entry.Task.State == "quota")
                { ImGui.TextWrapped(entry.Task.Project); if (ImGui.SmallButton("Voir le quota")) plugin.OpenQuota(); }
                else
                {
                ImGui.BeginDisabled(CodexTaskLink.Build(entry.Task.Id) is null || plugin.TaskLink.Busy);
                if (ImGui.SmallButton("Ouvrir dans Codex")) _ = plugin.TaskLink.Open(entry.Task.Id);
                ImGui.EndDisabled();
                }
                if (plugin.TaskLink.ErrorFor(entry.Task.Id) is { } error)
                { ImGui.TextWrapped(error); if (ImGui.SmallButton("Fermer le message")) plugin.TaskLink.DismissError(entry.Task.Id); }
                var current = history.CurrentLabel(entry, plugin.AllTasks);
                ImGui.TextColored(current == "À traiter" ? ObsidianTheme.Amber : ObsidianTheme.Muted, current);
                ImGui.Separator(); ImGui.PopID();
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.TextDisabled("100 derniers événements · Conservés sur ce PC");
    }
}
