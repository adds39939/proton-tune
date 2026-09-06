namespace ProtonTune.Core.Hosting;

/// <summary>
/// Serves a private URL scheme to the web view.
/// </summary>
/// <remarks>
/// Lets a feature hand the view bytes it cannot fetch for itself without the host knowing what
/// that feature is. Carries no web view type, so implementations stay free of one too.
/// </remarks>
public interface ICustomSchemeHandler
{
    /// <summary>The scheme served, without punctuation — <c>artwork</c>, not <c>artwork://</c>.</summary>
    string Scheme { get; }

    /// <summary>
    /// Answers a request, or returns <see langword="null"/> to decline it.
    /// </summary>
    /// <remarks>
    /// A handler sees every URL in its scheme, so declining is normal rather than a failure. A
    /// declined request fails to load, which is what lets an <c>&lt;img&gt;</c> fall back.
    /// </remarks>
    SchemeContent? Open(string? url);
}

/// <summary>What a handler answered with, ready to be written to whatever asked for it.</summary>
public sealed record SchemeContent(Stream Content, string ContentType);
