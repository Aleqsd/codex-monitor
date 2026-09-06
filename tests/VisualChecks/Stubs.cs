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
        internal MonitorSnapshot Snapshot { get; set; } = new(true, DateTimeOffset.UtcNow,
        [
            new("1", "Améliorer le plugin FF14", "Codex Monitor", "gpt-6-astra", "active", ["11111111111111111111111111111111"]),
            new("2", "Valider les nouveaux écrans du catalogue", "Catalogue", "gpt-6-astra", "needsInput"),
            new("3", "Préparer la prochaine livraison", "Projet démo", "gpt-6-astra", "active"),
            new("4", "Vérifier les tests et les dépendances du projet", "Projet démo", "gpt-6-astra", "idle"),
            new("5", "Une tâche avec un titre très long qui doit rester lisible et ne jamais recouvrir son état", "Projet de démonstration", "gpt-6-astra", "idle"),
        ], null, true, new AccountUsage(DateTimeOffset.UtcNow, [new(48, 10080, DateTimeOffset.UtcNow.AddDays(6).ToUnixTimeSeconds())]));
        internal NotificationHistory History { get; } = new();
        internal NotificationOverlay NotificationUi { get; }
        internal NotificationCenter Center { get; }
        internal MiniHud Hud { get; }
        internal NotificationSounds Sounds { get; } = new();
        internal int SaveCount;
        internal int OpenCount;
        internal Plugin()
        {
            Config.Normalize();
            NotificationUi = new NotificationOverlay(Config, Save, _ => { });
            Center = new NotificationCenter(History, NotificationUi.Queue);
            Hud = new MiniHud(this, () => { OpenCount++; });
            History.Add(Snapshot.Threads[3], DateTimeOffset.Now.AddMinutes(-3));
            History.Add(Snapshot.Threads[1], DateTimeOffset.Now);
        }
        internal void Save() { Config.Normalize(); SaveCount++; }
        internal void SetIndicator(IndicatorMode mode) { Config.Indicator = mode; Save(); }
    }
}
