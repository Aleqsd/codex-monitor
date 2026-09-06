using System.Net;
using System.Numerics;
using System.Text.Json;
using CodexMonitor;

var id = Guid.NewGuid().ToString();
var now = DateTimeOffset.UtcNow;
if (args.Contains("--quiet-return-checks")) { Console.WriteLine($"{QuietReturnChecks.Run()} quiet-return checks passed."); return; }
if (args.Contains("--hud-motion-checks")) { Console.WriteLine($"{HudMotionChecks.Run()} HUD motion checks passed."); return; }
var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine($"PASS {message}"); }
string Payload(string state = "active", bool connected = true, int version = 1, int generatedAge = 0, int confirmedAge = 0) => JsonSerializer.Serialize(new
{
    schemaVersion = version, generatedAt = now.AddSeconds(-generatedAge), connected,
    threads = new[] { new { id, title = "Une tâche\ntrès longue", project = "C:\\Work\\Meet", model = "gpt-6-astra",
        state, availability = "live", lastConfirmedAt = now.AddSeconds(-confirmedAge) } },
});
var live = MonitorContract.Parse(Payload(), now);
Check(live.Connected && live.Active == 1 && live.Threads[0].Project == "Meet", "Live bridge contract and project name");
Check(live.Threads[0].Title == "Une tâche très longue", "Titles render as plain single-line text");
Check(MonitorContract.Parse(Payload(connected: false), now).Threads.Count == 0, "Disconnected bridge clears all task states");
Check(MonitorContract.Parse(Payload(confirmedAge: 40), now).Threads[0].State == "unobserved", "Stale owner state is never shown as active");
try { MonitorContract.Parse(Payload(version: 2), now); throw new Exception("Schema check failed"); } catch (InvalidDataException) { checks++; Console.WriteLine("PASS Unsupported schema rejected"); }
try { MonitorContract.Parse(Payload(generatedAge: 20), now); throw new Exception("Age check failed"); } catch (InvalidDataException) { checks++; Console.WriteLine("PASS Stale entire response rejected"); }
var idle = MonitorContract.Parse(Payload("idle"), now);
Check(TransitionDetector.Find(live, idle, true, true).Count == 1, "Active-to-idle emits one completion");
Check(TransitionDetector.Find(idle, idle, true, true).Count == 0, "Repeated polling does not duplicate alerts");
Check(TransitionDetector.Find(MonitorSnapshot.Offline("offline"), idle, true, true).Count == 0, "Reconnect never announces initial idle tasks as completed");
Check(TransitionDetector.Find(live, MonitorContract.Parse(Payload("needsInput"), now), true, true).Count == 1, "Request for input emits an attention notification");
Check(TransitionDetector.Find(live, idle, false, false).Count == 0, "Disabled notifications remain disabled");

var handler = new ScriptedHandler(() => Payload());
using (var client = new BridgeClient(43187, handler))
{
    await Until(() => client.Current.Connected);
    Check(client.Current.Active == 1, "Background HTTP client consumes a response");
    handler.Fail = true;
    await Until(() => !client.Current.Connected);
    Check(client.Current.Threads.Count == 0, "HTTP failure clears previous active states");
    handler.Fail = false;
    await Until(() => client.Current.Connected);
    Check(client.Current.Active == 1, "HTTP client recovers automatically");
}
using (var http = new HttpClient())
{
    var response = await http.GetStringAsync("http://127.0.0.1:43187/api/threads");
    var snapshot = MonitorContract.Parse(response, DateTimeOffset.UtcNow);
    Check(snapshot.Connected && snapshot.Threads.Any(thread => thread.IsObserved), "Actual running relay accepted by the plugin's parser");
    Console.WriteLine($"LIVE {snapshot.Active} active, {snapshot.Attention} attention, {snapshot.Idle} idle");
}
var queue = new NotificationQueue();
MonitoredThread TaskItem(int n, string state = "idle") => new($"task-{n}", $"Task {n}", "Project", "", state);
for (var n = 0; n < 4; n++) queue.Add(TaskItem(n), 4);
queue.Advance(3, 3);
var paused = queue.Visible()[0];
queue.Advance(1, 3, paused.Id);
Check(queue.Count == 2 && queue.Visible()[0].Age == 3 && queue.Visible()[1].Age == 0,
    "Hovered notification pauses; waiting notification starts with its full duration");
queue.Add(TaskItem(0, "needsInput"), 7);
Check(queue.Count == 2 && queue.Visible()[0].Task.State == "needsInput" && queue.Visible()[0].Age == 0,
    "A newer state replaces the same task notification and restarts its timer");
queue.Dismiss(queue.Visible()[0].Id);
Check(queue.Count == 1 && queue.Visible()[0].Task.Id == "task-3", "Dismiss preserves other notifications");
queue.Clear();
for (var n = 0; n < 25; n++) queue.Add(TaskItem(n), float.NaN);
Check(queue.Count == NotificationQueue.Capacity && queue.Visible().Select(item => item.Task.Id).SequenceEqual(new[] { "task-0", "task-1", "task-2" }),
    "Burst overflow stays bounded and preserves visible notifications");
Check(queue.Visible().All(item => item.Duration == 7), "Invalid saved durations fall back to a finite timer");
queue.Advance(float.NaN, 3);
Check(queue.Visible().All(item => item.Age == 0), "Invalid frame time cannot corrupt notification timers");
queue.Clear();
Check(queue.Count == 0, "Disconnect clearing removes visible and queued notifications");

var origin = new Vector2(1920, 100);
var viewport = new Vector2(1920, 1080);
var size = new Vector2(440, 112);
var anchor = new Vector2(0.5f, 0.22f);
var position = NotificationGeometry.Place(anchor, origin, viewport, size, 0, 1, 10);
Check(Vector2.Distance(position, origin + new Vector2(740, 237.6f)) < 0.01f,
    "Normalized anchor uses viewport origin and notification center");
var dragged = NotificationGeometry.AnchorAfterDrag(position, size, new Vector2(96, 108), origin, viewport);
var placed = NotificationGeometry.Place(dragged, origin, viewport, size, 0, 1, 10);
Check(Vector2.Distance(placed, position + new Vector2(96, 108)) < 0.01f, "Dragging round-trips without an anchor jump");
var bottom = Enumerable.Range(0, 3).Select(index => NotificationGeometry.Place(new Vector2(1, 1), origin, viewport, size, index, 3, 10)).ToArray();
Check(bottom[0].Y > bottom[1].Y && bottom[1].Y > bottom[2].Y && bottom.All(p => p.X >= origin.X + 12 && p.X + size.X <= origin.X + viewport.X - 12 && p.Y >= origin.Y + 12 && p.Y + size.Y <= origin.Y + viewport.Y - 12),
    "Bottom-right anchor stacks upwards and keeps every toast inside the viewport");
var invalid = NotificationGeometry.Place(new Vector2(float.NaN, float.PositiveInfinity), origin, viewport, size, 0, 1, 10);
Check(Vector2.Distance(invalid, position) < 0.01f, "Invalid saved anchors recover to a visible default");
var resized = NotificationGeometry.Place(anchor, Vector2.Zero, new Vector2(1280, 720), size, 0, 1, 10);
Check(Math.Abs((resized.X + size.X / 2) / 1280 - anchor.X) < 0.001f && Math.Abs(resized.Y / 720 - anchor.Y) < 0.001f,
    "Anchor keeps its relative position after a resolution change");
var history = new NotificationHistory();
var alerts = new NotificationQueue();
var center = new NotificationCenter(history, alerts);
MonitorSnapshot SnapshotOf(params MonitoredThread[] tasks) => new(true, now, tasks, null);
bool Tick(MonitorSnapshot snapshot, bool quiet = false, bool notifyIdle = true, bool notifyAttention = true) =>
    center.Update(snapshot, quiet, notifyIdle, notifyAttention, 7, now);
var initial = SnapshotOf(TaskItem(0, "active"), TaskItem(1, "active"), TaskItem(2, "active"));
Check(!Tick(initial) && history.Entries.Count == 0, "Initial subscription does not create history or notifications");
var duringCombat = SnapshotOf(TaskItem(0), TaskItem(1, "needsInput"), TaskItem(2, "needsApproval"));
Check(Tick(duringCombat, true) && center.DeferredCount == 3 && history.Entries.Count == 3 && alerts.Count == 0,
    "Combat records and defers completion, input and approval without showing popups");
Check(!Tick(duringCombat, true) && history.Entries.Count == 3 && center.DeferredCount == 3, "Repeated quiet frames do not duplicate history");
var resolvedDuringCombat = SnapshotOf(TaskItem(0), TaskItem(1, "active"), TaskItem(2, "needsApproval"));
Tick(resolvedDuringCombat, true);
var digest = NotificationCenter.Summarize(history.Entries, resolvedDuringCombat);
Check(digest.Completed == 1 && digest.Input == 0 && digest.Approval == 1 && digest.Errors == 0,
    "Combat digest counts completed turns and only interventions still pending");
Tick(resolvedDuringCombat);
Check(alerts.Count == 1 && alerts.Visible()[0].Task.State == "summary" && center.DeferredCount == 0,
    "Quiet exit on the same snapshot emits one summary, even without a new relay poll");
Tick(resolvedDuringCombat);
Check(alerts.Count == 1 && history.Entries.Count == 3, "Summary is emitted once and never duplicates history");
var inputEntry = history.Entries.Single(entry => entry.Task.State == "needsInput");
var approvalEntry = history.Entries.Single(entry => entry.Task.State == "needsApproval");
Check(history.CurrentLabel(inputEntry, resolvedDuringCombat) == "Intervention terminée"
    && history.CurrentLabel(approvalEntry, resolvedDuringCombat) == "Intervention en cours",
    "History distinguishes a resolved intervention from a current approval");


var savedHistory = JsonSerializer.Serialize(history.Entries);
var restored = new NotificationHistory(JsonSerializer.Deserialize<List<HistoryEntry>>(savedHistory));
Check(restored.Entries.Count == 3 && restored.Entries[0].At == now,
    "History round-trip preserves timestamps and tasks");
var serializerPath = Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME") ?? "", "Newtonsoft.Json.dll");
if (File.Exists(serializerPath))
{
    var serializer = System.Reflection.Assembly.LoadFrom(serializerPath).GetType("Newtonsoft.Json.JsonConvert")!;
    var json = (string)serializer.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new object[] { history.Entries })!;
    var restoredByDalamudSerializer = (List<HistoryEntry>)serializer.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!
        .Invoke(null, new object[] { json, typeof(List<HistoryEntry>) })!;
    Check(new NotificationHistory(restoredByDalamudSerializer).Entries.Count == 3 && restoredByDalamudSerializer[0].Task == history.Entries[0].Task,
        "Installed Dalamud JSON serializer restores record-based history");
}
Check(restored.CurrentLabel(approvalEntry, MonitorSnapshot.Offline("offline")) == "État actuel inconnu",
    "Offline history never claims an approval is currently pending");
restored.Add(TaskItem(2, "needsApproval"), now.AddMinutes(1));
Check(restored.CurrentLabel(approvalEntry, resolvedDuringCombat) == "Ancienne alerte",
    "An old approval is not confused with a later approval on the same task");


for (var n = 0; n < 110; n++) restored.Add(TaskItem(n), now);
Check(restored.Entries.Count == 100 && restored.Entries[0].Task.Id == "task-109", "History retention keeps the newest 100 events");
restored.Clear();
Check(restored.Entries.Count == 0, "History can be cleared independently");

alerts.Clear();
Tick(SnapshotOf(TaskItem(0, "active"), TaskItem(1, "active"), TaskItem(2, "active")));
Tick(SnapshotOf(TaskItem(0, "needsInput"), TaskItem(1, "active"), TaskItem(2, "active")));
Check(alerts.Visible()[0].HistoryId is not null, "A live alert links to its persisted history event");
Tick(SnapshotOf(TaskItem(0, "active"), TaskItem(1, "active"), TaskItem(2, "active")));
Check(alerts.Count == 0, "A resolved input request disappears from the visible and waiting queue");
Tick(duringCombat, true);
var historyCountBeforeDisconnect = history.Entries.Count;
Tick(MonitorSnapshot.Offline("offline"), true);
Check(alerts.Count == 0 && center.DeferredCount == 0 && history.Entries.Count == historyCountBeforeDisconnect,
    "Disconnect clears deferred and live notifications but retains history");
Tick(duringCombat);
Check(alerts.Count == 0 && history.Entries.Count == historyCountBeforeDisconnect, "Reconnection does not resurrect a stale combat summary");
Tick(initial);
Tick(duringCombat, true, false, false);
Check(center.DeferredCount == 0 && alerts.Count == 0 && history.Entries.Count > historyCountBeforeDisconnect,
    "Disabled popups remain in history without leaking into the quiet summary");
Tick(duringCombat, false, false, false);
Check(alerts.Count == 0, "Leaving quiet mode with no enabled alerts stays silent");

var down = Enumerable.Range(0, 3).Select(i => NotificationGeometry.Place(new Vector2(0.5f, 0.95f), Vector2.Zero, viewport, size, i, 3, 10, direction: StackDirection.Down)).ToArray();
var up = Enumerable.Range(0, 3).Select(i => NotificationGeometry.Place(new Vector2(0.5f, 0.05f), Vector2.Zero, viewport, size, i, 3, 10, direction: StackDirection.Up)).ToArray();
Check(down[0].Y < down[1].Y && down[2].Y + size.Y <= viewport.Y - 12 && up[0].Y > up[1].Y && up[2].Y >= 12,
    "Explicit stack directions override automatic direction while keeping the group on screen");
var shifted = NotificationGeometry.Place(anchor, origin, viewport, size, 0, 1, 10, offset: new Vector2(17, -8));
Check(Vector2.Distance(shifted, position + new Vector2(17, -8)) < 0.01f, "Precise offsets apply in pixels independently of normalized position");
var snappedCenter = NotificationGeometry.Snap(new Vector2(0.502f, 0.498f), viewport, size, 0);
Check(Vector2.Distance(snappedCenter, new Vector2(0.5f)) < 0.001f, "Magnetic guides snap to both viewport center lines");
var snappedEdge = NotificationGeometry.Snap(new Vector2((size.X / 2 + 17) / viewport.X, 0.015f), viewport, size, 0);
var edgePosition = NotificationGeometry.Place(snappedEdge, Vector2.Zero, viewport, size, 0, 1, 0);
Check(Vector2.Distance(edgePosition, new Vector2(12)) < 0.01f, "Edge snapping aligns the toast bounds with the safe margins");
var gridAnchor = NotificationGeometry.Snap(new Vector2(0.32f, 0.31f), viewport, size, 40, 0);
Check(Math.Abs(gridAnchor.X * viewport.X % 40) < 0.01f && Math.Abs(gridAnchor.Y * viewport.Y % 40) < 0.01f,
    "Grid snapping quantizes the anchor without requiring frame-sized mouse movements");
var allLayoutsFit = true;
foreach (var screen in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(3840, 2160), new Vector2(800, 600) })
foreach (var uiScale in new[] { 0.75f, 1f, 1.5f, 2f })
foreach (var direction in Enum.GetValues<StackDirection>())
foreach (var a in new[] { Vector2.Zero, Vector2.One, new Vector2(0.5f), new Vector2(0.99f, 0.01f) })
{
    var s = Math.Min(uiScale, Math.Min((screen.X - 24) / 440, (screen.Y - 24) / 112));
    var toastSize = new Vector2(440, 112) * s;
    var gap = 10 * s;
    var count = Math.Clamp((int)((screen.Y - 24 + gap) / (toastSize.Y + gap)), 1, 3);
    for (var i = 0; i < count; i++)
    {
        var p = NotificationGeometry.Place(a, origin, screen, toastSize, i, count, gap, direction: direction, offset: new Vector2(-200, 100));
        allLayoutsFit &= p.X >= origin.X + 11.99f && p.Y >= origin.Y + 11.99f
            && p.X + toastSize.X <= origin.X + screen.X - 11.99f && p.Y + toastSize.Y <= origin.Y + screen.Y - 11.99f;
    }
}
Check(allLayoutsFit, "Notification groups fit across 192 resolution, scale, anchor and direction combinations");
var concurrentHistory = new NotificationHistory();
Parallel.For(0, 500, n =>
{
    var entry = concurrentHistory.Add(TaskItem(n), now);
    var snapshot = concurrentHistory.Entries;

    foreach (var row in snapshot) _ = row.Task.Title;
});
Check(concurrentHistory.Entries.Count == 100 && concurrentHistory.Entries.Select(row => row.Id).Distinct().Count() == 100,
    "Concurrent history updates and reads keep stable bounded snapshots");
Check(IndicatorOptions.Resolve(null, true, true) == IndicatorMode.Text && IndicatorOptions.Resolve(null, false, true) == IndicatorMode.MiniHud,
    "Legacy display settings migrate to one indicator without creating duplicates");
Check(IndicatorOptions.Resolve(IndicatorMode.Hidden, true, true) == IndicatorMode.Hidden,
    "An explicit indicator choice overrides legacy toggles");
var soundPolicy = new SoundGate();
Check(!soundPolicy.Accept(true, true, 0.2f, SoundTone.Glass, now)
    && !soundPolicy.Accept(false, false, 0.2f, SoundTone.Glass, now)
    && !soundPolicy.Accept(true, false, 0, SoundTone.Glass, now), "Quiet mode, disabled sounds and zero volume are silent");
Check(soundPolicy.Accept(true, false, 0.2f, SoundTone.Glass, now)
    && !soundPolicy.Accept(true, false, 0.2f, SoundTone.Droplet, now.AddSeconds(1))
    && soundPolicy.Accept(true, false, 0.2f, SoundTone.Droplet, now.AddSeconds(2)), "Sound bursts are limited to one cue per two seconds");
Check(!soundPolicy.Accept(true, true, 0.2f, SoundTone.Glass, now, true), "Listen preview also respects quiet mode");
var tones = new[] { SoundTone.Glass, SoundTone.Droplet, SoundTone.Velvet }.Select(SoundClip.Synthesize).ToArray();
Check(tones.All(clip => clip.Seconds < 1 && clip.Pcm.Take(2).All(value => value == 0))
    && !tones[0].Pcm.SequenceEqual(tones[1].Pcm), "Built-in tones are distinct, short and start without a click");
var custom = SoundClip.ReadWave(tones[0].ToWave());
Check(custom.Pcm.SequenceEqual(tones[0].Pcm) && custom.SampleRate == 44100, "Generated WAV round-trips through the custom audio parser");
Check(tones[0].AtVolume(0).Pcm.All(value => value == 0) && tones[0].AtVolume(1).Pcm.SequenceEqual(tones[0].Pcm),
    "Per-plugin volume scales PCM without changing the system mixer");
void RejectWave(byte[] wav, string label)
{
    try { SoundClip.ReadWave(wav); throw new Exception(label); }
    catch (InvalidDataException) { Check(true, label); }
}
RejectWave(custom.ToWave()[..30], "Truncated WAV is rejected before playback");
var invalidFormat = custom.ToWave(); invalidFormat[20] = 3;
RejectWave(invalidFormat, "Unsupported compressed or floating-point WAV is rejected");
var invalidChunk = custom.ToWave(); Array.Fill(invalidChunk, (byte)255, 40, 4);
RejectWave(invalidChunk, "Out-of-range WAV chunk size is rejected");
RejectWave(new SoundClip(44100, 1, new byte[44100 * 2 * 4]).ToWave(), "Custom sounds longer than three seconds are rejected");
var soundEvents = new List<string>();
var soundCenter = new NotificationCenter(new NotificationHistory(), new NotificationQueue(), soundEvents.Add);
soundCenter.Update(initial, false, true, true, 7, now);
soundCenter.Update(duringCombat, true, true, true, 7, now);
Check(soundEvents.Count == 0, "Initial subscription and combat transitions never emit sounds");
soundCenter.Update(resolvedDuringCombat, false, true, true, 7, now);
Check(soundEvents.SequenceEqual(new[] { "needsInput" }), "Quiet exit emits one intervention cue for the summary");
soundCenter.Update(initial, false, true, true, 7, now);
soundEvents.Clear();
soundCenter.Update(SnapshotOf(TaskItem(0), TaskItem(1, "needsInput"), TaskItem(2, "error")), false, true, true, 7, now);
Check(soundEvents.SequenceEqual(new[] { "error" }), "Simultaneous alerts emit one cue with error priority");
var legacyEntry = JsonSerializer.Serialize(new[] { new { Id = 1L, Task = TaskItem(1), At = now, IsRead = false } });
var migratedHistory = new NotificationHistory(JsonSerializer.Deserialize<List<HistoryEntry>>(legacyEntry));
Check(migratedHistory.Entries.Count == 1 && !JsonSerializer.Serialize(migratedHistory.Entries).Contains("IsRead"),
    "Legacy unread flags are discarded while historical events are preserved");

var q1 = new string('a', 32); var q2 = new string('b', 32);
MonitorSnapshot Questions(params string[] questions) => new(true, now,
    [new MonitoredThread(id, "Question while running", "Demo", "", "active", questions)], null, true);
var noQuestion = Questions(); var withQuestion = Questions(q1); var twoQuestions = Questions(q1, q2);
var questionJson = JsonSerializer.Serialize(new { schemaVersion = 1, connected = true, generatedAt = now, questionTrackingSupported = true,
    threads = new[] { new { id, state = "active", availability = "live", lastConfirmedAt = now, pendingQuestionIds = new[] { q1, q1, "invalid" } } } });
var parsedQuestions = MonitorContract.Parse(questionJson, now);
Check(parsedQuestions.Active == 1 && parsedQuestions.Attention == 1 && parsedQuestions.Threads[0].State == "active" && parsedQuestions.Threads[0].QuestionIds.Length == 1,
    "An async question needs attention while its task remains active; duplicate and malformed IDs are ignored");
var staleQuestionJson = System.Text.Json.Nodes.JsonNode.Parse(questionJson)!;
staleQuestionJson["threads"]![0]!["lastConfirmedAt"] = now.AddSeconds(-40);
var staleQuestionSnapshot = MonitorContract.Parse(staleQuestionJson.ToJsonString(), now);
Check(staleQuestionSnapshot.Attention == 0 && !staleQuestionSnapshot.Threads[0].HasQuestion,
    "A stale owner cannot keep an old question indicator alive");
Check(TransitionDetector.Find(noQuestion, withQuestion, true, true).Single().State == "question",
    "A question emits an alert without a runtime status change");
Check(TransitionDetector.Find(withQuestion, withQuestion, true, true).Count == 0,
    "An unanswered question does not repeat at every poll");
Check(TransitionDetector.Find(withQuestion, twoQuestions, true, true).Single().QuestionIds.SequenceEqual(new[] { q2 }),
    "A second question on an already flagged task emits its own alert");
Check(TransitionDetector.Find(MonitorSnapshot.Offline("offline"), withQuestion, true, true).Count == 0
    && TransitionDetector.Find(noQuestion with { QuestionTrackingSupported = false }, withQuestion, true, true).Count == 0,
    "Initial connection and upgrading the relay do not replay old questions");
Check(TransitionDetector.Find(withQuestion, noQuestion, true, true).Count == 0 && noQuestion.Active == 1,
    "Answering a question clears attention without a false completion");
var questionHistory = new NotificationHistory(); var questionQueue = new NotificationQueue(); var questionSounds = new List<string>();
var questionCenter = new NotificationCenter(questionHistory, questionQueue, questionSounds.Add);
questionCenter.Update(noQuestion, false, true, true, 7, now);
questionCenter.Update(withQuestion, false, true, true, 7, now);
Check(questionQueue.Count == 1 && questionSounds.SequenceEqual(new[] { "question" }) && questionHistory.CurrentLabel(questionHistory.Entries[0], withQuestion) == "Intervention en cours",
    "Question notifications, sound and journal agree on the pending request");
questionCenter.Update(noQuestion, false, true, true, 7, now);
Check(questionQueue.Count == 0 && questionHistory.CurrentLabel(questionHistory.Entries[0], noQuestion) == "Question traitée ou dépassée",
    "A reply removes the notification and marks the historical question resolved");
var mutedQuestionHistory = new NotificationHistory(); var mutedQuestionQueue = new NotificationQueue();
var mutedQuestionCenter = new NotificationCenter(mutedQuestionHistory, mutedQuestionQueue);
mutedQuestionCenter.Update(noQuestion, false, true, true, 7, now, false);
mutedQuestionCenter.Update(withQuestion, false, true, true, 7, now, false);
Check(mutedQuestionHistory.Entries.Count == 1 && mutedQuestionQueue.Count == 0,
    "The independent question preference suppresses popups while preserving the journal");
var deferredQuestions = new NotificationHistory(); var deferredQuestionQueue = new NotificationQueue();
var deferredQuestionCenter = new NotificationCenter(deferredQuestions, deferredQuestionQueue);
deferredQuestionCenter.Update(noQuestion, false, true, true, 7, now);
deferredQuestionCenter.Update(withQuestion, true, true, true, 7, now);
Check(deferredQuestionQueue.Count == 0 && NotificationCenter.Summarize(deferredQuestions.Entries, withQuestion).Questions == 1
    && NotificationCenter.Summarize(deferredQuestions.Entries, noQuestion).Questions == 0,
    "Combat defers question alerts and the digest only counts questions still unanswered");
Check(new SoundOptions().Select("question").Tone == SoundTone.Droplet,
    "Async questions use the customizable attention sound");
AccountUsage? UsagePayload(object? usage) => AccountUsage.Parse(JsonSerializer.SerializeToElement(usage));
var windows = new[] { new UsageWindow(80, 300, now.AddHours(1).ToUnixTimeSeconds()), new UsageWindow(48, 10080, now.AddDays(2).ToUnixTimeSeconds()) };
var account = UsagePayload(new { fetchedAt = now, windows })!;
Check(account.Current(now)?.RemainingPercent == 48 && account.Current(now)?.Period == "Semaine", "Weekly quota preferred over short window");
Check(account.Current(now.AddSeconds(121)) is null && account.Current(now.AddSeconds(-121)) is null, "Old and future-dated quota hidden");
Check(UsagePayload(null) is null && live.Usage is null, "Old relay and absent quota remain compatible");
Check(UsagePayload(new { fetchedAt = now, windows = new[] { new UsageWindow(0, 300, null) } })?.Current(now)?.Percent == "0%", "Exhausted quota is zero rather than unavailable");
Check(UsagePayload(new { fetchedAt = now, windows = new[] { new UsageWindow(101, 300, null) } }) is null, "Malformed quota cannot escape bounds");
Check(UsagePayload(new { fetchedAt = now, windows = new[] { new { remainingPercent = "unknown" } } }) is null, "Invalid optional quota is ignored");
Check(UsagePayload(new { fetchedAt = now, windows = new[] { new { windowDurationMins = 300 } } }) is null, "Missing percentage is not read as zero");
Check(UsagePayload(new { fetchedAt = now, windows = new[] { new UsageWindow(48, 10080, now.AddSeconds(-1).ToUnixTimeSeconds()) } })?.Current(now) is null, "Expired period does not keep its old percentage");
Check((live with { Usage = account }).CurrentUsage?.RemainingPercent == 48 && (MonitorSnapshot.Offline("offline") with { Usage = account }).CurrentUsage is null, "Offline snapshot never renders cached quota");
var quotaRoot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Payload())!;
quotaRoot["usage"] = JsonSerializer.SerializeToElement(new { fetchedAt = now, windows });
Check(MonitorContract.Parse(JsonSerializer.Serialize(quotaRoot), now).Usage?.Current(now)?.RemainingPercent == 48, "Bridge quota reaches plugin contract");
quotaRoot["usage"] = JsonSerializer.SerializeToElement((object?)null);
var unavailableQuota = MonitorContract.Parse(JsonSerializer.Serialize(quotaRoot), now);
Check(unavailableQuota.UsageTrackingSupported && unavailableQuota.Usage is null && !live.UsageTrackingSupported, "Unknown quota is distinguished from an old relay without quota support");
quotaRoot["usage"] = JsonSerializer.SerializeToElement(new { windows = "invalid" });
Check(MonitorContract.Parse(JsonSerializer.Serialize(quotaRoot), now).Active == 1, "Bad quota cannot hide task states");
Check(TransitionDetector.Find(live, live with { Usage = account }, true, true).Count == 0, "Quota refresh never triggers a task notification");
foreach (var style in Enum.GetValues<MiniHudStyle>())
{
    var hudSize = MiniHudOptions.Size(style, true) * 1.5f;
    var pos = NotificationGeometry.Place(new Vector2(1, 1), Vector2.Zero, new Vector2(560, 490), hudSize, 0, 1, 0);
    Check(pos.X >= 0 && pos.Y >= 0 && pos.X + hudSize.X <= 560 && pos.Y + hudSize.Y <= 490, $"{style} stays inside viewport at 150% and edge anchor");
}
checks += QuietReturnChecks.Run();
checks += HudMotionChecks.Run();
Console.WriteLine($"{checks} checks passed.");

static async Task Until(Func<bool> condition)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
    while (!condition()) await Task.Delay(20, timeout.Token);
}

sealed class ScriptedHandler(Func<string> payload) : HttpMessageHandler
{
    public volatile bool Fail;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Host != "127.0.0.1") throw new Exception("Unexpected remote host");
        if (Fail) throw new HttpRequestException("Simulated connection loss");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload()) });
    }
}
