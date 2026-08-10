using ProtonTune.Core.Proton;

namespace ProtonTune.Core.Tests.Proton;

/// <summary>
/// The catalogue answers "what does this game run under", which has more shapes than it first
/// appears: a game may choose for itself, inherit the default, or be pointed at a build that no
/// longer exists.
/// </summary>
public class ProtonCatalogueTests
{
    private const uint Overwatch = 2357570;

    private const uint Rematch = 2138720;

    private static ProtonBuild Build(string name, ProtonBuildKind kind = ProtonBuildKind.Valve) => new()
    {
        Name = name,
        DisplayName = name,
        InstallPath = $"/steam/{name}",
        Kind = kind
    };

    private static ProtonToolMapping Mapping(uint appId, string toolName, int priority = 250) => new()
    {
        AppId = appId,
        ToolName = toolName,
        Priority = priority
    };

    private static ProtonCatalogue Catalogue(
        IEnumerable<ProtonBuild> builds,
        params ProtonToolMapping[] mappings) => new()
    {
        Builds = builds.ToList(),
        Mappings = mappings.ToDictionary(mapping => mapping.AppId)
    };

    [Fact]
    public void ResolvesAnExplicitMappingToItsBuild()
    {
        var catalogue = Catalogue(
            [Build("proton_experimental"), Build("GE-Proton11-3", ProtonBuildKind.Custom)],
            Mapping(ProtonCatalogue.DefaultAppId, "proton_experimental", 75),
            Mapping(Overwatch, "GE-Proton11-3"));

        var selection = catalogue.SelectionFor(Overwatch);

        Assert.True(selection.IsExplicit);
        Assert.Equal("GE-Proton11-3", selection.Build?.Name);
        Assert.False(selection.IsMissing);
    }

    /// <summary>
    /// A game with no mapping of its own falls back to the default, but that is a likelihood
    /// rather than a certainty — Steam may name a build in the game's own metadata, which does
    /// not live in this file. The selection says so by reporting itself as not explicit.
    /// </summary>
    [Fact]
    public void FallsBackToTheDefaultWithoutClaimingItIsExplicit()
    {
        var catalogue = Catalogue(
            [Build("proton_experimental")],
            Mapping(ProtonCatalogue.DefaultAppId, "proton_experimental", 75));

        var selection = catalogue.SelectionFor(Rematch);

        Assert.False(selection.IsExplicit);
        Assert.Equal("proton_experimental", selection.Build?.Name);
    }

    [Fact]
    public void ReportsNothingWhenNoDefaultIsSet()
    {
        var catalogue = Catalogue([Build("proton_experimental")]);

        var selection = catalogue.SelectionFor(Rematch);

        Assert.False(selection.IsExplicit);
        Assert.Null(selection.ToolName);
        Assert.Null(selection.Build);
        Assert.False(selection.IsMissing);
    }

    /// <summary>
    /// Deleting a Proton build leaves every mapping to it behind. Those games will not start, so
    /// the name has to survive the lookup that fails to find a build for it.
    /// </summary>
    [Fact]
    public void KeepsTheNameOfAnUninstalledBuild()
    {
        var catalogue = Catalogue(
            [Build("proton_experimental")],
            Mapping(Overwatch, "GE-Proton11-3"));

        var selection = catalogue.SelectionFor(Overwatch);

        Assert.True(selection.IsMissing);
        Assert.Equal("GE-Proton11-3", selection.ToolName);
        Assert.Null(selection.Build);
        Assert.Equal(["GE-Proton11-3"], catalogue.MissingToolNames);
    }

    /// <summary>
    /// Steam writes SteamLinuxRuntime_sniper in its log but steamlinuxruntime_sniper in app
    /// metadata, so a mapping and a build can disagree on case and still mean the same tool.
    /// </summary>
    [Fact]
    public void MatchesToolNamesIgnoringCase()
    {
        var catalogue = Catalogue(
            [Build("GE-Proton11-3", ProtonBuildKind.Custom)],
            Mapping(Overwatch, "ge-proton11-3"));

        Assert.Equal("GE-Proton11-3", catalogue.SelectionFor(Overwatch).Build?.Name);
        Assert.Empty(catalogue.MissingToolNames);
    }

    /// <summary>
    /// The default covers every game at once, so counting it among a build's users would say that
    /// each game had chosen it.
    /// </summary>
    [Fact]
    public void ExcludesTheDefaultFromTheGamesUsingABuild()
    {
        var catalogue = Catalogue(
            [Build("proton_experimental"), Build("GE-Proton11-3", ProtonBuildKind.Custom)],
            Mapping(ProtonCatalogue.DefaultAppId, "proton_experimental", 75),
            Mapping(Overwatch, "GE-Proton11-3"),
            Mapping(Rematch, "GE-Proton11-3"));

        Assert.Empty(catalogue.AppsUsing("proton_experimental"));
        Assert.Equal([Rematch, Overwatch], catalogue.AppsUsing("GE-Proton11-3").Order());
    }

    /// <summary>
    /// App id 0 is the default rather than an app, so asking for it directly must not report a
    /// game that has deliberately chosen something.
    /// </summary>
    [Fact]
    public void TreatsTheDefaultAppIdAsInherited()
    {
        var catalogue = Catalogue(
            [Build("proton_experimental")],
            Mapping(ProtonCatalogue.DefaultAppId, "proton_experimental", 75));

        Assert.False(catalogue.SelectionFor(ProtonCatalogue.DefaultAppId).IsExplicit);
        Assert.Equal("proton_experimental", catalogue.Default.Build?.Name);
    }
}
