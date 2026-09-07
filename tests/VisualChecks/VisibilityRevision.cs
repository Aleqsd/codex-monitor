using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe partial class Program
{
    private static void VisibilityPreview(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var (width, height, scale) in new[] { (800, 780, 1f), (560, 600, 1f), (1200, 1170, 1.5f), (1600, 1560, 2f) })
        {
            Initialize(width, height, scale); window.ShowSettings = true; plugin.SetIndicator(IndicatorMode.Hidden);
            foreach (var category in new[] { 5, 3 })
            {
                Panel.Category = category;
                if (category == 3) plugin.Snapshot = MonitorSnapshot.Offline("Le relais local n’est pas encore lancé.");
                for (var n = 0; n < 3; n++) Frame();
                Render(Path.Combine(output, $"{(category == 5 ? "visibility" : "connection")}-{width}.ppm"));
            }
            FinishRevisionView();
        }
        Console.WriteLine("Rendered 8 native ImGui visibility/connection panels at minimum width and 100/150/200%.");
    }

    private static void VisibilityMigration()
    {
        var jsonConvert = System.Reflection.Assembly.LoadFrom(Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME")!, "Newtonsoft.Json.dll")).GetType("Newtonsoft.Json.JsonConvert")!;
        var deserialize = jsonConvert.GetMethod("DeserializeObject", [typeof(string), typeof(Type)])!;
        Configuration Read(string json) { var c = (Configuration)deserialize.Invoke(null, [json, typeof(Configuration)])!; c.Normalize(); return c; }
        var old = Read("{\"OpenOnLoad\":true,\"MiniHudAnchorX\":0.8,\"NotificationSeconds\":11,\"HudAppearance\":{\"Opacity\":0.3}}");
        if (old.OpenOnLoad || !old.Visibility!.HideOutsideGame || !old.Visibility.HideWhileLoading) throw new Exception("Old startup default was not migrated.");
        if (old.MiniHudAnchorX != .8f || old.NotificationSeconds != 11 || old.HudAppearance!.Opacity != .3f) throw new Exception("Migration changed unrelated settings.");
        old.OpenOnLoad = true; old.Visibility.HideOutsideGame = false; old.Visibility.HideInDuty = true;
        old.RelayNodePath = @"C:\Custom Node\node.exe";
        var saved = (string)jsonConvert.GetMethod("SerializeObject", [typeof(object)])!.Invoke(null, [old])!;
        var restored = Read(saved); restored.Normalize();
        if (!restored.OpenOnLoad || restored.Visibility!.HideOutsideGame || !restored.Visibility.HideInDuty || restored.RelayNodePath != old.RelayNodePath)
            throw new Exception("Explicit visibility/startup/Node choices did not survive JSON round trip.");
        var fresh = Configuration.NewInstall();
        if (fresh.OpenOnLoad || fresh.Visibility!.HideInCombat || !fresh.Visibility.HideInGpose) throw new Exception("New defaults are wrong.");
        Initialize(800, 780, 1); window.ShowSettings = true; Panel.Category = 5; plugin.SetIndicator(IndicatorMode.Hidden);
        for (var n = 0; n < 3; n++) Frame();
        Click(26, 375);
        if (!plugin.Config.Visibility!.HideInCombat || plugin.SaveCount < 2) throw new Exception("Native combat visibility toggle did not save.");
        Click(26, 235);
        if (plugin.Config.Visibility.HideOutsideGame) throw new Exception("Native title visibility toggle failed.");
        Click(26, 544);
        if (!plugin.Config.OpenOnLoad) throw new Exception("Native explicit startup option failed.");
        Click(100, 629);
        if (plugin.Config.OpenOnLoad || plugin.Config.Visibility.HideInCombat || !plugin.Config.Visibility.HideOutsideGame) throw new Exception("Native visibility reset failed.");
        FinishRevisionView();
        Console.WriteLine("PASS 4 native visibility interactions and 4 migration/persistence checks using the installed Dalamud JSON serializer.");
    }
}
