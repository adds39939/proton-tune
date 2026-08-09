namespace ProtonTune.Services.Steam;

/// <summary>
/// Finds the Steam installation root on the local machine.
/// </summary>
public interface ISteamInstallLocator
{
    /// <summary>
    /// Returns the Steam root directory, or <see langword="null"/> when Steam is not installed.
    /// </summary>
    string? Locate();
}
