using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// Reading cover art out of Steam's own cache. Steam has rearranged this directory more than once
/// and an upgraded install holds every arrangement at the same time, so the search has to be
/// exercised against a real directory rather than trusted to a single remembered layout.
/// </summary>
public sealed class SteamLibraryCacheArtworkTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("protontune-librarycache-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Writes an artwork file where Steam would, and returns where that was.</summary>
    private string WriteArtwork(uint appId, string name, string? hash = null)
    {
        var directory = hash is null
            ? Path.Combine(_root, "appcache", "librarycache", appId.ToString())
            : Path.Combine(_root, "appcache", "librarycache", appId.ToString(), hash);

        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "not really a jpeg");

        return path;
    }

    private SteamLibraryCacheArtworkService CreateService(string? root = null) =>
        new(new StubInstallLocator(root ?? _root));

    private static async Task<string?> SourceFor(
        SteamLibraryCacheArtworkService service,
        uint appId,
        GameArtworkKind kind = GameArtworkKind.Capsule) =>
        await service.GetArtworkSourceAsync(appId, kind);

    /// <summary>The arrangement older entries are in: the files sit in the app's own directory.</summary>
    [Fact]
    public void FindsArtworkSittingDirectlyInTheAppsDirectory()
    {
        var expected = WriteArtwork(2138720, "library_600x900.jpg");

        Assert.Equal(expected, SteamLibraryCache.Find(_root, 2138720, GameArtworkKind.Capsule));
    }

    /// <summary>
    /// The arrangement newer entries are in. The directory name is a content hash that cannot be
    /// derived from anything, which is why it is enumerated rather than constructed.
    /// </summary>
    [Fact]
    public void FindsArtworkInsideAHashedDirectory()
    {
        var expected = WriteArtwork(3751950, "library_capsule.jpg", "36a1644b03afce1a648ab90b232196609e827539");

        Assert.Equal(expected, SteamLibraryCache.Find(_root, 3751950, GameArtworkKind.Capsule));
    }

    /// <summary>Steam renamed the portrait cover, and both names are still in use.</summary>
    [Theory]
    [InlineData("library_600x900.jpg")]
    [InlineData("library_capsule.jpg")]
    public void AcceptsEitherNameForTheCover(string name)
    {
        var expected = WriteArtwork(440, name);

        Assert.Equal(expected, SteamLibraryCache.Find(_root, 440, GameArtworkKind.Capsule));
    }

    [Theory]
    [InlineData("header.jpg")]
    [InlineData("library_header.jpg")]
    public void AcceptsEitherNameForTheBanner(string name)
    {
        var expected = WriteArtwork(440, name);

        Assert.Equal(expected, SteamLibraryCache.Find(_root, 440, GameArtworkKind.Header));
    }

    /// <summary>A cover is not a banner, however close the two files sit.</summary>
    [Fact]
    public void DoesNotOfferOneShapeWhenAskedForTheOther()
    {
        WriteArtwork(440, "library_600x900.jpg");

        Assert.Null(SteamLibraryCache.Find(_root, 440, GameArtworkKind.Header));
    }

    [Fact]
    public void FindsNothingForAnAppSteamHasNotCached() =>
        Assert.Null(SteamLibraryCache.Find(_root, 1493710, GameArtworkKind.Capsule));

    /// <summary>
    /// One app's artwork is not another's, and the directory names are numbers that would sort
    /// into each other's way if the search ever widened past the app it was given.
    /// </summary>
    [Fact]
    public void DoesNotReachIntoAnotherAppsDirectory()
    {
        WriteArtwork(440, "library_600x900.jpg");

        Assert.Null(SteamLibraryCache.Find(_root, 4400, GameArtworkKind.Capsule));
    }

    [Fact]
    public async Task ServesACachedCoverOverTheArtworkScheme()
    {
        WriteArtwork(3751950, "library_capsule.jpg", "36a1644b");

        Assert.Equal("artwork://steam/3751950/capsule", await SourceFor(CreateService(), 3751950));
    }

    /// <summary>
    /// Offering nothing is what lets the CDN behind it have a go, so it matters that a miss is a
    /// null rather than a URL that will fail to load.
    /// </summary>
    [Fact]
    public async Task OffersNothingWhenSteamHasNotCachedTheApp() =>
        Assert.Null(await SourceFor(CreateService(), 1493710));

    [Fact]
    public async Task OffersNothingWhenSteamIsNotInstalled() =>
        Assert.Null(await SourceFor(new SteamLibraryCacheArtworkService(new StubInstallLocator(null)), 440));

    [Fact]
    public void OpensTheFileTheUrlStandsFor()
    {
        WriteArtwork(440, "library_600x900.jpg");

        var content = CreateService().Open("artwork://steam/440/capsule");

        Assert.NotNull(content);
        Assert.Equal("image/jpeg", content.ContentType);

        using var reader = new StreamReader(content.Content);
        Assert.Equal("not really a jpeg", reader.ReadToEnd());
    }

    /// <summary>
    /// The handler is given every URL in the scheme, including ones it did not write, so it has
    /// to decline rather than assume.
    /// </summary>
    [Theory]
    [InlineData("artwork://steam/440")]
    [InlineData("artwork://steam/440/hero")]
    [InlineData("artwork://steam/not-a-number/capsule")]
    [InlineData("https://example.invalid/440/capsule")]
    [InlineData("nonsense")]
    [InlineData(null)]
    public void DeclinesAUrlItDidNotWrite(string? url)
    {
        WriteArtwork(440, "library_600x900.jpg");

        Assert.Null(CreateService().Open(url));
    }

    /// <summary>
    /// Steam writes artwork the first time a game is shown in its own library, so a cover can
    /// appear while ProtonTune is open. Remembering the miss would hide it until a restart.
    /// </summary>
    [Fact]
    public async Task NoticesArtworkThatArrivesWhileRunning()
    {
        var service = CreateService();

        Assert.Null(await SourceFor(service, 440));

        WriteArtwork(440, "library_600x900.jpg");

        Assert.Equal("artwork://steam/440/capsule", await SourceFor(service, 440));
    }

    /// <summary>
    /// And a remembered hit has to be checked, since Steam clears this cache when it repairs it.
    /// </summary>
    [Fact]
    public void ForgetsAFileThatHasGoneSinceItWasFound()
    {
        var path = WriteArtwork(440, "library_600x900.jpg");
        var service = CreateService();
        var first = service.Open("artwork://steam/440/capsule");

        Assert.NotNull(first);
        first.Content.Dispose();

        File.Delete(path);

        Assert.Null(service.Open("artwork://steam/440/capsule"));
    }

    private sealed class StubInstallLocator(string? root) : ISteamInstallLocator
    {
        public string? Locate() => root;
    }
}
