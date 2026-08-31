using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Proton;
using ProtonTune.Services.GameConfiguration;

namespace ProtonTune.Services.Tests.GameConfiguration;

/// <summary>
/// Reads the definition files ProtonTune actually ships, rather than fixtures, so a mistake in one
/// fails here rather than in front of a user. They are hand-edited data, which is exactly the kind
/// of thing that goes wrong quietly.
/// </summary>
public class ShippedSettingsTests
{
    private static readonly SettingCatalog Catalog = new YamlSettingCatalogReader(
        YamlSettingCatalogReader.DefaultDirectory,
        NullLogger<YamlSettingCatalogReader>.Instance).Read();

    private static readonly string[] PresetSettings =
    [
        "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION",
        "DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION",
        "DXVK_NVAPI_DRS_NGX_DLSS_FG_OVERRIDE_RENDER_PRESET_SELECTION"
    ];

    [Fact]
    public void EveryShippedFileLoads()
    {
        Assert.NotEmpty(Catalog.Categories);
        Assert.NotEmpty(Catalog.All);

        Assert.All(Catalog.All, definition =>
        {
            Assert.NotEmpty(definition.Variable);
            Assert.NotEmpty(definition.Label);
            Assert.NotNull(Catalog.FindCategory(definition.Category.Id));
        });
    }

    /// <summary>
    /// A variable declared twice would silently take whichever definition loaded first, so the
    /// section a setting appears under would depend on file names.
    /// </summary>
    [Fact]
    public void NoVariableIsDeclaredTwice()
    {
        var duplicates = Catalog.All
            .GroupBy(definition => definition.Variable, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void SectionIdentifiersAreUnique()
    {
        var duplicates = Catalog.Categories
            .GroupBy(category => category.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Two sections carry a control the application looks for by identifier. Renaming one in its
    /// file removes that control rather than the section, which would be silent.
    /// </summary>
    [Theory]
    [InlineData(SettingCategoryIds.Cpu)]
    [InlineData(SettingCategoryIds.MangoHud)]
    public void KeepsTheSectionsTheApplicationLooksForByName(string id) =>
        Assert.NotNull(Catalog.FindCategory(id));

    [Fact]
    public void OrdersSectionsWithNvidiaFirst() =>
        Assert.Equal("nvidia", Catalog.Categories[0].Id);

    /// <summary>
    /// These fail silently when given a value the driver does not recognise — the game just runs
    /// with its own choice — so the offered values are pinned to DXVK-NVAPI's own table.
    /// </summary>
    [Theory]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION")]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION")]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_FG_OVERRIDE_RENDER_PRESET_SELECTION")]
    public void SuperResolutionRayReconstructionAndFrameGenerationAreAllOffered(string variable)
    {
        var definition = Catalog.Find(variable);

        Assert.NotNull(definition);
        Assert.Equal(SettingKind.Choice, definition.Kind);
    }

    /// <summary>
    /// One shared value table in nvapi64.dll serves all three, so they must not drift apart. The
    /// file writes the list once and refers to it, which is what keeps this true.
    /// </summary>
    [Fact]
    public void EveryPresetOverrideOffersTheSameValues()
    {
        var choices = PresetSettings.Select(variable => Catalog.Find(variable)!.Choices).ToList();

        Assert.All(choices, choice => Assert.Equal(choices[0], choice));
    }

    [Fact]
    public void OffersEveryLetterFromAToZ()
    {
        var choices = Catalog.Find(PresetSettings[0])!.Choices;

        Assert.All(
            Enumerable.Range('A', 26).Select(letter => $"RENDER_PRESET_{(char)letter}"),
            preset => Assert.Contains(preset, choices));
    }

    /// <summary>Mixed case, unlike the lettered presets. A mismatched name is ignored silently.</summary>
    [Fact]
    public void SpellsDefaultAndLatestExactlyAsTheDriverTableDoes()
    {
        var choices = Catalog.Find(PresetSettings[0])!.Choices;

        Assert.Contains("RENDER_PRESET_Default", choices);
        Assert.Contains("RENDER_PRESET_Latest", choices);
        Assert.DoesNotContain("RENDER_PRESET_DEFAULT", choices);
        Assert.DoesNotContain("RENDER_PRESET_LATEST", choices);
    }

    [Fact]
    public void OffersNothingBeyondTheDriverTable() =>
        Assert.Equal(28, Catalog.Find(PresetSettings[0])!.Choices.Count);

    /// <summary>
    /// The compound toggle, which writes a value rather than 1. Losing that in translation would
    /// switch the overlay on with something the driver ignores.
    /// </summary>
    [Fact]
    public void KeepsTheCompoundToggleValue()
    {
        var definition = Catalog.Find("DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS")!;

        Assert.Equal(SettingKind.Toggle, definition.Kind);
        Assert.Equal("DLSSIndicator=1024", definition.OnValue);
    }

    /// <summary>
    /// MangoHud's options used to be a list in code. Losing one in the move to a file would be
    /// silent — the option simply would not appear.
    /// </summary>
    [Fact]
    public void ReadsMangoHudsOptionsFromItsFile()
    {
        var compound = Catalog.Find("MANGOHUD_CONFIG")!.Compound;

        Assert.NotNull(compound);
        Assert.Equal([",", "="], new[] { compound.Separator, compound.Assignment });
        Assert.Equal(["Frame limiting", "Metrics", "Appearance"], compound.Groups.Select(group => group.Name));

        Assert.Equal(SettingKind.Toggle, compound.Find("fps")!.Kind);
        Assert.Equal(SettingKind.Text, compound.Find("fps_limit")!.Kind);
        Assert.Equal(["early", "late"], compound.Find("fps_limit_method")!.Choices);
    }

    /// <summary>
    /// A second variable using the same mechanism, which is what makes it a mechanism rather than
    /// MangoHud's own arrangement wearing a different name.
    /// </summary>
    [Fact]
    public void ReadsTheDxvkOverlayTheSameWay()
    {
        var compound = Catalog.Find("DXVK_HUD")!.Compound;

        Assert.NotNull(compound);
        Assert.NotNull(compound.Find("fps"));
        Assert.Equal(SettingKind.Text, compound.Find("scale")!.Kind);
    }

    /// <summary>
    /// No option may be declared twice inside one variable, or setting it would depend on which
    /// group happened to be read first.
    /// </summary>
    [Fact]
    public void NoCompoundDeclaresAnOptionTwice()
    {
        var duplicates = Catalog.All
            .Where(definition => definition.Compound is not null)
            .SelectMany(definition => definition.Compound!.AllOptions
                .GroupBy(option => option.Key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"{definition.Variable}.{group.Key}"));

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Gamescope is configured entirely on its own command line, so a section describing only
    /// variables could not describe it. Losing the command block leaves a section that names a few
    /// environment variables and cannot switch the compositor on at all.
    /// </summary>
    [Fact]
    public void ShipsGamescopeAsACommandRatherThanVariables()
    {
        var command = Catalog.FindCategory("gamescope")?.Command;

        Assert.NotNull(command);
        Assert.Equal("gamescope", command.Command);
        Assert.Equal("--", command.Terminator);
        Assert.NotEmpty(command.AllFlags);
    }

    /// <summary>
    /// Taken from gamescope 3.16.25's own --help, which is where they are written down. A flag
    /// spelled wrongly is not rejected — gamescope stops at the first thing that is not an option
    /// and runs the rest as the command, so the game launches with the setting quietly missing.
    /// </summary>
    [Theory]
    [InlineData("-W", SettingKind.Number)]
    [InlineData("-H", SettingKind.Number)]
    [InlineData("-r", SettingKind.Number)]
    [InlineData("-f", SettingKind.Toggle)]
    [InlineData("--adaptive-sync", SettingKind.Toggle)]
    [InlineData("--hdr-enabled", SettingKind.Toggle)]
    [InlineData("--hdr-itm-enabled", SettingKind.Toggle)]
    [InlineData("--hdr-itm-sdr-nits", SettingKind.Number)]
    [InlineData("--hdr-itm-target-nits", SettingKind.Number)]
    [InlineData("--sdr-gamut-wideness", SettingKind.Text)]
    [InlineData("--mangoapp", SettingKind.Toggle)]
    public void OffersTheGamescopeFlagsWorthReachingFor(string flag, SettingKind kind)
    {
        var found = GamescopeFlag(flag);

        Assert.NotNull(found);
        Assert.Equal(kind, found.Kind);
        Assert.NotEmpty(found.Label);
    }

    /// <summary>
    /// The short spellings are what the guides write and what ProtonTune writes; the long ones are
    /// what the documentation uses. A string using either has to be recognised, or setting a width
    /// beside a <c>--output-width</c> already there writes a second one.
    /// </summary>
    [Theory]
    [InlineData("-W", "--output-width")]
    [InlineData("-H", "--output-height")]
    [InlineData("-r", "--nested-refresh")]
    [InlineData("-f", "--fullscreen")]
    [InlineData("-S", "--scaler")]
    [InlineData("-F", "--filter")]
    public void RecognisesBothSpellingsOfAGamescopeFlag(string flag, string alias) =>
        Assert.Contains(alias, GamescopeFlag(flag)!.Aliases);

    /// <summary>
    /// No flag may be declared twice, or which control writes it would depend on which group
    /// happened to be read first. Aliases count: a spelling claimed by two flags is the same fault.
    /// </summary>
    [Fact]
    public void NoGamescopeFlagIsDeclaredTwice()
    {
        var duplicates = Gamescope.AllFlags
            .SelectMany(flag => flag.Spellings)
            .GroupBy(spelling => spelling, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Neither is normally worth setting — Gamescope enables its own layer for whatever it
    /// launches — but both turn up in guides, and a variable with a definition is shown in the
    /// section it belongs to rather than among the custom ones.
    /// </summary>
    [Theory]
    [InlineData("ENABLE_GAMESCOPE_WSI")]
    [InlineData("DISABLE_GAMESCOPE_WSI")]
    public void PlacesTheWaylandLayerVariablesInTheGamescopeSection(string variable)
    {
        var definition = Catalog.Find(variable);

        Assert.NotNull(definition);
        Assert.Equal(SettingKind.Toggle, definition.Kind);
        Assert.Equal("gamescope", definition.Category.Id);
    }

    private static CommandDefinition Gamescope => Catalog.FindCategory("gamescope")!.Command!;

    private static CommandFlagDefinition? GamescopeFlag(string flag) =>
        Gamescope.AllFlags.FirstOrDefault(candidate =>
            string.Equals(candidate.Flag, flag, StringComparison.Ordinal));

    /// <summary>
    /// Checked against the launch scripts of the builds installed here: GE-Proton reads these and
    /// nothing else does — proton-cachyos mentions HDR nowhere, and the rest are GE's own.
    /// Restricting them is what keeps a list of features that cannot be used off the screen.
    /// </summary>
    [Theory]
    [InlineData("PROTON_USE_HDR")]
    [InlineData("PROTON_NO_NTSYNC")]
    [InlineData("PROTON_USE_WRITECOPY")]
    [InlineData("PROTON_WAYLAND_MONITOR")]
    public void RestrictsWhatOnlyTheGeFamilyReads(string variable)
    {
        var definition = Catalog.Find(variable)!;

        Assert.Equal(["^GE-Proton"], definition.ProtonBuilds);
        Assert.True(definition.RestrictToProtonBuild);
    }

    /// <summary>
    /// The community builds read a great deal Valve's do not, and proton-cachyos reads most of
    /// what GE does. Naming only GE would hide a working setting on cachyos, which is the mistake
    /// this catches: the version file drops the leading "proton-", so the pattern has to match
    /// both the tool name and the version label.
    /// </summary>
    [Theory]
    [InlineData("PROTON_DLSS_UPGRADE")]
    [InlineData("PROTON_DLSS_INDICATOR")]
    [InlineData("PROTON_USE_WAYLAND")]
    [InlineData("PROTON_FSR4_UPGRADE")]
    [InlineData("PROTON_XESS_UPGRADE")]
    [InlineData("PROTON_USE_OPTISCALER")]
    [InlineData("PROTON_NVIDIA_LIBS")]
    public void RestrictsWhatBothCommunityFamiliesReadToBothOfThem(string variable)
    {
        var definition = Catalog.Find(variable)!;

        Assert.Equal(["^GE-Proton", "^(proton-)?cachyos"], definition.ProtonBuilds);
        Assert.True(definition.RestrictToProtonBuild);

        Assert.True(definition.AppliesTo(Build("GE-Proton11-6-x86_64", "GE-Proton11-6")));
        Assert.True(definition.AppliesTo(Build("proton-cachyos-11.0-20260703-slr-x86_64_v3", "cachyos-11.0-20260703-slr")));
        Assert.False(definition.AppliesTo(Build("proton_experimental", "experimental-11.0-20260826-x86_64")));
    }

    /// <summary>
    /// The settings proton-cachyos added and GE has no equivalent for. Named the other way round
    /// from the test above, and worth pinning for the same reason.
    /// </summary>
    [Theory]
    [InlineData("PROTON_DXVK_SAREK")]
    [InlineData("PROTON_DXVK_LOWLATENCY")]
    [InlineData("PROTON_VKD3D_LOWLATENCY")]
    [InlineData("PROTON_USE_PIPEWIRE")]
    [InlineData("PROTON_ENABLE_MEDIACONV")]
    public void RestrictsWhatOnlyCachyosReads(string variable)
    {
        var definition = Catalog.Find(variable)!;

        Assert.Equal(["^(proton-)?cachyos"], definition.ProtonBuilds);
        Assert.True(definition.RestrictToProtonBuild);

        Assert.True(definition.AppliesTo(Build("proton-cachyos-11.0-20260703-slr-x86_64_v3", "cachyos-11.0-20260703-slr")));
        Assert.False(definition.AppliesTo(Build("GE-Proton11-6-x86_64", "GE-Proton11-6")));
    }

    /// <summary>
    /// A section long enough to need headings has to give them, or it is the twenty-line list the
    /// headings exist to break up. These are the ones that would read worst without.
    /// </summary>
    [Theory]
    [InlineData("nvidia", "DLSS")]
    [InlineData("graphics", "Renderer")]
    [InlineData("compatibility", "Memory")]
    [InlineData("diagnostics", "Renderers")]
    public void GroupsTheLongSectionsUnderHeadings(string section, string heading)
    {
        var groups = Catalog.GroupsIn(Catalog.FindCategory(section)!);

        Assert.Contains(heading, groups.Select(group => group.Name));
        Assert.All(groups, group => Assert.NotEmpty(group.Settings));
    }

    /// <summary>
    /// Grouping must not lose or reorder anything: the headings are a way of reading the section,
    /// not a second list beside it.
    /// </summary>
    [Fact]
    public void EveryGroupedSettingIsStillTheSectionsOwnListInOrder() =>
        Assert.All(Catalog.Categories, category => Assert.Equal(
            Catalog.In(category),
            Catalog.GroupsIn(category).SelectMany(group => group.Settings)));

    private static ProtonBuild Build(string name, string version) => new()
    {
        Name = name,
        DisplayName = name,
        InstallPath = $"/tmp/{name}",
        Kind = ProtonBuildKind.Custom,
        Version = version
    };

    /// <summary>
    /// Read by every build installed here, so restricting them would hide settings that work.
    /// </summary>
    [Theory]
    [InlineData("PROTON_LOG")]
    [InlineData("PROTON_NO_ESYNC")]
    [InlineData("PROTON_NO_FSYNC")]
    [InlineData("PROTON_FORCE_LARGE_ADDRESS_AWARE")]
    [InlineData("PROTON_CPU_TOPOLOGY")]
    [InlineData("PROTON_DISABLE_NVAPI")]
    [InlineData("PROTON_LIMIT_RESOLUTIONS")]
    [InlineData("PROTON_SET_GAME_DRIVE")]
    [InlineData("PROTON_DISABLE_HIDRAW")]
    public void LeavesWhatEveryBuildReadsAlone(string variable) =>
        Assert.Empty(Catalog.Find(variable)!.ProtonBuilds);

    /// <summary>
    /// No build installed here reads these, but both were real in older Proton. A restriction has
    /// to name the builds a setting works on, and that cannot be checked against builds that are
    /// not present — so they are shown greyed out rather than hidden on a guess.
    /// </summary>
    [Theory]
    [InlineData("PROTON_ENABLE_NVAPI")]
    [InlineData("PROTON_ENABLE_NGX_UPDATER")]
    public void DoesNotGuessAtSettingsItCannotPlace(string variable) =>
        Assert.False(Catalog.Find(variable)!.RestrictToProtonBuild);

    /// <summary>
    /// The renderer variables live in shipped libraries rather than in the build's launch script,
    /// which is not something reading that script can settle. Restricting them would be a guess.
    /// </summary>
    [Fact]
    public void RestrictsNothingItCannotCheck() =>
        Assert.DoesNotContain(
            Catalog.All.Where(definition => definition.RestrictToProtonBuild),
            definition => !definition.Variable.StartsWith("PROTON_", StringComparison.Ordinal));
}
