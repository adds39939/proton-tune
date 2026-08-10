using ProtonTune.Core.Proton;

namespace ProtonTune.Services.Proton;

/// <summary>
/// Reads which Proton builds are installed and which games are pointed at them.
/// </summary>
public interface IProtonToolService
{
    /// <summary>
    /// Scans the Steam installation for Proton builds and their app mappings.
    /// </summary>
    /// <returns>
    /// The catalogue, or <see cref="ProtonCatalogue.Empty" /> when Steam is not installed. A
    /// machine with Steam but no Proton is a normal result, not an error.
    /// </returns>
    Task<ProtonCatalogue> GetCatalogueAsync(CancellationToken cancellationToken = default);
}
