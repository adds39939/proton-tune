using System.Collections.Concurrent;
using ProtonTune.Core.Hosting;
using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="IGameArtworkService" />
/// <remarks>
/// Serves the artwork Steam has already downloaded, which is the only source that covers every
/// installed game. Steam's own CDN is addressed by app id alone for most titles, but recent ones
/// publish under a content-hashed path that cannot be derived from the id, so a game released
/// after that change has no reachable URL and would otherwise show a lettered tile forever.
/// Reading the cache also means no network round trip and no artwork at all outside it.
/// </remarks>
public sealed class SteamLibraryCacheArtworkService(ISteamInstallLocator steam)
    : IGameArtworkService, ICustomSchemeHandler
{
    /// <inheritdoc />
    public string Scheme => ArtworkScheme.Name;
    
    /// <summary>
    /// Resolved paths, kept because the library re-renders every card on each keystroke in the
    /// search box and each miss would otherwise walk the app's cache directory again.
    /// </summary>
    /// <remarks>
    /// Only hits are remembered. Steam writes artwork the first time a game is shown in its own
    /// library, so a game can gain a cover while ProtonTune is open, and a remembered miss would
    /// hide it until the next launch.
    /// </remarks>
    private readonly ConcurrentDictionary<(uint AppId, GameArtworkKind Kind), string> _found = new();

    /// <inheritdoc />
    public Task<string?> GetArtworkSourceAsync(
        uint appId,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(FindFile(appId, kind) is null ? null : ArtworkScheme.UrlFor(appId, kind));

    /// <inheritdoc />
    /// <remarks>
    /// Declines anything that is not a <see cref="ArtworkScheme" /> URL, and anything whose file
    /// has gone since it was resolved.
    /// </remarks>
    public SchemeContent? Open(string? url)
    {
        if (!ArtworkScheme.TryParse(url, out var appId, out var kind) ||
            FindFile(appId, kind) is not { } path)
        {
            return null;
        }

        try
        {
            return new SchemeContent(File.OpenRead(path), ContentTypeFor(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The cached file backing a shape of artwork, if Steam has one.</summary>
    private string? FindFile(uint appId, GameArtworkKind kind)
    {
        if (_found.TryGetValue((appId, kind), out var remembered) && File.Exists(remembered))
        {
            return remembered;
        }

        if (steam.Locate() is not { } root || SteamLibraryCache.Find(root, appId, kind) is not { } path)
        {
            return null;
        }

        _found[(appId, kind)] = path;

        return path;
    }

    /// <summary>
    /// The type to serve a file as. Steam stores covers as JPEG and logos as PNG; anything else
    /// is served as a JPEG, which is what every artwork file in the cache has turned out to be.
    /// </summary>
    private static string ContentTypeFor(string path) =>
        Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";
}
