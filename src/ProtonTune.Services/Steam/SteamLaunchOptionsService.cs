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
    ISteamClientBridge bridge,
    IAppSettingsService settings,
    ILogger<SteamLaunchOptionsService> logger) : ISteamLaunchOptionsService
{
    /// <summary>
    /// How long to wait for Steam to exit. It flushes its configuration on the way out, so this
    /// covers a slow write rather than a hung process.
    /// </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How many times to ask Steam what it holds before deciding a change did not take, and how
    /// long to leave between asking. Short, since Steam has already said yes by this point.
    /// </summary>
    private const int SettleAttempts = 5;

    private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(400);

    /// <inheritdoc />
    public async Task<LaunchOptions> GetAsync(uint appId, CancellationToken cancellationToken = default) =>
        (await GetManyAsync([appId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(appId)
        ?? new LaunchOptions();

    /// <inheritdoc />
    /// <remarks>
    /// Asks the running client before reading the file: a Steam that is up holds current values in
    /// memory and writes them on its own schedule, so the file is behind whenever anything has
    /// just changed. Reading it first is how a change gets undone.
    /// </remarks>
    public async Task<IReadOnlyDictionary<uint, LaunchOptions>> GetManyAsync(
        IReadOnlyCollection<uint> appIds,
        CancellationToken cancellationToken = default)
    {
        var found = new Dictionary<uint, LaunchOptions>();

        if (appIds.Count == 0)
        {
            return found;
        }

        var outstanding = new List<uint>();

        await using (var session = await bridge.ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var appId in appIds)
            {
                if (session is not null &&
                    await session.GetAppDetailsAsync(appId, cancellationToken).ConfigureAwait(false) is { } details)
                {
                    found[appId] = LaunchOptions.Parse(details.LaunchOptions);
                }
                else
                {
                    outstanding.Add(appId);
                }
            }
        }

        if (outstanding.Count == 0)
        {
            return found;
        }

        var document = await ReadUserConfigAsync(cancellationToken).ConfigureAwait(false);

        foreach (var appId in outstanding)
        {
            found[appId] = document is null
                ? new LaunchOptions()
                : LaunchOptions.Parse(SteamConfigText.GetValue(document, PathTo(appId)));
        }

        return found;
    }

    /// <summary>
    /// Reads the account's configuration, or <see langword="null"/> when there is none to read.
    /// </summary>
    /// <remarks>
    /// An unreadable file is a warning rather than a failure: opening a game's configuration empty
    /// beats refusing to open it.
    /// </remarks>
    private async Task<string?> ReadUserConfigAsync(CancellationToken cancellationToken)
    {
        if (FindUserConfig() is not { } configPath)
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not read {ConfigPath}.", configPath);

            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SteamSaveMethod> GetSaveMethodAsync(CancellationToken cancellationToken = default)
    {
        if (!steamClient.IsRunning())
        {
            return SteamSaveMethod.Files;
        }

        await using var session = await bridge.ConnectAsync(cancellationToken).ConfigureAwait(false);

        if (session is not null)
        {
            return SteamSaveMethod.Live;
        }

        return steamClient.IsGameRunning() ? SteamSaveMethod.Blocked : SteamSaveMethod.Restart;
    }

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
    /// Through the running client where it will take the change, through its files where it will
    /// not. Every failure to reach the client falls through quietly, since the long way still
    /// works.
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

        await using (var session = await bridge.ConnectAsync(cancellationToken).ConfigureAwait(false))
        {
            if (session is not null)
            {
                return await SaveThroughSteamAsync(
                    session,
                    launchOptionsByApp,
                    compatToolsByApp,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return await SaveThroughFilesAsync(launchOptionsByApp, compatToolsByApp, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Hands the change to the running Steam client, which applies it at once and writes it out
    /// itself.
    /// </summary>
    /// <remarks>
    /// Nothing is closed and no file of Steam's is touched, so no backup is taken — backups guard
    /// against ProtonTune editing files it does not own, which this path does not do. A game in
    /// progress is no obstacle either; the change applies at the next launch.
    /// </remarks>
    private async Task<LaunchOptionsSaveResult> SaveThroughSteamAsync(
        ISteamClientSession session,
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken)
    {
        var refused = new List<string>();

        foreach (var (appId, launchOptions) in launchOptionsByApp)
        {
            if (!await session.SetLaunchOptionsAsync(appId, launchOptions, cancellationToken).ConfigureAwait(false))
            {
                refused.Add($"the launch options of {appId}");
            }
        }

        foreach (var (appId, toolName) in compatToolsByApp)
        {
            if (!await session.SetCompatToolAsync(appId, toolName, cancellationToken).ConfigureAwait(false))
            {
                refused.Add($"the Proton build of {appId}");
            }
        }

        if (refused.Count > 0)
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.WriteFailed,
                $"Steam did not accept {string.Join(", ", refused)}. Anything not named here was applied.");
        }

        var mismatched = await FindLiveMismatchesAsync(
            session,
            launchOptionsByApp,
            compatToolsByApp,
            cancellationToken).ConfigureAwait(false);

        if (mismatched.Count > 0)
        {
            return new LaunchOptionsSaveResult(
                LaunchOptionsSaveStatus.WriteFailed,
                $"Steam took the change but reports something else for {string.Join(", ", mismatched)}.");
        }

        logger.LogInformation(
            "Set launch options for {AppCount} apps and Proton builds for {ToolCount} through the running client.",
            launchOptionsByApp.Count,
            compatToolsByApp.Count);

        return new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved);
    }

    /// <summary>
    /// Asks Steam back for what it now holds, and reports what does not match what was asked for.
    /// </summary>
    /// <remarks>
    /// Re-read until it settles: Steam accepts a change and updates what it reports a moment
    /// later, so the first answer can still describe the state before it. A cleared Proton build
    /// is not checked at all, since Steam reports the build it picked rather than the stored
    /// nothing.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> FindLiveMismatchesAsync(
        ISteamClientSession session,
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken)
    {
        var outstanding = new List<Expectation>();

        foreach (var (appId, launchOptions) in launchOptionsByApp)
        {
            outstanding.Add(new Expectation(
                appId,
                $"the launch options of {appId}",
                details => string.Equals(details.LaunchOptions, launchOptions, StringComparison.Ordinal)));
        }

        foreach (var (appId, toolName) in compatToolsByApp)
        {
            if (toolName.Length == 0)
            {
                continue;
            }

            outstanding.Add(new Expectation(
                appId,
                $"the Proton build of {appId}",
                details => string.Equals(details.CompatToolName, toolName, StringComparison.OrdinalIgnoreCase)));
        }

        for (var attempt = 1; outstanding.Count > 0 && attempt <= SettleAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(SettleInterval, cancellationToken).ConfigureAwait(false);
            }

            var readBack = new Dictionary<uint, SteamAppDetails?>();

            foreach (var appId in outstanding.Select(expectation => expectation.AppId).Distinct())
            {
                readBack[appId] = await session.GetAppDetailsAsync(appId, cancellationToken).ConfigureAwait(false);
            }

            outstanding.RemoveAll(expectation =>
                readBack[expectation.AppId] is { } details && expectation.Matches(details));
        }

        return outstanding.Select(expectation => expectation.Description).ToList();
    }

    /// <summary>One thing that was asked for, and how to recognise Steam having done it.</summary>
    private sealed record Expectation(uint AppId, string Description, Func<SteamAppDetails, bool> Matches);

    /// <summary>
    /// Writes the change into the files Steam owns, closing it first where it is running.
    /// </summary>
    /// <remarks>
    /// The order is load bearing: both documents are read only after Steam has gone, since it
    /// rewrites them as it exits, and both are prepared in full before either is written, so an
    /// unexpected document stops the save while everything is untouched.
    /// </remarks>
    private async Task<LaunchOptionsSaveResult> SaveThroughFilesAsync(
        IReadOnlyDictionary<uint, string> launchOptionsByApp,
        IReadOnlyDictionary<uint, string> compatToolsByApp,
        CancellationToken cancellationToken)
    {
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
    /// Never allowed to fail a save: the write has already happened, so untidiness beats reporting
    /// a successful change as a failure.
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
    /// Where several accounts have signed in, the most recently written file belongs to the one in
    /// use: Steam rewrites it throughout a session, so its timestamp tracks the active user.
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
