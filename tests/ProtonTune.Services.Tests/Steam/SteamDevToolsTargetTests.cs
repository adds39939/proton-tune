using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// Picking the page to drive Steam through out of the dozen or so it publishes. The wrong one
/// reports Steam's own object as missing, so live editing would quietly never work.
/// </summary>
public sealed class SteamDevToolsTargetTests
{
    private static string Listing(params string[] pages) => $"[{string.Join(",", pages)}]";

    private static string Page(string title, string url, string? socket = "ws://127.0.0.1:8080/devtools/page/1") =>
        socket is null
            ? $$"""{ "title": {{Quote(title)}}, "url": {{Quote(url)}} }"""
            : $$"""
              { "title": {{Quote(title)}}, "url": {{Quote(url)}}, "webSocketDebuggerUrl": {{Quote(socket)}} }
              """;

    private static string Quote(string value) => $"\"{value}\"";

    private const string InterfaceUrl = "https://steamloopback.host/index.html?LANGUAGE=english";

    [Fact]
    public void FindsThePageSteamRunsItsOwnCodeIn()
    {
        var listing = Listing(
            Page("Steam Root Menu", "about:blank?createflags=4538378"),
            Page("Welcome to Steam", "https://store.steampowered.com/"),
            Page("SharedJSContext", InterfaceUrl, "ws://127.0.0.1:8080/devtools/page/ABC"));

        Assert.Equal("ws://127.0.0.1:8080/devtools/page/ABC", SteamDevToolsTarget.FindSharedContext(listing));
    }

    /// <summary>
    /// The store page is a web page inside Steam, not a page of Steam. Steam's own object is not
    /// given to it, so driving the client through it would fail on every call.
    /// </summary>
    [Fact]
    public void WillNotSettleForAPageThatMerelyBelongsToSteam()
    {
        var listing = Listing(
            Page("Welcome to Steam", "https://store.steampowered.com/"),
            Page("Steam Community", "https://steamcommunity.com/"));

        Assert.Null(SteamDevToolsTarget.FindSharedContext(listing));
    }

    /// <summary>
    /// Matched on where the page is served from rather than its name, which differs across Steam
    /// versions and branches.
    /// </summary>
    [Fact]
    public void FindsThePageOnAVersionThatNamesItSomethingElse()
    {
        var listing = Listing(
            Page("Steam Shared Context presented by Valve", InterfaceUrl, "ws://127.0.0.1:8080/devtools/page/XYZ"));

        Assert.Equal("ws://127.0.0.1:8080/devtools/page/XYZ", SteamDevToolsTarget.FindSharedContext(listing));
    }

    /// <summary>
    /// Several pages of Steam's own interface are published at once, so taking whichever came
    /// first would be a coin toss.
    /// </summary>
    [Fact]
    public void PrefersTheNamedPageWhereSeveralOfSteamsOwnAreListed()
    {
        var listing = Listing(
            Page("Steam", "https://steamloopback.host/routes/library", "ws://127.0.0.1:8080/devtools/page/FIRST"),
            Page("SharedJSContext", InterfaceUrl, "ws://127.0.0.1:8080/devtools/page/SHARED"));

        Assert.Equal("ws://127.0.0.1:8080/devtools/page/SHARED", SteamDevToolsTarget.FindSharedContext(listing));
    }

    /// <summary>
    /// A page with no debugging address cannot be connected to, so it is not a candidate however
    /// well it matches otherwise.
    /// </summary>
    [Fact]
    public void PassesOverAPageThatCannotBeConnectedTo()
    {
        var listing = Listing(Page("SharedJSContext", InterfaceUrl, socket: null));

        Assert.Null(SteamDevToolsTarget.FindSharedContext(listing));
    }

    /// <summary>
    /// Steam publishes an empty list while starting and while changing between its desktop and Big
    /// Picture interfaces. Neither is an error.
    /// </summary>
    [Fact]
    public void ReportsNothingWhenSteamIsBetweenInterfaces()
    {
        Assert.Null(SteamDevToolsTarget.FindSharedContext("[]"));
    }
}
