using Microsoft.Extensions.Logging;
using ProtonTune.Services.Profiles;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="ISteamConfigBackupService" />
public sealed class SteamConfigBackupService(
    ISteamInstallLocator installLocator,
    ISteamClient steamClient,
    IGlobalProfileService profile,
    ILogger<SteamConfigBackupService> logger) : ISteamConfigBackupService
{
    /// <summary>The same allowance the writer gives Steam to exit, for the same reason.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public Task<IReadOnlyList<SteamConfigBackup>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (installLocator.Locate() is not { } steamRoot)
        {
            return Task.FromResult<IReadOnlyList<SteamConfigBackup>>([]);
        }

        var backups = SteamConfigBackupStore
            .List(steamRoot, logger)
            .OrderByDescending(backup => backup.CreatedAt)
            .ThenBy(backup => backup.TargetName, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<SteamConfigBackup>>(backups);
    }

    /// <inheritdoc />
    public Task<int> PruneAsync(int keep, CancellationToken cancellationToken = default) =>
        Task.FromResult(installLocator.Locate() is { } steamRoot
            ? SteamConfigBackupStore.Prune(steamRoot, keep, logger)
            : 0);

    /// <inheritdoc />
    public async Task<SteamConfigRestoreResult> RestoreAsync(
        SteamConfigBackup backup,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backup.Path))
        {
            return new SteamConfigRestoreResult(false, "That backup is no longer on disk.");
        }

        if (steamClient.IsGameRunning())
        {
            return new SteamConfigRestoreResult(false, "A game is running. Close it before restoring.");
        }

        var steamWasRunning = steamClient.IsRunning();

        // The same order a save follows, and for the same reason: Steam holds these files in
        // memory and writes them out as it exits, so anything restored underneath a running Steam
        // is discarded moments later.
        if (steamWasRunning &&
            !await steamClient.ShutdownAsync(ShutdownTimeout, cancellationToken).ConfigureAwait(false))
        {
            return new SteamConfigRestoreResult(
                false,
                $"Steam did not close within {ShutdownTimeout.TotalSeconds:0} seconds. Nothing was restored.");
        }

        try
        {
            // What is being replaced is kept first, so restoring the wrong backup is no more final
            // than the save that prompted it.
            var replacedPath = SteamConfigBackup.NameFor(backup.TargetPath, DateTimeOffset.Now);

            File.Copy(backup.TargetPath, replacedPath, overwrite: true);

            var temporaryPath = $"{backup.TargetPath}.protontune-tmp";

            File.Copy(backup.Path, temporaryPath, overwrite: true);
            File.Move(temporaryPath, backup.TargetPath, overwrite: true);

            logger.LogInformation(
                "Restored {TargetPath} from {BackupPath}; what it replaced is at {ReplacedPath}.",
                backup.TargetPath,
                backup.Path,
                replacedPath);

            // The restored file may hold launch options a game had before it followed the profile,
            // so what ProtonTune believes about that game is now a claim rather than a fact.
            var unlinked = await profile.ReconcileLinksAsync(cancellationToken).ConfigureAwait(false);

            return Restart(new SteamConfigRestoreResult(true)
            {
                ReplacedPath = replacedPath,
                UnlinkedFromProfile = unlinked
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not restore {TargetPath}.", backup.TargetPath);

            return Restart(new SteamConfigRestoreResult(false, e.Message));
        }

        SteamConfigRestoreResult Restart(SteamConfigRestoreResult result)
        {
            if (steamWasRunning)
            {
                steamClient.Start();
            }

            return result with { SteamWasRestarted = steamWasRunning };
        }
    }
}
