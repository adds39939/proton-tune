using ProtonTune.Core.Steam;
using ProtonTune.UI.Components.Library;

namespace ProtonTune.UI.Tests.Components.Library;

/// <summary>
/// The order the library lists games in. Both orders have to be stable, or the grid reshuffles
/// itself between scans for no reason a user could explain.
/// </summary>
public class LibrarySortOrderTests
{
    private static SteamLibraryEntry Game(string name, DateTimeOffset? lastPlayed = null) => new()
    {
        AppId = (uint)name.GetHashCode(StringComparison.Ordinal),
        Name = name,
        InstallDirectory = $"/steam/{name}",
        LibraryPath = "/steam",
        Kind = SteamAppKind.Game,
        LastPlayed = lastPlayed
    };

    private static DateTimeOffset MinutesAgo(int minutes) => DateTimeOffset.Now - TimeSpan.FromMinutes(minutes);

    private static string[] Order(LibrarySortOrder order, params SteamLibraryEntry[] apps) =>
        order.Apply(apps).Select(app => app.Name).ToArray();

    [Fact]
    public void OrdersByNameAlphabetically() =>
        Assert.Equal(
            ["Alpha", "Beta", "Gamma"],
            Order(LibrarySortOrder.Name, Game("Gamma"), Game("Alpha"), Game("Beta")));

    /// <summary>Steam names are capitalised inconsistently, so case must not drive the order.</summary>
    [Fact]
    public void IgnoresCaseWhenOrderingByName() =>
        Assert.Equal(
            ["apple", "Banana", "cherry"],
            Order(LibrarySortOrder.Name, Game("cherry"), Game("apple"), Game("Banana")));

    [Fact]
    public void PutsTheMostRecentlyPlayedFirst() =>
        Assert.Equal(
            ["REMATCH", "Overwatch", "Lossless Scaling"],
            Order(
                LibrarySortOrder.RecentlyPlayed,
                Game("Overwatch", MinutesAgo(240)),
                Game("Lossless Scaling", MinutesAgo(1440)),
                Game("REMATCH", MinutesAgo(32))));

    /// <summary>
    /// No timestamp is not the same as a very old one — a game nobody has run is the least likely
    /// to be the one being looked for, so it sorts last rather than first.
    /// </summary>
    [Fact]
    public void PutsGamesNeverPlayedLast() =>
        Assert.Equal(
            ["Played", "Ancient", "Never"],
            Order(
                LibrarySortOrder.RecentlyPlayed,
                Game("Never"),
                Game("Ancient", MinutesAgo(100_000)),
                Game("Played", MinutesAgo(5))));

    /// <summary>
    /// Steam records this to the second, so two games can genuinely tie. Falling back to the name
    /// keeps the list from reshuffling between scans.
    /// </summary>
    [Fact]
    public void FallsBackToTheNameWhenTwoWerePlayedAtOnce()
    {
        var moment = MinutesAgo(10);

        Assert.Equal(
            ["Alpha", "Beta"],
            Order(LibrarySortOrder.RecentlyPlayed, Game("Beta", moment), Game("Alpha", moment)));
    }

    [Fact]
    public void OrdersGamesNeverPlayedByNameAmongThemselves() =>
        Assert.Equal(
            ["Alpha", "Beta"],
            Order(LibrarySortOrder.RecentlyPlayed, Game("Beta"), Game("Alpha")));

    [Theory]
    [InlineData(LibrarySortOrder.Name, "Name")]
    [InlineData(LibrarySortOrder.RecentlyPlayed, "Recently played")]
    public void NamesEachOrderForTheMenu(LibrarySortOrder order, string expected) =>
        Assert.Equal(expected, order.Title());
}
