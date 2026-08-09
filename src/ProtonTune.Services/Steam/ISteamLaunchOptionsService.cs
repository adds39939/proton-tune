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
}
