using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Proton;
using ProtonTune.Services.Proton;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Proton;

/// <summary>
/// Shows the Proton builds installed on this machine and which games are pointed at them.
/// </summary>
/// <remarks>
/// Read-only. Changing a game's build means editing <c>config.vdf</c> with Steam closed, which is
/// not wired up yet — this panel exists to make what Steam already holds visible first.
/// </remarks>
public partial class ProtonPanel : ComponentBase
{
    [Inject]
    private IProtonToolService Tools { get; set; } = null!;

    [Inject]
    private ISteamLibraryService Library { get; set; } = null!;

    /// <summary>The settings on offer, so a build can be described by what it ignores.</summary>
    [Inject]
    private SettingCatalog Catalog { get; set; } = null!;

    private ProtonCatalogue Catalogue { get; set; } = ProtonCatalogue.Empty;

    /// <summary>
    /// App id to name, for turning the ids in a mapping into something readable. A mapping can
    /// name a game that is no longer installed, so lookups have to tolerate a miss.
    /// </summary>
    private IReadOnlyDictionary<uint, string> InstalledNames { get; set; } = new Dictionary<uint, string>();

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Catalogue = await Tools.GetCatalogueAsync();

            InstalledNames = (await Library.GetInstalledAppsAsync())
                .ToDictionary(entry => entry.AppId, entry => entry.Name);
        }
        catch (Exception e)
        {
            LoadError = $"Could not read the installed Proton builds: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Describes the fallback build. Steam applies it only where a game's own metadata does not
    /// name a tool, so the wording stops short of promising it covers everything.
    /// </summary>
    private string DefaultSummary => Catalogue.Default switch
    {
        { Build: { } build } =>
            $"Games without a choice of their own use {build.DisplayName}, unless Steam names a " +
            "different build for them.",
        { ToolName: { } toolName } =>
            $"The default is set to “{toolName}”, which is not installed.",
        _ => "No default build is set, so Steam decides for each game."
    };

    private static string KindLabel(ProtonBuild build) => build.Kind switch
    {
        ProtonBuildKind.Valve => "Valve",
        ProtonBuildKind.Custom => "Community",
        _ => "Unknown"
    };

    private bool IsDefault(ProtonBuild build) =>
        string.Equals(Catalogue.Default.ToolName, build.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Names the games pointed at a build by hand. Games that inherit the default are left out:
    /// listing every game under the default would say nothing about the choice made.
    /// </summary>
    private string GamesSummary(ProtonBuild build)
    {
        var games = NamesFor(Catalogue.AppsUsing(build.Name));

        return games.Count == 0
            ? "No game is pointed at this build on its own."
            : $"Used by {string.Join(", ", games)}.";
    }

    /// <summary>
    /// Says which of ProtonTune's settings a build would ignore, which is the practical difference
    /// between one build and another — Valve's Experimental reads no HDR or Wayland variable at
    /// all, and none of the upgrade toggles.
    /// </summary>
    private string SupportSummary(ProtonBuild build)
    {
        if (!build.Capabilities.IsKnown)
        {
            return "Which settings it reads could not be checked.";
        }

        var ignored = Catalog.All
            .Where(definition => build.Capabilities.Ignores(definition.Variable))
            .Select(definition => definition.Label)
            .ToList();

        return ignored.Count == 0
            ? "Reads every Proton setting ProtonTune offers."
            : $"Ignores {string.Join(", ", ignored)}.";
    }

    /// <summary>Describes what is still pointed at a build that is no longer installed.</summary>
    private string MissingSummary(string toolName)
    {
        var games = NamesFor(Catalogue.AppsUsing(toolName));

        if (string.Equals(Catalogue.Default.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
        {
            games.Insert(0, "set as the default");
        }

        return games.Count == 0 ? "mapped, but nothing uses it" : string.Join(", ", games);
    }

    private List<string> NamesFor(IEnumerable<uint> appIds) =>
        appIds
            .Select(appId => InstalledNames.GetValueOrDefault(appId, $"app {appId}"))
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
