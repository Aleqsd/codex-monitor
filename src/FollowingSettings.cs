using Dalamud.Bindings.ImGui;

namespace CodexMonitor;

internal sealed partial class SettingsPanel
{
    private void DrawFollowing()
    {
        var options = plugin.Config.Following;
        ObsidianTheme.Section("Projets suivis", "Cette sélection s’applique aux tâches, aux compteurs du HUD et aux nouvelles notifications.");
        Toggle("Suivre tous les projets", options.AllProjects, v => options.AllProjects = v);
        var projects = plugin.AllTasks.Threads.GroupBy(t => t.ProjectKey).Select(g => g.First()).OrderBy(t => t.Project).ToArray();
        ImGui.BeginDisabled(options.AllProjects);
        foreach (var task in projects)
        {
            ImGui.PushID(task.ProjectKey);
            var selected = options.Projects.Contains(task.ProjectKey);
            if (ImGui.Checkbox(task.Project, ref selected))
            { if (selected) options.Projects.Add(task.ProjectKey); else options.Projects.Remove(task.ProjectKey); plugin.Save(); }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip(); ImGui.PushTextWrapPos(360 * ObsidianTheme.UiScale);
                foreach (var row in plugin.AllTasks.Threads.Where(t => t.ProjectKey == task.ProjectKey).Take(3))
                    EmojiText.Wrapped(UnicodeText.Truncate(row.Title, 120), 360 * ObsidianTheme.UiScale);
                ImGui.PopTextWrapPos(); ImGui.EndTooltip();
            }
            ImGui.PopID();
        }
        ImGui.EndDisabled();
        if (!options.AllProjects && options.Projects.Count == 0) ImGui.TextWrapped("Aucun projet sélectionné : le HUD et les notifications ne suivent aucune tâche.");
        if (projects.Length == 0) ImGui.TextWrapped("Connecter le relais pour voir les projets disponibles.");
        ObsidianTheme.Section("Favoris", "Cliquer sur une tâche pour l’épingler en tête de liste. Les favoris respectent la sélection de projets.");
        foreach (var id in options.Favorites.ToArray())
        {
            ImGui.PushID(id); var task = plugin.AllTasks.Threads.FirstOrDefault(t => t.Id == id);
            if (ImGui.SmallButton("Retirer")) plugin.ToggleFavorite(id);
            ImGui.SameLine(); EmojiText.Wrapped(task?.Title ?? "Tâche actuellement non observée", ImGui.GetContentRegionAvail().X); ImGui.PopID();
        }
        if (options.Favorites.Count == 0) ImGui.TextDisabled("Aucun favori");
        ObsidianTheme.Section("Tâches silencieuses", "Leurs états et leur historique restent visibles. Les nouvelles alertes reprennent à la fin du délai.");
        foreach (var muted in options.Muted.Where(m => m.Until > DateTimeOffset.UtcNow).ToArray())
        {
            ImGui.PushID(muted.Id);
            if (ImGui.SmallButton("Réactiver")) plugin.MuteTask(muted.Id, 0);
            ImGui.SameLine(); ImGui.TextDisabled($"Jusqu’à {muted.Until.LocalDateTime:HH:mm}");
            EmojiText.Wrapped(plugin.AllTasks.Threads.FirstOrDefault(t => t.Id == muted.Id)?.Title ?? "Tâche actuellement non observée", ImGui.GetContentRegionAvail().X); ImGui.PopID();
        }
    }

    private void DrawDiagnostic()
    {
        var report = plugin.Diagnostics.Report;
        ImGui.BeginDisabled(report.Busy);
        if (ImGui.Button(report.Busy ? "Vérification en cours…" : "Vérifier la connexion")) plugin.Diagnostics.Check(plugin.Config.Port, plugin.Config.RelayNodePath);
        ImGui.EndDisabled();
        foreach (var step in report.Steps)
        {
            ImGui.TextColored(step.Ready is true ? ObsidianTheme.Green : ObsidianTheme.Amber,
                $"{step.Name} · {(step.Ready is true ? "OK" : step.Ready is false ? "À vérifier" : "Non confirmé")}");
            ImGui.TextWrapped(step.Detail);
            if (step.Advice.Length > 0) ImGui.TextWrapped(step.Advice);
        }
        if (report.At is not null)
        {
            if (ImGui.SmallButton("Copier le diagnostic")) ImGui.SetClipboardText(report.CopyText());
            ImGui.TextWrapped("Le rapport contient seulement les versions et les résultats techniques, sans titres, chemins ni données du compte.");
        }
    }

    private void DrawQuotaAlerts()
    {
        var options = plugin.Config.QuotaAlerts;
        Toggle("Me prévenir quand le quota baisse", options.Enabled, value => options.Enabled = value);
        ImGui.BeginDisabled(!options.Enabled);
        for (var i = 0; i < options.Thresholds.Count; i++)
        {
            ImGui.PushID(i); var threshold = options.Thresholds[i];
            ImGui.SetNextItemWidth(150 * ObsidianTheme.UiScale);
            if (ImGui.SliderInt("% restants", ref threshold, 1, 99)) options.Thresholds[i] = threshold;
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
            ImGui.SameLine();
            if (ImGui.SmallButton("Retirer")) { options.Thresholds.RemoveAt(i); plugin.Save(); ImGui.PopID(); break; }
            ImGui.PopID();
        }
        if (options.Thresholds.Count < 5 && ImGui.SmallButton("Ajouter un seuil"))
        { options.Thresholds.Add(Enumerable.Range(1, 99).First(n => !options.Thresholds.Contains(n))); plugin.Save(); }
        ImGui.EndDisabled();
        ImGui.TextWrapped("Une alerte par seuil et par période. Aucun rattrapage à la connexion ; les pauses sont respectées.");
    }
}
