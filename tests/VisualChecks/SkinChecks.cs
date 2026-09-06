using System.Numerics;
using CodexMonitor;

internal static class SkinChecks
{
    internal static void Run()
    {
        var checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        var jsonConvert = System.Reflection.Assembly.LoadFrom(Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME")!, "Newtonsoft.Json.dll")).GetType("Newtonsoft.Json.JsonConvert")!;
        var deserialize = jsonConvert.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!;
        Configuration Read(string json) { var config = (Configuration)deserialize.Invoke(null, [json, typeof(Configuration)])!; config.Normalize(); return config; }
        var old = Read("{\"MiniHudOpacity\":0.6,\"MiniHudAnchorX\":0.8,\"NotificationSeconds\":11,\"HudAppearance\":{\"Red\":0.4,\"Green\":0.2,\"Blue\":0.3,\"Opacity\":0.17,\"Border\":false,\"Background\":2}}");
        Check(old.HudAppearance!.Red == .4f && old.HudAppearance.Opacity == .17f && !old.HudAppearance.Border, "Saved 0.5.2 colors and opacity were lost.");
        Check(old.WindowAppearance!.Skin == MonitorSkin.Obsidienne && old.ToastAppearance!.Skin == MonitorSkin.Obsidienne, "Old appearance changed silently.");
        Check(old.HudAppearance.Background == HudBackgroundMode.Hidden && old.MiniHudOpacity == .6f, "Old visibility or foreground opacity changed.");
        var fresh = Configuration.NewInstall();
        Check(fresh.WindowAppearance!.Skin == MonitorSkin.LMeter && fresh.HudAppearance!.Skin == MonitorSkin.LMeter && fresh.ToastAppearance!.Skin == MonitorSkin.LMeter, "Fresh defaults are not LMeter.");
        Check(fresh.Indicator == IndicatorMode.MiniHud && fresh.MiniHudOpacity == 1, "Fresh HUD default is not enabled and readable.");
        old.HudAppearance.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Hud);
        Check(old.MiniHudAnchorX == .8f && old.NotificationSeconds == 11 && old.MiniHudOpacity == .6f, "Preset changed non-appearance preferences.");
        Check(old.WindowAppearance.Skin == MonitorSkin.Obsidienne, "HUD preset changed another component.");
        old.HudAppearance.Text.Font = MonitorFont.LocalFile; old.HudAppearance.Text.FontFile = @"C:\Fonts\example.ttf";
        old.HudAppearance.Text.Size = 24; old.HudAppearance.Text.OffsetX = -20; old.HudAppearance.Text.OffsetY = 12; old.HudAppearance.Opacity = 0;
        var json = (string)jsonConvert.GetMethod("SerializeObject", [typeof(object)])!.Invoke(null, [old])!;
        var restored = Read(json);
        Check(restored.HudAppearance!.Text.FontFile == old.HudAppearance.Text.FontFile && restored.HudAppearance.Text.Size == 24, "Font choices did not round-trip.");
        Check(restored.HudAppearance.Opacity == 0 && restored.HudAppearance.Text.OffsetX == -20 && restored.MiniHudAnchorX == .8f, "Transparency or internal position did not round-trip.");
        foreach (var style in Enum.GetValues<MiniHudStyle>())
        {
            var baseSize = MiniHudOptions.Size(style, true); var size = MiniHudOptions.Size(style, true, restored.HudAppearance);
            Check(size.X >= baseSize.X && size.Y > baseSize.Y && float.IsFinite(size.X), "Text size and offsets did not reserve HUD space.");
        }
        var invalid = new SurfaceAppearance { Skin = (MonitorSkin)99, Opacity = float.NaN, PaddingX = float.PositiveInfinity,
            Text = new TextAppearance { Size = float.NaN, OffsetY = 999, Red = -2, Font = (MonitorFont)99 } };
        invalid.Normalize();
        Check(invalid.Skin == MonitorSkin.Obsidienne && float.IsFinite(invalid.Opacity) && invalid.PaddingX == 0, "Invalid appearance did not recover.");
        Check(invalid.Text.Size == 14 && invalid.Text.OffsetY == 12 && invalid.Text.Red == 0 && invalid.Text.Font == MonitorFont.Dalamud, "Invalid text did not recover.");
        var legacyOpacity = Read("{\"MiniHudOpacity\":0.45}");
        Check(legacyOpacity.HudAppearance!.Opacity == .45f && legacyOpacity.MiniHudOpacity == .45f, "Pre-0.5.2 opacity migration regressed.");
        Console.WriteLine($"PASS {checks} appearance migration and geometry checks with Dalamud's installed JSON serializer.");
    }
}
