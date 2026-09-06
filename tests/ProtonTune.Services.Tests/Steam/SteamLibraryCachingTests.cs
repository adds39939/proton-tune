using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// Whether the library scan is read again or answered from what was already found.
/// </summary>
/// <remarks>
/// Every screen wants the same list, and a scan parses a manifest per installed app off the disk.
/// Written against real files rather than a stub, since what is being pinned down is whether the
/// disk is touched a second time.
/// </remarks>
public sealed class SteamLibraryCachingTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("protontune-library-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SteamLibraryService CreateService() =>
        new(new StubInstallLocator(_root), NullLogger<SteamLibraryService>.Instance);

    /// <summary>Writes an app manifest where Steam would.</summary>
    private void WriteApp(uint appId, string name)
    {
        var steamApps = Path.Combine(_root, "steamapps");
        Directory.CreateDirectory(Path.Combine(steamApps, "common", name));

        File.WriteAllText(
            Path.Combine(steamApps, $"appmanifest_{appId}.acf"),
            $$"""
              "AppState"
              {
                  "appid"      "{{appId}}"
                  "name"       "{{name}}"
                  "installdir" "{{name}}"
                  "StateFlags" "4"
              }
              """);
    }

    private void DeleteApp(uint appId) =>
        File.Delete(Path.Combine(_root, "steamapps", $"appmanifest_{appId}.acf"));

    [Fact]
    public async Task ReadsTheLibraryOnTheFirstAsk()
    {
        WriteApp(440, "Team Fortress 2");

        var apps = await CreateService().GetInstalledAppsAsync();

        Assert.Equal(["Team Fortress 2"], apps.Select(app => app.Name));
    }

    /// <summary>
    /// The point of holding the answer: opening a game's configuration used to rescan every
    /// manifest to find the one entry the library already had.
    /// </summary>
    [Fact]
    public async Task AnswersAgainWithoutReadingTheDiskAgain()
    {
        WriteApp(440, "Team Fortress 2");

        var service = CreateService();

        var first = await service.GetInstalledAppsAsync();

        DeleteApp(440);

        var second = await service.GetInstalledAppsAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ReadsTheDiskAgainOnceTheAnswerIsDiscarded()
    {
        WriteApp(440, "Team Fortress 2");

        var service = CreateService();

        await service.GetInstalledAppsAsync();

        WriteApp(620, "Portal 2");
        service.Invalidate();

        var apps = await service.GetInstalledAppsAsync();

        Assert.Equal(["Portal 2", "Team Fortress 2"], apps.Select(app => app.Name));
    }

    /// <summary>
    /// Several screens opening at once must not each start a scan of their own.
    /// </summary>
    [Fact]
    public async Task ScansOnceWhenAskedFromEverywhereAtTheSameTime()
    {
        WriteApp(440, "Team Fortress 2");

        var service = CreateService();

        var answers = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => service.GetInstalledAppsAsync())));

        Assert.All(answers, answer => Assert.Same(answers[0], answer));
    }

    /// <summary>
    /// Nothing installed is a real answer rather than a missing one, so it is held like any other
    /// and does not mean a rescan on every ask.
    /// </summary>
    [Fact]
    public async Task HoldsAnEmptyLibraryToo()
    {
        Directory.CreateDirectory(Path.Combine(_root, "steamapps"));

        var service = CreateService();

        var first = await service.GetInstalledAppsAsync();
        var second = await service.GetInstalledAppsAsync();

        Assert.Empty(first);
        Assert.Same(first, second);
    }

    private sealed class StubInstallLocator(string? root) : ISteamInstallLocator
    {
        public string? Locate() => root;
    }
}
