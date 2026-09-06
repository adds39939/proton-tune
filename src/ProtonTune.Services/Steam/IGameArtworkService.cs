using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Resolves cover artwork for an app.
/// </summary>
public interface IGameArtworkService
{
    /// <summary>
    /// Returns a value usable directly as an <c>&lt;img&gt;</c> source, or <see langword="null"/>
    /// when no artwork of that shape can be offered.
    /// </summary>
    /// <remarks>
    /// An image source rather than bytes, so a provider can hand back a remote URL, a cached
    /// <c>file:</c> path, or a data URI. Not a promise the image exists: Steam publishes no
    /// artwork for compatibility tools, so callers must handle a failed load.
    /// </remarks>
    Task<string?> GetArtworkSourceAsync(
        uint appId,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default);
}
