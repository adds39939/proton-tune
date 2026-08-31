namespace ProtonTune.Services.Steam;

/// <summary>What became of Steam while live editing was being switched over.</summary>
public enum SteamRestartOutcome
{
    /// <summary>Steam was not running, so the change is simply there when it next starts.</summary>
    NotNeeded,

    /// <summary>Steam was closed and started again, and the change is in effect.</summary>
    Restarted,

    /// <summary>
    /// A game is running. Steam was left alone rather than closed underneath it, so the change
    /// waits for the next restart.
    /// </summary>
    DeferredGameRunning,

    /// <summary>Steam was asked to close and had not gone in time, so it was left as it was.</summary>
    RestartFailed
}

/// <summary>How switching live editing over ended.</summary>
public enum SteamLiveEditChange
{
    /// <summary>The file was written or removed as asked.</summary>
    Applied,

    /// <summary>No Steam installation was found to change.</summary>
    NoSteamInstall,

    /// <summary>The file could not be written or removed.</summary>
    Failed
}

/// <summary>
/// The outcome of switching live editing on or off, including what had to happen to Steam.
/// </summary>
/// <param name="Status">How it ended.</param>
/// <param name="Message">Detail worth showing the user, when there is any.</param>
public sealed record SteamLiveEditResult(SteamLiveEditChange Status, string? Message = null)
{
    /// <summary>Whether the file itself was changed.</summary>
    public bool IsSuccess => Status == SteamLiveEditChange.Applied;

    /// <summary>What happened to Steam, which decides whether the change is in effect yet.</summary>
    public SteamRestartOutcome Restart { get; init; } = SteamRestartOutcome.NotNeeded;

    /// <summary>Where live editing stands now that this is done.</summary>
    public SteamLiveEditState State { get; init; } = new();
}
