using System.Numerics;
using System.Reflection;
using System.Text.Json;
using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe partial class Program
{
    private static void UiFixture()
    {
        WorkflowFixture();
        plugin.Config.Indicator=IndicatorMode.MiniHud;plugin.Config.HudStyle=MiniHudStyle.Focus;
        plugin.Config.PinnedHudTaskId=DemoId;plugin.Config.MiniHudOpacity=1;
        plugin.Config.HudAppearance!.ApplyPreset(MonitorSkin.Obsidienne,AppearanceTarget.Hud);
        plugin.Config.ToastAppearance!.ApplyPreset(MonitorSkin.Obsidienne,AppearanceTarget.Notification);
        plugin.Snapshot=plugin.AllTasks with {Threads=plugin.AllTasks.Threads.Select(t=>t.QuestionIds.Length==0 ? t : t with
        {QuestionPreviews=t.QuestionIds.Select(id=>new QuestionPreview(id,"Préfères-tu un aperçu compact ou une liste détaillée des réponses ?")).ToArray()}).ToArray()};
    }

    private static void UiRevision(string output)
    {
        Directory.CreateDirectory(output);var checks=0;var images=0;
        void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
        void Capture(string name){ImGui.GetIO().AddMousePosEvent(1,screenHeight-1);for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,name+".ppm"));images++;}
        foreach(var (width,height,scale) in new[]{(1000,780,1f),(560,650,1f),(840,975,1.5f),(1120,1300,2f)})
        {
            Initialize(width,height,scale);UiFixture();Capture($"tasks-{width}");
            Check(plugin.Snapshot.Threads.Any(t=>t.InterventionLabel=="Question posée"&&t.Label=="En cours"),"Running question has two independent labels");
            window.ShowSettings=true;
            foreach(var category in new[]{0,1,2,6,4,5,3})
            {Panel.Category=category;Capture($"settings-{category}-{width}");}
            Panel.Category=0;plugin.Config.HudStyle=MiniHudStyle.Ruban;Capture($"settings-ruban-{width}");
            // The form scrolls independently while the preview stays visible.
            ImGui.GetIO().AddMousePosEvent(width-40*scale,height-100*scale);Frame();ImGui.GetIO().AddMouseWheelEvent(0,-45);
            Capture($"settings-scrolled-{width}");
            Check(ImGui.GetCurrentContext().CurrentWindowStack.Size==0,"Balanced native window stack");
            FinishRevisionView();
        }
        foreach(var scale in new[]{1f,1.5f,2f}) foreach(var style in Enum.GetValues<MiniHudStyle>())
        {
            Initialize((int)(820*scale),(int)(610*scale),scale);UiFixture();overlayOnly=hudOnly=true;
            plugin.Config.HudStyle=style;plugin.Config.MiniHudAnchorY=.05f;
            for(var i=0;i<3;i++)Frame();
            var hits=plugin.Hud.Hits.GroupBy(h=>h.Target).Select(g=>g.Last()).ToArray();
            Check(hits.Any(h=>h.Target==HudTarget.Active)&&hits.Any(h=>h.Target==HudTarget.Ready)&&hits.Any(h=>h.Target==HudTarget.Quota),$"Essential actions on {style}");
            foreach(var hit in hits)
            {
                var center=(hit.Min+hit.Max)/2;
                Check(center.X>=0&&center.X<screenWidth&&center.Y>=0&&center.Y<screenHeight,"Hit region inside viewport");
                Click(center.X,center.Y);
                Check(plugin.Hud.PeekTarget==hit.Target&&ImGui.GetCurrentContext().OpenPopupStack.Size>0,$"Peek opens for {style}/{hit.Target}/{scale}");
                Check(plugin.OpenCount==0&&plugin.OpenedLinks.Count==0,"Peek does not open Codex or main window automatically");
                if(style==MiniHudStyle.Focus&&hit.Target==HudTarget.Ready&&scale==1) Capture("peek-ready");
                Click(screenWidth-2,screenHeight-2);
                Check(plugin.Hud.PeekTarget is null,"Click outside closes peek");
            }
            Capture($"hud-{style}-{scale*100:0}");
            var bell=PauseButtonCenter();Click(bell.X,bell.Y);
            Check(ImGui.GetCurrentContext().OpenPopupStack.Size==1,"Pause menu is reachable");
            Click(screenWidth-2,screenHeight-2);
            var active=plugin.Hud.Hits.Last(h=>h.Target==HudTarget.Active);var point=(active.Min+active.Max)/2;
            plugin.Config.HudQuickPeek=false;Click(point.X,point.Y);
            Check(plugin.LastTaskFilter==1,"Direct list remains an option");
            FinishRevisionView();
        }
        foreach(var style in new[]{MiniHudStyle.Ruban,MiniHudStyle.Focus,MiniHudStyle.TacheEpinglee})
        {
            Initialize(820,350,1);UiFixture();overlayOnly=hudOnly=true;plugin.Config.HudStyle=style;
            var initial=plugin.Snapshot;
            foreach(var state in new[]{"offline","idle","ready","unknown","large","paused"})
            {
                plugin.Snapshot=state=="offline"?MonitorSnapshot.Offline("Relais absent"):state=="idle"?initial with{Threads=[]}
                    :state=="ready"?initial with{Threads=initial.Threads.Select(t=>t with{State="idle",PendingQuestionIds=null,HasUnreadTurn=true}).ToArray()}
                    :state=="unknown"?initial with{Usage=null,TaskMetadataSupported=false,Threads=initial.Threads.Select(t=>t with{HasUnreadTurn=null}).ToArray()}
                    :state=="large"?initial with{Threads=Enumerable.Range(0,200).Select(i=>initial.Threads[0] with{Id=i.ToString(),HasUnreadTurn=true}).ToArray()}:initial;
                if(state=="paused")plugin.PauseAlerts(30);
                Capture($"hud-{style}-{state}");
            }
            FinishRevisionView();
        }
        foreach(var skin in Enum.GetValues<MonitorSkin>()) foreach(var scale in new[]{1f,1.5f,2f})
        {
            Initialize((int)(650*scale),(int)(300*scale),scale);UiFixture();overlayOnly=true;plugin.Config.NotificationReducedMotion=true;
            plugin.Config.ToastAppearance!.ApplyPreset(skin,AppearanceTarget.Notification);
            plugin.NotificationUi.Queue.Add(NotificationOverlay.Example("question"),7);Capture($"question-{skin}-{scale*100:0}");
            plugin.Config.ShowQuestionExcerpts=false;Capture($"question-hidden-{skin}-{scale*100:0}");
            FinishRevisionView();
        }
        Initialize(1000,780,1);UiFixture();overlayOnly=hudOnly=true;
        for(var i=0;i<3;i++)Frame();var ready=plugin.Hud.Hits.First(h=>h.Target==HudTarget.Ready);Click((ready.Min.X+ready.Max.X)/2,(ready.Min.Y+ready.Max.Y)/2);
        var link=plugin.Hud.PeekLinks.First();Click((link.Min.X+link.Max.X)/2,(link.Min.Y+link.Max.Y)/2);
        var start=DateTime.UtcNow;while(plugin.TaskLink.Busy&&DateTime.UtcNow-start<TimeSpan.FromSeconds(2))System.Threading.Thread.Yield();
        Check(plugin.OpenedLinks.Count==1&&plugin.OpenedLinks[0].ToString()==CodexTaskLink.Build(link.Id)!.ToString(),"Explicit peek button opens the precise task");
        Check(plugin.Snapshot.Threads.First(t=>t.Id==link.Id).HasUnreadTurn==true,"Opening link never locally clears Codex unread state");
        FinishRevisionView();
        UiConfigChecks();checks+=5;
        Initialize(1000,780,1);UiFixture();window.ShowSettings=true;
        for(var i=0;i<3;i++)Frame();
        foreach(var category in new[]{1,2,6,4,5,3,0})
        {
            var control=Panel.Controls["nav-"+category];Click((control.Min.X+control.Max.X)/2,(control.Min.Y+control.Max.Y)/2);
            Check(Panel.Category==category,"Sidebar navigation uses real native controls");
        }
        var toggle=Panel.Controls["hud-peek"];Click((toggle.Min.X+toggle.Max.X)/2,(toggle.Min.Y+toggle.Max.Y)/2);
        Check(!plugin.Config.HudQuickPeek&&plugin.SaveCount>0,"Native preview preference persists");
        Panel.Category=3;Capture("settings-fixed-before");
        plugin.Config.WindowAppearance!.ApplyPreset(MonitorSkin.Nuit,AppearanceTarget.Window);plugin.Config.WindowAppearance.Text.Size=24;
        plugin.Config.WindowAppearance.Text.OffsetX=20;plugin.Config.WindowAppearance.Text.Font=MonitorFont.LocalFile;
        plugin.Config.HudAppearance!.ApplyPreset(MonitorSkin.LMeter,AppearanceTarget.Hud);plugin.Config.ToastAppearance!.Opacity=0;
        Capture("settings-fixed-after");
        Check(File.ReadAllBytes(Path.Combine(output,"settings-fixed-before.ppm")).SequenceEqual(File.ReadAllBytes(Path.Combine(output,"settings-fixed-after.ppm"))),"Settings pixels unaffected by functional appearance choices");
        FinishRevisionView();
        Initialize(1000,780,1);UiFixture();for(var i=0;i<3;i++)Frame();
        Click(130,250);Capture("task-pin-menu");Click(170,371);
        Check(plugin.Config.PinnedHudTaskId is null,"Task menu detaches existing pin");
        Click(130,250);Click(170,371);Check(plugin.Config.PinnedHudTaskId==DemoId,"Task menu pins exact task");
        Check(plugin.Config.HudStyle==MiniHudStyle.TacheEpinglee&&plugin.Config.Indicator==IndicatorMode.MiniHud,"Pin action displays the pinned HUD");
        FinishRevisionView();
        foreach(var anchor in new[]{new Vector2(0,0),new Vector2(1,1)})
        {
            Initialize(560,490,1);UiFixture();overlayOnly=hudOnly=true;plugin.Config.HudStyle=MiniHudStyle.Ruban;
            plugin.Config.MiniHudScale=1.5f;plugin.Config.MiniHudAnchorX=anchor.X;plugin.Config.MiniHudAnchorY=anchor.Y;
            for(var i=0;i<3;i++)Frame();var hit=plugin.Hud.Hits.First(h=>h.Target==HudTarget.Ready);Click((hit.Min.X+hit.Max.X)/2,(hit.Min.Y+hit.Max.Y)/2);
            Capture($"peek-edge-{anchor.Y:0}");
            var pop=new ImGuiWindowPtr(ImGui.GetCurrentContext().OpenPopupStack[0].Window);
            Check(pop.Pos.X>=0&&pop.Pos.Y>=0&&pop.Pos.X+pop.Size.X<=screenWidth+1&&pop.Pos.Y+pop.Size.Y<=screenHeight+1,"Popup fits viewport at either HUD edge");
            FinishRevisionView();
        }
        Initialize(800,650,1);UiFixture();
        var performance=new List<object>();
        foreach(var count in new[]{20,200})
        {
            plugin.Snapshot=plugin.Snapshot with{Threads=Enumerable.Range(0,count).Select(i=>new MonitoredThread($"11111111-2222-4333-8444-{i:000000000000}",$"🎨 {i:000} "+new string('a',1492),"Projet fictif","gpt-6-astra","active",ReasoningEffort:"xhigh")).ToArray()};
            for(var i=0;i<40;i++)Frame();
            var allocated=GC.GetAllocatedBytesForCurrentThread();var times=new List<double>();
            for(var i=0;i<120;i++){var startTicks=System.Diagnostics.Stopwatch.GetTimestamp();Frame();times.Add(System.Diagnostics.Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds);}
            allocated=(GC.GetAllocatedBytesForCurrentThread()-allocated)/120;times.Sort();
            Check(allocated<500000,"Long titles keep bounded per-frame allocations");
            performance.Add(new{tasks=count,medianMs=times[60],p95Ms=times[114],allocatedBytesPerFrame=allocated});
        }
        File.WriteAllText(Path.Combine(output,"performance.json"),JsonSerializer.Serialize(performance,new JsonSerializerOptions{WriteIndented=true}));
        FinishRevisionView();
        Console.WriteLine($"PASS {checks} native UI checks; {images} ImGui previews. Launches simulated; game untouched.");
    }

    private static void UiConfigChecks()
    {
        var serializer=Assembly.LoadFrom(Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME")!,"Newtonsoft.Json.dll")).GetType("Newtonsoft.Json.JsonConvert")!;
        var deserialize=serializer.GetMethod("DeserializeObject",[typeof(string),typeof(Type)])!;
        var serialize=serializer.GetMethod("SerializeObject",[typeof(object)])!;
        var old=(Configuration)deserialize.Invoke(null,["{\"HudStyle\":5,\"MiniHudAnchorX\":0.24,\"NotificationSeconds\":11}",typeof(Configuration)])!;old.Normalize();
        if(old.HudStyle!=MiniHudStyle.ObsidienneFine||old.MiniHudAnchorX!=.24f||old.NotificationSeconds!=11)throw new Exception("Saved HUD/position/duration changed");
        if(Configuration.NewInstall().HudStyle!=MiniHudStyle.Focus)throw new Exception("New installation default is Focus");
        old.PinnedHudTaskId=DemoId;old.HudStyle=MiniHudStyle.TacheEpinglee;old.HudQuickPeek=false;old.ShowQuestionExcerpts=false;
        var restored=(Configuration)deserialize.Invoke(null,[serialize.Invoke(null,[old])!,typeof(Configuration)])!;restored.Normalize();
        if(restored.PinnedHudTaskId!=DemoId||restored.HudStyle!=MiniHudStyle.TacheEpinglee||restored.HudQuickPeek||restored.ShowQuestionExcerpts)throw new Exception("New preferences do not roundtrip");
        var history=new NotificationHistory();history.Add(NotificationOverlay.Example("question"),DateTimeOffset.UtcNow);restored.NotificationHistory=history.Entries.ToList();
        if(((string)serialize.Invoke(null,[restored])!).Contains("Préfères-tu"))throw new Exception("Question excerpt persisted in config");
        restored.PinnedHudTaskId="unexpected";restored.Normalize();if(restored.PinnedHudTaskId is not null)throw new Exception("Invalid pin accepted");
    }
}
