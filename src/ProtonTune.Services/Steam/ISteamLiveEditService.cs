namespace ProtonTune.Services.Steam;

/// <summary>
/// Switches on the Steam interface that lets launch options be written into the running client.
/// </summary>
/// <remarks>
/// Steam's own interface is a browser, and it will expose that browser's debugging protocol when
/// it finds an empty file of a particular name in its directory as it starts. Through that
/// protocol Steam can be asked to change a game's launch options and Proton build directly, which
/// it applies at once and writes out itself — so a save no longer has to close Steam, edit the
/// files it owns, and start it again.
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
    /// The file is changed before Steam is restarted, never after: Steam reads it once, as it
    /// starts, so restarting first and writing second would leave the client running without the
    /// change it was just restarted for.
    /// </remarks>
    Task<SteamLiveEditResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
