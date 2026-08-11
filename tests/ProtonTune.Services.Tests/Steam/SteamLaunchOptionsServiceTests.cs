using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Settings;
using ProtonTune.Services.Settings;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// The orchestration around a write: what has to be true before it happens, what order Steam is
/// stopped and started in, and what is left behind afterwards.
/// </summary>
/// <remarks>
/// These run against a throwaway Steam directory and a stub client, so the sequencing can be
/// asserted without closing anybody's Steam.
/// </remarks>
public sealed class SteamLaunchOptionsServiceTests : IDisposable
{
    private const uint AppId = 2357570;

    private const string Document =
        "\"UserLocalConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n" +
        "\t\t\t\t\"apps\"\n\t\t\t\t{\n\t\t\t\t\t\"2357570\"\n\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"LaunchOptions\"\t\t\"PROTON_ENABLE_HDR=1 %command%\"\n" +
        "\t\t\t\t\t}\n\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

    /// <summary>
    /// The other game in the fixture, already pointed at a build, so changes to one app can be
    /// shown not to disturb another.
    /// </summary>
    private const uint OtherAppId = 2138720;

    /// <summary>
    /// A choice of compatibility tool lives in the installation's own config.vdf rather than the
    /// account's, so a save that changes both touches two files.
    /// </summary>
    private const string InstallDocument =
        "\"InstallConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n" +
        "\t\t\t\t\"CompatToolMapping\"\n\t\t\t\t{\n" +
        "\t\t\t\t\t\"0\"\n\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"name\"\t\t\"proton_experimental\"\n" +
        "\t\t\t\t\t\t\"config\"\t\t\"\"\n" +
        "\t\t\t\t\t\t\"priority\"\t\t\"75\"\n\t\t\t\t\t}\n" +
        "\t\t\t\t\t\"2138720\"\n\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"name\"\t\t\"GE-Proton11-3\"\n" +
        "\t\t\t\t\t\t\"config\"\t\t\"\"\n" +
        "\t\t\t\t\t\t\"priority\"\t\t\"250\"\n\t\t\t\t\t}\n" +
        "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

    private readonly string _root = Directory.CreateTempSubdirectory("protontune-test-").FullName;

    private string ConfigPath => Path.Combine(_root, "userdata", "145618525", "config", "localconfig.vdf");

    private string InstallConfigPath => Path.Combine(_root, "config", "config.vdf");

    public SteamLaunchOptionsServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, Document);

        Directory.CreateDirectory(Path.GetDirectoryName(InstallConfigPath)!);
        File.WriteAllText(InstallConfigPath, InstallDocument);
    }

    /// <summary>Reads one field of an app's mapping back out of the file that was written.</summary>
    private async Task<string?> MappingField(uint appId, string key) =>
        SteamConfigText.GetValue(
            await File.ReadAllTextAsync(InstallConfigPath),
            ["InstallConfigStore", "Software", "Valve", "Steam", "CompatToolMapping", appId.ToString(), key]);

    private static Dictionary<uint, string> Only(uint appId, string value) => new() { [appId] = value };

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// Built with the real backup and settings services, so the pruning a save performs is
    /// exercised rather than stubbed away.
    /// </summary>
    private SteamLaunchOptionsService CreateService(StubSteamClient client, int backupsToKeep = 10)
    {
        return new SteamLaunchOptionsService(
            new StubInstallLocator(_root),
            client,
            new StubSettings(new AppSettings { BackupsToKeep = backupsToKeep }),
            NullLogger<SteamLaunchOptionsService>.Instance);
    }

    [Fact]
    public async Task ReadsWhatSteamHasStored()
    {
        var options = await CreateService(new StubSteamClient()).GetAsync(AppId);

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", options.Format());
    }

    [Fact]
    public async Task WritesWhenSteamIsNotRunning()
    {
        var client = new StubSteamClient();

        var result = await CreateService(client).SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.True(result.IsSuccess);
        Assert.False(result.SteamWasRestarted);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.Equal(0, client.StartCalls);
        Assert.Contains("DXVK_HDR=1 %command%", await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task ClosesSteamBeforeWritingAndStartsItAfter()
    {
        var client = new StubSteamClient { Running = true };

        var result = await CreateService(client).SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.True(result.IsSuccess);
        Assert.True(result.SteamWasRestarted);
        Assert.Equal(["shutdown", "write", "start"], client.Sequence);
    }

    [Fact]
    public async Task RefusesWhileAGameIsRunning()
    {
        var client = new StubSteamClient { Running = true, GameRunning = true };

        var result = await CreateService(client).SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.Equal(LaunchOptionsSaveStatus.GameRunning, result.Status);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.Equal(Document, await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task WritesNothingWhenSteamWillNotClose()
    {
        var client = new StubSteamClient { Running = true, ShutdownSucceeds = false };

        var result = await CreateService(client).SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.Equal(LaunchOptionsSaveStatus.SteamStillRunning, result.Status);
        Assert.Equal(Document, await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task KeepsTheOldConfigurationAsABackup()
    {
        var result = await CreateService(new StubSteamClient()).SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.NotNull(result.BackupPath);
        Assert.Equal(Document, await File.ReadAllTextAsync(result.BackupPath));
    }

    [Fact]
    public async Task ChangesOnlyTheValueItWasAskedTo()
    {
        await CreateService(new StubSteamClient()).SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.Equal(
            Document.Replace("PROTON_ENABLE_HDR=1 %command%", "DXVK_HDR=1 %command%"),
            await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task LeavesNoTemporaryFileBehind()
    {
        await CreateService(new StubSteamClient()).SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.GetDirectoryName(ConfigPath)!),
            path => path.EndsWith("-tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReportsWhenThereIsNoSteamInstallation()
    {
        var service = new SteamLaunchOptionsService(
            new StubInstallLocator(null),
            new StubSteamClient(),
            new StubSettings(new AppSettings()),
            NullLogger<SteamLaunchOptionsService>.Instance);

        var result = await service.SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.Equal(LaunchOptionsSaveStatus.NoUserConfig, result.Status);
    }

    /// <summary>
    /// A mapping is more than a name. Steam settles competing mappings by priority, and the ones
    /// that come from app metadata sit at 90 — so a name written without a priority would lose to
    /// whatever Steam had already decided, and the change would appear to do nothing.
    /// </summary>
    [Fact]
    public async Task RecordsANewChoiceWithEverythingSteamNeedsToHonourIt()
    {
        await CreateService(new StubSteamClient())
            .SaveManyAsync(new Dictionary<uint, string>(), Only(AppId, "GE-Proton11-3"));

        Assert.Equal("GE-Proton11-3", await MappingField(AppId, "name"));
        Assert.Equal("250", await MappingField(AppId, "priority"));
        Assert.Equal(string.Empty, await MappingField(AppId, "config"));
    }

    [Fact]
    public async Task PointsAGameThatAlreadyHasAChoiceAtADifferentBuild()
    {
        await CreateService(new StubSteamClient())
            .SaveManyAsync(new Dictionary<uint, string>(), Only(OtherAppId, "proton_experimental"));

        Assert.Equal("proton_experimental", await MappingField(OtherAppId, "name"));
        Assert.Equal("250", await MappingField(OtherAppId, "priority"));
    }

    /// <summary>
    /// Clearing means "decide for me", so nothing is named and the entry goes back to the bottom.
    /// Leaving it at 250 with no tool would outrank Steam's own mapping with a blank.
    /// </summary>
    [Fact]
    public async Task ClearingAChoiceNamesNoToolAndGivesUpItsPriority()
    {
        await CreateService(new StubSteamClient())
            .SaveManyAsync(new Dictionary<uint, string>(), Only(OtherAppId, string.Empty));

        Assert.Equal(string.Empty, await MappingField(OtherAppId, "name"));
        Assert.Equal("0", await MappingField(OtherAppId, "priority"));
    }

    [Fact]
    public async Task LeavesEveryOtherMappingAlone()
    {
        await CreateService(new StubSteamClient())
            .SaveManyAsync(new Dictionary<uint, string>(), Only(OtherAppId, "proton_hotfix"));

        Assert.Equal("proton_experimental", await MappingField(0, "name"));
        Assert.Equal("75", await MappingField(0, "priority"));
    }

    /// <summary>
    /// The two files are held in memory by the same running Steam. Saving them one after the other
    /// would close and reopen it twice, and the second shutdown would discard the first write.
    /// </summary>
    [Fact]
    public async Task WritesBothFilesInsideOneShutdown()
    {
        var client = new StubSteamClient { Running = true };

        var result = await CreateService(client).SaveManyAsync(
            Only(AppId, "DXVK_HDR=1 %command%"),
            Only(AppId, "GE-Proton11-3"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, client.ShutdownCalls);
        Assert.Equal(1, client.StartCalls);

        Assert.Equal("GE-Proton11-3", await MappingField(AppId, "name"));
        Assert.Contains("DXVK_HDR=1 %command%", await File.ReadAllTextAsync(ConfigPath));
    }

    /// <summary>
    /// Changing only the build must not require an account configuration, which is a different
    /// file and not the one being written.
    /// </summary>
    [Fact]
    public async Task ChangesTheBuildWithoutTouchingTheAccountConfiguration()
    {
        var result = await CreateService(new StubSteamClient())
            .SaveManyAsync(new Dictionary<uint, string>(), Only(AppId, "GE-Proton11-3"));

        Assert.True(result.IsSuccess);
        Assert.Equal(Document, await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task KeepsTheOldInstallConfigurationAsABackup()
    {
        await CreateService(new StubSteamClient())
            .SaveManyAsync(new Dictionary<uint, string>(), Only(AppId, "GE-Proton11-3"));

        var backups = Directory
            .EnumerateFiles(Path.GetDirectoryName(InstallConfigPath)!, "config.vdf.protontune-*.bak")
            .ToList();

        Assert.Equal(InstallDocument, await File.ReadAllTextAsync(Assert.Single(backups)));
    }

    /// <summary>
    /// Nothing is written until both documents have been spliced, so a file that is not what was
    /// expected stops the save with the other one still untouched.
    /// </summary>
    [Fact]
    public async Task WritesNothingWhenOneOfTheFilesIsNotRecognised()
    {
        await File.WriteAllTextAsync(InstallConfigPath, "not a KeyValues document at all");

        var result = await CreateService(new StubSteamClient()).SaveManyAsync(
            Only(AppId, "DXVK_HDR=1 %command%"),
            Only(AppId, "GE-Proton11-3"));

        Assert.Equal(LaunchOptionsSaveStatus.ConfigUnrecognised, result.Status);
        Assert.Equal(Document, await File.ReadAllTextAsync(ConfigPath));
    }

    /// <summary>
    /// Every save leaves a copy behind, so without pruning the directory beside Steam's own
    /// configuration grows by a hundred and thirty kilobytes each time and never shrinks.
    /// </summary>
    [Fact]
    public async Task SavingKeepsOnlyTheNewestBackups()
    {
        var service = CreateService(new StubSteamClient(), backupsToKeep: 1);

        for (var i = 0; i < 3; i++)
        {
            if (i > 0)
            {
                await Task.Delay(1100);
            }

            await service.SaveAsync(AppId, $"DXVK_HDR={i} %command%");
        }

        var kept = Directory
            .EnumerateFiles(Path.GetDirectoryName(ConfigPath)!, "localconfig.vdf.protontune-*.bak")
            .ToList();

        Assert.Single(kept);
    }

    private sealed class StubSettings(AppSettings settings) : IAppSettingsService
    {
        public Task<AppSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(AppSettings updated, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubInstallLocator(string? root) : ISteamInstallLocator
    {
        public string? Locate() => root;
    }

    private sealed class StubSteamClient : ISteamClient
    {
        private readonly List<string> _sequence = [];

        public bool Running { get; init; }

        public bool GameRunning { get; init; }

        public bool ShutdownSucceeds { get; init; } = true;

        public int ShutdownCalls { get; private set; }

        public int StartCalls { get; private set; }

        /// <summary>
        /// Shutdown, write, and start in the order they happened. The write is recorded by
        /// watching <see cref="IsRunning" />, which the service only calls before writing.
        /// </summary>
        public IReadOnlyList<string> Sequence => _sequence;

        private bool _hasExited;

        public bool IsRunning() => Running && !_hasExited;

        public bool IsGameRunning() => GameRunning;

        public Task<bool> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            ShutdownCalls++;
            _sequence.Add("shutdown");

            if (ShutdownSucceeds)
            {
                _hasExited = true;
                _sequence.Add("write");
            }

            return Task.FromResult(ShutdownSucceeds);
        }

        public bool Start()
        {
            StartCalls++;
            _sequence.Add("start");

            return true;
        }
    }
}
