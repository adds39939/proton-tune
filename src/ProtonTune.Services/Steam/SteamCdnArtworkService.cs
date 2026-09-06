using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="IGameArtworkService" />
/// <remarks>
/// Serves the artwork Steam publishes on its own CDN, addressed purely by app id, so it needs no
/// credentials. Games released since Steam moved to content-hashed asset paths return 404 on the
/// flat URL below, and the hash is not derivable from the app id;
/// <see cref="SteamLibraryCacheArtworkService" /> covers those and runs first for that reason.
/// </remarks>
public sealed class SteamCdnArtworkService : IGameArtworkService
{
    private const string CdnRoot = "https://cdn.cloudflare.steamstatic.com/steam/apps";

    /// <inheritdoc />
    public Task<string?> GetArtworkSourceAsync(
        uint appId,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default)
    {
        var fileName = kind switch
        {
            GameArtworkKind.Capsule => "library_600x900.jpg",
            GameArtworkKind.Header => "header.jpg",
            _ => null
        };

        return Task.FromResult(fileName is null ? null : $"{CdnRoot}/{appId}/{fileName}");
    }
}
