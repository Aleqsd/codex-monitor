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
        Controls.Clear();
        var s=ObsidianTheme.UiScale;
        if(ImGui.BeginChild("settings-navigation",new Vector2(120*s,0),false))
        {
            foreach(var (id,label) in new[]{(0,"HUD"),(1,"Notifications"),(2,"Sons"),(6,"Suivi"),(4,"Tâches"),(5,"Visibilité"),(3,"Connexion")})
            { if(ImGui.Selectable(label,Category==id,ImGuiSelectableFlags.None,new Vector2(0,30*s))) Category=id; Remember("nav-"+id); }
        }
        ImGui.EndChild(); ImGui.SameLine();
        if(ImGui.BeginChild("settings-workspace",Vector2.Zero,false,ImGuiWindowFlags.NoScrollbar))
        {
            DrawSettingsWorkspace();
        }
        ImGui.EndChild();
    }

    private void DrawNotifications()
    {
        var c = plugin.Config; var n = plugin.NotificationUi;
        ObsidianTheme.Section("Ce qui mérite votre attention");
        Toggle("Une réponse est prête", c.NotifyOnIdle, value => c.NotifyOnIdle = value);
        Toggle("Une réponse attendue, une approbation ou une erreur", c.NotifyOnAttention, value => c.NotifyOnAttention = value);
        Toggle("Une question pendant que Codex continue", c.NotifyOnQuestions, value => c.NotifyOnQuestions = value);
        Toggle("Afficher un extrait des questions",c.ShowQuestionExcerpts,value=>c.ShowQuestionExcerpts=value);
        ImGui.TextWrapped("Les extraits disponibles restent temporaires et ne sont pas enregistrés dans l’historique.");
        Toggle("Regrouper les alertes rapprochées d’une tâche", c.GroupNotificationBursts, value => c.GroupNotificationBursts = value);
        ImGui.TextWrapped("Regroupement sur 2 s. Les erreurs apparaissent immédiatement ; les alertes dépassées disparaissent.");
        ImGui.TextDisabled("Pas d’alerte à la première connexion ni à la reconnexion.");
        ImGui.Separator();
        ObsidianTheme.Section("Mettre les notifications en pause", "Les alertes reprennent après 2 s au calme, avec leur durée complète.");
        Toggle("Pendant les combats", c.QuietInCombat, value => c.QuietInCombat = value);
        Toggle("Pendant les cinématiques", c.QuietInCutscene, value => c.QuietInCutscene = value);
        ImGui.Separator();
        ObsidianTheme.Section("Notifications");
        if (ImGui.Button(n.Preview ? "Terminer le placement" : "Placer les notifications")) { plugin.Hud.SetEditing(false); n.SetPreview(!n.Preview); }
        ImGui.SameLine();
        if (ImGui.Button("Recentrer"))
        { c.NotificationAnchorX = 0.5f; c.NotificationAnchorY = 0.22f; c.NotificationOffsetX = c.NotificationOffsetY = 0; n.SetPreview(true); }
        if (plugin.Center.IsQuiet) ImGui.TextColored(ObsidianTheme.Amber, "Notifications en pause : l’aperçu attendra.");
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
        if (ImGui.Combo("Exemple", ref example, new[] { "Réponse prête", "Réponse requise", "Approbation", "Erreur", "Question de Codex" }, 5)) n.PreviewState = states[example];
        if (ImGui.Button("Tester une notification")) { n.SetPreview(false); n.Test(n.PreviewState); plugin.Sounds.Play(n.PreviewState, c.Sounds, true); }
    }

    private void DrawSounds()
    {
        var sound = plugin.Config.Sounds;
        ObsidianTheme.Section("Sons des notifications", "Des sons courts, avec un volume indépendant du jeu.");
        Toggle("Activer les sons", sound.Enabled, value => sound.Enabled = value);
        Float("Volume", sound.Volume * 100, 0, 100, "%.0f %%", value => sound.Volume = value / 100);
        ImGui.TextWrapped("Silence pendant la pause des notifications · Au plus un son toutes les 2 s");
        if (plugin.Center.IsQuiet) ImGui.TextColored(ObsidianTheme.Amber, "Notifications en pause : les écoutes sont aussi silencieuses.");
        ImGui.Separator();
        SoundRow("Réponse prête", "idle", sound.Completion, sound.CompletionFile, value => sound.Completion = value, value => sound.CompletionFile = value);
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
        DrawDiagnostic();
        ImGui.TextWrapped(snapshot.Connected ? "Les tâches sont actualisées automatiquement depuis ce PC." : snapshot.Error ?? "Le relais local ne répond pas.");
        var relay = plugin.Relay.State;
        ImGui.BeginDisabled(relay.Busy || (snapshot.Connected && relay.Phase != RelayPhase.Running));
        if (relay.Phase == RelayPhase.Running)
        {
            if (ImGui.Button("Arrêter mon relais")) plugin.StopRelay();
        }
        else if (ImGui.Button(relay.Busy ? "Veuillez patienter…" : snapshot.Connected ? "Relais déjà connecté" : "Lancer le relais")) plugin.StartRelay();
        ImGui.EndDisabled();
        ImGui.TextWrapped(snapshot.Connected && relay.Phase == RelayPhase.Ready
            ? "Le relais actuel est géré séparément. Il peut rester actif." : relay.Message);
        Toggle("Lancer le relais automatiquement à la connexion au personnage", plugin.Config.AutoStartRelay, value => plugin.Config.AutoStartRelay = value);
        ImGui.TextWrapped(plugin.Config.AutoStartRelay ? plugin.AutoRelay.Status : "Démarrage manuel · le relais peut être lancé avec le bouton ci-dessus.");
        ImGui.TextWrapped("Node.js 22.22.2 minimum et Codex sur ce PC. Un relais déjà actif reste géré séparément.");
        if (ImGui.CollapsingHeader("Node.js introuvable ?"))
        {
            var node = plugin.Config.RelayNodePath;
            ImGui.SetNextItemWidth(Math.Max(100, ImGui.GetContentRegionAvail().X));
            if (ImGui.InputTextWithHint("##relay-node", @"C:\Program Files\nodejs\node.exe", ref node, 1024)) plugin.Config.RelayNodePath = node;
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.Save();
            ImGui.TextWrapped("Chemin facultatif vers node.exe. Vide : détection automatique. Le relais lui-même est fourni avec le plugin.");
        }
        var port = plugin.Config.Port;
        ImGui.BeginDisabled(relay.Busy || relay.Phase == RelayPhase.Running);
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Port local", ref port) && port is >= 1 and <= 65535) { plugin.Config.Port = port; plugin.Save(); }
        ImGui.EndDisabled();
        ImGui.TextDisabled("127.0.0.1 · Actualisation toutes les 2 secondes");
        ImGui.TextWrapped($"Plugin chargé : {PluginVersion.Current} · Relais : {snapshot.RelayVersion ?? "version non fournie"}");
        if (snapshot.Connected) ImGui.TextWrapped($"Suivi des questions : {(snapshot.QuestionTrackingSupported ? "disponible" : "relais à mettre à jour")} · Quota : {(snapshot.UsageTrackingSupported ? "pris en charge" : "relais à mettre à jour")}");
        ImGui.Separator();
        ObsidianTheme.Section("Quota Codex");
        MiniHud.DrawUsageDetails(snapshot, plugin.Config.UsagePeriod);
        var period = (int)plugin.Config.UsagePeriod;
        ImGui.SetNextItemWidth(Math.Min(260 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
        if (ImGui.Combo("Période du HUD", ref period, new[] { "Semaine (par défaut)", "Période courte / 5 heures", "Période la plus limitante" }, 3))
        { plugin.Config.UsagePeriod = (UsagePreference)period; plugin.Save(); }
        ImGui.TextWrapped("Compte connecté au CLI Codex sur ce PC. Actualisation chaque minute. Une autre période épuisée est signalée par un astérisque rouge dans le HUD.");
        DrawQuotaAlerts();
        if (snapshot.Connected && !snapshot.UsageTrackingSupported && snapshot.Usage is null)
            ImGui.TextWrapped("Le relais actif est ancien. Arrêter cette instance depuis son dossier, puis lancer le relais inclus avec ce plugin.");
        else if (snapshot.CurrentUsage is null) ImGui.TextWrapped("Le relais avec quota nécessite un CLI Codex installé et connecté au compte souhaité.");
        ImGui.Separator();
        ObsidianTheme.Section("Raccourcis");
        ImGui.TextUnformatted("/codex          Ouvrir les tâches\n/codex history  Consulter l’historique\n/codex preview  Placer les notifications\n/codex hud      Basculer Mini HUD / texte\n/codex dnd 30   Pause 30 minutes\n/codex dnd off  Reprendre les alertes");
        ImGui.Spacing(); ImGui.TextDisabled($"Codex Monitor {PluginVersion.Current} · Aleqsd");
        ImGui.TextWrapped(plugin.EmojiStatus);
        if (ImGui.CollapsingHeader("Informations techniques")) ImGui.TextWrapped(typeof(Configuration).Assembly.Location);
    }

    private void DrawVisibility()
    {
        var config = plugin.Config; var visibility = config.Visibility!;
        ObsidianTheme.Section("Quand masquer le plugin", "Case cochée : le plugin est masqué dans cette situation (fenêtre, mini HUD, texte de barre et notifications).");
        Toggle("Écran titre et sélection du personnage", visibility.HideOutsideGame, value => visibility.HideOutsideGame = value);
        Toggle("Écrans de chargement", visibility.HideWhileLoading, value => visibility.HideWhileLoading = value);
        Toggle("Cinématiques", visibility.HideInCutscenes, value => visibility.HideInCutscenes = value);
        Toggle("Mode photo / gpose", visibility.HideInGpose, value => visibility.HideInGpose = value);
        Toggle("Combats", visibility.HideInCombat, value => visibility.HideInCombat = value);
        Toggle("Instances et donjons", visibility.HideInDuty, value => visibility.HideInDuty = value);
        ImGui.Spacing();
        ImGui.TextWrapped("L’interface masquée avec Arrêt défil. reste respectée. En jeu, les alertes attendent le retour au calme. Celles de l’écran titre ne sont pas rejouées à la connexion.");
        ImGui.Separator();
        ObsidianTheme.Section("Fenêtre principale");
        Toggle("Ouvrir automatiquement après le chargement du plugin", config.OpenOnLoad, value => config.OpenOnLoad = value);
        ImGui.TextWrapped("Désactivé par défaut. Si activé, attend un personnage connecté et un contexte visible. La fenêtre se ferme à la déconnexion.");
        ImGui.TextWrapped("Les tâches et réglages restent accessibles à la demande avec /codex ou le bouton Dalamud, même si l’indicateur est masqué.");
        if (ImGui.Button("Rétablir la visibilité par défaut"))
        { config.Visibility = new VisibilityOptions(); config.OpenOnLoad = false; plugin.Save(); }
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
        SettingFloat(label,value,min,max,format,set);
    }
}
