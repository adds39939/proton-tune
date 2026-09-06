using System.Collections.Concurrent;
using ProtonTune.Core.Hosting;
using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="IGameArtworkService" />
/// <remarks>
/// Serves the artwork Steam has already downloaded, the only source covering every installed game:
/// titles published under a content-hashed CDN path have no URL derivable from the app id. Reading
/// the cache also means no network round trip.
/// </remarks>
public sealed class SteamLibraryCacheArtworkService(ISteamInstallLocator steam)
    : IGameArtworkService, ICustomSchemeHandler
{
    /// <inheritdoc />
    public string Scheme => ArtworkScheme.Name;
    
    /// <summary>
    /// Resolved paths, kept because the library re-renders every card on each keystroke and each
    /// miss would otherwise walk the app's cache directory again.
    /// </summary>
    /// <remarks>
    /// Only hits are remembered: a game can gain a cover while ProtonTune is open, and a remembered
    /// miss would hide it until the next launch.
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
    /// is served as a JPEG.
    /// </summary>
    private static string ContentTypeFor(string path) =>
        Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";
}
