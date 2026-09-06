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
    /// Reads the launch options of several apps at once.
    /// </summary>
    /// <returns>An entry for every app asked about, empty where the app has none set.</returns>
    /// <remarks>
    /// One trip for the batch, as with saving. One at a time would be a connection to Steam and a
    /// pass over its configuration file per game, so anything walking a library would wait in
    /// proportion to the library's size.
    /// </remarks>
    Task<IReadOnlyDictionary<uint, LaunchOptions>> GetManyAsync(
        IReadOnlyCollection<uint> appIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How a save made right now would reach Steam.
    /// </summary>
    /// <remarks>
    /// Asked rather than inferred from whether Steam is running: whether the client offers live
    /// editing cannot be known without speaking to it.
    /// </remarks>
    Task<SteamSaveMethod> GetSaveMethodAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes launch options for an app, restarting Steam around the write when it is running.
    /// </summary>
    /// <remarks>
    /// Where the client offers live editing the change is handed to Steam and nothing is closed.
    /// Otherwise Steam writes its in-memory configuration out as it exits, so the only order that
    /// works is close, write, start — never write and then restart.
    /// </remarks>
    Task<LaunchOptionsSaveResult> SaveAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes launch options for several apps at once.
    /// </summary>
    /// <remarks>
    /// One trip through Steam however many games are involved. Saving individually would close and
    /// reopen Steam per game where live editing is off, and leave the library half updated if one
    /// failed.
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
    /// The two land in different files — launch options in the account's <c>localconfig.vdf</c>,
    /// the build in the installation's <c>config.vdf</c> — but a running Steam holds both in
    /// memory, so they must be written inside the same shutdown or the second discards the first.
    /// Through live editing they are simply two requests to a client that stays up.
    /// </remarks>
    Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken = default);
}
