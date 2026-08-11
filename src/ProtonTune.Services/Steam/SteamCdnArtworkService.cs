using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="IGameArtworkService" />
/// <remarks>
/// Serves the artwork Steam publishes on its own CDN, addressed purely by app id. Unlike
/// SteamGridDB — which needs a registered API key and a lookup call per game — this needs no
/// credentials and no configuration.
/// <para>
/// It does not cover everything. Games released since Steam moved to content-hashed asset paths
/// — <c>store_item_assets/steam/apps/&lt;appid&gt;/&lt;hash&gt;/…</c> — return 404 on the flat URL
/// below, and the hash differs per asset and is not published anywhere derivable from the app id.
/// <see cref="SteamLibraryCacheArtworkService" /> covers those, and runs first for that reason.
/// </para>
/// <para>
/// SteamGridDB earns its keep for community art and non-Steam shortcuts; if that is wanted, it
/// slots in behind <see cref="IGameArtworkService" /> without the UI changing.
/// </para>
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
