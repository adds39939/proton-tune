using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Library;

/// <summary>
/// How the library orders its entries.
/// </summary>
public enum LibrarySortOrder
{
    /// <summary>Alphabetical, which is the order to reach for when looking something up.</summary>
    Name,

    /// <summary>
    /// Most recently played first, which puts the games worth configuring at the top. Games never
    /// played come last rather than first: no timestamp is not the same as a very old one.
    /// </summary>
    RecentlyPlayed
}

/// <summary>Presentation details for <see cref="LibrarySortOrder" />.</summary>
public static class LibrarySortOrders
{
    /// <summary>
    /// Orders a set of games.
    /// </summary>
    /// <remarks>
    /// Name is the tie-break in every case, so games that have never been played — or were played
    /// within the same minute — come out in a stable, readable order rather than whatever the
    /// filesystem happened to hand over.
    /// </remarks>
    public static IOrderedEnumerable<SteamLibraryEntry> Apply(
        this LibrarySortOrder order,
        IEnumerable<SteamLibraryEntry> apps) => order switch
    {
        // Never played sorts last rather than first: no timestamp is not the same as a very old
        // one, and a game nobody has run is the least likely to be the one being looked for.
        LibrarySortOrder.RecentlyPlayed => apps
            .OrderByDescending(app => app.LastPlayed ?? DateTimeOffset.MinValue)
            .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase),
        _ => apps.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
    };

    /// <summary>The label shown for an order, since the names run together otherwise.</summary>
    public static string Title(this LibrarySortOrder order) => order switch
    {
        LibrarySortOrder.Name => "Name",
        LibrarySortOrder.RecentlyPlayed => "Recently played",
        _ => order.ToString()
    };
}
