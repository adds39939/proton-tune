using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Dlss;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Dlss;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Profiles;

/// <summary>
/// The profile is ProtonTune's own state — Steam has nowhere to keep it — so it has to survive
/// restarts and cope with the file being absent or damaged.
/// </summary>
public sealed class GlobalProfileServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("protontune-profile-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private readonly RecordingLaunchOptionsService _steam = new();

    private GlobalProfileService CreateService() =>
        new(ProtonTuneStorage.At(_root),
            _steam,
            new StubDlssService(_root),
            NullLogger<GlobalProfileService>.Instance);

    [Fact]
    public async Task StartsEmpty() => Assert.True((await CreateService().GetAsync()).IsEmpty);

    [Fact]
    public async Task RemembersTheOptionsAcrossRestarts()
    {
        await CreateService().SaveAsync(LaunchOptions.Parse("PROTON_ENABLE_HDR=1 mangohud %command%"));

        // A second instance reads from disk rather than from the first one's memory.
        Assert.Equal("PROTON_ENABLE_HDR=1 mangohud %command%", (await CreateService().GetAsync()).Format());
    }

    [Fact]
    public async Task RemembersWhichGamesFollowIt()
    {
        var service = CreateService();

        await service.SetLinkedAsync(2357570, true);
        await service.SetLinkedAsync(2138720, true);
        await service.SetLinkedAsync(2138720, false);

        var reopened = CreateService();

        Assert.True(await reopened.IsLinkedAsync(2357570));
        Assert.False(await reopened.IsLinkedAsync(2138720));
        Assert.False(await reopened.IsLinkedAsync(993090));
    }

    [Fact]
    public async Task KeepsTheOptionsWhenLinkingAGame()
    {
        var service = CreateService();

        await service.SaveAsync(LaunchOptions.Parse("DXVK_HDR=1 %command%"));
        await service.SetLinkedAsync(2357570, true);

        var reopened = CreateService();

        Assert.Equal("DXVK_HDR=1 %command%", (await reopened.GetAsync()).Format());
        Assert.True(await reopened.IsLinkedAsync(2357570));
    }

    [Fact]
    public async Task KeepsTheLinksWhenTheOptionsChange()
    {
        var service = CreateService();

        await service.SetLinkedAsync(2357570, true);
        await service.SaveAsync(LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.True(await CreateService().IsLinkedAsync(2357570));
    }

    [Fact]
    public async Task LinkingTwiceIsHarmless()
    {
        var service = CreateService();

        await service.SetLinkedAsync(2357570, true);
        await service.SetLinkedAsync(2357570, true);
        await service.SetLinkedAsync(2357570, false);

        Assert.False(await CreateService().IsLinkedAsync(2357570));
    }

    [Fact]
    public async Task StartsFreshWhenTheFileIsDamaged()
    {
        // A profile is a convenience, not a record of the user's games. Losing it costs a retype;
        // refusing to start would cost far more.
        await File.WriteAllTextAsync(ProtonTuneStorage.At(_root).ProfileFile, "{ this is not json");

        var service = CreateService();

        Assert.True((await service.GetAsync()).IsEmpty);
        Assert.False(await service.IsLinkedAsync(2357570));
    }

    [Fact]
    public async Task CascadesToEveryGameFollowingIt()
    {
        var service = CreateService();

        await service.SetLinkedAsync(2357570, true);
        await service.SetLinkedAsync(993090, true);

        await service.SaveAndApplyAsync(LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(
            new Dictionary<uint, string>
            {
                [2357570] = "DXVK_HDR=1 %command%",
                [993090] = "DXVK_HDR=1 %command%"
            },
            _steam.LastBatch);
    }

    [Fact]
    public async Task WritesEveryGameInOnePass()
    {
        // One batch means one Steam shutdown. Saving them one at a time would close and reopen
        // Steam once per game.
        var service = CreateService();

        await service.SetLinkedAsync(2357570, true);
        await service.SetLinkedAsync(993090, true);
        await service.SaveAndApplyAsync(LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(1, _steam.BatchCount);
    }

    [Fact]
    public async Task LeavesGamesAloneWhenNoneFollowIt()
    {
        await CreateService().SaveAndApplyAsync(LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Equal(0, _steam.BatchCount);
    }

    [Fact]
    public async Task DoesNotStoreTheProfileWhenTheGamesCouldNotBeWritten()
    {
        // Otherwise the profile would claim settings the library never received.
        var service = CreateService();

        await service.SetLinkedAsync(2357570, true);
        _steam.Result = new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.GameRunning, "A game is running.");

        var result = await service.SaveAndApplyAsync(LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.False(result.IsSuccess);
        Assert.True((await CreateService().GetAsync()).IsEmpty);
    }

    [Fact]
    public async Task CarriesAGamesOwnDlssScriptAcross()
    {
        var service = CreateService();

        await service.SetLinkedAsync(2357570, true);
        await File.WriteAllTextAsync(new StubDlssService(_root).ScriptPathFor(2357570), "#!/bin/sh");

        await service.SaveAndApplyAsync(LaunchOptions.Parse("DXVK_HDR=1 %command%"));

        Assert.Contains("dlss-2357570.sh", _steam.LastBatch[2357570]);
        Assert.Contains("DXVK_HDR=1", _steam.LastBatch[2357570]);
    }

    [Fact]
    public async Task ResetClearsTheProfileAndEveryLink()
    {
        var service = CreateService();

        await service.SaveAsync(LaunchOptions.Parse("DXVK_HDR=1 %command%"));
        await service.SetLinkedAsync(2357570, true);

        await service.ResetAsync();

        var reopened = CreateService();

        Assert.True((await reopened.GetAsync()).IsEmpty);
        Assert.False(await reopened.IsLinkedAsync(2357570));

        // Resetting the profile must not reach into the games themselves.
        Assert.Equal(0, _steam.BatchCount);
    }

    [Fact]
    public async Task DoesNotDeadlockOnRepeatedUse()
    {
        // Reading takes a lock and writing takes it again; a missing release would hang here
        // rather than fail.
        var service = CreateService();

        for (var i = 0; i < 5; i++)
        {
            await service.GetAsync();
            await service.SaveAsync(LaunchOptions.Parse($"A{i}=1 %command%"));
            await service.SetLinkedAsync((uint)i, true);
        }

        Assert.Equal("A4=1 %command%", (await service.GetAsync()).Format());
    }

    /// <summary>Captures what would have been written to Steam.</summary>
    private sealed class RecordingLaunchOptionsService : ISteamLaunchOptionsService
    {
        public IReadOnlyDictionary<uint, string> LastBatch { get; private set; } =
            new Dictionary<uint, string>();

        public int BatchCount { get; private set; }

        public LaunchOptionsSaveResult Result { get; set; } = new(LaunchOptionsSaveStatus.Saved);

        public Task<LaunchOptions> GetAsync(uint appId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LaunchOptions());

        public bool RequiresSteamRestart() => false;

        public bool IsGameRunning() => false;

        public Task<LaunchOptionsSaveResult> SaveAsync(
            uint appId,
            string launchOptions,
            CancellationToken cancellationToken = default) =>
            SaveManyAsync(new Dictionary<uint, string> { [appId] = launchOptions }, cancellationToken);

        public Task<LaunchOptionsSaveResult> SaveManyAsync(
            IReadOnlyDictionary<uint, string> launchOptionsByApp,
            CancellationToken cancellationToken = default)
        {
            LastBatch = launchOptionsByApp;
            BatchCount++;

            return Task.FromResult(Result);
        }
    }

    /// <summary>Names scripts under the temporary root so none of this touches a real install.</summary>
    private sealed class StubDlssService(string root) : IDlssManagementService
    {
        public string ScriptPathFor(uint appId) => Path.Combine(root, $"dlss-{appId}.sh");

        public DlssGameStatus Inspect(SteamLibraryEntry entry) => new();

        public Task<string> ApplyAsync(
            SteamLibraryEntry entry,
            DlssRuntime runtime,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ScriptPathFor(entry.AppId));

        public Task RevertAsync(SteamLibraryEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
