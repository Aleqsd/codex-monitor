using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace CodexMonitor;

internal sealed partial class SettingsPanel
{
    internal AppearanceTarget AppearanceScope;
    private void DrawAppearanceEditor()
    {
        var config = plugin.Config;
        var appearance = AppearanceScope switch { AppearanceTarget.Window => config.WindowAppearance!, AppearanceTarget.Hud => config.HudAppearance!, _ => config.ToastAppearance! };
        if (AppearanceScope == AppearanceTarget.Window) ImGui.TextWrapped("Personnalise uniquement la liste des tâches. Les réglages gardent leur apparence fixe.");
        ObsidianTheme.Section("Thème", "Le preset ne change ni la position ni les autres composants.");
        foreach (var skin in new[] { MonitorSkin.LMeter, MonitorSkin.Obsidienne, MonitorSkin.Nuit })
        {
            if (skin != MonitorSkin.LMeter) ImGui.SameLine();
            if (ImGui.RadioButton(skin.ToString(), appearance.Skin == skin))
            { appearance.ApplyPreset(skin, AppearanceScope); plugin.Save(); }
        }
        if(!separatePreview) { ImGui.Spacing(); DrawAppearancePreview(appearance); ImGui.Spacing(); }
        ColorControl("Fond", new(appearance.Red, appearance.Green, appearance.Blue), value => { appearance.Red = value.X; appearance.Green = value.Y; appearance.Blue = value.Z; });
        SettingFloat("Opacité du fond", appearance.Opacity * 100, 0, 100, "%.0f %%", value => appearance.Opacity = value / 100);
        ImGui.TextDisabled("Le texte et les indicateurs gardent leur opacité.");
        if (AppearanceScope == AppearanceTarget.Notification)
        {
            Toggle("Contour du fond", appearance.Border, value => appearance.Border = value);
            SettingFloat("Arrondi des coins", appearance.ToastCornerRadius ?? (appearance.Skin == MonitorSkin.Obsidienne ? 14 : 2), 0, 24, "%.0f px", value => appearance.ToastCornerRadius = value);
            Toggle("Icône de statut", appearance.ToastShowIcon, value => appearance.ToastShowIcon = value);
            Toggle("Barre de durée", appearance.ToastShowTimer, value => appearance.ToastShowTimer = value);
        }
        if (AppearanceScope == AppearanceTarget.Hud)
        {
            var background = (int)config.HudAppearance!.Background;
            SettingCombo("Visibilité du fond", ref background, ["Selon le design", "Afficher", "Masquer"], value => config.HudAppearance.Background = (HudBackgroundMode)value);
            if (ImGui.Button("Texte lisible comme LMeter")) { HudTypography.Apply(appearance.Text); plugin.Save(); }
            ImGui.TextWrapped("Expressway 16, texte blanc et contour noir. Le fond et la position restent inchangés.");
        }
        ColorControl("Texte principal", new(appearance.Text.Red, appearance.Text.Green, appearance.Text.Blue), value => { appearance.Text.Red = value.X; appearance.Text.Green = value.Y; appearance.Text.Blue = value.Z; });
        var font = (int)appearance.Text.Font;
        SettingCombo("Police", ref font, ["Dalamud", "Expressway (locale)", "Segoe UI", "Fichier local"], value => appearance.Text.Font = (MonitorFont)value);
        if (appearance.Text.Font == MonitorFont.LocalFile)
        {
            ImGui.SetNextItemWidth(-1); var path = appearance.Text.FontFile;
            if (ImGui.InputTextWithHint("##font-file", "Chemin absolu du fichier TTF / OTF", ref path, 1024)) appearance.Text.FontFile = path;
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
        }
        ImGui.TextDisabled(UiFonts.Status(appearance.Text));
        SettingFloat("Taille du texte", appearance.Text.Size, 12, 24, "%.0f px", value => appearance.Text.Size = MathF.Round(value));
        var edge = (int)appearance.Text.Edge;
        SettingCombo("Lisibilité du texte", ref edge, ["Sans effet", "Ombre", "Contour sombre"], value => appearance.Text.Edge = (TextEdge)value);
        if (appearance.Text.Edge != TextEdge.None)
            SettingFloat("Opacité du contour ou de l’ombre", appearance.Text.EdgeOpacity * 100, 0, 100, "%.0f %%", value => appearance.Text.EdgeOpacity = value / 100);
        if (ImGui.CollapsingHeader("Disposition et détails"))
        {
            if (AppearanceScope != AppearanceTarget.Notification) Toggle("Contour du fond", appearance.Border, value => appearance.Border = value);
            if (AppearanceScope == AppearanceTarget.Hud)
                ColorControl("Accent", new(appearance.AccentRed, appearance.AccentGreen, appearance.AccentBlue), value => { appearance.AccentRed = value.X; appearance.AccentGreen = value.Y; appearance.AccentBlue = value.Z; });
            SettingFloat("Marge horizontale", appearance.PaddingX, 0, 24, "%.0f px", value => appearance.PaddingX = value);
            SettingFloat("Marge verticale", appearance.PaddingY, 0, 16, "%.0f px", value => appearance.PaddingY = value);
            if (AppearanceScope == AppearanceTarget.Window) SettingFloat("Espacement des tâches", appearance.RowSpacing, 0, 16, "%.0f px", value => appearance.RowSpacing = value);
            if (AppearanceScope != AppearanceTarget.Hud)
            {
                var alignment = (int)appearance.Alignment;
                SettingCombo("Alignement des titres", ref alignment, ["Gauche", "Centre", "Droite"], value => appearance.Alignment = (ContentAlignment)value);
            }
            ImGui.TextWrapped("Ces décalages déplacent le texte à l’intérieur du composant. La position du HUD se règle dans HUD.");
            SettingFloat("Décalage interne du texte X", appearance.Text.OffsetX, -20, 20, "%.0f px", value => appearance.Text.OffsetX = value);
            SettingFloat("Décalage interne du texte Y", appearance.Text.OffsetY, -12, 12, "%.0f px", value => appearance.Text.OffsetY = value);
        }
        ImGui.Spacing();
        if (ImGui.Button("Restaurer ce thème")) { appearance.ApplyPreset(appearance.Skin, AppearanceScope); plugin.Save(); }
    }

    private void DrawAppearancePreview(SurfaceAppearance appearance)
    {
        var s = ImGuiHelpers.GlobalScale; var p = ImGui.GetCursorScreenPos();
        ImGui.TextDisabled("APERÇU · Données fictives"); p = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (AppearanceScope == AppearanceTarget.Hud)
        {
            var shape = MiniHudOptions.Size(plugin.Config.HudStyle, plugin.Config.ShowUsage, plugin.Config.HudAppearance);
            var size = shape * Math.Min(s * plugin.Config.MiniHudScale, width / shape.X);
            MiniHud.DrawFace(p, size, ExampleSnapshot(), false, false, plugin.Config.MiniHudOpacity,
                plugin.Config.HudStyle, plugin.Config.ShowUsage, appearance: plugin.Config.HudAppearance); ImGui.Dummy(size);
        }
        else if (AppearanceScope == AppearanceTarget.Notification)
        {
            var shape = NotificationOverlay.LogicalSize(appearance); var scale = Math.Min(s * appearance.Text.Size / 17, width / shape.X);
            NotificationOverlay.DrawFace(ImGui.GetWindowDrawList(), p, shape * scale, scale,
                new NotificationItem(-10, new("example", "Préparer une prochaine version", "Projet d’exemple", "", "question"), 1, 7), appearance, preview: true);
            ImGui.Dummy(shape * scale);
        }
        else
        {
            using var font = UiFonts.Push(appearance.Text); using var palette = ObsidianTheme.Palette(appearance);
            var padding = appearance.Padding * s;
            var rowHeight = ImGui.GetFontSize() * 2 + (10 + appearance.RowSpacing + Math.Abs(appearance.Text.OffsetY) * 2) * ObsidianTheme.UiScale;
            var size = new Vector2(width, rowHeight + padding.Y * 2);
            var draw = ImGui.GetWindowDrawList(); draw.AddRectFilled(p, p + size, ObsidianTheme.U(appearance.Color), 2 * s);
            MainWindow.DrawTaskFace(draw, p + padding, width - padding.X * 2, rowHeight,
                new("example", "Construire la prochaine version", "Projet d’exemple", "", "active"), appearance);
            ImGui.Dummy(size);
        }
    }

    private static MonitorSnapshot ExampleSnapshot() => new(true, DateTimeOffset.UtcNow,
        [new("demo1", "🎨 Améliorer l’accueil", "Exemple", "gpt-6-astra", "active", ReasoningEffort:"xhigh"), new("demo2", "Valider un écran", "Exemple", "gpt-6-astra", "needsInput"),new("demo3","Vérifier les notifications","Exemple","gpt-6-astra","idle",HasUnreadTurn:true)], null, true,
        new AccountUsage(DateTimeOffset.UtcNow, [new(78, 10080, null)]),TaskMetadataSupported:true);
    private void ColorControl(string label, Vector3 color, Action<Vector3> set)
    {
        if (ImGui.ColorEdit3(label, ref color, ImGuiColorEditFlags.NoInputs)) set(color);
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
    }
    private void SettingFloat(string label, float value, float min, float max, string format, Action<float> set)
    {
        ImGui.TextUnformatted(label); ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##" + label, ref value, min, max, format)) set(value);
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
    }
    private void SettingCombo(string label, ref int value, string[] options, Action<int> set)
    {
        ImGui.TextUnformatted(label); ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##" + label, ref value, options, options.Length)) { set(value); plugin.Save(); }
    }
}
