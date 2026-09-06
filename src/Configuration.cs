using Dalamud.Configuration;

namespace CodexMonitor;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int Port { get; set; } = 43187;
    public bool ShowIdle { get; set; } = true;
    public bool ShowUnobserved { get; set; }
    public bool ShowDtr { get; set; } = true;
    public bool NotifyOnIdle { get; set; } = true;
    public bool NotifyOnAttention { get; set; } = true;
    public bool OpenOnLoad { get; set; } = true;
}
