using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace CodexMonitor;

internal sealed partial class SettingsPanel(Plugin plugin)
{
    internal int Category;
    private readonly HudMotion hudMotion = new();
    private static readonly string[] ToneNames = ["Silencieux", "Verre", "Goutte", "Velours", "Fichier WAV"];

    internal void Draw()
    {
        string[] tabs = ["Affichage", "Notifications", "Sons", "Connexion", "Apparence"];
        for (var i = 0; i < tabs.Length; i++)
        {
            if (i > 0 && ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.CalcTextSize(tabs[i]).X + 32 * ObsidianTheme.UiScale < ImGui.GetWindowWidth()) ImGui.SameLine();
            if (ObsidianTheme.Tab(tabs[i], Category == i)) Category = i;
        }
        if (ImGui.BeginChild($"settings-body-{Category}", Vector2.Zero, false))
        {
            switch (Category)
            {
                case 0: DrawDisplay(); break;
                case 1: DrawNotifications(); break;
                case 2: DrawSounds(); break;
                case 4: DrawAppearance(); break;
                default: DrawConnection(); break;
            }
        }
        ImGui.EndChild();
    }

    private void DrawDisplay()
    {
        var config = plugin.Config;
        var s = ImGuiHelpers.GlobalScale;
        ObsidianTheme.Section("Votre indicateur", "Choisissez ce qui reste visible lorsque la fenêtre est fermée.");
        if (ImGui.RadioButton("Mini HUD", config.Indicator == IndicatorMode.MiniHud)) plugin.SetIndicator(IndicatorMode.MiniHud);
        ImGui.SameLine();
        if (ImGui.RadioButton("Texte de la barre", config.Indicator == IndicatorMode.Text)) plugin.SetIndicator(IndicatorMode.Text);
        ImGui.SameLine();
        if (ImGui.RadioButton("Masqué", config.Indicator == IndicatorMode.Hidden)) plugin.SetIndicator(IndicatorMode.Hidden);
        ImGui.Spacing();
        var snapshot = plugin.Snapshot;
        var p = ImGui.GetCursorScreenPos();
        if (config.Indicator == IndicatorMode.MiniHud)
        {
            var style = (int)config.HudStyle;
            ImGui.SetNextItemWidth(220 * s);
            if (ImGui.Combo("Format", ref style, MiniHudOptions.Names, MiniHudOptions.Names.Length))
            { config.HudStyle = (MiniHudStyle)style; plugin.Save(); }
            Toggle("Afficher le quota restant", config.ShowUsage, value => config.ShowUsage = value);
            p = ImGui.GetCursorScreenPos();
            var baseSize = MiniHudOptions.Size(config.HudStyle, config.ShowUsage, config.HudAppearance);
            var size = baseSize * Math.Min(config.MiniHudScale * s, ImGui.GetContentRegionAvail().X / baseSize.X);
            var motion = hudMotion.Update(snapshot, config.AnimateHudChanges, ImGui.GetIO().DeltaTime);
            MiniHud.DrawFace(p, size, snapshot, plugin.Center.IsQuiet, false, config.MiniHudOpacity, config.HudStyle, config.ShowUsage, motion, config.HudAppearance);
            ImGui.Dummy(size);
            if (ImGui.IsItemHovered()) { ImGui.BeginTooltip(); MiniHud.DrawUsageDetails(snapshot); ImGui.EndTooltip(); }
            ImGui.TextDisabled("Détails au survol · Tâches au clic");
            Toggle("Animer les changements", config.AnimateHudChanges, value => config.AnimateHudChanges = value);
            if (config.AnimateHudChanges) { ImGui.SameLine(); if (ImGui.SmallButton("Tester l’animation")) hudMotion.Highlight(); }
            if (ImGui.Button(plugin.Hud.Editing ? "Verrouiller la position" : "Déplacer le mini HUD"))
            { plugin.NotificationUi.SetPreview(false); plugin.Hud.SetEditing(!plugin.Hud.Editing); }
            ImGui.SameLine();
            if (ImGui.Button("Recentrer"))
            { config.MiniHudAnchorX = 0.5f; config.MiniHudAnchorY = 0.08f; plugin.Hud.SetEditing(true); }
            Float("Taille du HUD", config.MiniHudScale, 0.75f, 1.5f, "%.2f ×", value => config.MiniHudScale = value);
            Float("Opacité du contenu", config.MiniHudOpacity * 100, 35, 100, "%.0f %%", value => config.MiniHudOpacity = value / 100);
            DrawHudAppearance();
        }
        else if (config.Indicator == IndicatorMode.Text)
        {
            var label = snapshot.Connected ? $"Codex {snapshot.Active} / {snapshot.Attention}!" : "Codex hors ligne";
            var size = new Vector2(Math.Min(320 * s, ImGui.GetContentRegionAvail().X), 52 * s);
            ImGui.GetWindowDrawList().AddRectFilled(p, p + size, ObsidianTheme.U(ObsidianTheme.Surface), 8 * s);
            ImGui.GetWindowDrawList().AddText(p + new Vector2(16, 17) * s, ObsidianTheme.U(ObsidianTheme.Text), label);
            ImGui.Dummy(size);
            ImGui.TextWrapped("Le compteur apparaît dans la barre d’informations de Dalamud. Un clic ouvre les tâches.");
        }
        else ImGui.TextWrapped("La fenêtre reste accessible avec /codex. Les notifications continuent de fonctionner.");
        ImGui.Separator();
        ObsidianTheme.Section("Liste des tâches");
        Toggle("Afficher les tâches au repos", config.ShowIdle, value => config.ShowIdle = value);
        Toggle("Inclure les tâches non observées", config.ShowUnobserved, value => config.ShowUnobserved = value);
        Toggle("Ouvrir la fenêtre au chargement", config.OpenOnLoad, value => config.OpenOnLoad = value);
    }

    private void DrawHudAppearance()
    {
        var config = plugin.Config;
        var appearance = config.HudAppearance!;
        ObsidianTheme.Section("Fond du HUD", "La transparence du fond est indépendante du texte et des icônes.");
        if (ImGui.SmallButton("Texte, police et thème")) { Category = 4; AppearanceScope = AppearanceTarget.Hud; }
        var previewSize = MiniHudOptions.Size(config.HudStyle, config.ShowUsage, appearance)
            * Math.Min(config.MiniHudScale * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X / MiniHudOptions.Size(config.HudStyle, config.ShowUsage, appearance).X);
        MiniHud.DrawFace(ImGui.GetCursorScreenPos(), previewSize, plugin.Snapshot, plugin.Center.IsQuiet, false,
            config.MiniHudOpacity, config.HudStyle, config.ShowUsage, appearance: appearance);
        ImGui.Dummy(previewSize);
        var mode = (int)appearance.Background;
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("Visibilité du fond", ref mode, new[] { "Selon le design", "Afficher", "Masquer" }, 3))
        { appearance.Background = (HudBackgroundMode)mode; plugin.Save(); }
        var color = new Vector3(appearance.Red, appearance.Green, appearance.Blue);
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.ColorEdit3("Couleur du fond", ref color, ImGuiColorEditFlags.NoInputs))
        { appearance.Red = color.X; appearance.Green = color.Y; appearance.Blue = color.Z; }
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
        for (var i = 0; i < HudAppearance.Presets.Length; i++)
        {
            var preset = HudAppearance.Presets[i];
            if (i > 0) ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(preset.Color, 1));
            if (ImGui.SmallButton(preset.Name))
            { appearance.Red = preset.Color.X; appearance.Green = preset.Color.Y; appearance.Blue = preset.Color.Z; plugin.Save(); }
            ImGui.PopStyleColor();
        }
        Float("Opacité du fond", appearance.Opacity * 100, 0, 100, "%.0f %%", value => appearance.Opacity = value / 100);
        Toggle("Contour du fond", appearance.Border, value => appearance.Border = value);
        if (!appearance.HasBackground(config.HudStyle)) ImGui.TextDisabled("Fond masqué : choisir « Afficher » pour voir la couleur.");
        if (ImGui.SmallButton("Réinitialiser l’apparence"))
        { config.HudAppearance = new HudAppearance(); config.MiniHudOpacity = 0.94f; plugin.Save(); }
    }

    private void DrawNotifications()
    {
        var c = plugin.Config; var n = plugin.NotificationUi;
        ObsidianTheme.Section("Ce qui mérite votre attention");
        Toggle("Un tour se termine", c.NotifyOnIdle, value => c.NotifyOnIdle = value);
        Toggle("Une réponse, une approbation ou une erreur", c.NotifyOnAttention, value => c.NotifyOnAttention = value);
        Toggle("Une question pendant que Codex continue", c.NotifyOnQuestions, value => c.NotifyOnQuestions = value);
        ImGui.TextDisabled("Pas d’alerte à la première connexion ni à la reconnexion.");
        ImGui.Separator();
        ObsidianTheme.Section("Mode discret", "Les alertes reprennent après 2 s au calme, avec leur durée complète.");
        Toggle("Pendant les combats", c.QuietInCombat, value => c.QuietInCombat = value);
        Toggle("Pendant les cinématiques", c.QuietInCutscene, value => c.QuietInCutscene = value);
        ImGui.Separator();
        ObsidianTheme.Section("Notifications");
        if (ImGui.SmallButton("Personnaliser l’apparence")) { Category = 4; AppearanceScope = AppearanceTarget.Notification; }
        if (ImGui.Button(n.Preview ? "Terminer le placement" : "Placer les notifications")) { plugin.Hud.SetEditing(false); n.SetPreview(!n.Preview); }
        ImGui.SameLine();
        if (ImGui.Button("Recentrer"))
        { c.NotificationAnchorX = 0.5f; c.NotificationAnchorY = 0.22f; c.NotificationOffsetX = c.NotificationOffsetY = 0; n.SetPreview(true); }
        if (plugin.Center.IsQuiet) ImGui.TextColored(ObsidianTheme.Amber, "Mode discret actif : l’aperçu attendra.");
        Float("Durée", c.NotificationSeconds, 4, 15, "%.0f s", value => c.NotificationSeconds = value);
        Float("Taille", c.NotificationScale, 0.75f, 1.5f, "%.2f ×", value => c.NotificationScale = value);
        Toggle("Réduire les animations", c.NotificationReducedMotion, value => c.NotificationReducedMotion = value);
        var direction = (int)c.NotificationDirection;
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("Empilement", ref direction, new[] { "Automatique", "Vers le bas", "Vers le haut" }, 3))
        { c.NotificationDirection = (StackDirection)direction; plugin.Save(); }
        if (ImGui.CollapsingHeader("Placement précis"))
        {
            Toggle("Grille", c.PreviewGrid, value => c.PreviewGrid = value);
            Toggle("Aimantation", c.PreviewSnap, value => c.PreviewSnap = value);
            var count = c.PreviewCount;
            ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
            if (ImGui.SliderInt("Nombre en aperçu", ref count, 1, 3)) { c.PreviewCount = count; plugin.Save(); }
            if (ImGui.SmallButton("Gauche")) n.Align(0, -1); ImGui.SameLine();
            if (ImGui.SmallButton("Centrer")) n.Align(1, -1); ImGui.SameLine();
            if (ImGui.SmallButton("Droite")) n.Align(2, -1);
            if (ImGui.SmallButton("Haut")) n.Align(-1, 0); ImGui.SameLine();
            if (ImGui.SmallButton("Milieu")) n.Align(-1, 1); ImGui.SameLine();
            if (ImGui.SmallButton("Bas")) n.Align(-1, 2);
            var offset = new Vector2(c.NotificationOffsetX, c.NotificationOffsetY);
            ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
            if (ImGui.DragFloat2("Position X / Y (px)", ref offset, 1, -4096, 4096, "%.0f")) { c.NotificationOffsetX = offset.X; c.NotificationOffsetY = offset.Y; }
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
        }
        string[] states = ["idle", "needsInput", "needsApproval", "error", "question"];
        var example = Math.Max(0, Array.IndexOf(states, n.PreviewState));
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("Exemple", ref example, new[] { "Tour terminé", "Réponse requise", "Approbation", "Erreur", "Question sans pause" }, 5)) n.PreviewState = states[example];
        if (ImGui.Button("Tester une notification")) { n.SetPreview(false); n.Test(n.PreviewState); plugin.Sounds.Play(n.PreviewState, c.Sounds, true); }
    }

    private void DrawSounds()
    {
        var sound = plugin.Config.Sounds;
        ObsidianTheme.Section("Une présence discrète", "Des sons courts, avec un volume indépendant du jeu.");
        Toggle("Activer les sons", sound.Enabled, value => sound.Enabled = value);
        Float("Volume", sound.Volume * 100, 0, 100, "%.0f %%", value => sound.Volume = value / 100);
        ImGui.TextDisabled("Silence en mode discret · Au plus un son toutes les 2 s");
        if (plugin.Center.IsQuiet) ImGui.TextColored(ObsidianTheme.Amber, "Mode discret actif : les écoutes sont aussi silencieuses.");
        ImGui.Separator();
        SoundRow("Tour terminé", "idle", sound.Completion, sound.CompletionFile, value => sound.Completion = value, value => sound.CompletionFile = value);
        SoundRow("Question, réponse ou approbation", "needsInput", sound.Attention, sound.AttentionFile, value => sound.Attention = value, value => sound.AttentionFile = value);
        SoundRow("Erreur", "error", sound.Error, sound.ErrorFile, value => sound.Error = value, value => sound.ErrorFile = value);
        if (plugin.Sounds.Error is { } error) ImGui.TextColored(ObsidianTheme.Red, error);
        if (ImGui.CollapsingHeader("Utiliser mon propre son"))
            ImGui.TextWrapped("Choisir « Fichier WAV » puis saisir son chemin complet. WAV PCM 16 bits, mono ou stéréo, 3 secondes et 2 Mo maximum. Le volume choisi s’applique aussi à votre fichier.");
    }

    private void SoundRow(string label, string state, SoundTone tone, string file, Action<SoundTone> chooseTone, Action<string> chooseFile)
    {
        ImGui.PushID(state); ImGui.Spacing(); ImGui.TextUnformatted(label);
        var selection = (int)tone;
        ImGui.SetNextItemWidth(Math.Min(260 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X - 110 * ImGuiHelpers.GlobalScale));
        if (ImGui.Combo("##tone", ref selection, ToneNames, ToneNames.Length)) { chooseTone((SoundTone)selection); plugin.Save(); }
        ImGui.SameLine();
        ImGui.BeginDisabled(!plugin.Config.Sounds.Enabled || plugin.Config.Sounds.Volume <= 0 || plugin.Center.IsQuiet || selection == 0);
        if (ImGui.Button("Écouter")) plugin.Sounds.Play(state, plugin.Config.Sounds, true);
        ImGui.EndDisabled();
        if ((SoundTone)selection == SoundTone.Custom)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##file", @"C:\Sons\notification.wav", ref file, 1024)) chooseFile(file);
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
        }
        ImGui.PopID();
    }

    private void DrawConnection()
    {
        var snapshot = plugin.Snapshot;
        ObsidianTheme.Section(snapshot.Connected ? "Relais connecté" : "Relais déconnecté");
        ImGui.TextWrapped(snapshot.Connected ? "Les tâches sont actualisées automatiquement depuis ce PC." : snapshot.Error ?? "Le relais local ne répond pas.");
        var port = plugin.Config.Port;
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Port local", ref port) && port is >= 1 and <= 65535) { plugin.Config.Port = port; plugin.Save(); }
        ImGui.TextDisabled("127.0.0.1 · Actualisation toutes les 2 secondes");
        if (snapshot.Connected && !snapshot.QuestionTrackingSupported)
            ImGui.TextWrapped("Pour repérer les questions sans pause, lancer le relais fourni avec la version 0.4.0.");
        ImGui.Separator();
        ObsidianTheme.Section("Quota Codex");
        MiniHud.DrawUsageDetails(snapshot);
        ImGui.TextWrapped("Compte connecté au CLI Codex sur ce PC. Actualisation chaque minute ; priorité à la période hebdomadaire.");
        if (snapshot.Connected && !snapshot.UsageTrackingSupported && snapshot.Usage is null)
            ImGui.TextWrapped("L’ancien relais ne fournit pas le quota. Utiliser Activer-Quota.ps1 dans le dossier bridge de cette livraison.");
        else if (snapshot.CurrentUsage is null) ImGui.TextWrapped("Le relais avec quota nécessite un CLI Codex installé et connecté au compte souhaité.");
        ImGui.Separator();
        ObsidianTheme.Section("Raccourcis");
        ImGui.TextUnformatted("/codex          Ouvrir les tâches\n/codex history  Consulter l’historique\n/codex preview  Placer les notifications\n/codex hud      Basculer Mini HUD / texte");
        ImGui.Spacing(); ImGui.TextDisabled("Codex Monitor 0.6.0 · Aleqsd");
        ImGui.TextWrapped(typeof(Configuration).Assembly.Location);
    }

    private void Toggle(string label, bool value, Action<bool> set)
    {
        if (ImGui.CalcTextSize(label).X + ImGui.GetFrameHeight() + 8 > ImGui.GetContentRegionAvail().X)
        {
            if (ImGui.Checkbox("##" + label, ref value)) { set(value); plugin.Save(); }
            ImGui.SameLine(); ImGui.TextWrapped(label);
        }
        else if (ImGui.Checkbox(label, ref value)) { set(value); plugin.Save(); }
    }
    private void Float(string label, float value, float min, float max, string format, Action<float> set)
    {
        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat(label, ref value, min, max, format)) set(value);
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
    }
}
