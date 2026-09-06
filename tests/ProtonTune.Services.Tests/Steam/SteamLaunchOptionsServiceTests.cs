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
    private SteamLaunchOptionsService CreateService(
        StubSteamClient client,
        int backupsToKeep = 10,
        StubBridge? bridge = null)
    {
        return new SteamLaunchOptionsService(
            new StubInstallLocator(_root),
            client,
            bridge ?? new StubBridge(),
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
            new StubBridge(),
            new StubSettings(new AppSettings()),
            NullLogger<SteamLaunchOptionsService>.Instance);

        var result = await service.SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.Equal(LaunchOptionsSaveStatus.NoUserConfig, result.Status);
    }

    /// <summary>
    /// A mapping is more than a name: Steam settles competing ones by priority and those from app
    /// metadata sit at 90, so a name written without one would silently lose.
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
    /// Every save leaves a copy behind, so without pruning the directory grows by about 130 KB
    /// each time and never shrinks.
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

        public bool LaunchGame(uint appId)
        {
            LaunchedAppIds.Add(appId);

            return true;
        }

        public List<uint> LaunchedAppIds { get; } = [];
    }

    /// <summary>
    /// A running Steam keeps running, and the file it holds in memory is left for Steam to write
    /// rather than spliced behind its back.
    /// </summary>
    [Fact]
    public async Task HandsTheChangeToARunningSteamWithoutClosingIt()
    {
        var client = new StubSteamClient { Running = true };
        var session = new StubSession();

        var result = await CreateService(client, bridge: new StubBridge(session))
            .SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.True(result.IsSuccess);
        Assert.False(result.SteamWasRestarted);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.Equal(0, client.StartCalls);
        Assert.Equal("DXVK_HDR=1 %command%", session.Held[AppId].LaunchOptions);
    }

    /// <summary>
    /// And leaves the file exactly as it was: Steam writes it out itself, and writing it here too
    /// would put two authors on one document with Steam's copy winning.
    /// </summary>
    [Fact]
    public async Task DoesNotTouchTheFileWhenSteamTakesTheChange()
    {
        var before = await File.ReadAllTextAsync(ConfigPath);

        await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(new StubSession()))
            .SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.Equal(before, await File.ReadAllTextAsync(ConfigPath));
    }

    /// <summary>
    /// Both halves of a change go through the one connection. Split across two, the second would
    /// be a second connection for what the user did once.
    /// </summary>
    [Fact]
    public async Task SendsLaunchOptionsAndProtonBuildThroughOneConnection()
    {
        var session = new StubSession();
        var bridge = new StubBridge(session);

        var result = await CreateService(new StubSteamClient { Running = true }, bridge: bridge)
            .SaveManyAsync(Only(AppId, "DXVK_HDR=1 %command%"), Only(AppId, "GE-Proton11-3"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, bridge.Connections);
        Assert.Equal("DXVK_HDR=1 %command%", session.Held[AppId].LaunchOptions);
        Assert.Equal("GE-Proton11-3", session.Held[AppId].CompatToolName);
    }

    /// <summary>
    /// One at a time. Two requests in flight against Steam's interface at once crash the client,
    /// so a batch has to be a sequence rather than a fan-out.
    /// </summary>
    [Fact]
    public async Task SendsABatchOneGameAtATime()
    {
        var session = new StubSession();

        await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(session))
            .SaveManyAsync(new Dictionary<uint, string>
            {
                [AppId] = "A=1 %command%",
                [AppId + 1] = "B=1 %command%",
                [AppId + 2] = "C=1 %command%"
            });

        Assert.Equal(
            [$"launch options {AppId}", $"launch options {AppId + 1}", $"launch options {AppId + 2}"],
            session.Calls.Where(call => call.StartsWith("launch options", StringComparison.Ordinal)));
    }

    /// <summary>The connection is given up afterwards rather than held across a Steam restart.</summary>
    [Fact]
    public async Task ClosesTheConnectionWhenItIsDone()
    {
        var session = new StubSession();

        await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(session))
            .SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.Equal(1, session.Disposals);
    }

    /// <summary>
    /// Nothing is closed, so a game in progress is not in the way.
    /// </summary>
    [Fact]
    public async Task SavesWhileAGameIsRunning()
    {
        var client = new StubSteamClient { Running = true, GameRunning = true };

        var result = await CreateService(client, bridge: new StubBridge(new StubSession()))
            .SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, client.ShutdownCalls);
    }

    /// <summary>
    /// A Steam that says no is reported by name rather than counted as done.
    /// </summary>
    [Fact]
    public async Task ReportsWhatSteamWouldNotAccept()
    {
        var session = new StubSession();

        session.Refusing.Add(AppId);

        var result = await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(session))
            .SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.False(result.IsSuccess);
        Assert.Equal(LaunchOptionsSaveStatus.WriteFailed, result.Status);
        Assert.Contains(AppId.ToString(), result.Message);
    }

    /// <summary>
    /// What Steam reports afterwards has to be what it was asked for. Steam accepting a request
    /// and then holding something else is the failure this catches.
    /// </summary>
    [Fact]
    public async Task ChecksWhatSteamHoldsAfterwards()
    {
        var session = new ContraryStubSession();

        var result = await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(session))
            .SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.False(result.IsSuccess);
        Assert.Equal(LaunchOptionsSaveStatus.WriteFailed, result.Status);
    }


    /// <summary>
    /// Steam accepts a change and updates what it reports a moment later, so the first read back
    /// can still describe the state before it. Seen against a running client with the Proton
    /// build; judged on that answer, every such save would be reported as failed.
    /// </summary>
    [Fact]
    public async Task WaitsForSteamToCatchUpWithItselfBeforeCallingItAMismatch()
    {
        var session = new LaggingStubSession(staleReads: 2);

        var result = await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(session))
            .SaveManyAsync(new Dictionary<uint, string>(), Only(AppId, "GE-Proton11-3"));

        Assert.True(result.IsSuccess);
        Assert.True(session.Reads > 2);
    }

    /// <summary>
    /// Asked to let Steam choose the build, Steam chooses one and reports it. Comparing that
    /// against the nothing it was given would call every successful reset a failure.
    /// </summary>
    [Fact]
    public async Task DoesNotCallASteamChosenBuildAMismatch()
    {
        var session = new StubSession();

        var result = await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(session))
            .SaveManyAsync(new Dictionary<uint, string>(), Only(AppId, string.Empty));

        Assert.True(result.IsSuccess);
        Assert.Equal("proton_experimental", session.Held[AppId].CompatToolName);
    }

    /// <summary>
    /// Read from the client rather than the file while Steam is up: Steam writes its copy out on
    /// its own schedule, so the file is behind whenever anything has just changed.
    /// </summary>
    [Fact]
    public async Task ReadsWhatTheRunningSteamHoldsRatherThanTheFile()
    {
        var session = new StubSession();

        session.Held[AppId] = new SteamAppDetails("CHANGED_IN_STEAM=1 %command%", string.Empty);

        var options = await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(session))
            .GetAsync(AppId);

        Assert.Equal("CHANGED_IN_STEAM=1 %command%", options.Format());
    }

    /// <summary>Falling back to the file when the client has nothing to say about a game.</summary>
    [Fact]
    public async Task FallsBackToTheFileWhenSteamDoesNotAnswerForTheGame()
    {
        var options = await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(new StubSession()))
            .GetAsync(AppId);

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", options.Format());
    }


    /// <summary>
    /// One connection for the batch: one game at a time would be a connection and a pass over the
    /// configuration file each.
    /// </summary>
    [Fact]
    public async Task ReadsAWholeBatchThroughOneConnection()
    {
        var session = new StubSession();

        session.Held[AppId] = new SteamAppDetails("A=1 %command%", string.Empty);
        session.Held[AppId + 1] = new SteamAppDetails("B=1 %command%", string.Empty);

        var bridge = new StubBridge(session);

        var found = await CreateService(new StubSteamClient { Running = true }, bridge: bridge)
            .GetManyAsync([AppId, AppId + 1]);

        Assert.Equal(1, bridge.Connections);
        Assert.Equal("A=1 %command%", found[AppId].Format());
        Assert.Equal("B=1 %command%", found[AppId + 1].Format());
    }

    /// <summary>
    /// Every game asked about is answered for, so a caller matching a library against a profile
    /// gets a value for each rather than a gap to interpret.
    /// </summary>
    [Fact]
    public async Task AnswersForEveryGameAskedAbout()
    {
        var found = await CreateService(new StubSteamClient()).GetManyAsync([AppId, 12210]);

        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", found[AppId].Format());
        Assert.Equal(string.Empty, found[12210].Format());
    }

    [Fact]
    public async Task SaysTheFilesCanBeWrittenWhenSteamIsNotRunning()
    {
        Assert.Equal(
            SteamSaveMethod.Files,
            await CreateService(new StubSteamClient()).GetSaveMethodAsync());
    }

    [Fact]
    public async Task SaysTheChangeGoesStraightInWhenSteamWillTakeIt()
    {
        Assert.Equal(
            SteamSaveMethod.Live,
            await CreateService(new StubSteamClient { Running = true }, bridge: new StubBridge(new StubSession()))
                .GetSaveMethodAsync());
    }

    [Fact]
    public async Task SaysSteamHasToBeRestartedWhenItWillNotTakeTheChange()
    {
        Assert.Equal(
            SteamSaveMethod.Restart,
            await CreateService(new StubSteamClient { Running = true }).GetSaveMethodAsync());
    }

    /// <summary>
    /// Only a Steam that would have to be closed is blocked by a game, which is what stops the
    /// screen warning about a restart that will not happen.
    /// </summary>
    [Fact]
    public async Task SaysNothingCanBeSavedWhileAGameRunsWithoutLiveEditing()
    {
        Assert.Equal(
            SteamSaveMethod.Blocked,
            await CreateService(new StubSteamClient { Running = true, GameRunning = true }).GetSaveMethodAsync());
    }

    /// <summary>
    /// A Steam that cannot be reached for live editing unless given a client to answer with, which
    /// is the state every test of the file path is describing.
    /// </summary>
    private sealed class StubBridge(ISteamClientSession? session = null) : ISteamClientBridge
    {
        public int Connections { get; private set; }

        public Task<ISteamClientSession?> ConnectAsync(CancellationToken cancellationToken = default)
        {
            Connections++;

            return Task.FromResult<ISteamClientSession?>(session);
        }
    }

    /// <summary>A running Steam that remembers what it was asked to change.</summary>
    private sealed class StubSession : ISteamClientSession
    {
        /// <summary>What Steam holds, which a write updates and a read reports.</summary>
        public Dictionary<uint, SteamAppDetails> Held { get; } = [];

        /// <summary>Games Steam refuses to change, standing in for a version that cannot.</summary>
        public HashSet<uint> Refusing { get; } = [];

        /// <summary>Recorded in order, so a batch can be shown never to overlap itself.</summary>
        public List<string> Calls { get; } = [];

        public int Disposals { get; private set; }

        public Task<SteamAppDetails?> GetAppDetailsAsync(
            uint appId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"read {appId}");

            return Task.FromResult(Held.GetValueOrDefault(appId));
        }

        public Task<bool> SetLaunchOptionsAsync(
            uint appId,
            string launchOptions,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"launch options {appId}");

            if (Refusing.Contains(appId))
            {
                return Task.FromResult(false);
            }

            Held[appId] = (Held.GetValueOrDefault(appId) ?? new SteamAppDetails(string.Empty, string.Empty))
                with { LaunchOptions = launchOptions };

            return Task.FromResult(true);
        }

        public Task<bool> SetCompatToolAsync(
            uint appId,
            string toolName,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"proton build {appId}");

            if (Refusing.Contains(appId))
            {
                return Task.FromResult(false);
            }

            Held[appId] = (Held.GetValueOrDefault(appId) ?? new SteamAppDetails(string.Empty, string.Empty))
                with { CompatToolName = toolName.Length == 0 ? "proton_experimental" : toolName };

            return Task.FromResult(true);
        }

        public ValueTask DisposeAsync()
        {
            Disposals++;

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A Steam that accepts every request and then holds something else.</summary>
    private sealed class ContraryStubSession : ISteamClientSession
    {
        public Task<SteamAppDetails?> GetAppDetailsAsync(
            uint appId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SteamAppDetails?>(new SteamAppDetails("something else entirely", string.Empty));

        public Task<bool> SetLaunchOptionsAsync(
            uint appId,
            string launchOptions,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> SetCompatToolAsync(
            uint appId,
            string toolName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// A Steam that takes a change but goes on reporting the old state for the first few reads,
    /// as the real client does.
    /// </summary>
    private sealed class LaggingStubSession(int staleReads) : ISteamClientSession
    {
        private string _held = string.Empty;

        public int Reads { get; private set; }

        public Task<SteamAppDetails?> GetAppDetailsAsync(
            uint appId,
            CancellationToken cancellationToken = default)
        {
            Reads++;

            return Task.FromResult<SteamAppDetails?>(
                new SteamAppDetails(string.Empty, Reads <= staleReads ? "proton_9" : _held));
        }

        public Task<bool> SetLaunchOptionsAsync(
            uint appId,
            string launchOptions,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> SetCompatToolAsync(
            uint appId,
            string toolName,
            CancellationToken cancellationToken = default)
        {
            _held = toolName;

            return Task.FromResult(true);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
