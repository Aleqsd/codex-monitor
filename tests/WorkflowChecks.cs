using System.Text.Json;
using CodexMonitor;

internal static class WorkflowChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
        var now = DateTimeOffset.UtcNow;
        var id = "11111111-2222-4333-8444-555555555555";
        var task = new MonitoredThread(id, "Tâche fictive", "Projet démo", "gpt-6-astra", "active", ReasoningEffort:"xhigh", ProjectId:"project-a");
        var snapshot = new MonitorSnapshot(true, now, [task], null, true);
        Check(snapshot.ReadyCount == "—", "Older relay read state stays unknown rather than zero");
        Check((snapshot with {TaskMetadataSupported=true}).ReadyCount == "0", "New relay can confirm zero ready responses");
        string Payload(object unread, object effort, int age = 0) => JsonSerializer.Serialize(new { schemaVersion=1, connected=true, generatedAt=now,
            threads=new[]{new {id,title="Demo",project="C:/work/demo",model="gpt-6-astra",state="idle",availability="live",lastConfirmedAt=now.AddSeconds(-age),
                hasUnreadTurn=unread,reasoningEffort=effort,latestTurnStatus="completed",modelSource="turn"}}});
        var ready = MonitorContract.Parse(Payload(true,"xhigh"),now).Threads.Single();
        Check(ready.ResponseReady && ready.HasUnreadTurn == true && ready.ModelLabel == "gpt-6-astra · xhigh", "Exact model, effort and unread flag parsed");
        Check(ready.Label == "Réponse prête" && !MonitorContract.Parse(Payload(false,"high"),now).Threads.Single().ResponseReady, "Codex read clears blue ready state");
        Check(MonitorContract.Parse(Payload("true",new {unexpected="value"}),now).Threads.Single() is {HasUnreadTurn:null,ReasoningEffort:null}, "Malformed optional metadata remains unknown");
        Check(MonitorContract.Parse(Payload(true,"xhigh",31),now).Ready == 0, "Stale owner cannot supply a blue dot");
        Check(ready.ProjectKey != ready.Project && ready.ProjectKey.Length == 64, "Project identity distinguishes same names without exporting path");
        var options = new FollowingOptions {AllProjects=false,Projects=["project-a"],Favorites=[id]};
        var follow = new TaskFollowing();
        snapshot = snapshot with {Threads=[task,task with {Id="other",ProjectId="project-b"}]};
        var selected = follow.Apply(snapshot, options);
        Check(selected.Active == 1 && selected.Threads.Single().IsFavorite, "Scope applies to counters and favorites");
        Check(ReferenceEquals(selected,follow.Apply(snapshot,options)),"Unchanged projection reused");
        options.Mute(id,30,now); Check(!options.Allows(task,now.AddMinutes(29)) && options.Allows(task,now.AddMinutes(30)), "Mute expires and does not remove tasks");
        options.AllProjects=true;follow.Invalidate();Check(follow.Apply(snapshot,options).Active==2,"Project selection reversible");
        var projection=new TaskListProjection();
        Check(projection.Get(selected,false,false,5,"").Length==1,"Favorites view includes favorite");
        Check(projection.Get(snapshot with {Threads=[ready]},false,false,4,"").Length==1,"Ready view remains visible with idle hidden");
        Check(projection.Get(snapshot with {Threads=[ready]},true,false,1,"").Length==0,"Ready task never counted as running");

        var queue=new NotificationQueue();var history=new NotificationHistory();var sounds=new List<string>();
        var center=new NotificationCenter(history,queue,sounds.Add);
        var active=snapshot with {Threads=[task]};var done=active with {Threads=[task with {State="idle",HasUnreadTurn=true}]};
        center.Update(active,false,true,true,7,now,true,true);
        center.Update(done,false,true,true,7,now.AddSeconds(1),true,true);
        Check(queue.Count==0&&history.Entries.Count==1,"Burst waits while preserving history");
        center.Update(done,false,true,true,7,now.AddSeconds(3),true,true);
        Check(queue.Count==1&&sounds.Count==1,"Burst flushed once even without new snapshot");
        center.Update(done with {Threads=[done.Threads[0] with {HasUnreadTurn=false}]},false,true,true,7,now.AddSeconds(4),true,true);
        Check(queue.Count==0,"Opening Codex removes completed toast via real read state");
        center.Update(active,false,true,true,7,now.AddSeconds(5),true,true);
        center.Update(done,false,true,true,7,now.AddSeconds(6),true,true);
        center.Update(active,false,true,true,7,now.AddSeconds(7),true,true);
        center.Update(active,false,true,true,7,now.AddSeconds(9),true,true);
        Check(queue.Count==0,"Resumed work removes obsolete completion before burst flush");
        center.Update(done,false,true,true,7,now.AddSeconds(10),true,true,_=>false);
        Check(queue.Count==0&&history.Entries.Count==3,"Muted tasks retain history without alerts");
        center.Update(active,false,true,true,7,now.AddSeconds(11),true,true);
        center.Update(active with {Threads=[task with {State="error"}]},false,true,true,7,now.AddSeconds(12),true,true);
        Check(queue.Visible()[0].Task.State=="error", "Error bypasses burst delay");
        queue.Add(task with {Id="completion",State="idle"},7);queue.Add(task with {Id="question",State="question"},7);
        Check(queue.Visible()[0].Task.State=="error"&&queue.Visible()[1].Task.State=="question","Attention prioritized over completions");
        var burstQueue=new NotificationQueue(); var burstHistory=new NotificationHistory(); var burstCenter=new NotificationCenter(burstHistory,burstQueue);
        var q1=task with {PendingQuestionIds=[new('a',32)]}; var q2=q1 with {PendingQuestionIds=[new('a',32),new('b',32)]};
        burstCenter.Update(active,false,true,true,7,now,true,true);
        burstCenter.Update(active with {Threads=[q1]},false,true,true,7,now.AddSeconds(1),true,true);
        var qs=active with {Threads=[q2]};burstCenter.Update(qs,false,true,true,7,now.AddSeconds(2),true,true);
        burstCenter.Update(qs,false,true,true,7,now.AddSeconds(4),true,true);
        Check(burstQueue.Count==1&&burstQueue.Visible()[0].Events==2,"Two questions coalesced with event count");
        burstCenter.BeginManualPause();Check(burstQueue.Count==0,"DND drains grouped toast");

        var quota = new QuotaAlerts();var config=new QuotaAlertOptions();
        var reset=now.AddDays(4).ToUnixTimeSeconds();
        MonitorSnapshot Usage(double left,long? periodReset=null) => active with {Usage=new(now,[new(left,10080,periodReset??reset)])};
        Check(quota.Update(Usage(48),config,false,false,now).Count==0,"No quota alert on initial connection");
        Check(quota.Update(Usage(19),config,false,false,now).Single().Threshold==20,"Quota threshold crossing");
        Check(quota.Update(Usage(18),config,false,false,now).Count==0,"Quota threshold never repeated");
        Check(quota.Update(Usage(4),config,true,false,now).Count==0,"Multiple crossings deferred silently");
        var notices=quota.Update(Usage(3),config,false,false,now);
        Check(notices.Count==1&&notices[0].Threshold==5&&notices[0].Window.RemainingPercent==3,"One current lowest quota alert after pause");
        var reload = new QuotaAlerts();Check(reload.Update(Usage(3),config,false,false,now).Count==0,"Saved checkpoint survives reload");
        Check(quota.Update(Usage(90,reset+10080*60),config,false,false,now).Count==0,"New period baseline silent");
        Check(quota.Update(Usage(15,reset+10080*60),config,false,false,now).Count==1,"New period rearms thresholds");
        var outside=new QuotaAlerts();outside.Update(Usage(60),new QuotaAlertOptions(),false,false,now);
        Check(outside.Update(Usage(4),new QuotaAlertOptions(),true,true,now).Count==0,"No quota replay from title screen");
        var stale=Usage(2) with {Usage=new(now.AddMinutes(-3),[new(2,10080,reset)])};
        Check(quota.Update(stale,config,false,false,now).Count==0,"Stale quota produces no warning");
        var reconnect=new QuotaAlerts();var reconnectOptions=new QuotaAlertOptions();reconnect.Update(Usage(60),reconnectOptions,false,false,now);
        reconnect.Update(MonitorSnapshot.Offline("Offline"),reconnectOptions,false,false,now);
        Check(reconnect.Update(Usage(4),reconnectOptions,false,false,now).Count==0,"Reconnection establishes a quota baseline without replay");
        Check(QuotaNotice.ResetLabel(reset,now).Contains("j"),"Quota alert carries reset countdown");
        history.Add(task with {State="question"},now);history.Add(task with {State="quota"},now);
        Check(HistoryQuery.Select(history.Entries,"démo",HistoryFilter.Questions,false,history,active).Length==1,"History project search and question filter combine");
        Check(HistoryQuery.Select(history.Entries,"",HistoryFilter.Quota,false,history,active).Length==1,"Quota history filter");
        var diagnostic=new DiagnosticReport(false,now,[new("Codex",true,"Application connectée"),new("Quota / CLI",false,"Connexion requise","codex login")]);
        Check(!diagnostic.CopyText().Contains(task.Title)&&!diagnostic.CopyText().Contains(task.Project)&&diagnostic.CopyText().Contains("codex login"),"Shareable diagnosis is bounded to technical data");
        return checks;
    }
}
