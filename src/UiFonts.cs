using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.FontIdentifier;

namespace CodexMonitor;

// Refresh runs outside Draw. File probing and atlas builds never happen in a render callback.
internal static class UiFonts
{
    private sealed record Entry(IFontHandle Handle, string? Fallback);
    private static readonly Dictionary<(MonitorFont, string, float), Entry> Entries = new();
    private static IFontAtlas? atlas;
    private static string? expressway;
    internal static void Initialize(IFontAtlas fontAtlas)
    {
        atlas = fontAtlas;
        foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Fonts), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts") })
        {
            try { expressway ??= Directory.EnumerateFiles(folder, "*expressway*", SearchOption.TopDirectoryOnly).FirstOrDefault(ValidFile); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static bool ValidFile(string path) => Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal)
        && new[] { ".ttf", ".otf", ".ttc" }.Contains(Path.GetExtension(path).ToLowerInvariant()) && new FileInfo(path) is { Exists: true, Length: > 0 and < 20_000_000 };
    private static (MonitorFont, string, float) Key(TextAppearance text) => (text.Font, text.FontFile, text.Size);
    internal static void Refresh(params TextAppearance[] texts)
    {
        if (atlas is null) return;
        var keys = texts.Select(Key).ToHashSet();
        foreach (var old in Entries.Keys.Where(key => !keys.Contains(key)).ToArray()) { Entries[old].Handle.Dispose(); Entries.Remove(old); }
        foreach (var key in keys)
        {
            if (Entries.ContainsKey(key)) continue;
            var (choice, custom, size) = key;
            string? path = choice switch { MonitorFont.Expressway => expressway, MonitorFont.SegoeUi => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"), MonitorFont.LocalFile => custom, _ => null };
            string? fallback = null;
            try { if (choice != MonitorFont.Dalamud && (path is null || !ValidFile(path))) { fallback = "Police absente · repli Dalamud"; path = null; } }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { fallback = "Police inaccessible · repli Dalamud"; path = null; }
            var localPath = path;
            var handle = atlas.NewDelegateFontHandle(build => build.OnPreBuild(tk =>
            {
                if (localPath is null) { tk.AddDalamudDefaultFont(size); return; }
                var config = new SafeFontConfig { SizePx = size };
                var font = tk.AddFontFromFile(localPath, config);
                // Retain the selected face while supplying missing glyphs from Dalamud's configured default.
                var fallbackFont = Plugin.PluginInterface.UiBuilder.DefaultFontSpec;
                if (fallbackFont is SingleFontSpec single) (single with { SizePx = size }).AddToBuildToolkit(tk, font);
                else fallbackFont.AddToBuildToolkit(tk, font);
                tk.Font = font;
            }));
            Entries[key] = new Entry(handle, fallback);
        }
    }
    internal static IDisposable? Push(TextAppearance? text) => text is not null && Entries.TryGetValue(Key(text), out var entry) && entry.Handle.Available
        ? entry.Handle.Push() : Plugin.PluginInterface.UiBuilder.DefaultFontHandle.Push();
    internal static string Status(TextAppearance text) => Entries.TryGetValue(Key(text), out var entry)
        ? entry.Handle.LoadException is not null ? "Chargement impossible · repli Dalamud" : entry.Fallback ?? (entry.Handle.Available ? "Police prête" : "Chargement de la police…") : "Application au prochain affichage…";
    internal static void Dispose() { foreach (var entry in Entries.Values) entry.Handle.Dispose(); Entries.Clear(); atlas = null; expressway = null; }
}
