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
    /// <remarks>
    /// The answer is held after the first read, since every screen wants the same list and reading
    /// it means parsing a manifest per installed app. Call <see cref="Invalidate" /> where the
    /// answer could have changed.
    /// </remarks>
    Task<IReadOnlyList<SteamLibraryEntry>> GetInstalledAppsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the held answer, so the next read goes back to disk.
    /// </summary>
    /// <remarks>
    /// Nothing watches the manifests, so this is what a rescan means: the user has installed or
    /// removed something and is asking to be told about it.
    /// </remarks>
    void Invalidate();
}
