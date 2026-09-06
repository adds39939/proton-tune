namespace ProtonTune.Services.Steam;

/// <summary>
/// Where live editing stands: what Steam has been asked to do, and what it is actually doing.
/// </summary>
/// <remarks>
/// Reported separately because they come apart: switching on while Steam runs leaves it asked for
/// but not in effect, and switching off leaves the port open until Steam next closes.
/// </remarks>
public sealed record SteamLiveEditState
{
    /// <summary>Whether a Steam installation was found at all.</summary>
    public bool IsSteamInstalled { get; init; }

    /// <summary>Whether the file asking Steam to open its debugging interface is in place.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Whether that interface is answering right now.</summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Whether what was asked for is what is happening, so the screen can say plainly that a
    /// restart is still owed.
    /// </summary>
    public bool IsSettled => IsEnabled == IsActive;

    /// <summary>
    /// Where the file lives, shown so it is clear what ProtonTune puts in the Steam directory.
    /// </summary>
    public string? MarkerPath { get; init; }
}
