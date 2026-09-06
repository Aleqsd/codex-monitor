using System.Numerics;
using System.Runtime.InteropServices;
using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe class Program
{
    private sealed record Texture(int Width, int Height, byte[] Pixels);
    private static readonly Dictionary<ImTextureID, Texture> Textures = new();
    private static Plugin plugin = null!;
    private static MainWindow window = null!;
    private static int screenWidth, screenHeight;
    private static bool overlayOnly;
    private static bool gallery;
    private static bool hudOnly;
    private static string galleryState = "live";
    private static bool quietRendering;
    private static HudMotionFrame? galleryMotion;
    private static bool appearanceOnly;
    private static bool skinOnly;
    private static float skinScroll;
    private static AppearanceTarget skinScope;
    private static int backdrop = 15;
    private static SettingsPanel? skinPanel;
    private sealed class FontPop : IDisposable { public void Dispose() => ImGui.PopFont(); }
    private static readonly Dictionary<int, ImFontPtr> SizedFonts = new();

    private static void Main(string[] args)
    {
        if (args.Contains("--skin-smoke"))
        {
            SkinChecks.Run();
            Initialize(800, 900, 1); skinOnly = true; skinScope = AppearanceTarget.Notification;
            plugin.Config.WindowAppearance!.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Window);
            plugin.Config.ToastAppearance!.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Notification);
            for (var i=0; i<3; i++) Frame();
            Click(106, 157);
            if (plugin.Config.ToastAppearance.Skin != MonitorSkin.Obsidienne || plugin.Config.WindowAppearance.Skin != MonitorSkin.LMeter) throw new Exception("Theme affected another component.");
            Click(26, 157);
            if (plugin.Config.ToastAppearance.Skin != MonitorSkin.LMeter) throw new Exception("LMeter preset did not persist.");
            var foreground = plugin.Config.ToastAppearance.Text.Color;
            Click(20, 391);
            if (plugin.Config.ToastAppearance.Opacity > .05f || plugin.Config.ToastAppearance.Text.Color != foreground) throw new Exception("Opacity control changed foreground.");
            Click(150, 512); Click(60, 543);
            if (plugin.Config.ToastAppearance.Text.Font != MonitorFont.Dalamud) throw new Exception("Fallback font was not selected.");
            Click(760, 598);
            if (plugin.Config.ToastAppearance.Text.Size < 23) throw new Exception("Text size was not saved.");
            if (plugin.SaveCount < 5) throw new Exception("Controls did not save.");
            ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose();
            Console.WriteLine("PASS five native appearance interactions: theme, independent scope, background opacity, font and text size."); return;
        }
        if (args.Contains("--hud-menu"))
        {
            Initialize(800, 700, 1); window.ShowSettings = true; plugin.SetIndicator(IndicatorMode.MiniHud);
            for (var i=0; i<3; i++) Frame(); Click(120,316); Render("artifacts/hud-menu.ppm"); return;
        }
        if (args.Contains("--skin-preview"))
        {
            var directory = Path.GetFullPath(args.Last()); Directory.CreateDirectory(directory);
            foreach (var (width, height, scale) in new[] { (800, 700, 1f), (560, 490, 1f), (1200, 1050, 1.5f), (1120, 980, 2f) })
            {
                foreach (var page in new[] { "lmeter-tasks", "lmeter-settings", "lmeter-hud", "lmeter-toast", "appearance-window", "appearance-hud", "appearance-toast", "appearance-font", "appearance-advanced", "nuit-tasks", "large-type", "light-backdrop", "transparent-hud", "local-font" })
                {
                    Initialize(width, height, scale); plugin.SetIndicator(IndicatorMode.MiniHud); plugin.Config.HudStyle = MiniHudStyle.ObsidienneFine;
                    plugin.Config.WindowAppearance!.ApplyPreset(page == "nuit-tasks" ? MonitorSkin.Nuit : MonitorSkin.LMeter, AppearanceTarget.Window);
                    plugin.Config.HudAppearance!.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Hud);
                    plugin.Config.ToastAppearance!.ApplyPreset(MonitorSkin.LMeter, AppearanceTarget.Notification);
                    plugin.Config.MiniHudOpacity = 1;
                    gallery = page is "lmeter-hud" or "transparent-hud"; galleryState = page;
                    if (page == "transparent-hud") plugin.Config.HudAppearance.Opacity = 0;
                    if (page == "large-type") { plugin.Config.WindowAppearance.Text.Size = 24; plugin.Config.WindowAppearance.Text.Font = MonitorFont.SegoeUi; }
                    if (page == "local-font") { plugin.Config.WindowAppearance.Text.Font = MonitorFont.LocalFile; plugin.Config.WindowAppearance.Text.FontFile = "C:/Windows/Fonts/consola.ttf"; }
                    if (page == "light-backdrop") { backdrop = 205; plugin.Config.WindowAppearance.Opacity = .7f; }
                    if (page == "lmeter-toast") { overlayOnly = true; plugin.Config.NotificationReducedMotion = true; plugin.NotificationUi.Queue.Add(NotificationOverlay.Example("question"), 7); }
                    skinOnly = page.StartsWith("appearance-");
                    skinScope = page == "appearance-hud" ? AppearanceTarget.Hud : page is "appearance-toast" or "appearance-advanced" ? AppearanceTarget.Notification : AppearanceTarget.Window;
                    skinScroll = page == "appearance-font" ? 340 * scale : page == "appearance-advanced" ? 620 * scale : 0;
                    if (page == "appearance-advanced") { plugin.Config.ToastAppearance.Text.OffsetX = -20; plugin.Config.ToastAppearance.Text.OffsetY = 12; plugin.Config.ToastAppearance.PaddingX = 24; }
                    window.ShowSettings = page == "lmeter-settings";
                    for (var frame = 0; frame < 4; frame++) Frame();
                    Render(Path.Combine(directory, $"{page}-{width}.ppm"));
                    ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose(); SizedFonts.Clear();
                }
            }
            Console.WriteLine("Rendered 56 actual ImGui skin views: three surfaces, fonts, independent transparency and 100/150/200% scales."); return;
        }
        if (args.Contains("--appearance-smoke"))
        {
            Initialize(800, 700, 1); appearanceOnly = true; plugin.SetIndicator(IndicatorMode.MiniHud);
            for (var frame = 0; frame < 3; frame++) Frame();
            Click(130, 253);
            if (plugin.Config.HudAppearance!.Blue != 0.23f) throw new Exception("Night preset was not saved.");
            Click(30, 284);
            if (plugin.Config.HudAppearance.Opacity > 0.15f || plugin.Config.MiniHudOpacity != 0.94f) throw new Exception("Background opacity changed content opacity.");
            Click(35, 318);
            if (plugin.Config.HudAppearance.Border) throw new Exception("Border toggle did not save.");
            Click(35, 225);
            var beforeColorSave = plugin.SaveCount;
            Click(200, 300);
            if (plugin.Config.HudAppearance.Red < 0.2f || plugin.SaveCount <= beforeColorSave) throw new Exception("Custom color picker did not save on release.");
            Click(700, 100);
            Click(90, 348);
            if (plugin.Config.HudAppearance.Blue != 0.13f || plugin.Config.HudAppearance.Opacity != 0.94f || !plugin.Config.HudAppearance.Border) throw new Exception("Appearance reset failed.");
            Click(110, 188); Click(70, 269);
            if (plugin.Config.HudAppearance.Background != HudBackgroundMode.Hidden) throw new Exception("Background visibility did not save.");
            var serializer = System.Reflection.Assembly.LoadFrom(Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME")!, "Newtonsoft.Json.dll")).GetType("Newtonsoft.Json.JsonConvert")!;
            var deserialize = serializer.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) })!;
            var legacy = (Configuration)deserialize.Invoke(null, new object[] { "{\"MiniHudOpacity\":0.6,\"Indicator\":1}", typeof(Configuration) })!;
            legacy.Normalize();
            if (legacy.HudAppearance!.Opacity != 0.6f || legacy.MiniHudOpacity != 0.6f) throw new Exception("Legacy opacity migration failed.");
            legacy.HudAppearance.Red = 0.4f; legacy.HudAppearance.Opacity = 0;
            var json = (string)serializer.GetMethod("SerializeObject", new[] { typeof(object) })!.Invoke(null, new object[] { legacy })!;
            var restored = (Configuration)deserialize.Invoke(null, new object[] { json, typeof(Configuration) })!; restored.Normalize();
            if (restored.HudAppearance!.Red != 0.4f || restored.HudAppearance.Opacity != 0 || restored.MiniHudOpacity != 0.6f) throw new Exception("Appearance did not survive save/reload.");
            ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose();
            Console.WriteLine("PASS appearance controls and installed Dalamud serializer: preset, independent opacity, border, picker, reset, visibility, migration and persistence."); return;
        }
        if (args.Contains("--appearance-preview"))
        {
            var appearanceOutput = Path.GetFullPath(args.Last()); Directory.CreateDirectory(appearanceOutput);
            foreach (var (width, height, scale) in new[] { (800, 700, 1f), (560, 490, 1f), (1200, 1050, 1.5f) })
            {
                Initialize(width, height, scale); plugin.SetIndicator(IndicatorMode.MiniHud);
                foreach (var theme in new[] { "prune", "nuit", "no-background", "low-opacity", "appearance-settings" })
                {
                    appearanceOnly = theme == "appearance-settings"; gallery = !appearanceOnly; galleryState = theme;
                    plugin.Config.HudAppearance = new HudAppearance { Red = theme == "nuit" ? 0.07f : 0.2f, Green = theme == "nuit" ? 0.12f : 0.09f, Blue = theme == "nuit" ? 0.23f : 0.22f,
                        Opacity = theme == "low-opacity" ? 0.2f : 0.94f, Background = theme == "no-background" ? HudBackgroundMode.Hidden : HudBackgroundMode.Visible };
                    for (var frame = 0; frame < 3; frame++) Frame();
                    Render(Path.Combine(appearanceOutput, $"{theme}-{width}.ppm"));
                }
                ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose();
            }
            Console.WriteLine("Rendered 15 real ImGui appearance views, including independent transparency and all six styles."); return;
        }
        if (args.Contains("--hud-animation-smoke"))
        {
            Initialize(800, 700, 1); window.ShowSettings = true; plugin.SetIndicator(IndicatorMode.MiniHud);
            for (var frame = 0; frame < 3; frame++) Frame();
            Click(255, 455);
            var settingsPanel = (SettingsPanel)typeof(MainWindow).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
            var previewMotion = (HudMotion)typeof(SettingsPanel).GetField("hudMotion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(settingsPanel)!;
            if (previewMotion.Update(plugin.Snapshot, true, 0).ActivePulse <= 0) throw new Exception("Animation preview button did not animate.");
            Click(35, 455);
            if (plugin.Config.AnimateHudChanges || previewMotion.Update(plugin.Snapshot, false, 0).ActivePulse != 0) throw new Exception("Animation toggle did not disable motion.");
            ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose();
            Console.WriteLine("PASS two real HUD animation interactions: preview and disable (off-game)."); return;
        }
        if (args.Contains("--motion-preview"))
        {
            Initialize(800, 700, 1); gallery = true; galleryState = "animation · données d’exemple";
            var motionOutput = Path.GetFullPath(args.Last()); Directory.CreateDirectory(motionOutput);
            var motion = new HudMotion(); var io = ImGui.GetIO(); io.DeltaTime = 1f / 20;
            for (var frame = 0; frame < 42; frame++)
            {
                if (frame == 8) plugin.Snapshot = plugin.Snapshot with {
                    Threads = [.. plugin.Snapshot.Threads, new("6", "Exemple", "", "", "active", ["22222222222222222222222222222222"])],
                    Usage = new AccountUsage(DateTimeOffset.UtcNow, [new(36, 10080, null)]) };
                galleryMotion = motion.Update(plugin.Snapshot, true, io.DeltaTime);
                Frame(); Render(Path.Combine(motionOutput, $"motion-{frame:00}.ppm"));
            }
            ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose();
            Console.WriteLine("Rendered 42 animation frames from actual HUD code with example data."); return;
        }
        if (args.Contains("--quiet-render-smoke"))
        {
            Initialize(800, 700, 1); overlayOnly = true;
            var now = DateTimeOffset.UtcNow; var mode = new QuietModeGate(); var ticks = 0;
            var snapshot = new MonitorSnapshot(true, now, [new("one", "Example", "", "", "active")], null);
            ImGui.GetIO().DeltaTime = 0.1f;
            void Step(bool combat, int count)
            {
                for (var i = 0; i < count; i++) {
                    var time = now.AddMilliseconds(++ticks * 100);
                    quietRendering = mode.Update(combat, time);
                    plugin.Center.Update(snapshot, quietRendering, true, true, 7, time);
                    Frame();
                }
            }
            Step(false, 1); snapshot = snapshot with { Threads = [snapshot.Threads[0] with { State = "idle" }] };
            Step(false, 68); Step(true, 50); Step(false, 1);
            if (!quietRendering) throw new Exception("Short combat release was drawn.");
            Step(true, 1); Step(false, 21);
            if (quietRendering || plugin.NotificationUi.Queue.Visible()[0].Age > 0.11f) throw new Exception("Toast resumed with old age.");
            Step(false, 59);
            if (plugin.NotificationUi.Queue.Count != 1) throw new Exception("Returned toast expired before six visible seconds.");
            Step(false, 12);
            if (plugin.NotificationUi.Queue.Count != 0) throw new Exception("Returned toast failed to expire.");
            ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose();
            Console.WriteLine("PASS real overlay frames: interrupted green toast, combat flicker, full-duration return and expiry (off-game)."); return;
        }
        if (args.Contains("--hud-interaction-smoke"))
        {
            Initialize(800, 700, 1);
            window.ShowSettings = true; plugin.SetIndicator(IndicatorMode.MiniHud);
            for (var frame = 0; frame < 3; frame++) Frame();
            Click(120, 316);
            Click(70, 427);
            if (plugin.Config.HudStyle != MiniHudStyle.Lisere) throw new Exception($"Style selector did not persist: {plugin.Config.HudStyle}");
            Click(35, 351);
            if (plugin.Config.ShowUsage) throw new Exception("Quota toggle did not save.");
            plugin.Config.HudAppearance!.Background = HudBackgroundMode.Visible;
            hudOnly = true; overlayOnly = true;
            for (var frame = 0; frame < 3; frame++) Frame();
            Click(400, 56);
            if (plugin.OpenCount != 1) throw new Exception("HUD click did not open task panel.");
            plugin.Hud.SetEditing(true);
            var io = ImGui.GetIO(); io.AddMousePosEvent(400, 56); Frame();
            io.AddMouseButtonEvent(0, true); Frame(); io.AddMousePosEvent(600, 450); Frame();
            io.AddMouseButtonEvent(0, false); Frame(); Frame();
            if (plugin.Config.MiniHudAnchorX < 0.7f || plugin.Config.MiniHudAnchorY < 0.6f) throw new Exception("Dragged anchor did not save.");
            io.AddMouseButtonEvent(1, true); Frame(); io.AddMouseButtonEvent(1, false); Frame();
            if (plugin.Hud.Editing) throw new Exception("Right click did not lock HUD.");
            ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose();
            Console.WriteLine("PASS five real HUD interactions: style, quota, open, drag, lock (off-game).");
            return;
        }
        if (args.Contains("--interaction-smoke"))
        {
            Initialize(800, 700, 1);
            for (var frame = 0; frame < 3; frame++) Frame();
            Click(210, 136);
            if (!window.ShowSettings) throw new Exception("Settings tab did not open.");
            Click(36, 273);
            if (plugin.Config.Indicator != IndicatorMode.MiniHud || plugin.SaveCount == 0) throw new Exception("Mini HUD selection was not saved.");
            Click(128, 273);
            if (plugin.Config.Indicator != IndicatorMode.Text) throw new Exception("Text mode did not replace Mini HUD.");
            Click(260, 273);
            if (plugin.Config.Indicator != IndicatorMode.Hidden) throw new Exception("Hidden mode did not replace text mode.");
            Click(215, 170);
            Click(36, 273);
            if (plugin.Config.Sounds.Enabled) throw new Exception("Sound toggle did not disable audio.");
            Click(125, 136);
            if (!window.ShowHistory || window.ShowSettings) throw new Exception("History tab did not open.");
            ImGui.DestroyContext(); Textures.Clear(); plugin.Sounds.Dispose();
            Console.WriteLine("PASS six real ImGui interactions: settings, Mini HUD, text, hidden, mute, history (off-game).");
            return;
        }
        if (args.Contains("--audio-smoke"))
        {
            var play = typeof(NotificationSounds).GetMethod("PlayClip", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var clip = SoundClip.Synthesize(SoundTone.Glass).AtVolume(0);
            ((Task)play.Invoke(null, new object[] { clip, CancellationToken.None })!).GetAwaiter().GetResult();
            using var canceled = new CancellationTokenSource(80);
            try
            {
                ((Task)play.Invoke(null, new object[] { clip, canceled.Token })!).GetAwaiter().GetResult();
                throw new Exception("Audio cancellation was ignored.");
            }
            catch (OperationCanceledException) { }
            Console.WriteLine("PASS native audio device accepts PCM and releases buffers on completion and cancellation (silent smoke test).");
            return;
        }
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/visual");
        Directory.CreateDirectory(output);
        foreach (var (width, height, scale) in new[] { (800, 700, 1f), (560, 490, 1f), (1200, 1050, 1.5f) })
        {
            Initialize(width, height, scale);
            var settings = (SettingsPanel)typeof(MainWindow).GetField("settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
            var liveSnapshot = plugin.Snapshot;
            foreach (var page in new[] { "hud-live", "hud-offline", "hud-no-quota", "hud-idle", "hud-many", "tasks", "display-mini", "display-text", "sounds", "notifications", "connection", "history", "question-toast", "offline" })
            {
                gallery = page.StartsWith("hud-"); galleryState = page;
                plugin.Snapshot = page is "offline" or "hud-offline" ? MonitorSnapshot.Offline("Le relais local ne répond pas.") : liveSnapshot;
                if (page is "hud-live" or "hud-no-quota") plugin.Snapshot = liveSnapshot with { Threads = liveSnapshot.Threads.Where(task => task.State != "needsInput").ToArray() };
                if (page == "hud-idle") plugin.Snapshot = liveSnapshot with { Threads = [] };
                if (page == "hud-many") plugin.Snapshot = liveSnapshot with { Threads = Enumerable.Range(0, 200).Select(i => new MonitoredThread(i.ToString(), "Exemple", "", "", "active", ["11111111111111111111111111111111"])).ToArray(), Usage = new AccountUsage(DateTimeOffset.UtcNow, [new(100, 10080, null)]) };
                plugin.Config.ShowUsage = page != "hud-no-quota";
                window.ShowSettings = page is "display-mini" or "display-text" or "sounds" or "notifications" or "connection";
                window.ShowHistory = page == "history";
                overlayOnly = page == "question-toast";
                settings.Category = page == "sounds" ? 2 : page == "notifications" ? 1 : page == "connection" ? 3 : 0;
                plugin.Config.Indicator = page == "display-text" ? IndicatorMode.Text : IndicatorMode.MiniHud;
                plugin.NotificationUi.Queue.Clear();
                if (page == "question-toast")
                {
                    plugin.Config.NotificationReducedMotion = true;
                    plugin.NotificationUi.Queue.Add(plugin.Snapshot.Threads[0] with { State = "question" }, 7);
                }
                for (var frame = 0; frame < 3; frame++) Frame();
                Render(Path.Combine(output, $"{page}-{width}.ppm"));
            }
            ImGui.DestroyContext(); Textures.Clear();
        }
        foreach (var tone in new[] { SoundTone.Glass, SoundTone.Droplet, SoundTone.Velvet })
            File.WriteAllBytes(Path.Combine(output, $"{tone.ToString().ToLowerInvariant()}.wav"), SoundClip.Synthesize(tone).AtVolume(0.18f).ToWave());
        Console.WriteLine("Rendered 42 views from the actual ImGui components, including all six HUDs; 3 sound previews exported.");
    }

    private static void Initialize(int width, int height, float scale)
    {
        screenWidth = width; screenHeight = height; overlayOnly = false; gallery = false; hudOnly = false; quietRendering = false; galleryMotion = null; appearanceOnly = false; skinOnly = false; skinScroll = 0; backdrop = 15; skinPanel = null;
        ImGui.CreateContext();
        var io = ImGui.GetIO(); io.DisplaySize = new Vector2(width, height); io.DeltaTime = 1f / 60;
        io.IniFilename = null;
        Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale = scale;
        ushort[] ranges = [0x20, 0xFF, 0x2000, 0x206F, 0];
        fixed (ushort* range = ranges)
        {
            foreach (var size in new[] {17, 12, 14, 20, 24})
            {
                SizedFonts[size] = io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf", size * scale, default, range);
                SizedFonts[size + 100] = io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/consola.ttf", size * scale, default, range);
            }
            if (!io.Fonts.Build()) throw new Exception("Font atlas build failed.");
        }
        for (var i = 0; i < io.Fonts.Textures.Size; i++)
        {
            byte* pixels = null; int w = 0, h = 0;
            io.Fonts.GetTexDataAsRGBA32(i, &pixels, &w, &h);
            var bytes = new byte[w * h * 4]; Marshal.Copy((nint)pixels, bytes, 0, bytes.Length);
            var id = new ImTextureID((nint)(i + 1));
            io.Fonts.SetTexID(i, id); Textures[id] = new Texture(w, h, bytes);
        }
        UiFonts.Resolver = text => { if (text is null) return null; ImGui.PushFont(SizedFonts.TryGetValue((int)text.Size + (text.Font == MonitorFont.LocalFile && text.FontFile.Length > 0 ? 100 : 0), out var font) ? font : SizedFonts[17]); return new FontPop(); };
        plugin = new Plugin(); window = new MainWindow(plugin);
    }

    private static void Frame()
    {
        ImGui.NewFrame(); using var font = UiFonts.Push(plugin.Config.WindowAppearance!.Text); ObsidianTheme.Push(plugin.Config.WindowAppearance);
        if (!overlayOnly)
        {
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
            if (ImGui.Begin(window.WindowName, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings))
            {
                if (skinOnly)
                {
                    if (ImGui.BeginChild("skin-settings", Vector2.Zero))
                    {
                        var panel = skinPanel ??= new SettingsPanel(plugin) { AppearanceScope = skinScope };
                        if (skinScroll > 500 * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale)
                            ImGui.GetStateStorage().SetInt(ImGui.GetID("Disposition et détails"), 1);
                        typeof(SettingsPanel).GetMethod("DrawAppearance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(panel, null);
                        skinScope = panel.AppearanceScope;
                        ImGui.SetScrollY(skinScroll);
                    }
                    ImGui.EndChild();
                }
                else if (appearanceOnly)
                {
                    var panel = new SettingsPanel(plugin);
                    typeof(SettingsPanel).GetMethod("DrawHudAppearance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(panel, null);
                }
                else if (gallery) DrawGallery(); else window.Draw();
            }
            ImGui.End();
        }
        if (hudOnly) plugin.Hud.Draw();
        plugin.NotificationUi.Draw(quietRendering); ObsidianTheme.Pop(); ImGui.Render();
    }

    private static void DrawGallery()
    {
        var s = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        ImGui.TextColored(ObsidianTheme.Mint, "SIX MINI HUD · CODEX MONITOR");
        ImGui.TextDisabled("Rendu ImGui hors jeu · Données d’exemple · " + galleryState.Replace("hud-", ""));
        var origin = ImGui.GetCursorScreenPos() + new Vector2(0, 10 * s);
        var cell = new Vector2((screenWidth - 50 * s) / 2, (screenHeight - origin.Y - 15 * s) / 3);
        foreach (var style in Enum.GetValues<MiniHudStyle>())
        {
            var index = (int)style;
            var p = origin + new Vector2(index % 2 * cell.X, index / 2 * cell.Y);
            ImGui.GetWindowDrawList().AddText(p, ObsidianTheme.U(ObsidianTheme.Muted), MiniHudOptions.Names[index]);
            var size = MiniHudOptions.Size(style, plugin.Config.ShowUsage, plugin.Config.HudAppearance) * s;
            MiniHud.DrawFace(p + new Vector2(0, 28 * s), size, plugin.Snapshot, false, false, plugin.Config.MiniHudOpacity, style, plugin.Config.ShowUsage, galleryMotion, plugin.Config.HudAppearance);
        }
    }

    private static void Click(float x, float y)
    {
        var io = ImGui.GetIO();
        io.AddMousePosEvent(x, y); Frame();
        io.AddMouseButtonEvent(0, true); Frame();
        io.AddMouseButtonEvent(0, false); Frame(); Frame();
    }


    private static void Render(string path)
    {
        var rgb = new byte[screenWidth * screenHeight * 3];
        Array.Fill(rgb, (byte)backdrop);
        var data = ImGui.GetDrawData();
        for (var listIndex = 0; listIndex < data.CmdListsCount; listIndex++)
        {
            var list = new ImDrawListPtr(data.CmdLists[listIndex]);
            for (var commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++)
            {
                var cmd = list.CmdBuffer[commandIndex];
                if (cmd.UserCallback != null || !Textures.TryGetValue(cmd.TextureId, out var texture)) continue;
                for (var index = 0u; index < cmd.ElemCount; index += 3)
                {
                    var a = list.VtxBuffer[(int)(cmd.VtxOffset + list.IdxBuffer[(int)(cmd.IdxOffset + index)])];
                    var b = list.VtxBuffer[(int)(cmd.VtxOffset + list.IdxBuffer[(int)(cmd.IdxOffset + index + 1)])];
                    var c = list.VtxBuffer[(int)(cmd.VtxOffset + list.IdxBuffer[(int)(cmd.IdxOffset + index + 2)])];
                    Triangle(rgb, a, b, c, cmd.ClipRect, texture);
                }
            }
        }
        using var stream = File.Create(path);
        stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{screenWidth} {screenHeight}\n255\n")); stream.Write(rgb);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static void Triangle(byte[] output, ImDrawVert a, ImDrawVert b, ImDrawVert c, Vector4 clip, Texture texture)
    {
        var area = Cross(b.Pos - a.Pos, c.Pos - a.Pos); if (Math.Abs(area) < 0.001f) return;
        var minX = Math.Max(0, Math.Max((int)clip.X, (int)MathF.Floor(Math.Min(a.Pos.X, Math.Min(b.Pos.X, c.Pos.X)))));
        var minY = Math.Max(0, Math.Max((int)clip.Y, (int)MathF.Floor(Math.Min(a.Pos.Y, Math.Min(b.Pos.Y, c.Pos.Y)))));
        var maxX = Math.Min(screenWidth, Math.Min((int)clip.Z, (int)MathF.Ceiling(Math.Max(a.Pos.X, Math.Max(b.Pos.X, c.Pos.X)))));
        var maxY = Math.Min(screenHeight, Math.Min((int)clip.W, (int)MathF.Ceiling(Math.Max(a.Pos.Y, Math.Max(b.Pos.Y, c.Pos.Y)))));
        for (var y = minY; y < maxY; y++) for (var x = minX; x < maxX; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            var wa = Cross(b.Pos - p, c.Pos - p) / area;
            var wb = Cross(c.Pos - p, a.Pos - p) / area;
            var wc = 1 - wa - wb;
            if (wa < 0 || wb < 0 || wc < 0) continue;
            var uv = a.Uv * wa + b.Uv * wb + c.Uv * wc;
            var fx = uv.X * texture.Width - 0.5f; var fy = uv.Y * texture.Height - 0.5f;
            var tx = (int)MathF.Floor(fx); var ty = (int)MathF.Floor(fy);
            var mixX = fx - tx; var mixY = fy - ty;
            float Sample(int channel)
            {
                float Pixel(int x, int y) => texture.Pixels[(Math.Clamp(y, 0, texture.Height - 1) * texture.Width + Math.Clamp(x, 0, texture.Width - 1)) * 4 + channel];
                return (Pixel(tx, ty) * (1 - mixX) + Pixel(tx + 1, ty) * mixX) * (1 - mixY)
                    + (Pixel(tx, ty + 1) * (1 - mixX) + Pixel(tx + 1, ty + 1) * mixX) * mixY;
            }
            var alpha = ((a.Col >> 24) * wa + (b.Col >> 24) * wb + (c.Col >> 24) * wc) / 255 * Sample(3) / 255;
            for (var channel = 0; channel < 3; channel++)
            {
                var color = ((a.Col >> (channel * 8) & 255) * wa + (b.Col >> (channel * 8) & 255) * wb + (c.Col >> (channel * 8) & 255) * wc) * Sample(channel) / 255;
                var dest = (y * screenWidth + x) * 3 + channel;
                output[dest] = (byte)Math.Clamp(color * alpha + output[dest] * (1 - alpha), 0, 255);
            }
        }
    }
}
