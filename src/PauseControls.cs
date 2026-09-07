using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CodexMonitor;

internal static class PauseControls
{
    internal static void Button(Plugin plugin)
    {
        if (ImGui.Button(plugin.ManualQuiet.Enabled ? "Reprendre" : "Ne pas déranger"))
        { if (plugin.ManualQuiet.Enabled) plugin.ResumeAlerts(); else ImGui.OpenPopup("pause-menu"); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(plugin.PauseDescription);
        Menu(plugin);
    }
    internal static void Icon(Plugin plugin, Vector2 p, float size)
    {
        ImGui.SetCursorScreenPos(p);
        if (ImGui.InvisibleButton("pause-button", new Vector2(size)))
        { if (plugin.ManualQuiet.Enabled) plugin.ResumeAlerts(); else ImGui.OpenPopup("pause-menu"); }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) ImGui.OpenPopup("pause-menu");
        var hovered = ImGui.IsItemHovered(); var draw = ImGui.GetWindowDrawList();
        var paused = plugin.ManualQuiet.Enabled || plugin.Center.IsQuiet;
        var color = ObsidianTheme.U(paused ? ObsidianTheme.Amber : ObsidianTheme.Muted);
        draw.AddRectFilled(p, p + new Vector2(size), ObsidianTheme.U(hovered ? ObsidianTheme.Line : ObsidianTheme.Surface), size * .18f);
        Vector2 At(float x, float y) => p + new Vector2(x, y) * size / 24;
        draw.AddLine(At(7, 15), At(8, 8), color, size / 16); draw.AddLine(At(8, 8), At(12, 6), color, size / 16);
        draw.AddLine(At(12, 6), At(16, 8), color, size / 16); draw.AddLine(At(16, 8), At(17, 15), color, size / 16);
        draw.AddLine(At(6, 16), At(18, 16), color, size / 16); draw.AddCircleFilled(At(12, 19), size / 16, color);
        if (paused) draw.AddLine(At(4, 4), At(20, 20), color, size / 14);
        if (hovered) { ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); ImGui.SetTooltip(plugin.PauseDescription + "\n" + (plugin.ManualQuiet.Enabled ? "Cliquer pour reprendre · Clic droit pour prolonger" : "Cliquer pour choisir une durée")); }
        Menu(plugin);
    }
    internal static void Menu(Plugin plugin)
    {
        if (!ImGui.IsPopupOpen("pause-menu")) return;
        var s = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        var width = Math.Min(340 * s, ImGui.GetMainViewport().Size.X - 24 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10 * s));
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, float.MaxValue));
        if (!ImGui.BeginPopup("pause-menu")) { ImGui.PopStyleVar(); return; }
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted("Ne pas déranger");
        ImGui.TextWrapped("Mettre les notifications et les sons en pause. Les tâches et l’historique restent à jour.");
        ImGui.Separator();
        if (plugin.ManualQuiet.Enabled && ImGui.Selectable("Reprendre les alertes")) plugin.ResumeAlerts();
        foreach (var minutes in new[] { 15, 30, 60 })
            if (ImGui.Selectable(minutes == 60 ? "Pendant 1 heure" : $"Pendant {minutes} minutes")) plugin.PauseAlerts(minutes);
        if (ImGui.Selectable("Jusqu’à réactivation (cette session)")) plugin.PauseAlerts(null);
        if (plugin.Center.IsQuiet) { ImGui.Separator(); ImGui.TextWrapped(plugin.PauseDescription); }
        ImGui.PopTextWrapPos(); ImGui.EndPopup(); ImGui.PopStyleVar();
    }
}
