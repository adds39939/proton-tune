using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// Which artwork source wins. Neither covers every game, so the order they are asked in is the
/// whole behaviour: get it backwards and recent titles quietly lose their covers again.
/// </summary>
public class FallbackArtworkServiceTests
{
    private static Task<string?> SourceFor(params IGameArtworkService[] providers) =>
        new FallbackArtworkService(providers).GetArtworkSourceAsync(440, GameArtworkKind.Capsule);

    [Fact]
    public async Task TakesTheFirstProviderThatOffersSomething() =>
        Assert.Equal("first", await SourceFor(new StubArtwork("first"), new StubArtwork("second")));

    [Fact]
    public async Task FallsThroughToTheNextWhenOneHasNothing() =>
        Assert.Equal("second", await SourceFor(new StubArtwork(null), new StubArtwork("second")));

    [Fact]
    public async Task OffersNothingWhenNoProviderCan() =>
        Assert.Null(await SourceFor(new StubArtwork(null), new StubArtwork(null)));

    [Fact]
    public async Task OffersNothingWhenThereAreNoProviders() =>
        Assert.Null(await SourceFor());

    /// <summary>
    /// The CDN is asked for a URL whether or not one exists there, so a provider that answers
    /// unconditionally must never be put in front of one that checks.
    /// </summary>
    [Fact]
    public async Task LeavesTheCdnBehindTheLocalCache()
    {
        var source = await SourceFor(new StubArtwork(null), new SteamCdnArtworkService());

        Assert.Equal("https://cdn.cloudflare.steamstatic.com/steam/apps/440/library_600x900.jpg", source);
    }

    private sealed class StubArtwork(string? source) : IGameArtworkService
    {
        public Task<string?> GetArtworkSourceAsync(
            uint appId,
            GameArtworkKind kind,
            CancellationToken cancellationToken = default) => Task.FromResult(source);
    }
}
