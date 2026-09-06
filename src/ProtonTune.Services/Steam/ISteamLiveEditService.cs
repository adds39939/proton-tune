namespace ProtonTune.Services.Steam;

/// <summary>
/// Switches on the Steam interface that lets launch options be written into the running client.
/// </summary>
/// <remarks>
/// Steam's interface is a browser, and it exposes that browser's debugging protocol when it finds
/// an empty file of a particular name in its directory as it starts. Through it Steam can be asked
/// to change launch options and Proton build directly, so a save need not close Steam at all.
/// </remarks>
public interface ISteamLiveEditService
{
    /// <summary>
    /// Whether live editing has been asked for, and whether Steam is currently offering it.
    /// </summary>
    Task<SteamLiveEditState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts the file in place or takes it away, and restarts Steam so the change takes effect.
    /// </summary>
    /// <remarks>
    /// The file is changed before the restart, never after: Steam reads it once as it starts.
    /// </remarks>
    Task<SteamLiveEditResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
