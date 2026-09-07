using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace CodexMonitor;

// Prepare is called from Framework.Update, never from Draw. Only one raster/upload worker runs.
internal sealed class EmojiImages(ITextureProvider textures) : IDisposable
{
    private sealed class Entry { internal IDalamudTextureWrap? Texture; internal readonly RetrySchedule Retry = new(); }
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Queue<(string Text, Entry Entry)> pending = new();
    private readonly CancellationTokenSource shutdown = new();
    private bool running, disposed;
    internal string Status
    {
        get
        {
            lock (gate)
            {
                var failed = entries.Values.Count(entry => entry.Texture is null && entry.Retry.Failures >= 3);
                return failed > 0 ? $"Emojis : {failed} indisponibles · symbole de remplacement utilisé" : "Emojis Windows · préparation automatique, jusqu’à 3 tentatives";
            }
        }
    }
    internal ImTextureID? Resolve(string text)
    {
        lock (gate) return entries.TryGetValue(text, out var entry) ? entry.Texture?.Handle : null;
    }
    internal void Prepare(IEnumerable<string> titles)
    {
        var wanted = titles.SelectMany(UnicodeText.Runs).Where(run => run.Emoji).Select(run => run.Text).Distinct().Take(256).ToHashSet(StringComparer.Ordinal);
        lock (gate)
        {
            if (disposed) return;
            // Drop requests whose titles disappeared before the worker could render them.
            var retained = pending.Where(item => wanted.Contains(item.Text)).ToArray(); pending.Clear();
            foreach (var item in retained) pending.Enqueue(item);
            foreach (var key in entries.Keys.Where(key => !wanted.Contains(key)).ToArray())
            { entries[key].Texture?.Dispose(); entries.Remove(key); }
            foreach (var text in wanted)
            {
                if (entries.TryGetValue(text, out var existing))
                {
                    if (existing.Texture is null && existing.Retry.CanRetry(DateTimeOffset.UtcNow))
                    { existing.Retry.Begin(); pending.Enqueue((text, existing)); }
                    continue;
                }
                var entry = new Entry(); entries.Add(text, entry); pending.Enqueue((text, entry));
            }
            if (running || pending.Count == 0) return;
            running = true; _ = Task.Run(Load);
        }
    }
    private async Task Load()
    {
        while (true)
        {
            (string Text, Entry Entry) item;
            lock (gate)
            {
                if (disposed || pending.Count == 0) { running = false; if (disposed) shutdown.Dispose(); return; }
                item = pending.Dequeue();
                if (!entries.TryGetValue(item.Text, out var current) || current != item.Entry) continue;
            }
            IDalamudTextureWrap? texture = null;
            try
            {
                var bitmap = EmojiRasterizer.Render(item.Text);
                texture = await textures.CreateFromRawAsync(RawImageSpecification.Rgba32(bitmap.Width, bitmap.Height), bitmap.Rgba,
                    "Codex Monitor emoji", shutdown.Token).ConfigureAwait(false);
                lock (gate)
                {
                    if (!disposed && entries.TryGetValue(item.Text, out var current) && current == item.Entry)
                    { current.Texture = texture; texture = null; }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { lock (gate) item.Entry.Retry.Fail(DateTimeOffset.UtcNow); }
            finally { texture?.Dispose(); }
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; shutdown.Cancel(); pending.Clear();
            foreach (var entry in entries.Values) entry.Texture?.Dispose(); entries.Clear();
            if (!running) shutdown.Dispose();
        }
    }
}
