using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Launch;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// Backups are the only thing standing between a bad splice and a Steam configuration the user
/// cannot get back, so finding them, keeping enough of them, and putting one back all have to
/// work on a real directory rather than in principle.
/// </summary>
public sealed class SteamConfigBackupServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("protontune-backups-").FullName;

    private string UserConfig => Path.Combine(_root, "userdata", "145618525", "config", "localconfig.vdf");

    private string InstallConfig => Path.Combine(_root, "config", "config.vdf");

    public SteamConfigBackupServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UserConfig)!);
        Directory.CreateDirectory(Path.GetDirectoryName(InstallConfig)!);

        File.WriteAllText(UserConfig, "the account's configuration, as it stands");
        File.WriteAllText(InstallConfig, "the installation's configuration, as it stands");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SteamConfigBackupService CreateService(StubSteamClient? client = null, StubProfile? profile = null) =>
        new(new StubInstallLocator(_root),
            client ?? new StubSteamClient(),
            profile ?? new StubProfile(),
            NullLogger<SteamConfigBackupService>.Instance);

    /// <summary>Writes a backup as the saver names them, dated so ordering can be asserted.</summary>
    private string WriteBackup(string target, DateTimeOffset when, string contents = "an earlier configuration")
    {
        var path = SteamConfigBackup.NameFor(target, when);

        File.WriteAllText(path, contents);

        return path;
    }

    [Fact]
    public async Task FindsBackupsOfBothKindsOfConfiguration()
    {
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 21, 0, 0, TimeSpan.Zero));
        WriteBackup(InstallConfig, new DateTimeOffset(2026, 8, 10, 22, 0, 0, TimeSpan.Zero));

        var found = await CreateService().ListAsync();

        Assert.Equal(["config.vdf", "localconfig.vdf"], found.Select(backup => backup.TargetName).Order());
    }

    /// <summary>Newest first: reaching for a backup almost always means the most recent one.</summary>
    [Fact]
    public async Task ListsTheNewestFirst()
    {
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 21, 30, 0, TimeSpan.Zero));

        var found = await CreateService().ListAsync();

        Assert.Equal(21, found[0].CreatedAt.Hour);
        Assert.Equal(9, found[1].CreatedAt.Hour);
    }

    [Fact]
    public async Task ReadsWhenABackupWasTakenFromItsName()
    {
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 21, 58, 30, TimeSpan.Zero));

        var backup = Assert.Single(await CreateService().ListAsync());

        Assert.Equal(new DateTime(2026, 8, 10, 21, 58, 30), backup.CreatedAt.DateTime);
        Assert.Equal(UserConfig, backup.TargetPath);
    }

    [Fact]
    public async Task IgnoresFilesThatAreNotItsOwnBackups()
    {
        File.WriteAllText(UserConfig + ".bak", "someone else's");
        File.WriteAllText(UserConfig + ".protontune-tmp", "a write in progress");

        Assert.Empty(await CreateService().ListAsync());
    }

    [Fact]
    public async Task KeepsTheNewestAndRemovesTheRest()
    {
        for (var hour = 1; hour <= 5; hour++)
        {
            WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, hour, 0, 0, TimeSpan.Zero));
        }

        var removed = await CreateService().PruneAsync(keep: 2);
        var left = await CreateService().ListAsync();

        Assert.Equal(3, removed);
        Assert.Equal([5, 4], left.Select(backup => backup.CreatedAt.Hour));
    }

    /// <summary>
    /// Counted within each file. A busy session editing launch options would otherwise push every
    /// copy of the installation's own configuration out of reach.
    /// </summary>
    [Fact]
    public async Task CountsEachFileSeparately()
    {
        for (var hour = 1; hour <= 4; hour++)
        {
            WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, hour, 0, 0, TimeSpan.Zero));
        }

        WriteBackup(InstallConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero));

        await CreateService().PruneAsync(keep: 2);

        var left = await CreateService().ListAsync();

        Assert.Equal(2, left.Count(backup => backup.TargetName == "localconfig.vdf"));
        Assert.Equal(1, left.Count(backup => backup.TargetName == "config.vdf"));
    }

    [Fact]
    public async Task KeepsEverythingWhenAskedForMoreThanExist()
    {
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, await CreateService().PruneAsync(keep: 10));
        Assert.Single(await CreateService().ListAsync());
    }

    /// <summary>Keeping none would make a bad save unrecoverable, so it is refused outright.</summary>
    [Fact]
    public async Task RefusesToRemoveEverything()
    {
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, await CreateService().PruneAsync(keep: 0));
        Assert.Single(await CreateService().ListAsync());
    }

    [Fact]
    public async Task PutsTheBackupBackOverTheLiveFile()
    {
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero), "what it was before");

        var backup = Assert.Single(await CreateService().ListAsync());
        var result = await CreateService().RestoreAsync(backup);

        Assert.True(result.IsSuccess);
        Assert.Equal("what it was before", await File.ReadAllTextAsync(UserConfig));
    }

    /// <summary>
    /// Restoring the wrong one must be no more final than the save that prompted it, so what is
    /// replaced is kept too.
    /// </summary>
    [Fact]
    public async Task KeepsWhatItReplaced()
    {
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero), "what it was before");

        var backup = Assert.Single(await CreateService().ListAsync());
        var result = await CreateService().RestoreAsync(backup);

        Assert.NotNull(result.ReplacedPath);
        Assert.Equal("the account's configuration, as it stands", await File.ReadAllTextAsync(result.ReplacedPath));
    }

    /// <summary>
    /// The same order a save follows: Steam holds these files in memory and writes them out as it
    /// exits, so anything restored underneath a running Steam is discarded moments later.
    /// </summary>
    [Fact]
    public async Task ClosesSteamAroundTheRestore()
    {
        var client = new StubSteamClient { Running = true };

        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero), "what it was before");

        var backup = Assert.Single(await CreateService(client).ListAsync());
        var result = await CreateService(client).RestoreAsync(backup);

        Assert.True(result.IsSuccess);
        Assert.True(result.SteamWasRestarted);
        Assert.Equal(1, client.ShutdownCalls);
        Assert.Equal(1, client.StartCalls);
    }

    [Fact]
    public async Task RefusesWhileAGameIsRunning()
    {
        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero));

        var backup = Assert.Single(await CreateService().ListAsync());
        var result = await CreateService(new StubSteamClient { GameRunning = true }).RestoreAsync(backup);

        Assert.False(result.IsSuccess);
        Assert.Equal("the account's configuration, as it stands", await File.ReadAllTextAsync(UserConfig));
    }

    [Fact]
    public async Task ReportsABackupThatIsNoLongerThere()
    {
        var path = WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero));
        var backup = Assert.Single(await CreateService().ListAsync());

        File.Delete(path);

        Assert.False((await CreateService().RestoreAsync(backup)).IsSuccess);
    }

    /// <summary>
    /// A restored file can hold launch options from before a game followed the profile, so what
    /// ProtonTune believes about that game is a claim rather than a fact until it is rechecked.
    /// </summary>
    [Fact]
    public async Task ChecksTheProfileStillMatchesAfterwards()
    {
        var profile = new StubProfile { Unlinked = 2 };

        WriteBackup(UserConfig, new DateTimeOffset(2026, 8, 10, 1, 0, 0, TimeSpan.Zero));

        var backup = Assert.Single(await CreateService(profile: profile).ListAsync());
        var result = await CreateService(profile: profile).RestoreAsync(backup);

        Assert.True(profile.WasReconciled);
        Assert.Equal(2, result.UnlinkedFromProfile);
    }

    private sealed class StubInstallLocator(string? root) : ISteamInstallLocator
    {
        public string? Locate() => root;
    }

    private sealed class StubProfile : IGlobalProfileService
    {
        public int Unlinked { get; init; }

        public bool WasReconciled { get; private set; }

        public Task<int> ReconcileLinksAsync(CancellationToken cancellationToken = default)
        {
            WasReconciled = true;

            return Task.FromResult(Unlinked);
        }

        public Task<LaunchOptions> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LaunchOptions());

        public Task SaveAsync(LaunchOptions options, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsLinkedAsync(uint appId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task SetLinkedAsync(uint appId, bool linked, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<uint>> GetLinkedAppsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<uint>>([]);

        public Task<LaunchOptionsSaveResult> SaveAndApplyAsync(
            LaunchOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved));

        public Task ResetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubSteamClient : ISteamClient
    {
        private bool _hasExited;

        public bool Running { get; init; }

        public bool GameRunning { get; init; }

        public int ShutdownCalls { get; private set; }

        public int StartCalls { get; private set; }

        public bool IsRunning() => Running && !_hasExited;

        public bool IsGameRunning() => GameRunning;

        public Task<bool> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            ShutdownCalls++;
            _hasExited = true;

            return Task.FromResult(true);
        }

        public bool Start()
        {
            StartCalls++;

            return true;
        }
    }
}
