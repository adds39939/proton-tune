using ProtonTune.Core.Settings;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Library;

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
