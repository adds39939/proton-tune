using ProtonTune.Core.Settings;

namespace ProtonTune.Services.Settings;

/// <summary>
/// Reads and writes ProtonTune's own settings.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// The current settings, or their defaults where none have been stored or the file cannot be
    /// read. These are preferences rather than a record of anything, so an unreadable file costs
    /// a re-choice and never fails the application.
    /// </summary>
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
