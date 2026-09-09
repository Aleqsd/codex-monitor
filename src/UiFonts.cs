using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.FontIdentifier;

namespace CodexMonitor;

// Refresh runs outside Draw. File probing and atlas builds never happen in a render callback.
internal static class UiFonts
{
    private sealed record Entry(IFontHandle Handle, string? Fallback);
    private static readonly Dictionary<(MonitorFont, string, float, bool), Entry> Entries = new();
    private static IFontAtlas? atlas;
    private static string? expressway;
    internal static void Initialize(IFontAtlas fontAtlas, string pluginConfigs)
    {
        atlas = fontAtlas;
        expressway = LocalFontFiles.FindExpressway(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts"), pluginConfigs);
    }
    private static (MonitorFont, string, float, bool) Key(TextAppearance text) => (text.Font, text.FontFile, text.Size, false);
    internal static void Refresh(TextAppearance window, TextAppearance hud, TextAppearance toast, float hudScale)
    {
        if (atlas is null) return;
        var keys = new[] { window, hud, toast }.Select(Key).ToHashSet();
        var scale = hudScale * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale * hud.Size / 14;
        foreach (var role in HudTypography.RoleSizes) keys.Add((hud.Font, hud.FontFile, HudTypography.Pixels(role, scale), true));
        foreach (var old in Entries.Keys.Where(key => !keys.Contains(key)).ToArray()) { Entries[old].Handle.Dispose(); Entries.Remove(old); }
        foreach (var key in keys)
        {
            if (Entries.ContainsKey(key)) continue;
            var (choice, custom, size, pixels) = key;
            string? path = choice switch { MonitorFont.Expressway => expressway, MonitorFont.SegoeUi => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"), MonitorFont.LocalFile => custom, _ => null };
            string? fallback = null;
            try { if (choice != MonitorFont.Dalamud && (path is null || !LocalFontFiles.Valid(path))) { fallback = "Police absente · repli Dalamud"; path = null; } }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { fallback = "Police inaccessible · repli Dalamud"; path = null; }
            var localPath = path;
            var handle = atlas.NewDelegateFontHandle(build => build.OnPreBuild(tk =>
            {
                if (localPath is null)
                {
                    var defaultFont = tk.AddDalamudDefaultFont(size);
                    tk.Font = defaultFont;
                    if (pixels) tk.SetFontScaleMode(defaultFont, FontScaleMode.SkipHandling);
                    return;
                }
                var config = new SafeFontConfig { SizePx = size, PixelSnapH = pixels, OversampleH = pixels ? 1 : 2, OversampleV = 1 };
                var font = tk.AddFontFromFile(localPath, config);
                // Retain the selected face while supplying missing glyphs from Dalamud's configured default.
                var fallbackFont = Plugin.PluginInterface.UiBuilder.DefaultFontSpec;
                if (fallbackFont is SingleFontSpec single) (single with { SizePx = size }).AddToBuildToolkit(tk, font);
                else fallbackFont.AddToBuildToolkit(tk, font);
                tk.Font = font;
                if (pixels) tk.SetFontScaleMode(font, FontScaleMode.SkipHandling);
            }));
            Entries[key] = new Entry(handle, fallback);
        }
    }
    internal static IDisposable? Push(TextAppearance? text) => text is not null && Entries.TryGetValue(Key(text), out var entry) && entry.Handle.Available
        ? entry.Handle.Push() : Plugin.PluginInterface.UiBuilder.DefaultFontHandle.Push();
    internal static IDisposable? PushHud(TextAppearance? text, float pixels) => text is not null
        && Entries.TryGetValue((text.Font, text.FontFile, pixels, true), out var entry) && entry.Handle.Available
        ? entry.Handle.Push() : Push(text);
    internal static string Status(TextAppearance text) => Entries.TryGetValue(Key(text), out var entry)
        ? entry.Handle.LoadException is not null ? "Chargement impossible · repli Dalamud" : entry.Fallback ?? (entry.Handle.Available ? text.Font == MonitorFont.Expressway ? "Expressway locale prête" : "Police prête" : "Chargement de la police…") : "Application au prochain affichage…";
    internal static void Dispose() { foreach (var entry in Entries.Values) entry.Handle.Dispose(); Entries.Clear(); atlas = null; expressway = null; }
}
