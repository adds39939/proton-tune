using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace ProtonTune.UI.Layout;

/// <summary>
/// Application shell: the title bar, the navigation tabs, and the region routed pages render into.
/// </summary>
public partial class MainLayout : LayoutComponentBase
{
    /// <summary>
    /// One tab per routable page, in the order they appear. Adding a page means adding its
    /// <c>@page</c> directive and an entry here.
    /// </summary>
    private static readonly IReadOnlyList<NavItem> NavItems =
    [
        new("/", "Library", NavLinkMatch.All),
        new("/settings", "Settings")
    ];

    /// <param name="Path">The route, matching the page's <c>@page</c> directive.</param>
    /// <param name="Label">The text shown on the tab.</param>
    /// <param name="Match">
    /// How the route is compared against the current URL. The root route needs
    /// <see cref="NavLinkMatch.All" />, or its tab stays highlighted on every other page.
    /// </param>
    private sealed record NavItem(string Path, string Label, NavLinkMatch Match = NavLinkMatch.Prefix);
}
