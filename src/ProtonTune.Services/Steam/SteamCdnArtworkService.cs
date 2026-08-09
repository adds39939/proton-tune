using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="IGameArtworkService" />
/// <remarks>
/// Serves the artwork Steam publishes on its own CDN, addressed purely by app id. Unlike
/// SteamGridDB — which needs a registered API key and a lookup call per game — this needs no
/// credentials and no configuration, and it returns the official art for every store title.
/// SteamGridDB earns its keep for community art and non-Steam shortcuts; if that is wanted, it
/// slots in behind <see cref="IGameArtworkService" /> without the UI changing.
/// <para>
/// Steam's local <c>appcache/librarycache</c> is not usable here: it stores artwork under
/// content-hashed file names that cannot be mapped back to an app id without parsing the binary
/// <c>appinfo.vdf</c>.
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
