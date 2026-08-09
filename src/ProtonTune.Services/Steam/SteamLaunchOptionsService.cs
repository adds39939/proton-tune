using Microsoft.Extensions.Logging;
using ProtonTune.Core.Launch;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="ISteamLaunchOptionsService" />
/// <remarks>
/// Launch options live per Steam user, in
/// <c>userdata/&lt;account&gt;/config/localconfig.vdf</c>, under
/// <c>UserLocalConfigStore/Software/Valve/Steam/apps/&lt;appid&gt;/LaunchOptions</c>.
/// </remarks>
public sealed class SteamLaunchOptionsService(
    ISteamInstallLocator installLocator,
    ISteamClient steamClient,
    ILogger<SteamLaunchOptionsService> logger) : ISteamLaunchOptionsService
{
    /// <summary>
    /// How long to wait for Steam to exit. It flushes its configuration on the way out, so this
    /// covers a slow write rather than a hung process.
    /// </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task<LaunchOptions> GetAsync(uint appId, CancellationToken cancellationToken = default)
    {
        var configPath = FindUserConfig();

        if (configPath is null)
        {
            return new LaunchOptions();
        }

        try
        {
            var document = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);

            return LaunchOptions.Parse(SteamConfigText.GetValue(document, PathTo(appId)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not read {ConfigPath}.", configPath);

            return new LaunchOptions();
        }
    }

    /// <inheritdoc />
    public bool RequiresSteamRestart() => steamClient.IsRunning();

    /// <inheritdoc />
    public bool IsGameRunning() => steamClient.IsGameRunning();

    /// <inheritdoc />
    public async Task<LaunchOptionsSaveResult> SaveAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default)
    {
        var configPath = FindUserConfig();

        if (configPath is null)
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.NoUserConfig,
                "No Steam user configuration was found to write to.");
        }

        if (steamClient.IsGameRunning())
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.GameRunning,
                "A game is running. Close it before changing launch options.");
        }

        var steamWasRunning = steamClient.IsRunning();

        if (steamWasRunning && !await steamClient.ShutdownAsync(ShutdownTimeout, cancellationToken).ConfigureAwait(false))
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.SteamStillRunning,
                $"Steam did not close within {ShutdownTimeout.TotalSeconds:0} seconds. Nothing was changed.");
        }

        try
        {
            // Read only now. Steam rewrites this file as it exits, so anything read before the
            // shutdown is already stale and would undo whatever else changed in the meantime.
            var document = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            var updated = SteamConfigText.SetValue(document, PathTo(appId), launchOptions);

            if (updated is null)
            {
                return Restart(new LaunchOptionsSaveResult(
                    LaunchOptionsSaveStatus.ConfigUnrecognised,
                    "The Steam configuration file was not in the expected format. Nothing was changed."));
            }

            var backupPath = await BackUpAsync(configPath, document, cancellationToken).ConfigureAwait(false);

            await WriteAtomicallyAsync(configPath, updated, cancellationToken).ConfigureAwait(false);

            var written = SteamConfigText.GetValue(
                await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false),
                PathTo(appId));

            if (written != launchOptions)
            {
                return Restart(new LaunchOptionsSaveResult(
                    LaunchOptionsSaveStatus.WriteFailed,
                    $"The file was written but read back differently. The previous version is at {backupPath}.")
                {
                    BackupPath = backupPath
                });
            }

            logger.LogInformation("Wrote launch options for {AppId}; previous configuration at {BackupPath}.",
                appId, backupPath);

            return Restart(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved) { BackupPath = backupPath });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not write {ConfigPath}.", configPath);

            return Restart(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.WriteFailed, e.Message));
        }

        LaunchOptionsSaveResult Restart(LaunchOptionsSaveResult result)
        {
            // Steam goes back up however the write went, so the user is never left without it
            // because ProtonTune failed.
            if (steamWasRunning)
            {
                steamClient.Start();
            }

            return result with { SteamWasRestarted = steamWasRunning };
        }
    }

    /// <summary>
    /// Copies the configuration aside before it is changed, named so several backups can coexist.
    /// </summary>
    private static async Task<string> BackUpAsync(
        string configPath,
        string document,
        CancellationToken cancellationToken)
    {
        var backupPath = $"{configPath}.protontune-{DateTime.Now:yyyyMMdd-HHmmss}.bak";

        await File.WriteAllTextAsync(backupPath, document, cancellationToken).ConfigureAwait(false);

        return backupPath;
    }

    /// <summary>
    /// Writes through a temporary file in the same directory, then moves it into place, so an
    /// interrupted write cannot leave a half-written configuration behind.
    /// </summary>
    private static async Task WriteAtomicallyAsync(
        string configPath,
        string document,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{configPath}.protontune-tmp";

        await File.WriteAllTextAsync(temporaryPath, document, cancellationToken).ConfigureAwait(false);

        File.Move(temporaryPath, configPath, overwrite: true);
    }

    /// <summary>The key path a game's launch options live at.</summary>
    private static string[] PathTo(uint appId) =>
        ["UserLocalConfigStore", "Software", "Valve", "Steam", "apps", appId.ToString(), "LaunchOptions"];

    /// <summary>
    /// Finds the <c>localconfig.vdf</c> of the Steam user to act on.
    /// </summary>
    /// <remarks>
    /// Most machines have exactly one. Where several accounts have signed in, the most recently
    /// written file is the one belonging to the account currently in use — Steam rewrites it
    /// throughout a session, so its timestamp tracks the active user closely.
    /// </remarks>
    private string? FindUserConfig()
    {
        var steamRoot = installLocator.Locate();

        if (steamRoot is null)
        {
            logger.LogWarning("No Steam installation was found on this machine.");

            return null;
        }

        var userdata = Path.Combine(steamRoot, "userdata");

        if (!Directory.Exists(userdata))
        {
            logger.LogWarning("Steam at {SteamRoot} has no userdata directory.", steamRoot);

            return null;
        }

        try
        {
            var configs = Directory
                .EnumerateDirectories(userdata)
                .Select(account => Path.Combine(account, "config", "localconfig.vdf"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();

            if (configs.Count == 0)
            {
                logger.LogWarning("No Steam user configuration was found under {UserDataPath}.", userdata);

                return null;
            }

            if (configs.Count > 1)
            {
                logger.LogInformation(
                    "{AccountCount} Steam accounts found; using the most recently active configuration {ConfigPath}.",
                    configs.Count,
                    configs[0]);
            }

            return configs[0];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not search {UserDataPath} for a Steam user configuration.", userdata);

            return null;
        }
    }
}
