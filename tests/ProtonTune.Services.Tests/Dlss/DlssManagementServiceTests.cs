using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Dlss;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Dlss;

namespace ProtonTune.Services.Tests.Dlss;

/// <summary>
/// Swapping a game's DLSS libraries edits files inside someone's install, so the contract is that
/// the original is always recoverable and nothing is replaced that was not found first.
/// </summary>
/// <remarks>
/// These run against a throwaway install laid out like a real one, including the deeply nested
/// path an Unreal game uses.
/// </remarks>
public sealed class DlssManagementServiceTests : IDisposable
{
    private const string NestedPath = "Engine/Plugins/Marketplace/DLSS/Binaries/ThirdParty/Win64";

    private readonly string _root = Directory.CreateTempSubdirectory("protontune-dlss-").FullName;

    private string InstallDirectory => Path.Combine(_root, "game");

    private string ShippedDirectory => Path.Combine(_root, "shipped", "310.7.0");

    private SteamLibraryEntry Entry => new()
    {
        AppId = 2138720,
        Name = "REMATCH",
        InstallDirectory = InstallDirectory,
        LibraryPath = _root,
        Kind = SteamAppKind.Game
    };

    private DlssRuntime Runtime => new(
        "310.7.0",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nvngx_dlss.dll"] = Path.Combine(ShippedDirectory, "nvngx_dlss.dll"),
            ["nvngx_dlssg.dll"] = Path.Combine(ShippedDirectory, "nvngx_dlssg.dll")
        });

    public DlssManagementServiceTests()
    {
        Directory.CreateDirectory(ShippedDirectory);
        File.WriteAllText(Path.Combine(ShippedDirectory, "nvngx_dlss.dll"), "new super resolution");
        File.WriteAllText(Path.Combine(ShippedDirectory, "nvngx_dlssg.dll"), "new frame generation");

        Directory.CreateDirectory(Path.Combine(InstallDirectory, NestedPath));
        File.WriteAllText(Path.Combine(InstallDirectory, NestedPath, "nvngx_dlss.dll"), "the game's own");
        File.WriteAllText(Path.Combine(InstallDirectory, "nvngx_dlssg.dll"), "the game's own");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// Storage is pointed at the temporary directory. Left at its default these tests would write
    /// into the user's real data directory, under app ids belonging to real games.
    /// </summary>
    private DlssManagementService CreateService() =>
        new(new StubRuntimeProvider(Runtime),
            ProtonTuneStorage.At(Path.Combine(_root, "storage")),
            NullLogger<DlssManagementService>.Instance);

    [Fact]
    public void FindsLibrariesHoweverDeeplyTheyAreBuried()
    {
        var status = CreateService().Inspect(Entry);

        Assert.Equal(2, status.Libraries.Count);
        Assert.Contains(status.Libraries, library => library.RelativePath.Contains("ThirdParty", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsUntouchedLibrariesAsOriginal()
    {
        var status = CreateService().Inspect(Entry);

        Assert.All(status.Libraries, library => Assert.Equal(DlssLinkState.Original, library.State));
        Assert.False(status.IsManaged);
    }

    [Fact]
    public void ReportsALinkSomebodyElseMadeAsForeign()
    {
        // Somebody's own arrangement, like a hand-made symlink to a personal DLSS folder. Worth
        // saying rather than silently replacing.
        var elsewhere = Path.Combine(_root, "mine.dll");
        var library = Path.Combine(InstallDirectory, "nvngx_dlssg.dll");

        File.WriteAllText(elsewhere, "mine");
        File.Delete(library);
        File.CreateSymbolicLink(library, elsewhere);

        var status = CreateService().Inspect(Entry);

        Assert.True(status.HasForeignLinks);
    }

    [Fact]
    public async Task LinksLibrariesAndKeepsTheOriginals()
    {
        await CreateService().ApplyAsync(Entry, Runtime);

        var status = CreateService().Inspect(Entry);

        Assert.True(status.IsManaged);
        Assert.All(status.Libraries, library => Assert.Equal(DlssLinkState.Managed, library.State));

        // The game now reads the shipped file.
        Assert.Equal("new super resolution",
            await File.ReadAllTextAsync(Path.Combine(InstallDirectory, NestedPath, "nvngx_dlss.dll")));
    }

    [Fact]
    public async Task RevertingPutsTheGamesOwnFilesBack()
    {
        var service = CreateService();

        await service.ApplyAsync(Entry, Runtime);
        await service.RevertAsync(Entry);

        var status = service.Inspect(Entry);

        Assert.All(status.Libraries, library => Assert.Equal(DlssLinkState.Original, library.State));
        Assert.Equal("the game's own",
            await File.ReadAllTextAsync(Path.Combine(InstallDirectory, NestedPath, "nvngx_dlss.dll")));
        Assert.Equal("the game's own",
            await File.ReadAllTextAsync(Path.Combine(InstallDirectory, "nvngx_dlssg.dll")));
    }

    /// <summary>
    /// The state Overwatch was found in: a link into ProtonTune's store with no backup behind it,
    /// which a revert had previously restored and something re-created afterwards. Reverting used
    /// to walk the backup directory alone, so with none there it did nothing at all and reported
    /// success — leaving the game permanently on ProtonTune's library with no way back.
    /// </summary>
    [Fact]
    public async Task UnpicksALinkWhoseBackupHasGone()
    {
        var service = CreateService();
        var linked = Path.Combine(InstallDirectory, NestedPath, "nvngx_dlss.dll");

        await service.ApplyAsync(Entry, Runtime);
        await service.RevertAsync(Entry);

        // Re-link by hand, as the launch script does when it runs after a revert. It points into
        // ProtonTune's own store, which is what makes the link managed, and backs nothing up —
        // there is no original left to move aside.
        File.Delete(linked);
        File.CreateSymbolicLink(
            linked,
            Path.Combine(_root, "storage", "dlss", "310.7.0", "nvngx_dlss.dll"));

        var result = await service.RevertAsync(Entry);

        Assert.False(service.Inspect(Entry).HasManagedLinks);
        Assert.False(result.IsComplete);
        Assert.Contains(Path.Combine(NestedPath, "nvngx_dlss.dll"), result.Replaced);

        // Left as a real file the game owns, rather than a link or a hole in the install.
        Assert.Null(new FileInfo(linked).LinkTarget);
        Assert.Equal("new super resolution", await File.ReadAllTextAsync(linked));
    }

    /// <summary>
    /// A revert that put everything back says so, so the difference between a complete restore and
    /// a partial one is visible to whatever reports it.
    /// </summary>
    [Fact]
    public async Task ReportsACompleteRestore()
    {
        var service = CreateService();

        await service.ApplyAsync(Entry, Runtime);

        var result = await service.RevertAsync(Entry);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Replaced);
        Assert.Equal(2, result.Restored.Count);
    }

    /// <summary>
    /// Half a swap is still a swap. Treating a game as untouched because only one of its libraries
    /// is linked leaves that one in place with nothing offering to undo it.
    /// </summary>
    [Fact]
    public async Task NoticesAGameThatIsOnlyPartlyLinked()
    {
        var service = CreateService();

        await service.ApplyAsync(Entry, Runtime);

        var loose = Path.Combine(InstallDirectory, "nvngx_dlssg.dll");

        File.Delete(loose);
        await File.WriteAllTextAsync(loose, "the game's own");

        var status = service.Inspect(Entry);

        Assert.False(status.IsManaged);
        Assert.True(status.HasManagedLinks);
    }

    /// <summary>
    /// The script re-establishes a backup it finds missing. Steam restores a game's own libraries
    /// when it verifies or updates it, and that is the one moment the original is on disk to be
    /// kept — without this, a swap re-applied after the backup has gone can never be undone.
    /// </summary>
    [Fact]
    public async Task TheScriptKeepsTheGamesOwnFileWhenNoBackupRemains()
    {
        var service = CreateService();
        var scriptPath = await service.ApplyAsync(Entry, Runtime);
        var script = await File.ReadAllTextAsync(scriptPath);

        Assert.Contains("cp -f \"$dst\" \"$bak\"", script);

        // Only when what is there is the game's own file rather than a link back to the store.
        Assert.Contains("[ -f \"$dst\" ] && [ ! -L \"$dst\" ]", script);
        Assert.Contains(Path.Combine(_root, "storage", "dlss-backup", "2138720"), script);
    }

    /// <summary>
    /// A game update replaces the libraries it ships, so the file found in the install is a newer
    /// original than the one stored the first time. Keeping the older one would revert the game to
    /// a library it no longer has.
    /// </summary>
    [Fact]
    public async Task ReplacesAStaleBackupWithWhatTheGameNowShips()
    {
        var service = CreateService();
        var nested = Path.Combine(InstallDirectory, NestedPath, "nvngx_dlss.dll");

        await service.ApplyAsync(Entry, Runtime);
        await service.RevertAsync(Entry);

        // The game updates: a different file, under the same name.
        await File.WriteAllTextAsync(nested, "the game's own, after an update");

        await service.ApplyAsync(Entry, Runtime);
        await service.RevertAsync(Entry);

        Assert.Equal("the game's own, after an update", await File.ReadAllTextAsync(nested));
    }

    [Fact]
    public async Task ApplyingTwiceDoesNotDestroyTheOriginal()
    {
        // The second pass sees links rather than files. Backing those up would replace the real
        // original with a link and lose it for good.
        var service = CreateService();

        await service.ApplyAsync(Entry, Runtime);
        await service.ApplyAsync(Entry, Runtime);
        await service.RevertAsync(Entry);

        Assert.Equal("the game's own",
            await File.ReadAllTextAsync(Path.Combine(InstallDirectory, NestedPath, "nvngx_dlss.dll")));
    }

    [Fact]
    public async Task WritesAnExecutableLaunchScriptThatRelinks()
    {
        var scriptPath = await CreateService().ApplyAsync(Entry, Runtime);
        var script = await File.ReadAllTextAsync(scriptPath);

        Assert.True(File.Exists(scriptPath));
        Assert.Contains("ln -sfn", script);
        Assert.EndsWith("exec \"$@\"\n", script.ReplaceLineEndings("\n"));

        if (OperatingSystem.IsLinux())
        {
            Assert.True(File.GetUnixFileMode(scriptPath).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public async Task RemovesTheLaunchScriptOnRevert()
    {
        var service = CreateService();
        var scriptPath = await service.ApplyAsync(Entry, Runtime);

        await service.RevertAsync(Entry);

        Assert.False(File.Exists(scriptPath));
    }

    [Fact]
    public void ReportsNothingForAGameWithNoDlssLibraries()
    {
        var empty = Entry with { InstallDirectory = Path.Combine(_root, "nothing-here") };

        Assert.False(CreateService().Inspect(empty).HasLibraries);
    }

    private sealed class StubRuntimeProvider(DlssRuntime runtime) : IDlssRuntimeProvider
    {
        public IReadOnlyList<DlssRuntime> GetAll() => [runtime];

        public DlssRuntime? Latest => runtime;
    }
}
