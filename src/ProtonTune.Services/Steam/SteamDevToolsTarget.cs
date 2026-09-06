using System.Text.Json;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Finds the page inside Steam's interface that the client can be driven through.
/// </summary>
/// <remarks>
/// Steam's debugging interface lists every page it is showing, but only the shared context its own
/// code runs in holds the object that can change a game's configuration.
/// </remarks>
internal static class SteamDevToolsTarget
{
    /// <summary>
    /// The host Steam serves its own interface from. Every page of the interface is served from
    /// it, and no page outside the interface is.
    /// </summary>
    /// <remarks>
    /// Matched on rather than the page title, which differs across Steam versions and branches.
    /// </remarks>
    private const string InterfaceHost = "steamloopback.host";

    /// <summary>The page's title on the versions that use one, as a tie-break.</summary>
    private const string SharedContextTitle = "SharedJSContext";

    /// <summary>
    /// Picks the page to drive Steam through out of the list it publishes.
    /// </summary>
    /// <returns>Its debugging address, or <see langword="null"/> when no such page is listed.</returns>
    public static string? FindSharedContext(string listing)
    {
        List<(string Title, string Address)> candidates = [];

        using var document = JsonDocument.Parse(listing);

        foreach (var page in document.RootElement.EnumerateArray())
        {
            if (!page.TryGetProperty("url", out var url) ||
                url.GetString() is not { } address ||
                !address.Contains(InterfaceHost, StringComparison.Ordinal))
            {
                continue;
            }

            if (!page.TryGetProperty("webSocketDebuggerUrl", out var socket) ||
                socket.GetString() is not { } socketUrl)
            {
                continue;
            }

            var title = page.TryGetProperty("title", out var name) ? name.GetString() ?? string.Empty : string.Empty;

            candidates.Add((title, socketUrl));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var candidate in candidates)
        {
            if (string.Equals(candidate.Title, SharedContextTitle, StringComparison.Ordinal))
            {
                return candidate.Address;
            }
        }

        return candidates[0].Address;
    }
}
