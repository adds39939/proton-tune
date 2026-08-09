using Microsoft.Extensions.Logging.Abstractions;
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

    private readonly string _root = Directory.CreateTempSubdirectory("protontune-test-").FullName;

    private string ConfigPath => Path.Combine(_root, "userdata", "145618525", "config", "localconfig.vdf");

    public SteamLaunchOptionsServiceTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, Document);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SteamLaunchOptionsService CreateService(StubSteamClient client) =>
        new(new StubInstallLocator(_root), client, NullLogger<SteamLaunchOptionsService>.Instance);

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
        // The order is the whole point. Steam flushes its in-memory copy as it exits, so writing
        // first and restarting afterwards would discard the change moments later.
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
            NullLogger<SteamLaunchOptionsService>.Instance);

        var result = await service.SaveAsync(AppId, "DXVK_HDR=1 %command%");

        Assert.Equal(LaunchOptionsSaveStatus.NoUserConfig, result.Status);
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
