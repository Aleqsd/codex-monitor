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
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;

    private readonly WindowSystem windows = new("CodexMonitor");
    private readonly MainWindow main;
    private readonly BridgeClient client;
    private readonly IDtrBarEntry dtr;
    private readonly object saveGate = new();
    private readonly QuietModeGate quietGate = new();
    private readonly QuestionDismissals questionDismissals;
    private readonly EmojiImages emojis;
    private DateTimeOffset nextEmojiRefresh;
    private bool fontsDirty;
    private bool manuallyOpened;
    private bool pendingOpen;
    private bool wasLoggedIn;
    private bool suspended;
    private MonitorSnapshot previous = MonitorSnapshot.Offline("Initialisation");
    internal Configuration Config { get; }
    internal NotificationOverlay NotificationUi { get; }
    internal NotificationHistory History { get; }
    internal NotificationCenter Center { get; }
    internal MiniHud Hud { get; }
    internal NotificationSounds Sounds { get; } = new();
    internal RelayLauncher Relay { get; }
    internal CodexTaskLink TaskLink { get; } = new();
    internal MonitorSnapshot Snapshot => questionDismissals.Apply(client.Current);

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? Configuration.NewInstall();
        Config.Port = Math.Clamp(Config.Port, 1, 65535);
        Config.Normalize();
        Relay = new RelayLauncher(Path.Combine(PluginInterface.GetPluginConfigDirectory(), "relay"));
        PluginInterface.SavePluginConfig(Config);
        ApplyVisibility();
        questionDismissals = new QuestionDismissals(Config.DismissedQuestions);
        UiFonts.Initialize(PluginInterface.UiBuilder.FontAtlas);
        emojis = new EmojiImages(Textures);
        EmojiText.Resolve = emojis.Resolve;
        UiFonts.Refresh(Config.WindowAppearance!.Text, Config.HudAppearance!.Text, Config.ToastAppearance!.Text);
        History = new NotificationHistory(Config.NotificationHistory);
        NotificationUi = new NotificationOverlay(Config, Save, OnNotificationClick, TaskLink);
        Center = new NotificationCenter(History, NotificationUi.Queue, state => Sounds.Play(state, Config.Sounds));
        Hud = new MiniHud(this, OpenMain);
        client = new BridgeClient(Config.Port);
        main = new MainWindow(this) { IsOpen = false };
        pendingOpen = Config.OpenOnLoad;
        wasLoggedIn = ClientState.IsLoggedIn;
        windows.AddWindow(main);
        dtr = DtrBar.Get("Codex Monitor", Text("Codex …"));
        dtr.Shown = Config.Indicator == IndicatorMode.Text && !Config.Visibility!.IsHidden(CurrentGame());
        dtr.OnClick = _ => ToggleMain();
        Commands.AddHandler("/codex", new CommandInfo(OnCommand) { HelpMessage = "Tâches Codex. config : réglages ; history : historique ; hud : mini HUD ; preview : placement ; test [idle|input|approval|error|question] : exemple." });
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenMainUi += OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        Framework.Update += Update;
        Log.Information("Codex Monitor 0.8.0 loaded; local bridge port {Port}", Config.Port);
    }

    private static SeString Text(string text) => new SeStringBuilder().AddText(text).Build();
    private void Draw()
    {
        var game = CurrentGame();
        var hidden = Config.Visibility!.IsHidden(game);
        ObsidianTheme.Push(ObsidianTheme.Chrome);
        try
        {
            if (!hidden || (manuallyOpened && game.CanOpenManually)) windows.Draw();
            if (!hidden) { Hud.Draw(); NotificationUi.Draw(Center.IsQuiet); }
        }
        finally { ObsidianTheme.Pop(); }
    }
    private void MarkManualOpen() { manuallyOpened = Config.Visibility!.IsHidden(CurrentGame()); pendingOpen = false; }
    private void ToggleMain() { MarkManualOpen(); main.Toggle(); }
    private void OpenMain() { MarkManualOpen(); main.ShowSettings = false; main.ShowHistory = false; main.IsOpen = true; }
    private void OpenConfig() { MarkManualOpen(); main.ShowSettings = true; main.ShowHistory = false; main.IsOpen = true; }
    private void OpenHistory() { MarkManualOpen(); main.ShowSettings = false; main.ShowHistory = true; main.IsOpen = true; }
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
        else ToggleMain();
    }

    internal void Save()
    {
        lock (saveGate)
        {
            Config.Normalize();
            ApplyVisibility();
            fontsDirty = true;
            Config.NotificationHistory = History.Entries.ToList();
            PluginInterface.SavePluginConfig(Config);
            client.SetPort(Config.Port);
            dtr.Shown = Config.Indicator == IndicatorMode.Text && !Config.Visibility!.IsHidden(CurrentGame());
        }
    }

    internal void SetIndicator(IndicatorMode mode)
    {
        Config.Indicator = mode;
        if (mode != IndicatorMode.MiniHud) Hud.SetEditing(false);
        Save();
    }

    internal void DismissQuestions(MonitoredThread task)
    {
        if (!questionDismissals.Dismiss(task)) return;
        Config.DismissedQuestions = questionDismissals.Export();
        NotificationUi.Queue.Reconcile(Snapshot);
        Save();
    }

    internal void RestoreQuestions(string threadId)
    {
        var restored = Snapshot.Threads.FirstOrDefault(row => row.Id == threadId)?.HiddenQuestionIds ?? [];
        if (!questionDismissals.Restore(threadId)) return;
        Config.DismissedQuestions = questionDismissals.Export();
        Center.RestoreQuestions(threadId, restored);
        Save();
    }

    private void Update(IFramework framework)
    {
        if (fontsDirty)
        {
            fontsDirty = false;
            UiFonts.Refresh(Config.WindowAppearance!.Text, Config.HudAppearance!.Text, Config.ToastAppearance!.Text);
        }
        var game = CurrentGame();
        var hidden = Config.Visibility!.IsHidden(game);
        if (!hidden) manuallyOpened = false;
        if (wasLoggedIn && !game.LoggedIn)
        {
            main.IsOpen = false; manuallyOpened = pendingOpen = false;
            Hud.SetEditing(false); NotificationUi.Preview = false;
        }
        wasLoggedIn = game.LoggedIn;
        if (pendingOpen && game.LoggedIn && !hidden) { pendingOpen = false; main.IsOpen = true; }
        dtr.Shown = Config.Indicator == IndicatorMode.Text && !hidden;
        var snapshot = Snapshot;
        var now = DateTimeOffset.UtcNow;
        if (now >= nextEmojiRefresh)
        {
            nextEmojiRefresh = now.AddSeconds(1);
            emojis.Prepare(snapshot.Threads.Select(task => task.Title).Concat(History.Entries.Select(entry => entry.Task.Title))
                .Append("🔔 Notification d’exemple"));
        }
        var outside = Config.Visibility.HideOutsideGame && !game.LoggedIn;
        var requestedQuiet = hidden || (Config.QuietInCombat && game.Combat) || (Config.QuietInCutscene && game.Cutscene);
        var quiet = quietGate.Update(requestedQuiet, now);
        Sounds.SetQuiet(quiet || !Config.Sounds.Enabled || Config.Sounds.Volume <= 0);
        if (outside || suspended) Center.Suspend(snapshot);
        suspended = outside;
        if (!outside && Center.Update(snapshot, quiet, Config.NotifyOnIdle, Config.NotifyOnAttention, Config.NotificationSeconds, now, Config.NotifyOnQuestions)) Save();
        if (ReferenceEquals(previous, snapshot)) return;
        dtr.Text = Text(snapshot.Connected ? $"Codex {snapshot.Active} / {snapshot.Attention}!" : "Codex hors ligne");
        dtr.Tooltip = Text(snapshot.Connected
            ? $"{snapshot.Active} en cours\n{snapshot.Attention} à voir\n{snapshot.Idle} sans activité\nCliquer pour ouvrir"
            : "Relais local déconnecté. Cliquer pour ouvrir.");
        if (snapshot.Connected != previous.Connected)
            Log.Information("Local Codex bridge connected: {Connected}; observed tasks: {Count}", snapshot.Connected, snapshot.Threads.Count(thread => thread.IsObserved));
        previous = snapshot;
    }

    private static GameContext CurrentGame() => new(ClientState.IsLoggedIn,
        Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51],
        Conditions[ConditionFlag.WatchingCutscene] || Conditions[ConditionFlag.WatchingCutscene78] || Conditions[ConditionFlag.OccupiedInCutSceneEvent],
        ClientState.IsGPosing, Conditions[ConditionFlag.InCombat],
        Conditions[ConditionFlag.BoundByDuty] || Conditions[ConditionFlag.BoundByDuty56] || Conditions[ConditionFlag.BoundByDuty95], GameGui.GameUiHidden);

    private void ApplyVisibility()
    {
        PluginInterface.UiBuilder.DisableCutsceneUiHide = !Config.Visibility!.HideInCutscenes;
        PluginInterface.UiBuilder.DisableGposeUiHide = !Config.Visibility.HideInGpose;
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
        Relay.Dispose();
        dtr.Remove();
        windows.RemoveAllWindows();
        client.Dispose();
        EmojiText.Reset();
        emojis.Dispose();
        UiFonts.Dispose();
        Log.Information("Codex Monitor unloaded.");
    }
}
