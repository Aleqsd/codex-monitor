namespace CodexMonitor;

[Serializable]
public sealed class VisibilityOptions
{
    public bool HideOutsideGame { get; set; } = true;
    public bool HideWhileLoading { get; set; } = true;
    public bool HideInCutscenes { get; set; } = true;
    public bool HideInGpose { get; set; } = true;
    public bool HideInCombat { get; set; }
    public bool HideInDuty { get; set; }

    public bool IsHidden(GameContext game) => game.UiHidden
        || (HideOutsideGame && !game.LoggedIn)
        || (HideWhileLoading && game.Loading)
        || (HideInCutscenes && game.Cutscene)
        || (HideInGpose && game.Gpose)
        || (HideInCombat && game.Combat)
        || (HideInDuty && game.Duty);
}

public readonly record struct GameContext(bool LoggedIn, bool Loading = false, bool Cutscene = false,
    bool Gpose = false, bool Combat = false, bool Duty = false, bool UiHidden = false)
{
    // Explicit opening remains possible at the title screen or in a duty, without reviving overlays.
    public bool CanOpenManually => !Loading && !Cutscene && !Gpose && !UiHidden;
}
