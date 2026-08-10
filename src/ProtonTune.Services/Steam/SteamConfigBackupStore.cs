using Microsoft.Extensions.Logging;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Finding and tidying the copies ProtonTune takes of Steam's configuration files.
/// </summary>
/// <remarks>
/// Depends on nothing but a path, deliberately. Both the writer — which prunes after every save —
/// and the service the settings screen uses need this, and the latter also reaches the global
/// profile, which in turn reaches the writer. Keeping the file handling here is what stops that
/// becoming a circle.
/// </remarks>
internal static class SteamConfigBackupStore
{
    /// <summary>Every backup taken of every configuration file, in no particular order.</summary>
    public static IReadOnlyList<SteamConfigBackup> List(string steamRoot, ILogger logger) =>
        SteamConfigFiles.In(steamRoot).SelectMany(target => For(target, logger)).ToList();

    /// <summary>
    /// Keeps the newest few backups of each file and removes the rest.
    /// </summary>
    /// <remarks>
    /// Counted within each file rather than across all of them, or a busy session editing launch
    /// options would push every copy of the installation's own configuration out of reach.
    /// </remarks>
    /// <returns>How many were removed.</returns>
    public static int Prune(string steamRoot, int keep, ILogger logger)
    {
        if (keep < 1)
        {
            return 0;
        }

        var removed = 0;

        foreach (var target in SteamConfigFiles.In(steamRoot))
        {
            foreach (var stale in For(target, logger).OrderByDescending(backup => backup.CreatedAt).Skip(keep))
            {
                try
                {
                    File.Delete(stale.Path);

                    removed++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(e, "Could not remove the old backup {BackupPath}.", stale.Path);
                }
            }
        }

        if (removed > 0)
        {
            logger.LogInformation("Removed {RemovedCount} backups beyond the {Keep} kept.", removed, keep);
        }

        return removed;
    }

    /// <summary>Every backup taken of one file.</summary>
    private static IReadOnlyList<SteamConfigBackup> For(string targetPath, ILogger logger)
    {
        var directory = Path.GetDirectoryName(targetPath);

        if (directory is null)
        {
            return [];
        }

        try
        {
            return Directory
                .EnumerateFiles(directory, SteamConfigBackup.SearchPatternFor(targetPath))
                .Select(path => Describe(path, targetPath))
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not list backups beside {TargetPath}.", targetPath);

            return [];
        }
    }

    /// <summary>
    /// Reads a backup's moment out of its name, falling back to the file's own timestamp where the
    /// name has been changed by hand.
    /// </summary>
    private static SteamConfigBackup Describe(string path, string targetPath)
    {
        var name = Path.GetFileName(path);
        var start = name.LastIndexOf(SteamConfigBackup.Marker, StringComparison.Ordinal) +
                    SteamConfigBackup.Marker.Length;

        var stamp = name[start..^SteamConfigBackup.Extension.Length];

        var createdAt = DateTimeOffset.TryParseExact(
            stamp,
            SteamConfigBackup.TimestampFormat,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed
            : new DateTimeOffset(File.GetLastWriteTime(path));

        return new SteamConfigBackup
        {
            Path = path,
            TargetPath = targetPath,
            CreatedAt = createdAt,
            SizeBytes = new FileInfo(path).Length
        };
    }
}
