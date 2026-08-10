namespace ProtonTune.Core.Proton;

/// <summary>
/// Everything ProtonTune knows about Proton on this machine: which builds are installed, and
/// which games have been pointed at which.
/// </summary>
public sealed record ProtonCatalogue
{
    /// <summary>
    /// The app id Steam uses for "everything without a choice of its own" — the default shown in
    /// the client as the Steam Play compatibility tool.
    /// </summary>
    public const uint DefaultAppId = 0;

    /// <summary>A catalogue for a machine with no readable Steam installation.</summary>
    public static ProtonCatalogue Empty { get; } = new()
    {
        Builds = [],
        Mappings = new Dictionary<uint, ProtonToolMapping>()
    };

    /// <summary>The installed builds, Valve's first and then those installed by hand.</summary>
    public required IReadOnlyList<ProtonBuild> Builds { get; init; }

    /// <summary>Every mapping in <c>config.vdf</c>, keyed by app id.</summary>
    public required IReadOnlyDictionary<uint, ProtonToolMapping> Mappings { get; init; }

    /// <summary>The build every game falls back to, resolved from the default mapping.</summary>
    public ProtonSelection Default => SelectionFor(DefaultAppId);

    /// <summary>
    /// Tool names that are mapped to something but are not installed. Usually a Proton build that
    /// was deleted while games were still pointed at it — those games will not start until Steam
    /// is given a build that exists.
    /// </summary>
    public IReadOnlyList<string> MissingToolNames =>
        Mappings.Values
            .Select(mapping => mapping.ToolName)
            .Where(name => FindBuild(name) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Finds an installed build by its internal name. Steam is inconsistent about the casing of
    /// these — <c>SteamLinuxRuntime_sniper</c> appears in the log with a capital S but in app
    /// metadata without one — so the comparison ignores case.
    /// </summary>
    public ProtonBuild? FindBuild(string? toolName) =>
        toolName is null
            ? null
            : Builds.FirstOrDefault(build => string.Equals(build.Name, toolName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Works out which build an app runs under, falling back to the default when the app has no
    /// mapping of its own.
    /// </summary>
    public ProtonSelection SelectionFor(uint appId)
    {
        if (appId != DefaultAppId && Mappings.TryGetValue(appId, out var mapping))
        {
            return new ProtonSelection
            {
                IsExplicit = true,
                ToolName = mapping.ToolName,
                Build = FindBuild(mapping.ToolName)
            };
        }

        var fallback = Mappings.GetValueOrDefault(DefaultAppId);

        return new ProtonSelection
        {
            IsExplicit = false,
            ToolName = fallback?.ToolName,
            Build = FindBuild(fallback?.ToolName)
        };
    }

    /// <summary>
    /// The app ids explicitly pointed at a build. The default mapping is excluded: it covers
    /// every game at once and so belongs to no build's list.
    /// </summary>
    public IReadOnlyList<uint> AppsUsing(string toolName) =>
        Mappings.Values
            .Where(mapping => mapping.AppId != DefaultAppId &&
                              string.Equals(mapping.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
            .Select(mapping => mapping.AppId)
            .ToList();
}
