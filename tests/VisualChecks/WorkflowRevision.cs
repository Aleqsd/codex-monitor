using System.Numerics;
using System.Reflection;
using System.Text.Json;
using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe partial class Program
{
    private static void WorkflowFixture()
    {
        UseEmojiTasks();
        plugin.Snapshot = plugin.AllTasks with { Threads=plugin.AllTasks.Threads.Select((t,i)=>t with
        {
            ReasoningEffort=i==2 ? "high" : "xhigh", Model=i==2 ? "gpt-5.6-sol" : "gpt-6-astra", ModelSource="turn",
            State=i==3 ? "idle" : t.State, HasUnreadTurn=i==3, LatestTurnStatus=i>=3 ? "completed" : "inProgress",
        }).ToArray() };
        plugin.Config.Following.Favorites.Add(DemoId);plugin.Save();
        plugin.History.Clear();
        plugin.History.Add(plugin.AllTasks.Threads[0] with {State="idle"},DateTimeOffset.Now.AddMinutes(-5));
        plugin.History.Add(plugin.AllTasks.Threads[1] with {State="question"},DateTimeOffset.Now.AddMinutes(-3));
        plugin.History.Add(plugin.AllTasks.Threads[0] with {State="error"},DateTimeOffset.Now.AddMinutes(-1));
    }
    private static void WorkflowRevision(string output)
    {
        Directory.CreateDirectory(output); var checks=0;
        void Check(bool condition,string label) { if(!condition) throw new Exception(label); checks++; }
        foreach(var (width,height,scale) in new[]{(800,760,1f),(560,650,1f),(840,975,1.5f),(1120,1300,2f)})
        {
            Initialize(width,height,scale); WorkflowFixture();
            for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,$"tasks-{width}.ppm"));
            window.SelectTasks(4);for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,$"ready-{width}.ppm"));
            window.SelectTasks(0);
            window.ShowHistory=true;for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,$"history-{width}.ppm"));
            window.ShowHistory=false;window.ShowSettings=true;Panel.Category=6;
            plugin.Config.Following.AllProjects=false;plugin.Config.Following.Projects=[plugin.AllTasks.Threads[0].ProjectKey];
            plugin.MuteTask(DemoId,30);for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,$"following-{width}.ppm"));
            Check(plugin.Snapshot.Threads.All(t=>t.ProjectKey==plugin.AllTasks.Threads[0].ProjectKey),"Project scope affects real view");
            Panel.Category=3;
            typeof(ConnectionDiagnostics).GetField("report",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin.Diagnostics,
                new DiagnosticReport(false,DateTimeOffset.UtcNow,[new("Node.js",true,"Version compatible (22.22.2 minimum)"),new("Relais",true,"Relais reconnu"),new("Codex",true,"Application connectée"),new("Quota / CLI",false,"Connexion au compte requise dans le CLI Codex","Se connecter avec codex login dans un terminal")]));
            for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,$"diagnostic-{width}.ppm"));
            ImGui.GetIO().AddMousePosEvent(width/2,height-100*scale);Frame();ImGui.GetIO().AddMouseWheelEvent(0,-14);
            for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,$"quota-settings-{width}.ppm"));
            FinishRevisionView();
        }
        foreach(var scale in new[]{1f,1.5f,2f}) foreach(var style in Enum.GetValues<MiniHudStyle>())
        {
            Initialize((int)(700*scale),(int)(340*scale),scale);WorkflowFixture();overlayOnly=hudOnly=true;
            plugin.Config.Indicator=IndicatorMode.MiniHud;plugin.Config.HudStyle=style;
            plugin.Config.HudAppearance!.ApplyPreset(MonitorSkin.LMeter,AppearanceTarget.Hud);
            plugin.Config.HudAppearance.Text.OffsetX=-3; plugin.Config.HudAppearance.Text.OffsetY=2;
            plugin.Config.MiniHudAnchorY=.25f;
            for(var i=0;i<3;i++)Frame();
            var hits=plugin.Hud.Hits.ToArray();
            Check(hits.Select(h=>h.Target).Distinct().Count()==4,$"Four actions on {style} at {scale}");
            foreach(var hit in hits)
            {
                var center=(hit.Min+hit.Max)/2;
                Check(center.X>=0&&center.X<screenWidth&&center.Y>=0&&center.Y<screenHeight,"HUD action inside viewport");
                plugin.LastTaskFilter=-1;plugin.QuotaOpened=false;
                Click(center.X,center.Y);
                Check(hit.Target switch {HudTarget.Active=>plugin.LastTaskFilter==1,HudTarget.Ready=>plugin.LastTaskFilter==4,HudTarget.Attention=>plugin.LastTaskFilter==2,HudTarget.Quota=>plugin.QuotaOpened,_=>false},$"Actual {hit.Target} click on {style} at {scale}");
            }
            ImGui.GetIO().AddMousePosEvent(1,screenHeight-1);Frame();Render(Path.Combine(output,$"hud-{style}-{scale:0.0}.ppm"));
            var bell=PauseButtonCenter();Click(bell.X,bell.Y);Check(ImGui.GetCurrentContext().OpenPopupStack.Size==1,"Pause remains reachable beside clickable HUD");
            FinishRevisionView();
        }
        Initialize(800,760,1);WorkflowFixture();for(var i=0;i<3;i++)Frame();
        Click(130,250);Render(Path.Combine(output,"task-menu.ppm"));
        Click(170,346);Check(!plugin.Config.Following.Favorites.Contains(DemoId),"Native menu removes favorite");
        Click(130,250);Click(170,371);Check(plugin.Config.Following.Muted.Any(m=>m.Id==DemoId),"Native menu silences a task");
        Click(130,250);Click(170,371);Check(plugin.Config.Following.Muted.Count==0,"Native menu resumes task alerts");
        Click(130,250);Click(170,346);Check(plugin.Config.Following.Favorites.Contains(DemoId),"Native menu adds favorite");
        Click(275,135);Check((int)typeof(MainWindow).GetField("stateFilter",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)! == 4,"Ready summary selects ready view");
        FinishRevisionView();
        Initialize(800,450,1);overlayOnly=true;plugin.Config.NotificationReducedMotion=true;
        plugin.NotificationUi.Queue.Add(new QuotaNotice("10080:demo",10,new(8,10080,DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds())).Task(DateTimeOffset.UtcNow),10);
        for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,"quota-toast.ppm"));
        Click(250,212);Check(plugin.OpenCount==1&&plugin.OpenedLinks.Count==0,"Quota toast routes locally without launching a task");FinishRevisionView();
        WorkflowConfigCheck();checks+=3;
        Console.WriteLine($"PASS {checks} workflow native checks; 44 previews, 72 HUD action clicks and migration.");
    }
    private static void WorkflowConfigCheck()
    {
        var serializer=Assembly.LoadFrom(Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME")!,"Newtonsoft.Json.dll")).GetType("Newtonsoft.Json.JsonConvert")!;
        var deserialize=serializer.GetMethod("DeserializeObject",[typeof(string),typeof(Type)])!;
        var serialize=serializer.GetMethod("SerializeObject",[typeof(object)])!;
        var config=(Configuration)deserialize.Invoke(null,["{\"MiniHudAnchorX\":0.21,\"NotificationSeconds\":9,\"AutoStartRelay\":true}",typeof(Configuration)])!;
        config.Normalize();if(config.MiniHudAnchorX!=.21f||config.NotificationSeconds!=9||!config.AutoStartRelay||!config.Following.AllProjects)throw new Exception("Legacy settings preserved");
        config.Following.Favorites=[DemoId];config.Following.Mute(DemoId,30,DateTimeOffset.UtcNow);config.Following.AllProjects=false;config.Following.Projects=["project-key"];
        config.QuotaAlerts.Checkpoints=[new("10080:123456",[20,10])];
        var roundtrip=(Configuration)deserialize.Invoke(null,[serialize.Invoke(null,[config])!,typeof(Configuration)])!;roundtrip.Normalize();
        if(roundtrip.Following.AllProjects||roundtrip.Following.Favorites.Single()!=DemoId||roundtrip.Following.Muted.Count!=1)throw new Exception("Following settings roundtrip");
        if(roundtrip.QuotaAlerts.Checkpoints.Single().Triggered.Length!=2)throw new Exception("Quota checkpoints roundtrip");
    }
}
