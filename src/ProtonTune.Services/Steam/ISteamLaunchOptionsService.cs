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
    /// One trip for the batch, as with saving. Read one at a time this would be a connection to
    /// Steam and a pass over its configuration file per game, which anything walking a library —
    /// working out which games still follow the global profile, most of all — turns into a wait
    /// proportional to how many games someone owns.
    /// </remarks>
    Task<IReadOnlyDictionary<uint, LaunchOptions>> GetManyAsync(
        IReadOnlyCollection<uint> appIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How a save made right now would reach Steam.
    /// </summary>
    /// <remarks>
    /// Asked rather than worked out from whether Steam is running, because the answer turns on
    /// whether the running client is offering live editing — which cannot be known without
    /// speaking to it.
    /// </remarks>
    Task<SteamSaveMethod> GetSaveMethodAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes launch options for an app, restarting Steam around the write when it is running.
    /// </summary>
    /// <remarks>
    /// Where the running client offers live editing, the change is handed to Steam itself and
    /// nothing is closed. Otherwise Steam keeps its configuration in memory and writes it out as
    /// it exits, so a change made while it is running is discarded moments later — and the only
    /// order that works is to close Steam, write, and start it again, never to write and then
    /// restart.
    /// </remarks>
    Task<LaunchOptionsSaveResult> SaveAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes launch options for several apps at once.
    /// </summary>
    /// <remarks>
    /// One trip through Steam, however many games are involved. Saving them individually would
    /// close and reopen Steam once per game where live editing is off, which a profile applied
    /// across a library makes intolerable — and would leave the library half updated if one
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
    /// The two land in different files — launch options in the account's
    /// <c>localconfig.vdf</c>, the build in the installation's <c>config.vdf</c> — but both are
    /// held in memory by a running Steam and must be written inside the same shutdown. Saving them
    /// separately would close and reopen Steam twice for one change, and the second shutdown would
    /// discard the first write. Through live editing they are two requests to a client that is
    /// staying up, so the same call covers both without the choreography.
    /// </remarks>
    Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken = default);
}
