using System.Numerics;
using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe partial class Program
{
    private static SettingsPanel Panel => (SettingsPanel)typeof(MainWindow).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
    private static void FinishRevisionView() { ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose(); SizedFonts.Clear(); }
    private static void RevisionPreview(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var (width, height, scale) in new[] { (800, 900, 1f), (560, 600, 1f), (1200, 1100, 1.5f), (1600, 1400, 2f) })
        {
            Initialize(width, height, scale);
            for (var n=0; n<3; n++) Frame();
            Render(Path.Combine(output, $"tasks-{width}.ppm"));
            Click(100 * scale, 205 * scale);
            Render(Path.Combine(output, $"question-menu-{width}.ppm"));
            Click(width - 20, height - 20);
            window.ShowSettings = true; Panel.Category = 1; Panel.NotificationDesign = true;
            plugin.Config.ToastAppearance!.ApplyPreset(MonitorSkin.Nuit, AppearanceTarget.Notification);
            plugin.Config.ToastAppearance.ToastCornerRadius = 18;
            for (var n=0; n<3; n++) Frame();
            Render(Path.Combine(output, $"notification-design-{width}.ppm"));
            var io = ImGui.GetIO(); io.AddMousePosEvent(width / 2, height - 60); Frame(); io.AddMouseWheelEvent(0, -25);
            for (var n=0; n<3; n++) Frame();
            Render(Path.Combine(output, $"notification-design-bottom-{width}.ppm"));
            window.ShowSettings = false; overlayOnly = true;
            foreach (var (name, skin, alpha, icon, timer) in new[] { ("obsidienne", MonitorSkin.Obsidienne, .94f, true, true), ("nuit", MonitorSkin.Nuit, .7f, true, true), ("minimal", MonitorSkin.LMeter, .5f, false, false), ("transparent", MonitorSkin.Obsidienne, 0f, false, true) })
            {
                var appearance = plugin.Config.ToastAppearance!; appearance.ApplyPreset(skin, AppearanceTarget.Notification);
                appearance.Opacity = alpha; appearance.ToastShowIcon = icon; appearance.ToastShowTimer = timer; appearance.ToastCornerRadius = 18;
                plugin.Config.NotificationReducedMotion = true;
                plugin.NotificationUi.Queue.Clear(); plugin.NotificationUi.Queue.Add(NotificationOverlay.Example("question"), 7);
                Frame(); Render(Path.Combine(output, $"toast-{name}-{width}.ppm"));
            }
            FinishRevisionView();
        }
        Console.WriteLine("Rendered 32 real ImGui revision views at 100/150/200% and minimum width.");
    }

    private static void RevisionSmoke()
    {
        const string output = "artifacts/revision-smoke"; Directory.CreateDirectory(output);
        Initialize(800, 924, 1);
        for (var n=0; n<3; n++) Frame();
        Click(100,205); Render(Path.Combine(output,"menu.ppm"));
        Click(170,302);
        if (plugin.Snapshot.Attention != 1 || plugin.Config.DismissedQuestions.Count != 1) throw new Exception("Native dismissal click did not clear the question.");
        Click(100,263); Click(175,360);
        if (plugin.Snapshot.Attention != 2 || plugin.Config.DismissedQuestions.Count != 0) throw new Exception("Native restore click did not restore the question.");
        Click(195,87); Click(130,134); Click(180,172);
        if (!window.ShowSettings || Panel.Category != 1 || !Panel.NotificationDesign) throw new Exception("Direct notification design navigation failed.");
        Click(232,274);
        if (plugin.Config.ToastAppearance!.Skin != MonitorSkin.Nuit || plugin.Config.WindowAppearance!.Skin != MonitorSkin.Obsidienne) throw new Exception("Notification theme changed the data appearance.");
        Click(210,534); Click(25,594); Click(440,654); Click(25,689); Click(25,724);
        if (plugin.Config.ToastAppearance.Opacity > .35f || plugin.Config.ToastAppearance.Border || plugin.Config.ToastAppearance.ToastCornerRadius is not (> 12 and < 16) || plugin.Config.ToastAppearance.ToastShowIcon || plugin.Config.ToastAppearance.ToastShowTimer) throw new Exception("Notification design controls failed.");
        Click(150,819); Click(60,709); Click(755,901);
        if (plugin.Config.ToastAppearance.Text.Font != MonitorFont.Dalamud || plugin.Config.ToastAppearance.Text.Size < 23) { Render(Path.Combine(output,"text-control-failure.ppm")); throw new Exception($"Notification text controls failed: {plugin.Config.ToastAppearance.Text.Font}, {plugin.Config.ToastAppearance.Text.Size}."); }
        Render(Path.Combine(output,"design-custom.ppm"));
        window.ShowSettings = true; Panel.Category = 3; Click(780,880);
        for (var n=0; n<3; n++) Frame();
        Render(Path.Combine(output,"settings-before.ppm"));
        var old = plugin.Config.WindowAppearance!;
        old.ApplyPreset(MonitorSkin.Obsidienne, AppearanceTarget.Window);
        old.Red = 1; old.Opacity = 0; old.Text.Size = 24; old.Text.Font = MonitorFont.LocalFile; old.Text.FontFile = "local.ttf";
        old.Text.Red = 0; old.Text.Green = 1; old.Text.OffsetX = 20; old.PaddingX = 24;
        plugin.Config.HudAppearance!.ApplyPreset(MonitorSkin.Nuit, AppearanceTarget.Hud);
        plugin.Config.ToastAppearance!.ApplyPreset(MonitorSkin.Obsidienne, AppearanceTarget.Notification);
        plugin.Config.ToastAppearance.ToastCornerRadius = 23; plugin.Config.ToastAppearance.ToastShowIcon = false; plugin.Config.ToastAppearance.ToastShowTimer = false;
        for (var n=0; n<3; n++) Frame();
        Render(Path.Combine(output,"settings-after.ppm"));
        if (!File.ReadAllBytes(Path.Combine(output,"settings-before.ppm")).SequenceEqual(File.ReadAllBytes(Path.Combine(output,"settings-after.ppm")))) throw new Exception("Settings changed with a data/HUD/toast appearance choice.");
        var serializer = System.Reflection.Assembly.LoadFrom(Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME")!, "Newtonsoft.Json.dll")).GetType("Newtonsoft.Json.JsonConvert")!;
        var deserialize = serializer.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!;
        plugin.DismissQuestions(plugin.Snapshot.Threads[0]);
        var json = (string)serializer.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new object[] { plugin.Config })!;
        var restored = (Configuration)deserialize.Invoke(null, new object[] { json, typeof(Configuration) })!; restored.Normalize();
        if (restored.DismissedQuestions.Count != 1 || restored.ToastAppearance!.ToastShowIcon || restored.ToastAppearance.ToastShowTimer || restored.ToastAppearance.ToastCornerRadius != 23 || restored.WindowAppearance!.Text.Size != 24) throw new Exception("Saved choices did not survive installed Dalamud serializer.");
        var legacy = (Configuration)deserialize.Invoke(null, new object[] { "{\"Port\":43187,\"NotificationAnchorX\":0.8,\"WindowAppearance\":{\"Opacity\":0.2,\"Text\":{\"Size\":24}}}", typeof(Configuration) })!; legacy.Normalize();
        if (legacy.DismissedQuestions.Count != 0 || !legacy.ToastAppearance!.ToastShowIcon || !legacy.ToastAppearance.ToastShowTimer || legacy.ToastAppearance.ToastCornerRadius != null || legacy.NotificationAnchorX != .8f) throw new Exception("Legacy migration changed useful settings.");
        FinishRevisionView();
        Console.WriteLine("PASS 13 native interactions, fixed settings pixel equality and installed Dalamud JSON migration/persistence.");
    }
}
