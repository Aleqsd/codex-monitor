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
        plugin.Config.HudClickAction=HudClickAction.TaskPreview;
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
            Initialize(width,height,scale);UiFixture();plugin.Config.HudClickAction=HudClickAction.Settings;Capture($"tasks-{width}");
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
            var hits=plugin.Hud.Hits.Where(h=>h.Target!=HudTarget.Pause).GroupBy(h=>h.Target).Select(g=>g.Last()).ToArray();
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
            var popup=new ImGuiWindowPtr(ImGui.GetCurrentContext().OpenPopupStack[0].Window);
            Click(popup.Pos.X+30*scale,popup.Pos.Y+popup.Size.Y-10*scale-ImGui.GetFontSize()/2);
            Check(plugin.ManualQuiet.Enabled&&plugin.OpenCount==0,"Integrated bell pauses without opening settings");
            if(scale==1) Capture($"hud-paused-{style}");
            Click(bell.X,bell.Y);
            Check(!plugin.ManualQuiet.Enabled&&plugin.OpenCount==0,"Integrated bell resumes without opening settings");
            var cfg=plugin.Config;var viewport=ImGui.GetMainViewport();
            var basis=MiniHudOptions.Size(style,cfg.ShowUsage,cfg.HudAppearance);
            var faceSize=basis*MiniHudOptions.FitScale(basis,cfg.MiniHudScale*scale,viewport.Size);
            var facePosition=NotificationGeometry.Place(new(cfg.MiniHudAnchorX,cfg.MiniHudAnchorY),viewport.Pos,viewport.Size,faceSize,0,1,0);
            var bellHit=plugin.Hud.Hits.Single(h=>h.Target==HudTarget.Pause);
            Check(bellHit.Min.X>=facePosition.X&&bellHit.Min.Y>=facePosition.Y&&bellHit.Max.X<=facePosition.X+faceSize.X+.1f&&bellHit.Max.Y<=facePosition.Y+faceSize.Y+.1f,"Bell is entirely inside the HUD face");
            plugin.Config.HudClickAction=HudClickAction.Settings;
            foreach(var hit in hits)
            {
                var before=plugin.OpenCount;var point=(hit.Min+hit.Max)/2;Click(point.X,point.Y);
                Check(plugin.ConfigOpened&&plugin.OpenCount==before+1&&plugin.Hud.PeekTarget is null,$"Settings opens on {style}/{hit.Target}/{scale}, including quota and pinned title");
            }
            var openBefore=plugin.OpenCount;Click(facePosition.X+2*scale,facePosition.Y+2*scale);
            Check(plugin.OpenCount==openBefore+1,"HUD background also opens settings");
            FinishRevisionView();
        }
        foreach(var style in Enum.GetValues<MiniHudStyle>())
        {
            Initialize(820,610,1);UiFixture();overlayOnly=hudOnly=true;
            var cfg=plugin.Config;cfg.HudClickAction=HudClickAction.Settings;cfg.HudStyle=style;cfg.ShowUsage=false;
            cfg.HudAppearance!.Background=HudBackgroundMode.Visible;cfg.HudAppearance.PaddingX=8;cfg.HudAppearance.PaddingY=6;
            cfg.HudAppearance.Text.Size=20;cfg.HudAppearance.Text.OffsetX=-5;cfg.HudAppearance.Text.OffsetY=3;
            Capture($"hud-custom-no-quota-{style}");
            var viewport=ImGui.GetMainViewport();var basis=MiniHudOptions.Size(style,false,cfg.HudAppearance);
            var size=basis*MiniHudOptions.FitScale(basis,cfg.MiniHudScale,viewport.Size);
            var p=NotificationGeometry.Place(new(cfg.MiniHudAnchorX,cfg.MiniHudAnchorY),viewport.Pos,viewport.Size,size,0,1,0);
            var bell=plugin.Hud.Hits.Single(h=>h.Target==HudTarget.Pause);
            Check(bell.Min.X>=p.X&&bell.Min.Y>=p.Y&&bell.Max.X<=p.X+size.X+.1f&&bell.Max.Y<=p.Y+size.Y+.1f,"Custom padding and text keep the bell inside the HUD without quota");
            Check(plugin.Hud.Hits.Where(h=>h.Target!=HudTarget.Pause).All(h=>h.Max.X<=bell.Min.X||h.Min.X>=bell.Max.X||h.Max.Y<=bell.Min.Y||h.Min.Y>=bell.Max.Y),"Pause hit never overlaps another HUD counter");
            plugin.Hud.SetEditing(true);Frame();var io=ImGui.GetIO();var origin=(bell.Min+bell.Max)/2;
            var beforeX=cfg.MiniHudAnchorX;var beforeY=cfg.MiniHudAnchorY;
            io.AddMousePosEvent(origin.X,origin.Y);Frame();io.AddMouseButtonEvent(0,true);Frame();
            io.AddMousePosEvent(origin.X+38,origin.Y+24);Frame();io.AddMouseButtonEvent(0,false);Frame();
            Check(cfg.MiniHudAnchorX!=beforeX||cfg.MiniHudAnchorY!=beforeY,"Edit mode can drag the whole HUD from the integrated bell");
            Check(plugin.OpenCount==0&&!plugin.ManualQuiet.Enabled&&ImGui.GetCurrentContext().OpenPopupStack.Size==0,"Dragging does not open settings or activate pause");
            io.AddMouseButtonEvent(1,true);Frame();io.AddMouseButtonEvent(1,false);Frame();
            Check(!plugin.Hud.Editing,"Right click still locks HUD placement");
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
        UiConfigChecks();checks+=8;
        Initialize(1000,780,1);UiFixture();window.ShowSettings=true;
        for(var i=0;i<3;i++)Frame();
        foreach(var category in new[]{1,2,6,4,5,3,0})
        {
            var control=Panel.Controls["nav-"+category];Click((control.Min.X+control.Max.X)/2,(control.Min.Y+control.Max.Y)/2);
            Check(Panel.Category==category,"Sidebar navigation uses real native controls");
        }
        var choice=Panel.Controls["hud-click"];Click((choice.Min.X+choice.Max.X)/2,(choice.Min.Y+choice.Max.Y)/2);
        var choices=new ImGuiWindowPtr(ImGui.GetCurrentContext().OpenPopupStack[0].Window);
        Click(choices.Pos.X+30,choices.Pos.Y+ImGui.GetStyle().WindowPadding.Y+ImGui.GetFontSize()/2);
        Check(plugin.Config.HudClickAction==HudClickAction.Settings&&plugin.SaveCount>0,"Native HUD click preference persists");
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
        old.PinnedHudTaskId=DemoId;old.HudStyle=MiniHudStyle.TacheEpinglee;old.HudClickAction=HudClickAction.TaskPreview;old.ShowQuestionExcerpts=false;
        var restored=(Configuration)deserialize.Invoke(null,[serialize.Invoke(null,[old])!,typeof(Configuration)])!;restored.Normalize();
        if(restored.PinnedHudTaskId!=DemoId||restored.HudStyle!=MiniHudStyle.TacheEpinglee||restored.HudClickAction!=HudClickAction.TaskPreview||restored.ShowQuestionExcerpts)throw new Exception("New preferences do not roundtrip");
        var previous=(Configuration)deserialize.Invoke(null,["{\"HudQuickPeek\":true,\"HudStyle\":8,\"MiniHudAnchorX\":0.24}",typeof(Configuration)])!;previous.Normalize();
        if(previous.HudClickAction!=HudClickAction.Settings||previous.HudStyle!=MiniHudStyle.TacheEpinglee||previous.MiniHudAnchorX!=.24f)throw new Exception("0.11.0 migration must restore settings click without changing HUD or anchor");
        if(Configuration.NewInstall().HudClickAction!=HudClickAction.Settings)throw new Exception("New installs must open settings on HUD click");
        previous.HudClickAction=(HudClickAction)99;previous.Normalize();if(previous.HudClickAction!=HudClickAction.Settings)throw new Exception("Invalid click preference not normalized");
        var history=new NotificationHistory();history.Add(NotificationOverlay.Example("question"),DateTimeOffset.UtcNow);restored.NotificationHistory=history.Entries.ToList();
        if(((string)serialize.Invoke(null,[restored])!).Contains("Préfères-tu"))throw new Exception("Question excerpt persisted in config");
        restored.PinnedHudTaskId="unexpected";restored.Normalize();if(restored.PinnedHudTaskId is not null)throw new Exception("Invalid pin accepted");
    }
}
