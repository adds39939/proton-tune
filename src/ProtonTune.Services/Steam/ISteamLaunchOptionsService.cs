using ProtonTune.Core.Launch;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Reads the launch options Steam has stored for a game.
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
}
