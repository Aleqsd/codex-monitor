using System.Numerics;
using System.Reflection;
using CodexMonitor;
using Dalamud.Bindings.ImGui;

internal static unsafe partial class Program
{
    private static void TypographyRevision(string output)
    {
        Directory.CreateDirectory(output);var checks=0;var images=0;
        void Check(bool value,string message) { if(!value)throw new Exception(message);checks++; }
        var serializer=Assembly.LoadFrom(Path.Combine(Environment.GetEnvironmentVariable("DALAMUD_HOME")!,"Newtonsoft.Json.dll")).GetType("Newtonsoft.Json.JsonConvert")!;
        var read=serializer.GetMethod("DeserializeObject",[typeof(string),typeof(Type)])!;
        var write=serializer.GetMethod("SerializeObject",[typeof(object)])!;
        Configuration Read(string json) { var c=(Configuration)read.Invoke(null,[json,typeof(Configuration)])!;c.Normalize();return c; }
        var old=Read("""{"HudStyle":5,"MiniHudScale":1.25,"MiniHudAnchorX":0.24,"MiniHudOpacity":1,"HudAppearance":{"Skin":0,"Red":0,"Green":0,"Blue":0,"Opacity":0.5,"Text":{"Font":0,"Size":14,"Edge":1}}} """);
        Check(old.HudAppearance!.Text is {Font:MonitorFont.Expressway,Size:16,Edge:TextEdge.Outline},"Existing stock HUD typography upgrades to Expressway 16 outlined");
        Check(old.HudStyle==MiniHudStyle.ObsidienneFine&&old.MiniHudScale==1.25f&&old.MiniHudAnchorX==.24f&&old.HudAppearance.Opacity==.5f&&old.HudAppearance.Red==0&&old.MiniHudOpacity==1,"HUD background, opacity, design, scale and position preserved");
        Check(Math.Abs(old.HudAppearance.Text.EdgeOpacity-128f/255)<.001f&&old.HudAppearance.Text.Red==1,"LMeter white text and 128/255 black outline");
        old.HudAppearance.Text.Font=MonitorFont.Dalamud;old.HudAppearance.Text.Size=20;old.HudAppearance.Text.Edge=TextEdge.None;
        var restored=Read((string)write.Invoke(null,[old])!);
        Check(restored.HudAppearance!.Text is {Font:MonitorFont.Dalamud,Size:20,Edge:TextEdge.None},"Later explicit choices survive save/reload");
        var custom=Read("""{"HudAppearance":{"Text":{"Font":3,"FontFile":"C:/fonts/custom.ttf","Size":20,"Edge":0,"Red":0.4}}} """);
        Check(custom.HudAppearance!.Text is {Font:MonitorFont.LocalFile,Size:20,Edge:TextEdge.None,Red:.4f},"Custom typography is preserved");
        Check(Configuration.NewInstall().HudAppearance!.Text is {Font:MonitorFont.Expressway,Size:16,Edge:TextEdge.Outline},"New installs use readable typography");
        var unique=Path.Combine(Path.GetTempPath(),"codex-font-check-"+Guid.NewGuid().ToString("N"));
        var windows=Path.Combine(unique,"system");var user=Path.Combine(unique,"user");var configs=Path.Combine(unique,"configs");
        var local=Path.Combine(configs,"LMeter","Fonts","Expressway.ttf");Directory.CreateDirectory(Path.GetDirectoryName(local)!);File.WriteAllBytes(local,[1]);
        Check(LocalFontFiles.FindExpressway(windows,user,configs)==local,"Existing LMeter config font is detected without copying");
        Directory.CreateDirectory(windows);var system=Path.Combine(windows,"expressway.ttf");File.WriteAllBytes(system,[1]);
        Check(LocalFontFiles.FindExpressway(windows,user,configs)==system,"Installed system font has priority");
        File.Delete(system);File.Delete(local);
        Check(LocalFontFiles.FindExpressway(windows,user,configs) is null,"Missing files return a fallback without failing");
        Check(!LocalFontFiles.Valid(@"\\server\fonts\font.ttf")&&!LocalFontFiles.Valid("relative.ttf"),"Network and relative font files are rejected");
        // Cleanup only the exact directories created above; no recursive deletion.
        Directory.Delete(windows);Directory.Delete(Path.GetDirectoryName(local)!);Directory.Delete(Path.Combine(configs,"LMeter"));Directory.Delete(configs);Directory.Delete(unique);
        foreach(var missing in new[]{false,true}) foreach(var scale in new[]{1f,1.5f,2f}) foreach(var style in Enum.GetValues<MiniHudStyle>())
        {
            forceMissingExpressway=missing;Initialize((int)(900*scale),(int)(400*scale),scale);UiFixture();overlayOnly=hudOnly=true;
            plugin.Config.HudStyle=style;plugin.Config.HudClickAction=HudClickAction.Settings;plugin.Config.MiniHudScale=1.25f;
            plugin.Config.HudAppearance!.Red=plugin.Config.HudAppearance.Green=plugin.Config.HudAppearance.Blue=0;
            plugin.Config.HudAppearance.Opacity=.5f;
            plugin.Config.MiniHudAnchorX=0;plugin.Config.MiniHudAnchorY=0;
            backdrop=missing?190:55;
            for(var i=0;i<3;i++)Frame();
            Check(UiFonts.ExpresswayAvailable!=missing,"Expressway or fallback loads as requested");
            foreach(var hit in plugin.Hud.Hits)
            { Check(hit.Min.X>=0&&hit.Min.Y>=0&&hit.Max.X<=screenWidth&&hit.Max.Y<=screenHeight,"Typography keeps hitboxes inside the viewport"); }
            var bell=PauseButtonCenter();Click(bell.X,bell.Y);
            Check(ImGui.GetCurrentContext().OpenPopupStack.Size==1&&plugin.OpenCount==0,"Integrated pause remains independent of font choice");
            Click(screenWidth-1,screenHeight-1);
            var active=plugin.Hud.Hits.First(h=>h.Target==HudTarget.Active);Click((active.Min.X+active.Max.X)/2,(active.Min.Y+active.Max.Y)/2);
            Check(plugin.ConfigOpened,"HUD still opens settings");
            ImGui.GetIO().AddMousePosEvent(screenWidth-1,screenHeight-1);for(var i=0;i<3;i++)Frame();
            Render(Path.Combine(output,$"{(missing?"fallback":"expressway")}-{style}-{scale*100:0}.ppm"));images++;
            FinishRevisionView();
        }
        forceMissingExpressway=false;backdrop=55;Initialize(540,130,1);UiFixture();overlayOnly=hudOnly=true;
        plugin.Config.HudStyle=MiniHudStyle.ObsidienneFine;plugin.Config.MiniHudScale=1.25f;
        plugin.Config.MiniHudAnchorX=plugin.Config.MiniHudAnchorY=0;
        plugin.Config.HudAppearance!.Red=plugin.Config.HudAppearance.Green=plugin.Config.HudAppearance.Blue=0;plugin.Config.HudAppearance.Opacity=.5f;
        plugin.Snapshot=plugin.Snapshot with { Threads=Enumerable.Range(0,10).Select(i=>new MonitoredThread($"11111111-2222-4333-8444-{i:000000000000}","Exemple fictif","Démo","gpt-6-astra",i==0?"active":"idle",ReasoningEffort:"high",HasUnreadTurn:i>0)).ToArray(),Usage=new AccountUsage(DateTimeOffset.UtcNow,[new(30,10080)]) };
        for(var i=0;i<3;i++)Frame();Render(Path.Combine(output,"hud-readable.ppm"));images++;FinishRevisionView();backdrop=15;
        Console.WriteLine($"PASS {checks} typography checks; {images} native previews. Local Expressway read only, no font distributed.");
    }
}
