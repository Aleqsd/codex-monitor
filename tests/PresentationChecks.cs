using CodexMonitor;

internal static class PresentationChecks
{
    internal static async Task<int> Run()
    {
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        const string task = "11111111-2222-4333-8444-555555555555";
        Check(CodexTaskLink.Build(task)?.AbsoluteUri == $"codex://threads/{task}", "Exact app task route");
        foreach (var id in new string?[] { null, "", "preview", "quiet-summary", Guid.Empty.ToString(), task + "?evil=x", "https://example.com", "../" + task, task.Replace("-", ""), "codex://threads/" + task })
            Check(CodexTaskLink.Build(id) is null, "Non-task IDs cannot launch a URI");
        var opened = new List<Uri>(); using var release = new ManualResetEventSlim();
        var opener = new CodexTaskLink(uri => { release.Wait(TimeSpan.FromSeconds(3)); opened.Add(uri); });
        var first = opener.Open(task); var second = opener.Open(task);
        Check(opener.Busy, "Opening runs outside the caller's render thread");
        release.Set(); await Task.WhenAll(first, second);
        await opener.Open(task);
        Check(opened.Count == 1 && opener.Error is null && !opener.Busy, "Double click and immediate repeated click launch once");
        var failed = new CodexTaskLink(_ => throw new InvalidOperationException()); await failed.Open(task);
        Check(failed.Error is not null && !failed.Busy, "Windows launch error is visible and releases busy state");
        foreach (var emoji in new[] { "🔔", "👩🏽‍💻", "🇫🇷", "1️⃣", "✅" })
        {
            var runs = UnicodeText.Runs("Avant " + emoji + " après");
            Check(runs.Length == 3 && runs[1].Text == emoji && runs[1].Emoji, "Emoji sequence stays one inline image");
            Check(UnicodeText.Truncate("A" + emoji + " suite", emoji.Length) == "A…", "Length limit cannot split an emoji sequence");
            Check(UnicodeText.Truncate("A" + emoji + " suite", emoji.Length + 1) == "A" + emoji + "…", "Complete emoji fits at an exact boundary");
        }
        Check(UnicodeText.Truncate("e\u0301long", 1) == "…", "Combining accent stays attached to its letter");
        Check(!UnicodeText.Runs("☀\uFE0E")[0].Emoji, "Explicit text presentation remains text");
        Check(UnicodeText.Truncate("Très bien", 140) == "Très bien", "Short accented title unchanged");
        return count;
    }
}
