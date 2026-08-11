namespace ProtonTune.Core.Hosting;

/// <summary>
/// Serves a private URL scheme to the web view.
/// </summary>
/// <remarks>
/// The extension point exists so that a feature needing to hand the view something it cannot
/// fetch for itself — a file on disk, bytes assembled at runtime — can say so without the host
/// having to know what that feature is. The host registers whatever has been registered with the
/// container; it does not name any of them.
/// <para>
/// Deliberately free of any web view type, so the layers that implement it stay free of one too.
/// </para>
/// </remarks>
public interface ICustomSchemeHandler
{
    /// <summary>The scheme served, without punctuation — <c>artwork</c>, not <c>artwork://</c>.</summary>
    string Scheme { get; }

    /// <summary>
    /// Answers a request, or returns <see langword="null"/> to decline it.
    /// </summary>
    /// <remarks>
    /// A handler is given every URL in its scheme, including ones it did not write, so declining
    /// is a normal outcome rather than a failure. A declined request fails to load, which is what
    /// lets an <c>&lt;img&gt;</c> fall back to whatever it shows on error.
    /// </remarks>
    SchemeContent? Open(string? url);
}

/// <summary>What a handler answered with, ready to be written to whatever asked for it.</summary>
public sealed record SchemeContent(Stream Content, string ContentType);
