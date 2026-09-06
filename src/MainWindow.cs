using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace CodexMonitor;

internal sealed class MainWindow : Window
{
    private readonly Plugin plugin;
    private string filter = "";
    internal bool ShowSettings;
    private static readonly Vector4 Cyan = new(0.42f, 0.80f, 0.94f, 1);
    private static readonly Vector4 Amber = new(1, 0.76f, 0.36f, 1);
    private static readonly Vector4 Green = new(0.62f, 0.84f, 0.60f, 1);
    private static readonly Vector4 Muted = new(0.58f, 0.62f, 0.66f, 1);

    public MainWindow(Plugin plugin) : base("Codex Monitor###CodexMonitorMain")
    {
        this.plugin = plugin;
        Size = new Vector2(630, 410);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(430, 260), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
    }

    private static Vector4 Color(string state) => state switch
    {
        "active" => Cyan,
        "idle" => Green,
        "needsInput" or "needsApproval" => Amber,
        "error" => new Vector4(1, 0.43f, 0.43f, 1),
        _ => Muted,
    };

    public override void Draw()
    {
        var snapshot = plugin.Snapshot;
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.TextColored(snapshot.Connected ? Green : Amber, snapshot.Connected ? "CONNECTÉ À CODEX" : "RELAIS DÉCONNECTÉ");
        ImGui.SameLine();
        var right = ImGui.GetWindowContentRegionMax().X - 78 * scale;
        if (right > ImGui.GetCursorPosX()) ImGui.SameLine(right);
        if (ImGui.SmallButton(ShowSettings ? "Retour" : "Réglages")) ShowSettings = !ShowSettings;
        ImGui.Spacing();

        if (ShowSettings) { DrawSettings(); return; }
        if (!snapshot.Connected)
        {
            ImGui.TextWrapped(snapshot.Error ?? "Connexion au relais local…");
            ImGui.Spacing();
            ImGui.TextDisabled("Les anciens états sont masqués jusqu'à la reconnexion.");
            return;
        }

        ImGui.TextColored(Cyan, $"{snapshot.Active} en cours");
        ImGui.SameLine(135 * scale);
        ImGui.TextColored(Amber, $"{snapshot.Attention} à regarder");
        ImGui.SameLine(290 * scale);
        ImGui.TextColored(Green, $"{snapshot.Idle} au repos");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##search", "Filtrer les tâches ou projets…", ref filter, 150);
        ImGui.Spacing();

        var threads = snapshot.Threads.Where(thread => (plugin.Config.ShowIdle || thread.State != "idle")
            && (plugin.Config.ShowUnobserved || thread.IsObserved)
            && (filter.Length == 0 || thread.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || thread.Project.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(thread => thread.NeedsAttention ? 0 : thread.State == "active" ? 1 : thread.State == "idle" ? 2 : 3)
            .ThenBy(thread => thread.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();

        var height = Math.Max(60 * scale, ImGui.GetContentRegionAvail().Y - 28 * scale);
        if (ImGui.BeginTable("tasks", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
            new Vector2(0, height)))
        {
            ImGui.TableSetupColumn("État", ImGuiTableColumnFlags.WidthFixed, 123 * scale);
            ImGui.TableSetupColumn("Tâche", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();
            foreach (var thread in threads)
            {
                ImGui.PushID(thread.Id);
                ImGui.TableNextRow(ImGuiTableRowFlags.None, 55 * scale);
                ImGui.TableSetColumnIndex(0);
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Color(thread.State), thread.Label);
                ImGui.TableSetColumnIndex(1);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(Fit(thread.Title, ImGui.GetContentRegionAvail().X));
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(520 * scale);
                    ImGui.TextUnformatted(thread.Title);
                    ImGui.PopTextWrapPos();
                    ImGui.Separator();
                    ImGui.TextDisabled(thread.Model);
                    ImGui.EndTooltip();
                }
                ImGui.TextDisabled(Fit(thread.Project, ImGui.GetContentRegionAvail().X));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (threads.Length == 0) ImGui.TextDisabled("Aucune tâche ne correspond aux filtres.");
        else ImGui.TextDisabled($"{threads.Length} tâche(s) · actualisation toutes les 2 s");
    }

    private void DrawSettings()
    {
        var config = plugin.Config;
        ImGui.TextUnformatted("AFFICHAGE");
        var idle = config.ShowIdle;
        if (ImGui.Checkbox("Afficher les tâches au repos", ref idle)) { config.ShowIdle = idle; plugin.Save(); }
        var unavailable = config.ShowUnobserved;
        if (ImGui.Checkbox("Afficher les tâches non observées", ref unavailable)) { config.ShowUnobserved = unavailable; plugin.Save(); }
        var bar = config.ShowDtr;
        if (ImGui.Checkbox("Compteur dans la barre d'informations", ref bar)) { config.ShowDtr = bar; plugin.Save(); }
        var open = config.OpenOnLoad;
        if (ImGui.Checkbox("Ouvrir la fenêtre au chargement", ref open)) { config.OpenOnLoad = open; plugin.Save(); }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted("NOTIFICATIONS");
        var completion = config.NotifyOnIdle;
        if (ImGui.Checkbox("Quand un tour se termine", ref completion)) { config.NotifyOnIdle = completion; plugin.Save(); }
        var attention = config.NotifyOnAttention;
        if (ImGui.Checkbox("Quand une intervention est nécessaire", ref attention)) { config.NotifyOnAttention = attention; plugin.Save(); }
        ImGui.TextDisabled("Aucune alerte à la première connexion ou reconnexion.");
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Connexion locale"))
        {
            var port = config.Port;
            ImGui.SetNextItemWidth(160 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt("Port du relais", ref port) && port is >= 1 and <= 65535) { config.Port = port; plugin.Save(); }
            ImGui.TextWrapped("Le relais doit fonctionner sur ce PC. La connexion reste sur 127.0.0.1.");
        }
    }

    private static string Fit(string value, float width)
    {
        if (width <= 0) return "";
        if (ImGui.CalcTextSize(value).X <= width) return value;
        var low = 0;
        var high = value.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(value[..middle] + "…").X <= width) low = middle;
            else high = middle - 1;
        }
        if (low > 0 && char.IsHighSurrogate(value[low - 1])) low--;
        return value[..low] + "…";
    }
}
