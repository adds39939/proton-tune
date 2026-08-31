using Microsoft.Extensions.Logging;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="ISteamLiveEditService" />
public sealed class SteamLiveEditService(
    ISteamInstallLocator installLocator,
    ISteamClient steamClient,
    ISteamDebugPort debugPort,
    ILogger<SteamLiveEditService> logger) : ISteamLiveEditService
{
    /// <summary>
    /// The file Steam looks for as it starts. Its contents are never read — only whether it is
    /// there — so it is written empty.
    /// </summary>
    private const string MarkerFileName = ".cef-enable-remote-debugging";

    /// <summary>The same allowance the rest of the application gives Steam to exit.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to keep watching for the debugging interface after starting Steam.
    /// </summary>
    /// <remarks>
    /// Steam answers on the port a second or two after the process appears, not immediately, so
    /// reading the state the moment it is started would report live editing as off directly after
    /// switching it on. Generous, because a cold start is slower than a warm one and reporting it
    /// as off is worse than the screen taking a moment longer to settle.
    /// </remarks>
    private static readonly TimeSpan ActivationTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task<SteamLiveEditState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        if (installLocator.Locate() is not { } steamRoot)
        {
            return new SteamLiveEditState();
        }

        var markerPath = Path.Combine(steamRoot, MarkerFileName);

        return new SteamLiveEditState
        {
            IsSteamInstalled = true,
            IsEnabled = File.Exists(markerPath),
            IsActive = await debugPort.IsListeningAsync(cancellationToken).ConfigureAwait(false),
            MarkerPath = markerPath
        };
    }

    /// <inheritdoc />
    public async Task<SteamLiveEditResult> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (installLocator.Locate() is not { } steamRoot)
        {
            return new SteamLiveEditResult(
                SteamLiveEditChange.NoSteamInstall,
                "No Steam installation was found to change.");
        }

        var markerPath = Path.Combine(steamRoot, MarkerFileName);

        try
        {
            if (enabled)
            {
                await File.WriteAllTextAsync(markerPath, string.Empty, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                File.Delete(markerPath);
            }

            logger.LogInformation(
                "Live editing {Action} at {MarkerPath}.",
                enabled ? "asked for" : "withdrawn",
                markerPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(
                e,
                "Could not {Action} {MarkerPath}.",
                enabled ? "write" : "remove",
                markerPath);

            return new SteamLiveEditResult(SteamLiveEditChange.Failed, e.Message)
            {
                State = await GetStateAsync(cancellationToken).ConfigureAwait(false)
            };
        }

        var restart = await RestartSteamAsync(cancellationToken).ConfigureAwait(false);

        if (enabled && restart == SteamRestartOutcome.Restarted)
        {
            await debugPort.WaitUntilListeningAsync(ActivationTimeout, cancellationToken).ConfigureAwait(false);
        }

        return new SteamLiveEditResult(SteamLiveEditChange.Applied)
        {
            Restart = restart,
            State = await GetStateAsync(cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Closes Steam and starts it again, since the file is only read as Steam starts.
    /// </summary>
    /// <remarks>
    /// A running game is checked for first and stops the restart rather than the whole change.
    /// The file is already correct by this point and costs nothing sitting there, so ending
    /// someone's session to make it take effect a few minutes sooner would be the wrong trade —
    /// it takes effect the next time Steam starts either way.
    /// </remarks>
    private async Task<SteamRestartOutcome> RestartSteamAsync(CancellationToken cancellationToken)
    {
        if (!steamClient.IsRunning())
        {
            return SteamRestartOutcome.NotNeeded;
        }

        if (steamClient.IsGameRunning())
        {
            logger.LogInformation("A game is running, so Steam was left alone.");

            return SteamRestartOutcome.DeferredGameRunning;
        }

        if (!await steamClient.ShutdownAsync(ShutdownTimeout, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("Steam did not close, so it is still running as it was.");

            return SteamRestartOutcome.RestartFailed;
        }

        steamClient.Start();

        return SteamRestartOutcome.Restarted;
    }
}
