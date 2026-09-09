namespace CodexMonitor;

// Local reads only, called when preparing fonts, never while drawing a HUD.
internal static class LocalFontFiles
{
    internal static bool Valid(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal)
                && new[] { ".ttf", ".otf", ".ttc" }.Contains(Path.GetExtension(path).ToLowerInvariant())
                && new FileInfo(path) is { Exists: true, Length: > 0 and < 20_000_000 };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }

    internal static string? FindExpressway(string windowsFonts, string userFonts, string pluginConfigs)
    {
        foreach (var folder in new[] { windowsFonts, userFonts })
        {
            try
            {
                var font = Directory.EnumerateFiles(folder, "*expressway*", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault(Valid);
                if (font is not null) return font;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        var local = Path.Combine(pluginConfigs, "LMeter", "Fonts", "Expressway.ttf");
        return Valid(local) ? local : null;
    }
}
