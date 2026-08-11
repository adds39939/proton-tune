using Microsoft.Extensions.Logging;
using ProtonTune.Core.Launch;
using ProtonTune.Services.Settings;

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
    IAppSettingsService settings,
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
    public Task<LaunchOptionsSaveResult> SaveAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default) =>
        SaveManyAsync(new Dictionary<uint, string> { [appId] = launchOptions }, cancellationToken);

    /// <inheritdoc />
    public Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        CancellationToken cancellationToken = default) =>
        SaveManyAsync(launchOptionsByApp, NoCompatTools, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// The order is load bearing. Both documents are read only after Steam has gone, since it
    /// rewrites them as it exits and anything read earlier is already stale; both are then
    /// prepared in full before either is written, so a file that turns out not to be the document
    /// expected stops the save while everything is still untouched.
    /// </remarks>
    public async Task<LaunchOptionsSaveResult> SaveManyAsync(
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken = default)
    {
        if (launchOptionsByApp.Count == 0 && compatToolsByApp.Count == 0)
        {
            return new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved);
        }

        string? userConfigPath = null;

        if (launchOptionsByApp.Count > 0 && (userConfigPath = FindUserConfig()) is null)
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.NoUserConfig,
                "No Steam user configuration was found to write to.");
        }

        string? installConfigPath = null;

        if (compatToolsByApp.Count > 0)
        {
            if (installLocator.Locate() is not { } steamRoot)
            {
                return new LaunchOptionsSaveResult(
                    LaunchOptionsSaveStatus.NoUserConfig,
                    "No Steam installation was found to write to.");
            }

            installConfigPath = SteamCompatTools.ConfigPathIn(steamRoot);
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
            var edits = new List<PendingEdit>();

            if (userConfigPath is not null)
            {
                var document = await File.ReadAllTextAsync(userConfigPath, cancellationToken).ConfigureAwait(false);
                var updated = document;

                foreach (var (appId, launchOptions) in launchOptionsByApp)
                {
                    if (SteamConfigText.SetValue(updated, PathTo(appId), launchOptions) is not { } next)
                    {
                        return Restart(Unrecognised(userConfigPath));
                    }

                    updated = next;
                }

                edits.Add(new PendingEdit(userConfigPath, document, updated));
            }

            if (installConfigPath is not null)
            {
                var document = await File.ReadAllTextAsync(installConfigPath, cancellationToken).ConfigureAwait(false);
                var updated = document;

                foreach (var (appId, toolName) in compatToolsByApp)
                {
                    foreach (var (path, value) in SteamCompatTools.Assignment(appId, toolName))
                    {
                        if (SteamConfigText.SetValue(updated, path, value) is not { } next)
                        {
                            return Restart(Unrecognised(installConfigPath));
                        }

                        updated = next;
                    }
                }

                edits.Add(new PendingEdit(installConfigPath, document, updated));
            }

            var backupPaths = new List<string>();

            foreach (var edit in edits)
            {
                backupPaths.Add(await BackUpAsync(edit.Path, edit.Original, cancellationToken).ConfigureAwait(false));

                await WriteAtomicallyAsync(edit.Path, edit.Updated, cancellationToken).ConfigureAwait(false);
            }

            await PruneBackupsAsync(cancellationToken).ConfigureAwait(false);

            var mismatched = await FindMismatchesAsync(
                userConfigPath,
                launchOptionsByApp,
                installConfigPath,
                compatToolsByApp,
                cancellationToken).ConfigureAwait(false);

            if (mismatched.Count > 0)
            {
                return Restart(new LaunchOptionsSaveResult(
                    LaunchOptionsSaveStatus.WriteFailed,
                    $"Written, but read back differently for {string.Join(", ", mismatched)}. " +
                    $"The previous version is at {string.Join(" and ", backupPaths)}.")
                {
                    BackupPath = backupPaths[0]
                });
            }

            logger.LogInformation(
                "Wrote launch options for {AppCount} apps and Proton builds for {ToolCount}; " +
                "previous configuration at {BackupPaths}.",
                launchOptionsByApp.Count,
                compatToolsByApp.Count,
                string.Join(", ", backupPaths));

            return Restart(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved) { BackupPath = backupPaths[0] });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not write the Steam configuration.");

            return Restart(new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.WriteFailed, e.Message));
        }

        LaunchOptionsSaveResult Restart(LaunchOptionsSaveResult result)
        {
            if (steamWasRunning)
            {
                steamClient.Start();
            }

            return result with { SteamWasRestarted = steamWasRunning };
        }
    }

    private static LaunchOptionsSaveResult Unrecognised(string path) =>
        new(LaunchOptionsSaveStatus.ConfigUnrecognised,
            $"{path} was not in the expected format. Nothing was changed.");

    /// <summary>
    /// Reads both files back and reports what did not survive the write, described so the message
    /// says which change was lost rather than only which app it belonged to.
    /// </summary>
    private static async Task<IReadOnlyList<string>> FindMismatchesAsync(
        string? userConfigPath,
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        string? installConfigPath,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken)
    {
        var mismatched = new List<string>();

        if (userConfigPath is not null)
        {
            var readBack = await File.ReadAllTextAsync(userConfigPath, cancellationToken).ConfigureAwait(false);

            mismatched.AddRange(launchOptionsByApp
                .Where(entry => SteamConfigText.GetValue(readBack, PathTo(entry.Key)) != entry.Value)
                .Select(entry => $"the launch options of {entry.Key}"));
        }

        if (installConfigPath is not null)
        {
            var readBack = await File.ReadAllTextAsync(installConfigPath, cancellationToken).ConfigureAwait(false);

            mismatched.AddRange(compatToolsByApp
                .Where(entry => SteamConfigText.GetValue(
                    readBack,
                    SteamCompatTools.PathTo(entry.Key, SteamCompatTools.NameKey)) != entry.Value)
                .Select(entry => $"the Proton build of {entry.Key}"));
        }

        return mismatched;
    }

    /// <summary>A file about to be replaced, and what it is being replaced with.</summary>
    private sealed record PendingEdit(string Path, string Original, string Updated);

    private static readonly IReadOnlyDictionary<uint, string> NoCompatTools = new Dictionary<uint, string>();

    /// <summary>
    /// Keeps the newest few backups and removes the rest, to the count the user has chosen.
    /// </summary>
    /// <remarks>
    /// Never allowed to fail a save. The write has already happened by this point, and reporting
    /// a successful change as a failure because some old copies could not be tidied away would be
    /// worse than the untidiness.
    /// </remarks>
    private async Task PruneBackupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var keep = (await settings.GetAsync(cancellationToken).ConfigureAwait(false)).BackupsToKeep;

            if (installLocator.Locate() is { } steamRoot)
            {
                SteamConfigBackupStore.Prune(steamRoot, keep, logger);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not remove old configuration backups.");
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
        var backupPath = SteamConfigBackup.NameFor(configPath, DateTimeOffset.Now);

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
