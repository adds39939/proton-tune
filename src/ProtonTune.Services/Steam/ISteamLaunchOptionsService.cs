using ProtonTune.Core.Launch;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Reads and writes the launch options Steam has stored for a game.
/// </summary>
public interface ISteamLaunchOptionsService
{
    /// <summary>
    /// Reads the launch options for an app.
    /// </summary>
    /// <returns>
    /// The parsed options, or an empty <see cref="LaunchOptions" /> when the game has none set,
    /// or when no Steam user configuration can be found.
    /// </returns>
    Task<LaunchOptions> GetAsync(uint appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether saving right now would require closing Steam first.
    /// </summary>
    bool RequiresSteamRestart();

    /// <summary>
    /// Whether a game is currently running, in which case saving is refused outright.
    /// </summary>
    bool IsGameRunning();

    /// <summary>
    /// Writes launch options for an app, restarting Steam around the write when it is running.
    /// </summary>
    /// <remarks>
    /// Steam keeps its configuration in memory and writes it out as it exits, so a change made
    /// while it is running is discarded moments later. The only order that works is to close
    /// Steam, write, and start it again — never to write and then restart.
    /// </remarks>
    Task<LaunchOptionsSaveResult> SaveAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes launch options for several apps at once.
    /// </summary>
    /// <remarks>
    /// One shutdown, one write, one restart, however many games are involved. Saving them
    /// individually would close and reopen Steam once per game, which a profile applied across a
    /// library makes intolerable — and would leave the library half updated if one failed.
    /// </remarks>
    Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes launch options and choices of Proton build together.
    /// </summary>
    /// <param name="launchOptionsByApp">Launch options to write, by app id.</param>
    /// <param name="compatToolsByApp">
    /// Proton builds to point apps at, by app id, named as Steam knows them. An empty value clears
    /// the choice and lets Steam decide; an app absent from the map keeps whatever it has.
    /// </param>
    /// <remarks>
    /// The two land in different files — launch options in the account's
    /// <c>localconfig.vdf</c>, the build in the installation's <c>config.vdf</c> — but both are
    /// held in memory by a running Steam and must be written inside the same shutdown. Saving them
    /// separately would close and reopen Steam twice for one change, and the second shutdown would
    /// discard the first write.
    /// </remarks>
    Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken = default);
}
