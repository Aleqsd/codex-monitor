using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CodexMonitor;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static INotificationManager Notifications { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windows = new("CodexMonitor");
    private readonly MainWindow main;
    private readonly BridgeClient client;
    private readonly IDtrBarEntry dtr;
    private MonitorSnapshot previous = MonitorSnapshot.Offline("Initialisation");
    internal Configuration Config { get; }
    internal MonitorSnapshot Snapshot => client.Current;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Port = Math.Clamp(Config.Port, 1, 65535);
        client = new BridgeClient(Config.Port);
        main = new MainWindow(this) { IsOpen = Config.OpenOnLoad };
        windows.AddWindow(main);
        dtr = DtrBar.Get("Codex Monitor", Text("Codex …"));
        dtr.OnClick = _ => main.Toggle();
        Commands.AddHandler("/codex", new CommandInfo(OnCommand) { HelpMessage = "Afficher les tâches Codex. /codex config : réglages." });
        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        Framework.Update += Update;
        Log.Information("Codex Monitor 0.1.0 loaded; local bridge port {Port}", Config.Port);
    }

    private static SeString Text(string text) => new SeStringBuilder().AddText(text).Build();
    private void OpenMain() => main.IsOpen = true;
    private void OpenConfig() { main.ShowSettings = true; main.IsOpen = true; }
    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("config", StringComparison.OrdinalIgnoreCase)) OpenConfig();
        else main.Toggle();
    }

    internal void Save()
    {
        PluginInterface.SavePluginConfig(Config);
        client.SetPort(Config.Port);
        dtr.Shown = Config.ShowDtr;
    }

    private void Update(IFramework framework)
    {
        dtr.Shown = Config.ShowDtr;
        var snapshot = Snapshot;
        if (ReferenceEquals(previous, snapshot)) return;
        dtr.Text = Text(snapshot.Connected ? $"Codex {snapshot.Active} / {snapshot.Attention}!" : "Codex hors ligne");
        dtr.Tooltip = Text(snapshot.Connected
            ? $"{snapshot.Active} en cours\n{snapshot.Attention} intervention(s)\n{snapshot.Idle} au repos\nCliquer pour ouvrir"
            : "Relais local déconnecté. Cliquer pour ouvrir.");
        var changes = TransitionDetector.Find(previous, snapshot, Config.NotifyOnIdle, Config.NotifyOnAttention);
        foreach (var thread in changes.Take(3))
        {
            Notifications.AddNotification(new Notification
            {
                Title = thread.State == "idle" ? "Codex · Tour terminé" : "Codex · Intervention requise",
                Content = MonitorContract.Clean(thread.Title, "Tâche Codex", 140),
                Type = thread.State == "idle" ? NotificationType.Success : NotificationType.Warning,
                InitialDuration = TimeSpan.FromSeconds(7),
            });
        }
        if (snapshot.Connected != previous.Connected)
            Log.Information("Local Codex bridge connected: {Connected}; observed tasks: {Count}", snapshot.Connected, snapshot.Threads.Count(thread => thread.IsObserved));
        previous = snapshot;
    }

    public void Dispose()
    {
        Framework.Update -= Update;
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Commands.RemoveHandler("/codex");
        dtr.Remove();
        windows.RemoveAllWindows();
        client.Dispose();
        Log.Information("Codex Monitor unloaded.");
    }
}
