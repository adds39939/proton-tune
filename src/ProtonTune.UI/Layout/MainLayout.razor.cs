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
        new("/", "Library", NavLinkMatch.All, "library"),
        new("/global", "Global"),
        new("/proton", "Proton"),
        new("/settings", "Settings")
    ];

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>
    /// Whether a tab should look current because of a page it owns but does not sit at.
    /// </summary>
    /// <remarks>
    /// <see cref="NavLink" /> compares one address, which cannot speak for a section spread over
    /// two: the library is at the root while one game's configuration is at
    /// <c>/library/&lt;appid&gt;</c>, and a tab bar with nothing lit says the person has left the
    /// application rather than opened something inside it. The class this adds is the one
    /// <see cref="NavLink" /> applies itself, so a tab is lit the same way whichever decided it.
    /// </remarks>
    /// <summary>
    /// The classes a tab carries. <see cref="NavLink" /> appends its own when its address matches,
    /// so a tab already lit by it is unaffected by this adding the same class.
    /// </summary>
    private string TabClass(NavItem item) => IsUnder(item) ? "nav-tab active" : "nav-tab";

    private bool IsUnder(NavItem item) =>
        item.Section is { } section &&
        Navigation.ToBaseRelativePath(Navigation.Uri).TrimStart('/')
            .StartsWith(section, StringComparison.OrdinalIgnoreCase);

    /// <param name="Path">The route, matching the page's <c>@page</c> directive.</param>
    /// <param name="Label">The text shown on the tab.</param>
    /// <param name="Match">
    /// How the route is compared against the current URL. The root route needs
    /// <see cref="NavLinkMatch.All" />, or its tab stays highlighted on every other page.
    /// </param>
    /// <param name="Section">
    /// A path prefix the tab also owns, for a page reached from it rather than from the tab bar.
    /// Null where the tab's own address is the whole of it.
    /// </param>
    private sealed record NavItem(
        string Path,
        string Label,
        NavLinkMatch Match = NavLinkMatch.Prefix,
        string? Section = null);
}
