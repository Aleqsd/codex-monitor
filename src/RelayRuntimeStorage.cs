namespace CodexMonitor;

internal static class RelayRuntimeStorage
{
    internal const string Marker = ".codex-monitor-stopped";
    private static readonly HashSet<string> Files = new(StringComparer.OrdinalIgnoreCase)
        { Marker, "stop", "status.json", "status.tmp", "events.jsonl", "events.1.jsonl" };
    internal static void MarkStopped(string runtime) => File.WriteAllText(Path.Combine(runtime, Marker), "CodexMonitor stopped v1");
    // Only flat, recognized directories explicitly marked after our child exited are eligible.
    internal static void Prune(string storageRoot, int keep = 3)
    {
        try
        {
            var root = Path.GetFullPath(Path.Combine(storageRoot, "runtime"));
            if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return;
            var candidates = new DirectoryInfo(root).GetDirectories().Where(directory => Guid.TryParseExact(directory.Name, "N", out _)
                && (directory.Attributes & FileAttributes.ReparsePoint) == 0 && File.Exists(Path.Combine(directory.FullName, Marker)))
                .OrderByDescending(directory => directory.LastWriteTimeUtc).Skip(Math.Max(0, keep));
            foreach (var directory in candidates)
            {
                try
                {
                    var target = Path.GetFullPath(directory.FullName);
                    if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                    var files = directory.GetFileSystemInfos();
                    if (files.Any(file => !Files.Contains(file.Name) || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)) continue;
                    var marker = Path.Combine(target, Marker);
                    if (new FileInfo(marker).Length > 100 || File.ReadAllText(marker) != "CodexMonitor stopped v1") continue;
                    foreach (var file in files) File.Delete(file.FullName);
                    Directory.Delete(target, false);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
