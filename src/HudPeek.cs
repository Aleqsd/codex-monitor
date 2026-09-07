using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CodexMonitor;

internal sealed partial class MiniHud
{
    internal HudTarget? PeekTarget { get; private set; }
    internal readonly List<(string Id, Vector2 Min, Vector2 Max)> PeekLinks = [];
    private readonly TaskListProjection peekProjection = new();
    private float peekMaxHeight;

    internal static string FocusLabel(MonitorSnapshot snapshot)
    {
        if (!snapshot.Connected) return "Relais déconnecté";
        var errors = snapshot.Threads.Count(t => t.State == "error");
        if (errors > 0) return errors == 1 ? "1 erreur à voir" : $"{errors} erreurs à voir";
        if (snapshot.Attention > 0) return snapshot.Attention == 1 ? "1 tâche à voir" : $"{snapshot.Attention} tâches à voir";
        if (snapshot.Ready > 0) return snapshot.Ready == 1 ? "1 réponse à lire" : $"{snapshot.Ready} réponses à lire";
        if (snapshot.Active > 0) return snapshot.Active == 1 ? "1 tâche en cours" : $"{snapshot.Active} tâches en cours";
        return snapshot.ReadStateSupported ? "Aucune activité" : "Lecture non synchronisée";
    }

    private void OpenPeek(HudTarget target, Vector2 position, Vector2 faceSize)
    {
        PeekTarget = target;
        var s = ObsidianTheme.UiScale; var viewport = ImGui.GetMainViewport();
        var width = Math.Min(430*s,viewport.Size.X-24);
        var below = position.Y+faceSize.Y+6*s;
        var belowSpace=viewport.Pos.Y+viewport.Size.Y-12-below;
        var aboveSpace=position.Y-6*s-viewport.Pos.Y-12;
        var useBelow=belowSpace>=aboveSpace;
        peekMaxHeight=Math.Max(1,Math.Min(420*s,useBelow ? belowSpace : aboveSpace));
        var y=useBelow ? below : position.Y-6*s;
        ImGui.SetNextWindowPos(new(Math.Clamp(position.X,viewport.Pos.X+12,Math.Max(viewport.Pos.X+12,viewport.Pos.X+viewport.Size.X-width-12)),y),ImGuiCond.Appearing,new Vector2(0,useBelow ? 0 : 1));
        ImGui.OpenPopup("hud-peek");
    }

    private void DrawPeek(MonitorSnapshot snapshot)
    {
        PeekLinks.Clear();
        if (!ImGui.IsPopupOpen("hud-peek")) { PeekTarget = null; return; }
        var s = ObsidianTheme.UiScale; var viewport = ImGui.GetMainViewport();
        var width = Math.Min(430*s,viewport.Size.X-24);
        ImGui.SetNextWindowSizeConstraints(new(width,0),new(width,Math.Min(peekMaxHeight,viewport.Size.Y-24)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(12*s));
        if (ImGui.BeginPopup("hud-peek",ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted(PeekTarget switch { HudTarget.Active=>"En cours",HudTarget.Ready=>"Réponses non lues",HudTarget.Attention=>"À voir",HudTarget.Quota=>"Quota Codex",HudTarget.Pinned=>"Tâche épinglée",_=>"Tâches suivies" });
            ImGui.Separator();
            if (!snapshot.Connected) ImGui.TextWrapped(snapshot.Error ?? "Relais déconnecté");
            else if(PeekTarget==HudTarget.Quota) DrawUsageDetails(snapshot,plugin.Config.UsagePeriod);
            else if(PeekTarget==HudTarget.Ready && !snapshot.ReadStateSupported) ImGui.TextWrapped("Indicateur de lecture non fourni par le relais. Relancer le relais récent depuis Connexion.");
            else
            {
                var filter=PeekTarget switch { HudTarget.Active=>1,HudTarget.Ready=>4,HudTarget.Attention=>2,_=>0 };
                var rows=peekProjection.Get(snapshot,true,false,filter,"");
                if(PeekTarget==HudTarget.Pinned) rows=rows.Where(t=>t.Id==plugin.Config.PinnedHudTaskId).ToArray();
                if(rows.Length==0) ImGui.TextWrapped(PeekTarget==HudTarget.Pinned ? "Choisir une tâche dans les réglages HUD ou avec « Épingler dans le HUD » dans son menu." : "Aucune tâche dans cette vue.");
                foreach(var task in rows.Take(5))
                {
                    ImGui.PushID(task.Id);
                    EmojiText.Wrapped(UnicodeText.Truncate(task.Title,160),ImGui.GetContentRegionAvail().X);
                    ImGui.TextColored(ObsidianTheme.TaskState(task),task.Label);
                    if(task.InterventionLabel is { } label) ImGui.TextColored(ObsidianTheme.Amber,label);
                    ImGui.TextDisabled(ObsidianTheme.Fit(task.ModelLabel,ImGui.GetContentRegionAvail().X));
                    if(plugin.Config.ShowQuestionExcerpts && task.QuestionExcerpt is { } excerpt) EmojiText.Wrapped(excerpt,ImGui.GetContentRegionAvail().X);
                    ImGui.BeginDisabled(CodexTaskLink.Build(task.Id) is null || plugin.TaskLink.Busy);
                    if(ImGui.SmallButton("Ouvrir dans Codex")) _=plugin.TaskLink.Open(task.Id);
                    PeekLinks.Add((task.Id,ImGui.GetItemRectMin(),ImGui.GetItemRectMax()));
                    ImGui.EndDisabled();
                    if(plugin.TaskLink.ErrorFor(task.Id) is { } error) ImGui.TextWrapped(error);
                    ImGui.Separator(); ImGui.PopID();
                }
                if(rows.Length>5) ImGui.TextDisabled($"{rows.Length-5} autres tâches dans la liste");
            }
            if(ImGui.Button(PeekTarget==HudTarget.Quota ? "Réglages du quota" : "Ouvrir la liste"))
            {
                if(PeekTarget==HudTarget.Quota) plugin.OpenQuota();
                else plugin.OpenTasks(PeekTarget switch { HudTarget.Active=>1,HudTarget.Ready=>4,HudTarget.Attention=>2,_=>0 });
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopTextWrapPos();ImGui.EndPopup();
        }
        ImGui.PopStyleVar();
    }
}
