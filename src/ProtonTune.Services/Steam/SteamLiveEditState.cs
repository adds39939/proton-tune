namespace ProtonTune.Services.Steam;

/// <summary>
/// Where live editing stands: what Steam has been asked to do, and what it is actually doing.
/// </summary>
/// <remarks>
/// The two are reported separately rather than folded into one answer because they genuinely come
/// apart. Switching live editing on while Steam is running leaves it asked for but not yet in
/// effect; switching it off leaves the port open until Steam next closes. A single flag would
/// have to pick one of those to lie about.
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
    /// Where the file lives, shown so it is clear what ProtonTune is putting in the Steam
    /// directory and what to remove by hand if it ever has to be.
    /// </summary>
    public string? MarkerPath { get; init; }
}
