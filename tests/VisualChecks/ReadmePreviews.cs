using System.Numerics;
using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe partial class Program
{
    // Documentation only: compact galleries drawn by the actual plugin components.
    private static void ReadmePreviews(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var notifications in new[] { false, true })
        {
            Initialize(notifications ? 680 : 1080, notifications ? 850 : 720, 1);
            for (var frame = 0; frame < 3; frame++)
            {
                ImGui.NewFrame(); ObsidianTheme.Push(ObsidianTheme.Chrome);
                ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(new(screenWidth, screenHeight));
                ImGui.Begin("Documentation", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings);
                var draw = ImGui.GetWindowDrawList();
                draw.AddText(new(24, 20), ObsidianTheme.U(ObsidianTheme.Text), notifications ? "Notifications" : "Six formats de mini HUD");
                draw.AddText(new(24, 45), ObsidianTheme.U(ObsidianTheme.Muted), "Rendu ImGui hors jeu · Données fictives");
                if (notifications)
                {
                    var rows = new[]
                    {
                        (MonitorSkin.LMeter, "idle", "🚀 Préparer la prochaine version", "Projet démo"),
                        (MonitorSkin.Obsidienne, "question", "🎨 Comparer les variantes de l’accueil", "Projet démo"),
                        (MonitorSkin.Nuit, "summary", "2 réponses prêtes · 1 réponse attendue", "Cliquer pour consulter l’historique"),
                    };
                    var y = 86f;
                    foreach (var (skin, state, title, project) in rows)
                    {
                        var appearance = new SurfaceAppearance(); appearance.ApplyPreset(skin, AppearanceTarget.Notification);
                        draw.AddText(new(24, y), ObsidianTheme.U(ObsidianTheme.Muted), skin.ToString());
                        var size = NotificationOverlay.LogicalSize(appearance) * 1.25f;
                        NotificationOverlay.DrawFace(draw, new(24, y + 25), size, 1.25f,
                            new NotificationItem(-1, NotificationOverlay.Example(state) with {Id=state == "summary" ? "quiet-summary" : DemoId,Title=title,Project=project}, 1.5f, 7), appearance);
                        y += size.Y + 54;
                    }
                }
                else
                {
                    var snapshot = new MonitorSnapshot(true, DateTimeOffset.UtcNow,
                        [new("demo1", "Préparer une version", "Démo", "", "active", [new string('a', 32)]),
                         new("demo2", "Vérifier un écran", "Démo", "", "active"),
                         new("demo3", "Livrer un correctif", "Démo", "gpt-6-astra", "idle", HasUnreadTurn:true, LatestTurnStatus:"completed")], null, true,
                        new AccountUsage(DateTimeOffset.UtcNow, [new(48, 10080, null)]));
                    foreach (var style in Enum.GetValues<MiniHudStyle>().Where(style=>(int)style<6))
                    {
                        ImGui.PushID((int)style);
                        var index = (int)style; var p = new Vector2(24 + (index % 2) * 520, 90 + (index / 2) * 170);
                        draw.AddText(p, ObsidianTheme.U(ObsidianTheme.Muted), MiniHudOptions.Names[index]);
                        var appearance = new HudAppearance(); appearance.ApplyPreset(MonitorSkin.Obsidienne, AppearanceTarget.Hud);
                        MiniHud.DrawFace(p + new Vector2(0, 28), MiniHudOptions.Size(style, true, appearance) * 1.25f,
                            snapshot, false, false, 1, style, true, appearance: appearance);
                        ImGui.PopID();
                    }
                }
                ImGui.End(); ObsidianTheme.Pop(); ImGui.Render();
            }
            Render(Path.Combine(output, notifications ? "notifications.ppm" : "mini-huds.ppm"));
            FinishRevisionView();
        }
        Initialize(780,580,1);UiFixture();
        for(var frame=0;frame<3;frame++)
        {
            ImGui.NewFrame();ObsidianTheme.Push(ObsidianTheme.Chrome);
            ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(new(screenWidth,screenHeight));
            ImGui.Begin("Nouveaux HUD",ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoSavedSettings);
            var draw=ImGui.GetWindowDrawList();
            draw.AddText(new(24,20),ObsidianTheme.U(ObsidianTheme.Text),"Ruban · Focus · Tâche épinglée");
            draw.AddText(new(24,45),ObsidianTheme.U(ObsidianTheme.Muted),"Rendu ImGui hors jeu · Données fictives");
            var y=82f;
            foreach(var style in new[]{MiniHudStyle.Ruban,MiniHudStyle.Focus,MiniHudStyle.TacheEpinglee})
            {
                ImGui.PushID((int)style);draw.AddText(new(24,y),ObsidianTheme.U(ObsidianTheme.Muted),MiniHudOptions.Names[(int)style]);
                var size=MiniHudOptions.Size(style,true,plugin.Config.HudAppearance)*1.25f;
                var p=new Vector2(24,y+24);
                MiniHud.DrawFace(p,size,plugin.Snapshot,false,false,1,style,true,appearance:plugin.Config.HudAppearance,pinnedTaskId:DemoId);
                y+=size.Y+51;ImGui.PopID();
            }
            ImGui.End();ObsidianTheme.Pop();ImGui.Render();
        }
        Render(Path.Combine(output,"new-huds.ppm"));FinishRevisionView();
        Console.WriteLine("Rendered three README galleries from the native components.");
    }
}
