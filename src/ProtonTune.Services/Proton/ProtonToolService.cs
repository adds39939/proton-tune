using System.Text.RegularExpressions;
using Gameloop.Vdf.Linq;
using Microsoft.Extensions.Logging;
using ProtonTune.Core.Proton;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Proton;

/// <inheritdoc cref="IProtonToolService" />
/// <remarks>
/// Nothing here writes. Pointing a game at a different build means editing <c>config.vdf</c>
/// while Steam is closed, which is a separate job from reading it.
/// </remarks>
public sealed partial class ProtonToolService(
    ISteamInstallLocator installLocator,
    ISteamLibraryService library,
    ILogger<ProtonToolService> logger) : IProtonToolService
{
    /// <summary>
    /// The layer name a Proton build declares in its <c>toolmanifest.vdf</c>. Steam's container
    /// runtimes are compatibility tools too and sit in the same directories, but declare
    /// <c>container-runtime</c> — so this is what separates a build a game runs under from the
    /// scaffolding around it. Tools such as Luxtorpeda declare their own names and are likewise
    /// left out: they are not Proton and none of ProtonTune's settings apply to them.
    /// </summary>
    private const string ProtonLayerName = "proton";

    /// <summary>Cheap test applied before the registration pattern, which is the expensive part.</summary>
    private const string RegistrationMarker = "Registering tool ";

    /// <inheritdoc />
    public async Task<ProtonCatalogue> GetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        var steamRoot = installLocator.Locate();

        if (steamRoot is null)
        {
            logger.LogWarning("No Steam installation was found, so no Proton builds can be listed.");

            return ProtonCatalogue.Empty;
        }

        var registeredNames = await ReadRegisteredNamesAsync(steamRoot, cancellationToken).ConfigureAwait(false);

        var builds = new List<ProtonBuild>();
        builds.AddRange(await ReadValveBuildsAsync(registeredNames, cancellationToken).ConfigureAwait(false));
        builds.AddRange(await ReadCustomBuildsAsync(steamRoot, cancellationToken).ConfigureAwait(false));

        logger.LogInformation("Found {BuildCount} installed Proton builds.", builds.Count);

        return new ProtonCatalogue
        {
            // Valve's builds first, then those installed by hand, each set ordered by name. That
            // matches how Steam presents them and keeps the list stable between scans.
            Builds = builds
                .OrderBy(build => build.Kind)
                .ThenBy(build => build.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            Mappings = await ReadMappingsAsync(steamRoot, cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Finds Valve's builds among the installed apps. They are ordinary Steam apps, so the
    /// library scan already knows where they are; what marks one out is the layer name in its
    /// tool manifest.
    /// </summary>
    private async Task<IReadOnlyList<ProtonBuild>> ReadValveBuildsAsync(
        IReadOnlyDictionary<uint, string> registeredNames,
        CancellationToken cancellationToken)
    {
        var apps = await library.GetInstalledAppsAsync(cancellationToken).ConfigureAwait(false);
        var builds = new List<ProtonBuild>();

        foreach (var app in apps)
        {
            if (app.Kind != SteamAppKind.Tool ||
                !await IsProtonAsync(app.InstallDirectory, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var registered = registeredNames.GetValueOrDefault(app.AppId);

            if (registered is null)
            {
                logger.LogInformation(
                    "Steam's compatibility log does not name app {AppId} ({AppName}), so its internal name was inferred.",
                    app.AppId,
                    app.Name);
            }

            builds.Add(new ProtonBuild
            {
                Name = registered ?? ProtonToolName.Derive(app.Name),
                DisplayName = app.Name,
                InstallPath = app.InstallDirectory,
                Kind = ProtonBuildKind.Valve,
                Version = await ReadVersionAsync(app.InstallDirectory, cancellationToken).ConfigureAwait(false),
                AppId = app.AppId,
                NameIsDerived = registered is null,
                Capabilities = await ProbeAsync(app.InstallDirectory, cancellationToken).ConfigureAwait(false)
            });
        }

        return builds;
    }

    /// <summary>
    /// Reads the builds unpacked into <c>compatibilitytools.d</c>. Each declares itself in a
    /// <c>compatibilitytool.vdf</c>, where the key of the entry is the internal name Steam will
    /// use — so unlike Valve's builds, nothing has to be inferred.
    /// </summary>
    private async Task<IReadOnlyList<ProtonBuild>> ReadCustomBuildsAsync(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        var toolsPath = Path.Combine(steamRoot, "compatibilitytools.d");
        string[] directories;

        try
        {
            if (!Directory.Exists(toolsPath))
            {
                return [];
            }

            directories = Directory.GetDirectories(toolsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not list {ToolsPath}; builds installed by hand will be missing.", toolsPath);

            return [];
        }

        var builds = new List<ProtonBuild>();

        foreach (var directory in directories)
        {
            var manifestPath = Path.Combine(directory, "compatibilitytool.vdf");
            var manifest = await SteamVdf.TryReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            var tools = manifest?.GetObject("compat_tools");

            if (tools is null)
            {
                logger.LogInformation("Skipped {Directory}; it declares no compatibility tools.", directory);

                continue;
            }

            foreach (var property in tools.Properties())
            {
                if (property.Value is not VObject tool)
                {
                    continue;
                }

                // install_path is "." when the manifest sits inside the tool's own directory,
                // which is how every build unpacked into compatibilitytools.d is laid out, but the
                // manifest may also be dropped in on its own and point elsewhere.
                var installPath = Path.GetFullPath(tool.GetString("install_path") ?? ".", directory);

                if (!await IsProtonAsync(installPath, cancellationToken).ConfigureAwait(false))
                {
                    logger.LogInformation("Skipped {ToolName}; it is a compatibility tool but not Proton.", property.Key);

                    continue;
                }

                builds.Add(new ProtonBuild
                {
                    Name = property.Key,
                    DisplayName = tool.GetString("display_name") ?? property.Key,
                    InstallPath = installPath,
                    Kind = ProtonBuildKind.Custom,
                    Version = await ReadVersionAsync(installPath, cancellationToken).ConfigureAwait(false),
                    Capabilities = await ProbeAsync(installPath, cancellationToken).ConfigureAwait(false)
                });
            }
        }

        return builds;
    }

    /// <summary>
    /// Reads the deliberate tool choices from <c>config/config.vdf</c>.
    /// </summary>
    private async Task<IReadOnlyDictionary<uint, ProtonToolMapping>> ReadMappingsAsync(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        var configPath = SteamCompatTools.ConfigPathIn(steamRoot);
        var document = await SteamVdf.TryReadAsync(configPath, cancellationToken).ConfigureAwait(false);

        // Walked from the same key path the writer uses, so reading and writing cannot drift
        // apart. The first segment is dropped: the parser returns the root object's contents.
        var mappings = SteamCompatTools.MappingRoot
            .Skip(1)
            .Aggregate(document, (node, key) => node?.GetObject(key));

        var result = new Dictionary<uint, ProtonToolMapping>();

        if (mappings is null)
        {
            // Absent until the first tool is chosen in Steam, so this is not necessarily a fault.
            logger.LogInformation("No compatibility tool mappings were found in {ConfigPath}.", configPath);

            return result;
        }

        foreach (var property in mappings.Properties())
        {
            if (!uint.TryParse(property.Key, out var appId) || property.Value is not VObject entry)
            {
                continue;
            }

            var toolName = entry.GetString(SteamCompatTools.NameKey);

            // Steam leaves the entry behind with an empty name when a choice is cleared, which
            // means "decide for me" rather than naming a tool.
            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            result[appId] = new ProtonToolMapping
            {
                AppId = appId,
                ToolName = toolName,
                Config = entry.GetString("config") ?? string.Empty,
                Priority = (int)entry.GetInt64("priority")
            };
        }

        return result;
    }

    /// <summary>
    /// Reads the internal names Steam has registered its own builds under, from the plain-text
    /// compatibility log.
    /// </summary>
    /// <remarks>
    /// The log is the only place outside Steam's binary metadata cache where the name a Valve
    /// build is written into <c>CompatToolMapping</c> as — <c>proton_experimental</c> — is
    /// spelled out next to the app id that identifies the install on disk. It is a log, so Steam
    /// truncates it freely; a missing entry falls back to
    /// <see cref="ProtonToolName.Derive" /> rather than dropping the build.
    /// </remarks>
    /// <returns>App id to internal name. Custom builds register under app id 0 and are excluded.</returns>
    private async Task<IReadOnlyDictionary<uint, string>> ReadRegisteredNamesAsync(
        string steamRoot,
        CancellationToken cancellationToken)
    {
        var logPath = Path.Combine(steamRoot, "logs", "compat_log.txt");
        var names = new Dictionary<uint, string>();

        try
        {
            if (!File.Exists(logPath))
            {
                logger.LogInformation("No compatibility log at {LogPath}; build names will be inferred.", logPath);

                return names;
            }

            using var reader = new StreamReader(logPath);

            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (!line.Contains(RegistrationMarker, StringComparison.Ordinal))
                {
                    continue;
                }

                var registration = ToolRegistration().Match(line);

                if (!registration.Success ||
                    !uint.TryParse(registration.Groups["appId"].ValueSpan, out var appId) ||
                    appId == 0)
                {
                    continue;
                }

                // The log spans many Steam sessions, so the same build is registered repeatedly.
                // Later lines win, which matters if Valve ever renames one.
                names[appId] = registration.Groups["name"].Value;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not read {LogPath}; build names will be inferred.", logPath);
        }

        return names;
    }

    /// <summary>
    /// Whether a directory holds a Proton build, as opposed to another kind of compatibility tool.
    /// </summary>
    private static async Task<bool> IsProtonAsync(string installPath, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(installPath, "toolmanifest.vdf");
        var manifest = await SteamVdf.TryReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        return string.Equals(
            manifest?.GetString("compatmanager_layer_name"),
            ProtonLayerName,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Works out which variables a build honours by reading the script that honours them.
    /// </summary>
    /// <remarks>
    /// <c>proton</c> is Python source, so every variable it consults is in there as a literal.
    /// That makes this exact rather than a guess, and it stays right across releases — GE adds
    /// variables with almost every one, and a table written into ProtonTune would be stale within
    /// a month.
    /// </remarks>
    private async Task<ProtonCapabilities> ProbeAsync(string installPath, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(installPath, "proton");

        try
        {
            if (!File.Exists(scriptPath))
            {
                logger.LogWarning("{InstallPath} has no proton script, so its settings cannot be checked.", installPath);

                return ProtonCapabilities.Unknown;
            }

            var script = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);

            var variables = ProtonVariable()
                .Matches(script)
                .Select(match => match.Value)
                .ToHashSet(StringComparer.Ordinal);

            return new ProtonCapabilities { Variables = variables, IsKnown = true };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not read {ScriptPath}; its settings will not be checked.", scriptPath);

            return ProtonCapabilities.Unknown;
        }
    }

    /// <summary>
    /// Reads a build's <c>version</c> file, which holds a build timestamp and a label separated
    /// by a space. Only the label is worth showing.
    /// </summary>
    private static async Task<string?> ReadVersionAsync(string installPath, CancellationToken cancellationToken)
    {
        var versionPath = Path.Combine(installPath, "version");

        try
        {
            if (!File.Exists(versionPath))
            {
                return null;
            }

            var text = (await File.ReadAllTextAsync(versionPath, cancellationToken).ConfigureAwait(false)).Trim();
            var separator = text.IndexOf(' ');

            return separator >= 0 ? text[(separator + 1)..] : text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"Registering tool (?<name>.+), AppID (?<appId>\d+)\s*$")]
    private static partial Regex ToolRegistration();

    [GeneratedRegex("PROTON_[A-Z0-9_]+")]
    private static partial Regex ProtonVariable();
}
