namespace ProtonTune.Services.Steam;

/// <summary>
/// The Steam configuration files ProtonTune edits.
/// </summary>
/// <remarks>
/// Two kinds, and they belong to different things: launch options are per signed-in account, and
/// the choice of Proton build is per installation. Anything that backs them up has to know about
/// both, or half the edits would be recoverable.
/// </remarks>
internal static class SteamConfigFiles
{
    /// <summary>Every configuration file ProtonTune might write, whether or not it has.</summary>
    public static IReadOnlyList<string> In(string steamRoot)
    {
        var paths = new List<string>();
        var installConfig = SteamCompatTools.ConfigPathIn(steamRoot);

        if (File.Exists(installConfig))
        {
            paths.Add(installConfig);
        }

        paths.AddRange(UserConfigsIn(steamRoot));

        return paths;
    }

    /// <summary>
    /// The <c>localconfig.vdf</c> of every account that has signed in on this machine, most
    /// recently written first — Steam rewrites the file throughout a session, so its timestamp
    /// tracks the account in use.
    /// </summary>
    public static IReadOnlyList<string> UserConfigsIn(string steamRoot)
    {
        var userdata = Path.Combine(steamRoot, "userdata");

        try
        {
            if (!Directory.Exists(userdata))
            {
                return [];
            }

            return Directory
                .EnumerateDirectories(userdata)
                .Select(account => Path.Combine(account, "config", "localconfig.vdf"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
