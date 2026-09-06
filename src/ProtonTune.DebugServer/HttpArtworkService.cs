using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.DebugServer;

/// <summary>
/// Rewrites the private scheme the desktop window serves artwork over into an address a browser
/// can fetch.
/// </summary>
/// <remarks>
/// The window answers <c>artwork://</c> itself through a registered handler, which no browser
/// knows about. The same handlers are mounted under <c>/scheme/</c> here, so what changes is the
/// spelling of the address rather than who answers it.
/// </remarks>
/// <param name="inner">The service that decides what the artwork actually is.</param>
public sealed class HttpArtworkService(IGameArtworkService inner) : IGameArtworkService
{
    /// <summary>Where the scheme handlers are mounted.</summary>
    public const string Prefix = "/scheme";

    /// <inheritdoc />
    public async Task<string?> GetArtworkSourceAsync(
        uint appId,
        GameArtworkKind kind,
        CancellationToken cancellationToken = default)
    {
        var source = await inner.GetArtworkSourceAsync(appId, kind, cancellationToken).ConfigureAwait(false);

        return source is null || !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.IsFile
            ? source
            : Rewrite(uri, source);
    }

    /// <summary>
    /// Turns a private scheme's URL into a path under <see cref="Prefix" />, leaving anything a
    /// browser can already fetch alone.
    /// </summary>
    internal static string Rewrite(Uri uri, string source) =>
        uri.Scheme is "http" or "https" or "data"
            ? source
            : $"{Prefix}/{uri.Scheme}/{uri.Host}{uri.AbsolutePath}";
}
