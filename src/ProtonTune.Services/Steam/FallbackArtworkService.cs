using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="IGameArtworkService" />
/// <remarks>
/// Asks each provider in turn and takes the first that offers something. Neither covers
/// everything: the local cache holds only what Steam has drawn, and the CDN's predictable URLs
/// stop at titles from before Steam moved to content-hashed asset paths.
/// </remarks>
public sealed class FallbackArtworkService(IEnumerable<IGameArtworkService> providers) : IGameArtworkService
{
    private readonly IReadOnlyList<IGameArtworkService> _providers = [..providers];

    /// <inheritdoc />
    public async Task<string?> GetArtworkSourceAsync(
        uint appId,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            if (await provider.GetArtworkSourceAsync(appId, kind, cancellationToken) is { } source)
            {
                return source;
            }
        }

        return null;
    }
}
