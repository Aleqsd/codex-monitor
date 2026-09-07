using System.Numerics;
using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe partial class Program
{
    private const string DemoId = "11111111-2222-4333-8444-555555555555";
    private static readonly Dictionary<string, EmojiRasterizer.Bitmap> EmojiBitmaps = new();
    private static void LoadEmojiTextures()
    {
        var ids = new Dictionary<string, ImTextureID>();
        foreach (var text in new[] { "🔔", "🎨", "🐛", "👩🏽‍💻", "✅", "🚀", "🇫🇷", "1️⃣" })
        {
            if (!EmojiBitmaps.TryGetValue(text, out var bitmap)) EmojiBitmaps[text] = bitmap = EmojiRasterizer.Render(text);
            var id = new ImTextureID((nint)(1000 + ids.Count)); ids[text] = id;
            Textures[id] = new(bitmap.Width, bitmap.Height, bitmap.Rgba);
        }
        EmojiText.Resolve = text => ids.TryGetValue(text, out var id) ? id : null;
    }
    private static void UseEmojiTasks()
    {
        var titles = new[] { "🎨 Améliorer l’accueil", "🐛 Corriger la connexion", "👩🏽‍💻 Préparer une livraison", "Vérifier les notifications 🔔 et leurs titres", "🚀 Un titre très long avec des accents et un emoji composé 👩🏽‍💻 pour vérifier la troncature" };
        plugin.Snapshot = plugin.Snapshot with { Threads = plugin.Snapshot.Threads.Select((row, i) => row with { Title = titles[i], Id = i == 0 ? DemoId : $"22222222-3333-4444-8555-{i:000000000000}" }).ToArray() };
    }
    private static void NavigationPreview(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var (width, height, scale) in new[] { (800, 780, 1f), (560, 600, 1f), (1200, 1170, 1.5f), (1600, 1560, 2f) })
        {
            Initialize(width, height, scale); UseEmojiTasks();
            for (var i = 0; i < 3; i++) Frame();
            Render(Path.Combine(output, $"tasks-{width}.ppm"));
            window.ShowHistory = true; Frame(); Render(Path.Combine(output, $"history-{width}.ppm"));
            window.ShowHistory = false; window.ShowSettings = true; Panel.Category = 1;
            for (var i = 0; i < 3; i++) Frame();
            Render(Path.Combine(output, $"notifications-settings-{width}.ppm"));
            overlayOnly = true; plugin.Config.NotificationReducedMotion = true;
            foreach (var skin in new[] { MonitorSkin.LMeter, MonitorSkin.Obsidienne })
            {
                plugin.Config.ToastAppearance!.ApplyPreset(skin, AppearanceTarget.Notification);
                plugin.NotificationUi.Queue.Clear();
                plugin.NotificationUi.Queue.Add(plugin.Snapshot.Threads[0] with { State = "idle" }, 7);
                plugin.NotificationUi.Queue.Add(plugin.Snapshot.Threads[2] with { State = "question" }, 7);
                for (var i = 0; i < 3; i++) Frame();
                Render(Path.Combine(output, $"notifications-{skin}-{width}.ppm"));
            }
            FinishRevisionView();
        }
        Initialize(680, 470, 1);
        for (var frame = 0; frame < 3; frame++)
        {
            ImGui.NewFrame(); ObsidianTheme.Push(ObsidianTheme.Chrome);
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(new(680, 470));
            ImGui.Begin("Quota", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings);
            ImGui.TextUnformatted("Panneau fin · Quota restant"); ImGui.TextDisabled("Rendu ImGui hors jeu · Données fictives");
            var appearance = new HudAppearance(); appearance.ApplyPreset(MonitorSkin.Obsidienne, AppearanceTarget.Hud);
            var y = 75f;
            foreach (double? percent in new double?[] { 78, 35, 12, null })
            {
                var snapshot = plugin.Snapshot with { Usage = percent is null ? null : new AccountUsage(DateTimeOffset.UtcNow, [new(percent.Value, 10080, null)]) };
                MiniHud.DrawFace(new(25, y), MiniHudOptions.Size(MiniHudStyle.ObsidienneFine, true, appearance) * 1.5f, snapshot, false, false, 1,
                    MiniHudStyle.ObsidienneFine, true, appearance: appearance); y += 96;
            }
            ImGui.End(); ObsidianTheme.Pop(); ImGui.Render();
        }
        Render(Path.Combine(output, "quota-colors.ppm")); FinishRevisionView();
        Console.WriteLine("PASS 21 native previews: emoji titles, opening actions, settings, quota colors; minimum width and 100/150/200%.");
    }
    private static void NavigationSmoke()
    {
        Initialize(800, 780, 1); UseEmojiTasks();
        for (var i = 0; i < 3; i++) Frame();
        Click(743, 220);
        SpinWait.SpinUntil(() => { lock (plugin.OpenedLinks) return plugin.OpenedLinks.Count == 1; }, 1000);
        if (plugin.OpenedLinks.Count != 1 || plugin.OpenedLinks[0].AbsoluteUri != $"codex://threads/{DemoId}") throw new Exception("Task row did not open the correct task.");
        FinishRevisionView();
        Initialize(800, 780, 1); UseEmojiTasks(); overlayOnly = true;
        plugin.Config.NotificationReducedMotion = true;
        plugin.Config.NotificationAnchorX = .5f; plugin.Config.NotificationAnchorY = .1f;
        plugin.NotificationUi.Queue.Add(plugin.Snapshot.Threads[0] with { State = "idle" }, 7);
        for (var i = 0; i < 3; i++) Frame();
        Click(250, 191);
        SpinWait.SpinUntil(() => { lock (plugin.OpenedLinks) return plugin.OpenedLinks.Count == 1; }, 1000);
        if (plugin.OpenedLinks.Count != 1 || plugin.OpenCount != 0 || plugin.NotificationUi.Queue.Count != 1) throw new Exception("Toast action misrouted, clicked body or dismissed notification.");
        FinishRevisionView();
        Initialize(800, 780, 1); overlayOnly = true;
        plugin.Config.NotificationReducedMotion = true; plugin.Config.NotificationAnchorX = .5f; plugin.Config.NotificationAnchorY = .1f;
        plugin.NotificationUi.Queue.Add(new("quiet-summary", "2 réponses prêtes", "Historique", "", "summary"), 7);
        for (var i = 0; i < 3; i++) Frame(); Click(250, 191);
        if (plugin.OpenCount != 1 || plugin.OpenedLinks.Count != 0) throw new Exception("Summary must open history, never a fabricated Codex URI.");
        plugin.NotificationUi.Queue.Clear(); plugin.NotificationUi.SetPreview(true); for (var i = 0; i < 3; i++) Frame(); Click(250, 191);
        if (plugin.OpenCount != 1 || plugin.OpenedLinks.Count != 0) throw new Exception("Preview launched an action.");
        using (ObsidianTheme.Palette(new HudAppearance()))
        {
            if (MiniHud.QuotaColor(null) != ObsidianTheme.Muted || MiniHud.QuotaColor(19.9) != ObsidianTheme.Red || MiniHud.QuotaColor(20) != ObsidianTheme.Amber
                || MiniHud.QuotaColor(50) != ObsidianTheme.Amber || MiniHud.QuotaColor(51) != ObsidianTheme.Green) throw new Exception("Quota threshold colors incorrect.");
        }
        FinishRevisionView(); Console.WriteLine("PASS task link, toast link, retained toast, summary history, inert preview, quota thresholds (native ImGui; OS launch simulated).");
    }
}
