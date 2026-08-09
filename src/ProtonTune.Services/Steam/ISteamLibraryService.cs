using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Reads the local Steam installation to discover which apps are installed.
/// </summary>
public interface ISteamLibraryService
{
    /// <summary>
    /// Reads every app installed across all Steam library folders, games and compatibility
    /// tools alike, ordered by name.
    /// </summary>
    /// <returns>
    /// The installed apps, or an empty list when Steam is not installed on this machine.
    /// </returns>
    Task<IReadOnlyList<SteamLibraryEntry>> GetInstalledAppsAsync(CancellationToken cancellationToken = default);
}
