using System.Numerics;
using Dalamud.Bindings.ImGui;

// Only host services are simulated. All UI components below are linked from src/.
namespace Dalamud.Configuration { public interface IPluginConfiguration { int Version { get; set; } } }
namespace Dalamud.Interface.Utility { public static class ImGuiHelpers { public static float GlobalScale = 1; } }
namespace Dalamud.Interface.Windowing
{
    public sealed class WindowSizeConstraints { public Vector2 MinimumSize, MaximumSize; }
    public abstract class Window(string name)
    {
        public string WindowName = name;
        public Vector2 Size; public ImGuiCond SizeCondition; public WindowSizeConstraints? SizeConstraints;
        public abstract void Draw();
    }
}
namespace CodexMonitor
{
    internal static class UiFonts
    {
        internal static Func<TextAppearance?, IDisposable?>? Resolver;
        internal static IDisposable? Push(TextAppearance? text) => Resolver?.Invoke(text);
        internal static string Status(TextAppearance text) => text.Font is MonitorFont.Expressway or MonitorFont.LocalFile ? "Police absente · repli Dalamud" : "Police prête";
    }
    internal sealed class Plugin
    {
        internal Configuration Config { get; } = new();
        private readonly QuestionDismissals dismissals = new();
        private readonly TaskFollowing following = new();
        internal ConnectionDiagnostics Diagnostics { get; } = new();
        internal MonitorSnapshot AllTasks => dismissals.Apply(raw);
        internal void ToggleFavorite(string id) { Config.Following.ToggleFavorite(id); Save(); }
        internal void MuteTask(string id, int minutes) { Config.Following.Mute(id, minutes, DateTimeOffset.UtcNow); Save(); }
        internal int LastTaskFilter = -1; internal bool QuotaOpened;
        internal void OpenTasks(int filter) { LastTaskFilter = filter; OpenCount++; }
        internal void OpenQuota() { QuotaOpened = true; OpenCount++; }
        private MonitorSnapshot raw = new(true, DateTimeOffset.UtcNow,
        [
            new("1", "Améliorer le plugin FF14", "Codex Monitor", "gpt-6-astra", "active", ["11111111111111111111111111111111"]),
            new("2", "Valider les nouveaux écrans du catalogue", "Catalogue", "gpt-6-astra", "needsInput"),
            new("3", "Préparer la prochaine livraison", "Projet démo", "gpt-6-astra", "active"),
            new("4", "Vérifier les tests et les dépendances du projet", "Projet démo", "gpt-6-astra", "idle"),
            new("5", "Une tâche avec un titre très long qui doit rester lisible et ne jamais recouvrir son état", "Projet de démonstration", "gpt-6-astra", "idle"),
        ], null, true, new AccountUsage(DateTimeOffset.UtcNow, [new(48, 10080, DateTimeOffset.UtcNow.AddDays(6).ToUnixTimeSeconds())]));
        internal MonitorSnapshot Snapshot { get => following.Apply(AllTasks, Config.Following); set { raw = value; following.Invalidate(); } }
        internal void DismissQuestions(MonitoredThread task) { dismissals.Dismiss(task); Config.DismissedQuestions = dismissals.Export(); NotificationUi.Queue.Reconcile(Snapshot); Save(); }
        internal void RestoreQuestions(string id) { var restored = Snapshot.Threads.First(row => row.Id == id).HiddenQuestionIds ?? []; dismissals.Restore(id); Config.DismissedQuestions = dismissals.Export(); Center.RestoreQuestions(id, restored); Save(); }
        internal NotificationHistory History { get; } = new();
        internal NotificationOverlay NotificationUi { get; }
        internal NotificationCenter Center { get; }
        internal MiniHud Hud { get; }
        internal NotificationSounds Sounds { get; } = new();
        internal RelayLauncher Relay { get; } = new(Path.Combine(Path.GetTempPath(), "codex-monitor-visual-unused"));
        internal ManualQuietMode ManualQuiet { get; } = new();
        internal RelayAutoStart AutoRelay { get; } = new();
        internal string EmojiStatus => "Emojis prêts";
        internal string PauseDescription => ManualQuiet.Label(DateTimeOffset.UtcNow);
        internal void PauseAlerts(int? minutes) { if (!ManualQuiet.Enabled) Center.BeginManualPause(); ManualQuiet.Start(minutes, DateTimeOffset.UtcNow); }
        internal void ResumeAlerts() { ManualQuiet.Stop(); Center.Update(Snapshot, false, true, true, 7, DateTimeOffset.UtcNow); }
        internal void StartRelay() => AutoRelay.ManualStart();
        internal void StopRelay() => AutoRelay.ManualStop();
        internal int SaveCount;
        internal int OpenCount;
        internal bool ConfigOpened;
        internal readonly List<Uri> OpenedLinks = new();
        internal CodexTaskLink TaskLink { get; }
        internal Plugin()
        {
            TaskLink = new(uri => { lock (OpenedLinks) OpenedLinks.Add(uri); });
            Config.Normalize();
            NotificationUi = new NotificationOverlay(Config, Save, _ => { OpenCount++; }, TaskLink);
            Center = new NotificationCenter(History, NotificationUi.Queue);
            Hud = new MiniHud(this, () => { OpenCount++; ConfigOpened = true; });
            History.Add(Snapshot.Threads[3], DateTimeOffset.Now.AddMinutes(-3));
            History.Add(Snapshot.Threads[1], DateTimeOffset.Now);
        }
        internal void Save() { Config.Normalize(); following.Invalidate(); SaveCount++; }
        internal void SetIndicator(IndicatorMode mode) { Config.Indicator = mode; Save(); }
    }
}
