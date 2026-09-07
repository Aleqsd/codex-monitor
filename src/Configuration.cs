using Dalamud.Configuration;

namespace CodexMonitor;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int Port { get; set; } = 43187;
    public bool ShowIdle { get; set; } = true;
    public FollowingOptions Following { get; set; } = new();
    public QuotaAlertOptions QuotaAlerts { get; set; } = new();
    public bool GroupNotificationBursts { get; set; } = true;
    public bool ShowUnobserved { get; set; }
    public bool ShowDtr { get; set; } = true;
    public IndicatorMode? Indicator { get; set; }
    public float MiniHudScale { get; set; } = 1;
    public float MiniHudOpacity { get; set; } = 0.94f;
    public HudAppearance? HudAppearance { get; set; }
    public SurfaceAppearance? WindowAppearance { get; set; }
    public SurfaceAppearance? ToastAppearance { get; set; }
    public MiniHudStyle HudStyle { get; set; } = MiniHudStyle.Capsule;
    public string? PinnedHudTaskId { get; set; }
    public bool HudQuickPeek { get; set; } = true;
    public bool ShowQuestionExcerpts { get; set; } = true;
    public bool ShowUsage { get; set; } = true;
    public UsagePreference UsagePeriod { get; set; } = UsagePreference.Weekly;
    public bool AnimateHudChanges { get; set; } = true;
    public SoundOptions Sounds { get; set; } = new();
    public bool NotifyOnIdle { get; set; } = true;
    public bool NotifyOnAttention { get; set; } = true;
    public bool NotifyOnQuestions { get; set; } = true;
    public bool OpenOnLoad { get; set; }
    public VisibilityOptions? Visibility { get; set; }
    public string RelayNodePath { get; set; } = "";
    public bool AutoStartRelay { get; set; }
    public float NotificationSeconds { get; set; } = 7;
    public float NotificationScale { get; set; } = 1;
    public float NotificationAnchorX { get; set; } = 0.5f;
    public float NotificationAnchorY { get; set; } = 0.22f;
    public bool NotificationReducedMotion { get; set; }
    public bool QuietInCombat { get; set; } = true;
    public bool QuietInCutscene { get; set; } = true;
    public List<HistoryEntry> NotificationHistory { get; set; } = [];
    public List<DismissedQuestions> DismissedQuestions { get; set; } = [];
    public bool ShowMiniHud { get; set; } = true;
    public float MiniHudAnchorX { get; set; } = 0.5f;
    public float MiniHudAnchorY { get; set; } = 0.08f;
    public StackDirection NotificationDirection { get; set; }
    public float NotificationOffsetX { get; set; }
    public float NotificationOffsetY { get; set; }
    public bool PreviewGrid { get; set; } = true;
    public bool PreviewSnap { get; set; } = true;
    public int PreviewCount { get; set; } = 3;

    // Only used when no saved config exists; absent fields in older JSON retain their appearance.
    internal static Configuration NewInstall()
    {
        var config = new Configuration { Indicator = IndicatorMode.MiniHud, HudStyle = MiniHudStyle.Focus, MiniHudOpacity = 1 };
        config.Normalize();
        config.WindowAppearance!.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Window);
        config.HudAppearance!.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Hud);
        config.ToastAppearance!.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Notification);
        return config;
    }

    public void Normalize()
    {
        Following ??= new(); Following.Normalize();
        if (!Guid.TryParse(PinnedHudTaskId, out _)) PinnedHudTaskId = null;
        QuotaAlerts ??= new(); QuotaAlerts.Normalize();
        // Previous versions opened the window by default. Apply the new quiet startup once;
        // later explicit opt-ins, appearance, anchors and history survive normalization.
        if (Visibility is null) { Visibility = new VisibilityOptions(); OpenOnLoad = false; }
        RelayNodePath = RelayNodePath?.Trim() ?? "";
        if (!Enum.IsDefined(UsagePeriod)) UsagePeriod = UsagePreference.Weekly;
        DismissedQuestions = QuestionDismissals.Clean(DismissedQuestions);
        Indicator = IndicatorOptions.Resolve(Indicator, ShowDtr, ShowMiniHud);
        if (!Enum.IsDefined(HudStyle)) HudStyle = MiniHudStyle.Capsule;
        MiniHudScale = NotificationGeometry.FiniteClamp(MiniHudScale, 0.75f, 1.5f, 1);
        MiniHudOpacity = NotificationGeometry.FiniteClamp(MiniHudOpacity, 0.35f, 1, 0.94f);
        HudAppearance ??= new HudAppearance { Opacity = MiniHudOpacity };
        HudAppearance.Normalize();
        WindowAppearance ??= SurfaceAppearance.Legacy(AppearanceTarget.Window);
        ToastAppearance ??= SurfaceAppearance.Legacy(AppearanceTarget.Notification);
        WindowAppearance.Normalize(); ToastAppearance.Normalize();
        Sounds ??= new SoundOptions();
        Sounds.Normalize();
        NotificationSeconds = NotificationGeometry.FiniteClamp(NotificationSeconds, 4, 15, 7);
        NotificationScale = NotificationGeometry.FiniteClamp(NotificationScale, 0.75f, 1.5f, 1);
        NotificationAnchorX = NotificationGeometry.FiniteClamp(NotificationAnchorX, 0, 1, 0.5f);
        NotificationAnchorY = NotificationGeometry.FiniteClamp(NotificationAnchorY, 0, 1, 0.22f);
        MiniHudAnchorX = NotificationGeometry.FiniteClamp(MiniHudAnchorX, 0, 1, 0.5f);
        MiniHudAnchorY = NotificationGeometry.FiniteClamp(MiniHudAnchorY, 0, 1, 0.08f);
        NotificationOffsetX = NotificationGeometry.FiniteClamp(NotificationOffsetX, -4096, 4096, 0);
        NotificationOffsetY = NotificationGeometry.FiniteClamp(NotificationOffsetY, -4096, 4096, 0);
        if (!Enum.IsDefined(NotificationDirection)) NotificationDirection = StackDirection.Auto;
        PreviewCount = Math.Clamp(PreviewCount, 1, 3);
    }
}
