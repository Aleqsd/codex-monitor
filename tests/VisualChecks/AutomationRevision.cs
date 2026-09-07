using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe partial class Program
{
    private static void AutomationConfigChecks()
    {
        var serializer = System.Reflection.Assembly.LoadFrom(Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME")!, "Newtonsoft.Json.dll")).GetType("Newtonsoft.Json.JsonConvert")!;
        var read = serializer.GetMethod("DeserializeObject", [typeof(string), typeof(Type)])!;
        var write = serializer.GetMethod("SerializeObject", [typeof(object)])!;
        Configuration Read(string value) { var config = (Configuration)read.Invoke(null, [value, typeof(Configuration)])!; config.Normalize(); return config; }
        var legacy = Read("{\"ShowIdle\":false,\"MiniHudOpacity\":0.62}");
        if (legacy.AutoStartRelay || legacy.UsagePeriod != UsagePreference.Weekly || legacy.ShowIdle || legacy.MiniHudOpacity != .62f) throw new Exception("Legacy defaults changed existing preferences.");
        legacy.AutoStartRelay = true; legacy.UsagePeriod = UsagePreference.Limiting;
        var restored = Read((string)write.Invoke(null, [legacy])!);
        if (!restored.AutoStartRelay || restored.UsagePeriod != UsagePreference.Limiting || restored.MiniHudOpacity != .62f) throw new Exception("New options did not round-trip through Dalamud serializer.");
        if (Read("{\"UsagePeriod\":999}").UsagePeriod != UsagePreference.Weekly) throw new Exception("Invalid saved period not normalized.");
        Console.WriteLine("PASS 3 installed Dalamud serializer checks for opt-in, quota preference and migration.");
    }
    private static Vector2 PauseButtonCenter()
    {
        var hit = plugin.Hud.Hits.Single(h => h.Target == HudTarget.Pause);
        return (hit.Min + hit.Max) / 2;
    }
    private static void AutomationRevision(string output)
    {
        Directory.CreateDirectory(output);
        var checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        foreach (var (width, height, scale) in new[] { (800, 750, 1f), (560, 650, 1f), (840, 975, 1.5f), (1120, 1300, 2f) })
        {
            Initialize(width, height, scale); UseEmojiTasks();
            plugin.Config.ShowIdle = false;
            plugin.Snapshot = plugin.Snapshot with { Threads = [plugin.Snapshot.Threads[0] with { State = "idle", PendingQuestionIds = [new('a',32), new('b',32), new('c',32)] }] };
            typeof(MainWindow).GetField("stateFilter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(window, 2);
            for (var n = 0; n < 3; n++) Frame(); Render(Path.Combine(output, $"attention-{width}.ppm"));
            Click(width - 40 * scale, 280 * scale);
            SpinWait.SpinUntil(() => { lock (plugin.OpenedLinks) return plugin.OpenedLinks.Count == 1; }, 1000);
            Check(plugin.OpenedLinks.Count == 1, "Filtered idle question remains visible and its Open button works");
            window.ShowSettings = true; Panel.Category = 3; plugin.Config.AutoStartRelay = true;
            plugin.Snapshot = plugin.Snapshot with { RelayVersion = "0.9.0", QuotaDiagnostic = new("ready"), UsageTrackingSupported = true,
                Usage = new(DateTimeOffset.UtcNow, [new(78,10080), new(0,300)]) };
            for (var n = 0; n < 3; n++) Frame(); Render(Path.Combine(output, $"connection-{width}.ppm"));
            var io = ImGui.GetIO(); io.AddMousePosEvent(width / 2, height - 65); Frame(); io.AddMouseWheelEvent(0, -20);
            for (var n = 0; n < 3; n++) Frame(); Render(Path.Combine(output, $"quota-{width}.ppm"));
            FinishRevisionView();
        }
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        foreach (var style in Enum.GetValues<MiniHudStyle>())
        {
            Initialize((int)(560 * scale), (int)(350 * scale), scale); overlayOnly = hudOnly = true;
            plugin.Config.Indicator = IndicatorMode.MiniHud; plugin.Config.HudStyle = style;
            plugin.Config.HudAppearance!.ApplyPreset(MonitorSkin.Obsidienne, AppearanceTarget.Hud);
            plugin.Config.MiniHudAnchorY = .1f;
            for (var n = 0; n < 3; n++) Frame();
            var button = PauseButtonCenter();
            Click(button.X, button.Y); Frame();
            Check(plugin.OpenCount == 0 && !plugin.ManualQuiet.Enabled, "Pause icon opens its menu without opening tasks");
            var context = ImGui.GetCurrentContext();
            Check(context.OpenPopupStack.Size == 1, "Pause popup is open");
            var popup = new ImGuiWindowPtr(context.OpenPopupStack[0].Window);
            // The last native menu row is session pause; click its measured window geometry.
            Click(popup.Pos.X + 30 * scale, popup.Pos.Y + popup.Size.Y - 10 * scale - ImGui.GetFontSize() / 2);
            Check(plugin.ManualQuiet.Enabled && plugin.ManualQuiet.Until is null, "Pause menu starts session pause");
            var io = ImGui.GetIO();
            io.AddMousePosEvent(0, 0); for (var n = 0; n < 3; n++) Frame();
            Render(Path.Combine(output, $"pause-{style}-{scale * 100:0}.ppm"));
            Click(button.X, button.Y); Check(!plugin.ManualQuiet.Enabled && plugin.OpenCount == 0, "Pause icon resumes without opening tasks");
            io.AddMousePosEvent(button.X, button.Y); io.AddMouseButtonEvent(1, true); Frame(); io.AddMouseButtonEvent(1, false); Frame();
            Render(Path.Combine(output, $"pause-menu-{style}-{scale * 100:0}.ppm"));
            FinishRevisionView();
        }
        Initialize(800, 650, 1); UseEmojiTasks();
        var results = new List<object>();
        foreach (var count in new[] { 20, 200 })
        {
            plugin.Snapshot = plugin.Snapshot with { Threads = Enumerable.Range(0, count).Select(i =>
                new MonitoredThread($"11111111-2222-4333-8444-{i:000000000000}", $"🎨 {i:000} " + new string('a',1492), "Projet fictif", "", "active")).ToArray() };
            for (var n = 0; n < 40; n++) Frame();
            var times = new List<double>(); var allocation = GC.GetAllocatedBytesForCurrentThread();
            for (var n = 0; n < 120; n++) { var start = Stopwatch.GetTimestamp(); Frame(); times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
            allocation = (GC.GetAllocatedBytesForCurrentThread() - allocation) / 120; times.Sort();
            Check(allocation < 500_000, "Long titles do not cause multi-megabyte allocations each frame");
            results.Add(new { tasks=count, titleLength=1500, medianMs=times[60], p95Ms=times[114], allocatedBytesPerFrame=allocation });
        }
        ImGui.GetIO().AddMousePosEvent(300, 400); Frame(); ImGui.GetIO().AddMouseWheelEvent(0, -1000);
        for (var n = 0; n < 3; n++) Frame(); Render(Path.Combine(output, "long-list-scrolled.ppm"));
        FinishRevisionView();
        File.WriteAllText(Path.Combine(output, "performance.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented=true }));
        Console.WriteLine($"PASS {checks} automation UI checks, 49 native previews and long-title render measurements. OS launches simulated.");
    }
}
