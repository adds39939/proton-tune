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
    ILogger<SteamLaunchOptionsService> logger) : ISteamLaunchOptionsService
{
    /// <inheritdoc />
    public async Task<LaunchOptions> GetAsync(uint appId, CancellationToken cancellationToken = default)
    {
        var configPath = FindUserConfig();

        if (configPath is null)
        {
            return new LaunchOptions();
        }

        var config = await SteamVdf.TryReadAsync(configPath, cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            logger.LogWarning("Could not read Steam user configuration at {ConfigPath}.", configPath);

            return new LaunchOptions();
        }

        var apps = config
            .GetObject("Software")?
            .GetObject("Valve")?
            .GetObject("Steam")?
            .GetObject("apps");

        if (apps is null)
        {
            logger.LogWarning("{ConfigPath} has no app section.", configPath);

            return new LaunchOptions();
        }

        // A game with no launch options set has no key at all, which is not an error.
        var launchOptions = apps.GetObject(appId.ToString())?.GetString("LaunchOptions");

        return LaunchOptions.Parse(launchOptions);
    }

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
