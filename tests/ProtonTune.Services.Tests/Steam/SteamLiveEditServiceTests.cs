using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// Switching live editing on writes a file into the Steam directory and closes the client someone
/// is using, so it has to do exactly that and nothing more — and report honestly whether Steam is
/// offering the interface yet.
/// </summary>
public sealed class SteamLiveEditServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("protontune-liveedit-").FullName;

    private string MarkerPath => Path.Combine(_root, ".cef-enable-remote-debugging");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private SteamLiveEditService CreateService(
        StubSteamClient? client = null,
        StubDebugPort? port = null) =>
        CreateServiceFor(_root, client, port);

    /// <summary>A service that can find no Steam at all, which is a machine without it.</summary>
    private static SteamLiveEditService CreateServiceWithoutSteam() => CreateServiceFor(null);

    private static SteamLiveEditService CreateServiceFor(
        string? steamRoot,
        StubSteamClient? client = null,
        StubDebugPort? port = null) =>
        new(new StubInstallLocator(steamRoot),
            client ?? new StubSteamClient(),
            port ?? new StubDebugPort(),
            NullLogger<SteamLiveEditService>.Instance);

    [Fact]
    public async Task IsOffWhenNothingHasAskedForIt()
    {
        var state = await CreateService().GetStateAsync();

        Assert.True(state.IsSteamInstalled);
        Assert.False(state.IsEnabled);
        Assert.False(state.IsActive);
        Assert.True(state.IsSettled);
        Assert.Equal(MarkerPath, state.MarkerPath);
    }

    [Fact]
    public async Task ReportsNoInstallationRatherThanGuessingAtAPath()
    {
        var state = await CreateServiceWithoutSteam().GetStateAsync();

        Assert.False(state.IsSteamInstalled);
        Assert.Null(state.MarkerPath);
    }

    /// <summary>
    /// The file is what Steam reads, so it is the only thing that decides what has been asked
    /// for — including when someone else, or an earlier ProtonTune, put it there.
    /// </summary>
    [Fact]
    public async Task NoticesTheFileWhateverPutItThere()
    {
        await File.WriteAllTextAsync(MarkerPath, string.Empty);

        var state = await CreateService(port: new StubDebugPort { Listening = true }).GetStateAsync();

        Assert.True(state.IsEnabled);
        Assert.True(state.IsActive);
        Assert.True(state.IsSettled);
    }

    /// <summary>
    /// Asked for but not yet offered: where the client stands between switching it on and Steam
    /// coming back.
    /// </summary>
    [Fact]
    public async Task SeparatesWhatWasAskedForFromWhatSteamIsDoing()
    {
        await File.WriteAllTextAsync(MarkerPath, string.Empty);

        var state = await CreateService(port: new StubDebugPort { Listening = false }).GetStateAsync();

        Assert.True(state.IsEnabled);
        Assert.False(state.IsActive);
        Assert.False(state.IsSettled);
    }

    [Fact]
    public async Task WritesTheFileSteamLooksFor()
    {
        var result = await CreateService().SetEnabledAsync(true);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(MarkerPath));
        Assert.Empty(await File.ReadAllTextAsync(MarkerPath));
    }

    [Fact]
    public async Task TakesTheFileAwayAgain()
    {
        await File.WriteAllTextAsync(MarkerPath, string.Empty);

        var result = await CreateService().SetEnabledAsync(false);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(MarkerPath));
    }

    /// <summary>
    /// Switching off something that was never on is what a screen opened twice does, and it is
    /// not a failure.
    /// </summary>
    [Fact]
    public async Task IsUntroubledByTurningOffWhatWasNeverOn()
    {
        var result = await CreateService().SetEnabledAsync(false);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(MarkerPath));
    }

    [Fact]
    public async Task LeavesSteamAloneWhenItIsNotRunning()
    {
        var client = new StubSteamClient { Running = false };

        var result = await CreateService(client).SetEnabledAsync(true);

        Assert.Equal(SteamRestartOutcome.NotNeeded, result.Restart);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.Equal(0, client.StartCalls);
    }

    /// <summary>
    /// Steam only reads the file as it starts, so a running client has to be taken through a
    /// restart or the change sits there doing nothing.
    /// </summary>
    [Fact]
    public async Task RestartsARunningSteamSoTheChangeTakesEffect()
    {
        var client = new StubSteamClient { Running = true };

        var result = await CreateService(client, new StubDebugPort { Listening = true })
            .SetEnabledAsync(true);

        Assert.Equal(SteamRestartOutcome.Restarted, result.Restart);
        Assert.Equal(1, client.ShutdownCalls);
        Assert.Equal(1, client.StartCalls);
        Assert.True(result.State.IsActive);
    }

    /// <summary>
    /// The file is already correct by the time Steam would be closed and takes effect at the next
    /// restart, so a session in progress is worth more.
    /// </summary>
    [Fact]
    public async Task WillNotCloseSteamOutFromUnderAGame()
    {
        var client = new StubSteamClient { Running = true, GameRunning = true };

        var result = await CreateService(client).SetEnabledAsync(true);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(MarkerPath));
        Assert.Equal(SteamRestartOutcome.DeferredGameRunning, result.Restart);
        Assert.Equal(0, client.ShutdownCalls);
        Assert.Equal(0, client.StartCalls);
    }

    /// <summary>
    /// A Steam that will not close is left running rather than started a second time, which would
    /// leave two of them.
    /// </summary>
    [Fact]
    public async Task DoesNotStartASecondSteamWhenTheFirstWillNotClose()
    {
        var client = new StubSteamClient { Running = true, ClosesOnRequest = false };

        var result = await CreateService(client).SetEnabledAsync(true);

        Assert.True(result.IsSuccess);
        Assert.Equal(SteamRestartOutcome.RestartFailed, result.Restart);
        Assert.Equal(0, client.StartCalls);
    }

    /// <summary>
    /// The file is written before Steam is restarted, never after. The other order would restart
    /// the client for a change it could not yet see.
    /// </summary>
    [Fact]
    public async Task WritesTheFileBeforeClosingSteam()
    {
        var client = new StubSteamClient { Running = true };

        client.OnShutdown = () => Assert.True(File.Exists(MarkerPath));

        await CreateService(client).SetEnabledAsync(true);

        Assert.Equal(1, client.ShutdownCalls);
    }

    [Fact]
    public async Task RemovesTheFileBeforeClosingSteam()
    {
        await File.WriteAllTextAsync(MarkerPath, string.Empty);

        var client = new StubSteamClient { Running = true };

        client.OnShutdown = () => Assert.False(File.Exists(MarkerPath));

        await CreateService(client).SetEnabledAsync(false);

        Assert.Equal(1, client.ShutdownCalls);
    }

    [Fact]
    public async Task ReportsAMissingSteamInsteadOfWritingSomewhereElse()
    {
        var result = await CreateServiceWithoutSteam().SetEnabledAsync(true);

        Assert.False(result.IsSuccess);
        Assert.Equal(SteamLiveEditChange.NoSteamInstall, result.Status);
        Assert.False(File.Exists(MarkerPath));
    }

    /// <summary>
    /// Steam answers on its port a moment after the process appears, so reporting the state the
    /// instant it is started would say "off" straight after switching it on.
    /// </summary>
    [Fact]
    public async Task WaitsForSteamToStartAnsweringBeforeReportingBack()
    {
        var port = new StubDebugPort { ListeningOnceWaitedFor = true };

        var result = await CreateService(new StubSteamClient { Running = true }, port).SetEnabledAsync(true);

        Assert.True(port.WasWaitedFor);
        Assert.True(result.State.IsActive);
    }

    /// <summary>
    /// Nothing to wait for when Steam was not restarted — it is not coming up, so waiting would
    /// only hold the screen for as long as the timeout allows.
    /// </summary>
    [Fact]
    public async Task DoesNotWaitOnASteamThatWasNeverRestarted()
    {
        var port = new StubDebugPort();

        await CreateService(new StubSteamClient { Running = false }, port).SetEnabledAsync(true);

        Assert.False(port.WasWaitedFor);
    }

    /// <summary>
    /// Nor when switching off. The port goes as Steam closes, so waiting for it to answer would
    /// wait for something that is not meant to happen.
    /// </summary>
    [Fact]
    public async Task DoesNotWaitForAPortItJustAskedToClose()
    {
        await File.WriteAllTextAsync(MarkerPath, string.Empty);

        var port = new StubDebugPort();

        await CreateService(new StubSteamClient { Running = true }, port).SetEnabledAsync(false);

        Assert.False(port.WasWaitedFor);
    }

    private sealed class StubInstallLocator(string? root) : ISteamInstallLocator
    {
        public string? Locate() => root;
    }

    private sealed class StubDebugPort : ISteamDebugPort
    {
        /// <summary>Whether it answers at all.</summary>
        public bool Listening { get; init; }

        /// <summary>
        /// Whether it starts answering once waited for, standing in for a Steam that comes up
        /// shortly after being started.
        /// </summary>
        public bool ListeningOnceWaitedFor { get; init; }

        public bool WasWaitedFor { get; private set; }

        public Task<bool> IsListeningAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Listening || (ListeningOnceWaitedFor && WasWaitedFor));

        public Task<bool> WaitUntilListeningAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            WasWaitedFor = true;

            return IsListeningAsync(cancellationToken);
        }
    }

    private sealed class StubSteamClient : ISteamClient
    {
        private bool _hasExited;

        public bool Running { get; init; }

        public bool GameRunning { get; init; }

        /// <summary>Whether it goes when asked, so a client that hangs can be tested too.</summary>
        public bool ClosesOnRequest { get; init; } = true;

        /// <summary>Checked as Steam is asked to close, to pin down what has happened by then.</summary>
        public Action? OnShutdown { get; set; }

        public int ShutdownCalls { get; private set; }

        public int StartCalls { get; private set; }

        public bool IsRunning() => Running && !_hasExited;

        public bool IsGameRunning() => GameRunning;

        public Task<bool> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            ShutdownCalls++;

            OnShutdown?.Invoke();

            if (!ClosesOnRequest)
            {
                return Task.FromResult(false);
            }

            _hasExited = true;

            return Task.FromResult(true);
        }

        public bool Start()
        {
            StartCalls++;

            return true;
        }

        public bool LaunchGame(uint appId)
        {
            LaunchedAppIds.Add(appId);

            return true;
        }

        public List<uint> LaunchedAppIds { get; } = [];
    }
}
