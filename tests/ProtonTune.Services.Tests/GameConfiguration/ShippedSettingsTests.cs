using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Launch;
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

        // Every setting names a variable and belongs to a section that was actually declared.
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
    /// Three sections carry a control the application looks for by identifier. Renaming one in its
    /// file removes that control rather than the section, which would be silent.
    /// </summary>
    [Theory]
    [InlineData(SettingCategoryIds.Dlss)]
    [InlineData(SettingCategoryIds.Cpu)]
    [InlineData(SettingCategoryIds.MangoHud)]
    public void KeepsTheSectionsTheApplicationLooksForByName(string id) =>
        Assert.NotNull(Catalog.FindCategory(id));

    [Fact]
    public void OrdersSectionsWithDlssFirst() =>
        Assert.Equal(SettingCategoryIds.Dlss, Catalog.Categories[0].Id);

    // The DLSS preset overrides ----------------------------------------------

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

    [Fact]
    public void DeclaresProtonsOwnDlssUpgradeAsAGeBuildFeature()
    {
        var definition = Catalog.Find("PROTON_DLSS_UPGRADE")!;

        Assert.Equal(["^GE-Proton"], definition.ProtonBuilds);
    }
}
