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
    /// Name is the tie-break in every case, so games never played, or played within the same
    /// minute, still come out in a stable order.
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
