namespace CodexMonitor;

internal static class PluginVersion
{
    internal static string Current => typeof(PluginVersion).Assembly.GetName().Version?.ToString(3) ?? "inconnue";
}
