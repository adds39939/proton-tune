using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <summary>
/// The private URL scheme artwork held on disk is served to the web view over.
/// </summary>
/// <remarks>
/// A file path cannot be given to an <c>&lt;img&gt;</c> directly: the page is served from Photino's
/// own scheme, and a <c>file:</c> source from that origin is not something a web view can be
/// relied on to load. Reading the image and inlining it as a data URI would work, but it puts a
/// six-figure string in the render tree for every card in the library. A scheme handler keeps the
/// markup down to a short URL and lets the bytes go straight from disk to the view.
/// </remarks>
public static class ArtworkScheme
{
    /// <summary>The scheme name, as registered with the window.</summary>
    public const string Name = "artwork";

    private const string Authority = "steam";

    /// <summary>The URL that serves an app's artwork of a given shape.</summary>
    public static string UrlFor(uint appId, GameArtworkKind kind) =>
        $"{Name}://{Authority}/{appId}/{kind.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Reads back what <see cref="UrlFor" /> wrote. Returns <see langword="false"/> for anything
    /// else, since a handler is given every URL in its scheme and cannot assume it made them all.
    /// </summary>
    public static bool TryParse(string? url, out uint appId, out GameArtworkKind kind)
    {
        appId = 0;
        kind = default;

        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Scheme, Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = parsed.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments is [var app, var shape] &&
               uint.TryParse(app, out appId) &&
               Enum.TryParse(shape, ignoreCase: true, out kind) &&
               Enum.IsDefined(kind);
    }
}
