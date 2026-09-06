using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
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
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ICondition Conditions { get; private set; } = null!;

    private readonly WindowSystem windows = new("CodexMonitor");
    private readonly MainWindow main;
    private readonly BridgeClient client;
    private readonly IDtrBarEntry dtr;
    private readonly object saveGate = new();
    private readonly QuietModeGate quietGate = new();
    private bool fontsDirty;
    private MonitorSnapshot previous = MonitorSnapshot.Offline("Initialisation");
    internal Configuration Config { get; }
    internal NotificationOverlay NotificationUi { get; }
    internal NotificationHistory History { get; }
    internal NotificationCenter Center { get; }
    internal MiniHud Hud { get; }
    internal NotificationSounds Sounds { get; } = new();
    internal MonitorSnapshot Snapshot => client.Current;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? Configuration.NewInstall();
        Config.Port = Math.Clamp(Config.Port, 1, 65535);
        Config.Normalize();
        UiFonts.Initialize(PluginInterface.UiBuilder.FontAtlas);
        UiFonts.Refresh(Config.WindowAppearance!.Text, Config.HudAppearance!.Text, Config.ToastAppearance!.Text);
        History = new NotificationHistory(Config.NotificationHistory);
        NotificationUi = new NotificationOverlay(Config, Save, OnNotificationClick);
        Center = new NotificationCenter(History, NotificationUi.Queue, state => Sounds.Play(state, Config.Sounds));
        Hud = new MiniHud(this, OpenMain);
        client = new BridgeClient(Config.Port);
        main = new MainWindow(this) { IsOpen = Config.OpenOnLoad };
        windows.AddWindow(main);
        dtr = DtrBar.Get("Codex Monitor", Text("Codex …"));
        dtr.OnClick = _ => main.Toggle();
        Commands.AddHandler("/codex", new CommandInfo(OnCommand) { HelpMessage = "Tâches Codex. config : réglages ; history : historique ; hud : mini HUD ; preview : placement ; test [idle|input|approval|error|question] : exemple." });
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenMainUi += OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        Framework.Update += Update;
        Log.Information("Codex Monitor 0.6.0 loaded; local bridge port {Port}", Config.Port);
    }

    private static SeString Text(string text) => new SeStringBuilder().AddText(text).Build();
    private void Draw()
    {
        using var font = UiFonts.Push(Config.WindowAppearance!.Text);
        ObsidianTheme.Push(Config.WindowAppearance);
        try { windows.Draw(); Hud.Draw(); NotificationUi.Draw(Center.IsQuiet); }
        finally { ObsidianTheme.Pop(); }
    }
    private void OpenMain() { main.ShowSettings = false; main.ShowHistory = false; main.IsOpen = true; }
    private void OpenConfig() { main.ShowSettings = true; main.ShowHistory = false; main.IsOpen = true; }
    private void OpenHistory() { main.ShowSettings = false; main.ShowHistory = true; main.IsOpen = true; }
    private void OnNotificationClick(NotificationItem item)
    {
        if (item.Task.State == "summary") OpenHistory();
        else OpenMain();
    }
    private void OnCommand(string command, string args)
    {
        var parts = args.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.FirstOrDefault() == "config") OpenConfig();
        else if (parts.FirstOrDefault() == "history") OpenHistory();
        else if (parts.FirstOrDefault() == "hud")
        {
            SetIndicator(Config.Indicator == IndicatorMode.MiniHud ? IndicatorMode.Text : IndicatorMode.MiniHud);
        }
        else if (parts.FirstOrDefault() == "preview") NotificationUi.SetPreview(!NotificationUi.Preview);
        else if (parts.FirstOrDefault() == "test")
        {
            NotificationUi.SetPreview(false);
            var state = parts.ElementAtOrDefault(1) switch
            {
                "input" => "needsInput", "approval" => "needsApproval", "error" => "error", "question" => "question", _ => "idle",
            };
            NotificationUi.Test(state);
            Sounds.Play(state, Config.Sounds, true);
        }
        else main.Toggle();
    }

    internal void Save()
    {
        lock (saveGate)
        {
            Config.Normalize();
            fontsDirty = true;
            Config.NotificationHistory = History.Entries.ToList();
            PluginInterface.SavePluginConfig(Config);
            client.SetPort(Config.Port);
            dtr.Shown = Config.Indicator == IndicatorMode.Text;
        }
    }

    internal void SetIndicator(IndicatorMode mode)
    {
        Config.Indicator = mode;
        if (mode != IndicatorMode.MiniHud) Hud.SetEditing(false);
        Save();
    }

    private void Update(IFramework framework)
    {
        if (fontsDirty)
        {
            fontsDirty = false;
            UiFonts.Refresh(Config.WindowAppearance!.Text, Config.HudAppearance!.Text, Config.ToastAppearance!.Text);
        }
        dtr.Shown = Config.Indicator == IndicatorMode.Text;
        var snapshot = Snapshot;
        var now = DateTimeOffset.UtcNow;
        var requestedQuiet = (Config.QuietInCombat && Conditions[ConditionFlag.InCombat])
            || (Config.QuietInCutscene && (Conditions[ConditionFlag.WatchingCutscene]
                || Conditions[ConditionFlag.WatchingCutscene78] || Conditions[ConditionFlag.OccupiedInCutSceneEvent]));
        var quiet = quietGate.Update(requestedQuiet, now);
        Sounds.SetQuiet(quiet || !Config.Sounds.Enabled || Config.Sounds.Volume <= 0);
        if (Center.Update(snapshot, quiet, Config.NotifyOnIdle, Config.NotifyOnAttention, Config.NotificationSeconds, now, Config.NotifyOnQuestions)) Save();
        if (ReferenceEquals(previous, snapshot)) return;
        dtr.Text = Text(snapshot.Connected ? $"Codex {snapshot.Active} / {snapshot.Attention}!" : "Codex hors ligne");
        dtr.Tooltip = Text(snapshot.Connected
            ? $"{snapshot.Active} en cours\n{snapshot.Attention} intervention(s)\n{snapshot.Idle} au repos\nCliquer pour ouvrir"
            : "Relais local déconnecté. Cliquer pour ouvrir.");
        if (snapshot.Connected != previous.Connected)
            Log.Information("Local Codex bridge connected: {Connected}; observed tasks: {Count}", snapshot.Connected, snapshot.Threads.Count(thread => thread.IsObserved));
        previous = snapshot;
    }

    public void Dispose()
    {
        Framework.Update -= Update;
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Commands.RemoveHandler("/codex");
        NotificationUi.Queue.Clear();
        Sounds.Dispose();
        dtr.Remove();
        windows.RemoveAllWindows();
        client.Dispose();
        UiFonts.Dispose();
        Log.Information("Codex Monitor unloaded.");
    }
}
