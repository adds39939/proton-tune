using System.Runtime.CompilerServices;
using Gameloop.Vdf.Linq;
using Microsoft.Extensions.Logging;
using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="ISteamLibraryService" />
public sealed class SteamLibraryService(
    ISteamInstallLocator installLocator,
    ILogger<SteamLibraryService> logger) : ISteamLibraryService
{
    /// <summary>
    /// Bit 2 of <c>StateFlags</c> in an app manifest. Set once the install has finished and the
    /// app is runnable; clear while it is downloading, updating, or awaiting repair.
    /// </summary>
    private const int StateFullyInstalled = 4;

    /// <summary>
    /// Support apps that ship without a <c>toolmanifest.vdf</c> and so cannot be recognised as
    /// tools by inspecting their install directory.
    /// </summary>
    private static readonly HashSet<uint> KnownToolAppIds =
    [
        228980, // Steamworks Common Redistributables
        353370  // Steam Controller Configs
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<SteamLibraryEntry>> GetInstalledAppsAsync(
        CancellationToken cancellationToken = default)
    {
        var steamRoot = installLocator.Locate();

        if (steamRoot is null)
        {
            logger.LogWarning("No Steam installation was found on this machine.");

            return [];
        }

        logger.LogInformation("Scanning Steam installation at {SteamRoot}.", steamRoot);

        var entries = new List<SteamLibraryEntry>();
        var seenAppIds = new HashSet<uint>();

        foreach (var libraryPath in await GetLibraryPathsAsync(steamRoot, cancellationToken).ConfigureAwait(false))
        {
            await foreach (var entry in ReadLibraryAsync(libraryPath, cancellationToken).ConfigureAwait(false))
            {
                // A game moved between libraries can briefly leave a manifest behind in both.
                if (seenAppIds.Add(entry.AppId))
                {
                    entries.Add(entry);
                }
            }
        }

        logger.LogInformation("Found {AppCount} installed Steam apps.", entries.Count);

        return entries
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Resolves every library folder Steam knows about. The root install is always a library;
    /// additional drives are listed in <c>steamapps/libraryfolders.vdf</c>.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetLibraryPathsAsync(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        var paths = new List<string> { steamRoot };
        var seen = new HashSet<string>(StringComparer.Ordinal) { steamRoot };

        var manifestPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        var libraryFolders = await SteamVdf.TryReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        if (libraryFolders is null)
        {
            logger.LogWarning("Could not read {ManifestPath}; only the root library will be scanned.", manifestPath);

            return paths;
        }

        foreach (var folder in libraryFolders.Properties())
        {
            // Current clients nest the path in an object; older ones map the index straight to it.
            var path = folder.Value switch
            {
                VObject details => details.GetString("path"),
                VValue value => value.Value?.ToString(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && seen.Add(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>
    /// Reads every <c>appmanifest_&lt;appid&gt;.acf</c> in a library folder.
    /// </summary>
    private async IAsyncEnumerable<SteamLibraryEntry> ReadLibraryAsync(
        string libraryPath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var steamAppsPath = Path.Combine(libraryPath, "steamapps");

        string[] manifestPaths;

        try
        {
            manifestPaths = Directory.GetFiles(steamAppsPath, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not list app manifests in {SteamAppsPath}.", steamAppsPath);

            yield break;
        }

        foreach (var manifestPath in manifestPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifest = await SteamVdf.TryReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);

            if (manifest is null)
            {
                logger.LogWarning("Skipped unreadable app manifest {ManifestPath}.", manifestPath);

                continue;
            }

            var entry = CreateEntry(manifest, libraryPath, steamAppsPath);

            if (entry is null)
            {
                logger.LogWarning("Skipped app manifest {ManifestPath}; it is missing required fields.", manifestPath);

                continue;
            }

            yield return entry;
        }
    }

    /// <summary>
    /// Projects a parsed app manifest onto a library entry, or returns <see langword="null"/> if
    /// it lacks the fields needed to identify the app.
    /// </summary>
    private static SteamLibraryEntry? CreateEntry(VObject manifest, string libraryPath, string steamAppsPath)
    {
        if (!uint.TryParse(manifest.GetString("appid"), out var appId))
        {
            return null;
        }

        var installDirName = manifest.GetString("installdir");

        if (string.IsNullOrWhiteSpace(installDirName))
        {
            return null;
        }

        var installDirectory = Path.Combine(steamAppsPath, "common", installDirName);

        return new SteamLibraryEntry
        {
            AppId = appId,
            Name = manifest.GetString("name") ?? installDirName,
            InstallDirectory = installDirectory,
            LibraryPath = libraryPath,
            Kind = ClassifyApp(appId, installDirectory),
            SizeOnDisk = manifest.GetInt64("SizeOnDisk"),
            LastPlayed = manifest.GetUnixTime("LastPlayed"),
            IsFullyInstalled = (manifest.GetInt64("StateFlags") & StateFullyInstalled) != 0
        };
    }

    /// <summary>
    /// Decides whether an app is a game or a compatibility tool. Proton builds and the Steam
    /// Linux Runtimes declare themselves by shipping a <c>toolmanifest.vdf</c>; the handful of
    /// support apps that predate that convention are recognised by app id.
    /// </summary>
    private static SteamAppKind ClassifyApp(uint appId, string installDirectory)
    {
        if (KnownToolAppIds.Contains(appId))
        {
            return SteamAppKind.Tool;
        }

        return File.Exists(Path.Combine(installDirectory, "toolmanifest.vdf"))
            ? SteamAppKind.Tool
            : SteamAppKind.Game;
    }
}
