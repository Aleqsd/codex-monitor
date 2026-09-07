using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CodexMonitor;

internal sealed partial class SettingsPanel
{
    private bool separatePreview;
    internal readonly Dictionary<string,(Vector2 Min,Vector2 Max)> Controls = [];

    private void DrawSettingsWorkspace()
    {
        var s=ObsidianTheme.UiScale;
        var preview=Category is 0 or 1 or 4;
        var width=ImGui.GetContentRegionAvail().X;
        var shape=Category==0 ? MiniHudOptions.Size(plugin.Config.HudStyle,plugin.Config.ShowUsage,plugin.Config.HudAppearance)*plugin.Config.MiniHudScale
            : Category==1 ? NotificationOverlay.LogicalSize(plugin.Config.ToastAppearance!)*(plugin.Config.ToastAppearance!.Text.Size/17)*plugin.Config.NotificationScale : new Vector2(370,90);
        var side=preview && width >= (shape.X+310)*s;
        var previewWidth=Math.Min(shape.X*s,width);
        separatePreview=preview;
        if(preview && !side) DrawFixedPreview(Math.Min((shape.Y+35)*s,ImGui.GetContentRegionAvail().Y*.40f));
        if(ImGui.BeginChild($"settings-body-{Category}",new Vector2(side ? width-previewWidth-16*s : 0,0),false))
        {
            switch(Category)
            {
                case 0: DrawHudPage(); break;
                case 1: DrawNotifications(); ImGui.Separator(); AppearanceScope=AppearanceTarget.Notification; DrawAppearanceEditor(); break;
                case 2: DrawSounds(); break;
                case 4: DrawTaskSettings(); break;
                case 5: DrawVisibility(); break;
                case 6: DrawFollowing(); break;
                default: DrawConnection(); break;
            }
        }
        ImGui.EndChild();
        if(side) { ImGui.SameLine(0,16*s); DrawFixedPreview(0); }
        separatePreview=false;
    }

    private void DrawFixedPreview(float height)
    {
        var s=ObsidianTheme.UiScale;
        if(ImGui.BeginChild("fixed-preview",new Vector2(0,height),false,ImGuiWindowFlags.HorizontalScrollbar))
        {
            ImGui.TextDisabled("APERÇU · Données fictives");
            if(Category==0)
            {
                var example=ExampleSnapshot();
                var size=MiniHudOptions.Size(plugin.Config.HudStyle,plugin.Config.ShowUsage,plugin.Config.HudAppearance)*s*plugin.Config.MiniHudScale;
                MiniHud.DrawFace(ImGui.GetCursorScreenPos(),size,example,false,false,plugin.Config.MiniHudOpacity,plugin.Config.HudStyle,plugin.Config.ShowUsage,
                    motion:hudMotion.Update(example,plugin.Config.AnimateHudChanges,ImGui.GetIO().DeltaTime),appearance:plugin.Config.HudAppearance,pinnedTaskId:"demo1");
                ImGui.Dummy(size);
            }
            else if(Category==1)
            {
                var appearance=plugin.Config.ToastAppearance!; var scale=s*appearance.Text.Size/17*plugin.Config.NotificationScale;
                var size=NotificationOverlay.LogicalSize(appearance)*scale;
                size.X=Math.Min(size.X,ImGui.GetContentRegionAvail().X);
                NotificationOverlay.DrawFace(ImGui.GetWindowDrawList(),ImGui.GetCursorScreenPos(),size,scale,
                    new(-10,NotificationOverlay.Example(plugin.NotificationUi.PreviewState),1,7),appearance,preview:true,showQuestionExcerpts:plugin.Config.ShowQuestionExcerpts);
                ImGui.Dummy(size);
            }
            else DrawAppearancePreview(plugin.Config.WindowAppearance!);
        }
        ImGui.EndChild();
    }

    private void Remember(string label) => Controls[label]=(ImGui.GetItemRectMin(),ImGui.GetItemRectMax());

    private void DrawHudPage()
    {
        var config=plugin.Config;
        ObsidianTheme.Section("Indicateur", "Visible lorsque la fenêtre est fermée.");
        var indicator=(int)config.Indicator!;
        SettingCombo("Affichage",ref indicator,["Texte de la barre","Mini HUD","Masqué"],v=>plugin.SetIndicator((IndicatorMode)v));
        if(config.Indicator!=IndicatorMode.MiniHud)
        { ImGui.TextWrapped(config.Indicator==IndicatorMode.Text ? "Un clic dans la barre d’informations ouvre les tâches." : "Les notifications restent actives. /codex ouvre la fenêtre."); return; }
        var style=(int)config.HudStyle;
        SettingCombo("Format",ref style,MiniHudOptions.Names,v=>config.HudStyle=(MiniHudStyle)v);Remember("hud-style");
        if(config.HudStyle==MiniHudStyle.TacheEpinglee)
        {
            ImGui.TextUnformatted("Tâche épinglée"); ImGui.SetNextItemWidth(-1);
            var pinned=plugin.Snapshot.Threads.FirstOrDefault(t=>t.Id==config.PinnedHudTaskId);
            if(ImGui.BeginCombo("##pinned-task",pinned is null ? "Choisir une tâche…" : UnicodeText.Truncate(pinned.Title,60)))
            {
                if(ImGui.Selectable("Aucune",config.PinnedHudTaskId is null)){config.PinnedHudTaskId=null;plugin.Save();}
                foreach(var task in plugin.Snapshot.Threads)
                {
                    ImGui.PushID(task.Id);
                    if(ImGui.Selectable(UnicodeText.Truncate(task.Title,100),task.Id==config.PinnedHudTaskId)){config.PinnedHudTaskId=task.Id;plugin.Save();}
                    ImGui.PopID();
                }
                ImGui.EndCombo();
            }
            Remember("pinned-task");
        }
        Toggle("Afficher le quota restant",config.ShowUsage,v=>config.ShowUsage=v);
        var clickAction = (int)config.HudClickAction;
        SettingCombo("Au clic sur le HUD", ref clickAction, ["Ouvrir les réglages", "Aperçu des tâches"], v => config.HudClickAction = (HudClickAction)v);Remember("hud-click");
        ImGui.TextWrapped("La cloche intégrée met les notifications et les sons en pause.");
        Toggle("Animer les changements",config.AnimateHudChanges,v=>config.AnimateHudChanges=v);
        if(config.AnimateHudChanges && ImGui.SmallButton("Tester l’animation")) hudMotion.Highlight();
        ObsidianTheme.Section("Position et taille");
        if(ImGui.Button(plugin.Hud.Editing ? "Verrouiller la position" : "Déplacer le mini HUD"))
        {plugin.NotificationUi.SetPreview(false);plugin.Hud.SetEditing(!plugin.Hud.Editing);}Remember("hud-move");
        if(ImGui.Button("Recentrer")){config.MiniHudAnchorX=.5f;config.MiniHudAnchorY=.08f;plugin.Hud.SetEditing(true);}Remember("hud-center");
        Float("Taille du HUD",config.MiniHudScale,.75f,1.5f,"%.2f ×",v=>config.MiniHudScale=v);
        Float("Opacité du contenu",config.MiniHudOpacity*100,35,100,"%.0f %%",v=>config.MiniHudOpacity=v/100);
        ImGui.Separator();AppearanceScope=AppearanceTarget.Hud;DrawAppearanceEditor();
    }

    private void DrawTaskSettings()
    {
        ObsidianTheme.Section("Liste des tâches");
        Toggle("Afficher les tâches sans activité",plugin.Config.ShowIdle,v=>plugin.Config.ShowIdle=v);
        Toggle("Inclure les tâches non observées",plugin.Config.ShowUnobserved,v=>plugin.Config.ShowUnobserved=v);
        ImGui.Separator();AppearanceScope=AppearanceTarget.Window;DrawAppearanceEditor();
    }
}
