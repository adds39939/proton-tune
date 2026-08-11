using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="IGameArtworkService" />
/// <remarks>
/// Asks each provider in turn and takes the first that offers something. Neither source covers
/// everything on its own: the local cache only holds games Steam has drawn in its own library,
/// and the CDN's predictable URLs stop at titles released before Steam moved to content-hashed
/// asset paths. Between them almost every game is covered, and the lettered tile is left to the
/// things that genuinely have no artwork, such as Proton builds and the runtimes.
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
