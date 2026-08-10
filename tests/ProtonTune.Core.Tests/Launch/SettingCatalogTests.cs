using ProtonTune.Core.Launch;

namespace ProtonTune.Core.Tests.Launch;

/// <summary>
/// The DLSS preset overrides fail silently when given a value the driver does not recognise — the
/// game just runs with its own choice — so the offered values are pinned to what DXVK-NVAPI's own
/// table contains.
/// </summary>
public class SettingCatalogTests
{
    private static readonly string[] PresetSettings =
    [
        "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION",
        "DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION",
        "DXVK_NVAPI_DRS_NGX_DLSS_FG_OVERRIDE_RENDER_PRESET_SELECTION"
    ];

    [Theory]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION")]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION")]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_FG_OVERRIDE_RENDER_PRESET_SELECTION")]
    public void SuperResolutionRayReconstructionAndFrameGenerationAreAllOffered(string variable)
    {
        var definition = SettingCatalog.Find(variable);

        Assert.NotNull(definition);
        Assert.Equal(SettingKind.Choice, definition.Kind);
    }

    [Fact]
    public void EveryPresetOverrideOffersTheSameValues()
    {
        // One shared value table in nvapi64.dll serves all of them, so they must not drift apart.
        var choices = PresetSettings.Select(variable => SettingCatalog.Find(variable)!.Choices).ToList();

        Assert.All(choices, choice => Assert.Equal(choices[0], choice));
    }

    [Fact]
    public void OffersEveryLetterFromAToZ()
    {
        var choices = SettingCatalog.Find(PresetSettings[0])!.Choices;

        Assert.All(
            Enumerable.Range('A', 26).Select(letter => $"RENDER_PRESET_{(char)letter}"),
            preset => Assert.Contains(preset, choices));
    }

    [Fact]
    public void SpellsDefaultAndLatestExactlyAsTheDriverTableDoes()
    {
        // Mixed case, unlike the lettered presets. A mismatched name is ignored silently.
        var choices = SettingCatalog.Find(PresetSettings[0])!.Choices;

        Assert.Contains("RENDER_PRESET_Default", choices);
        Assert.Contains("RENDER_PRESET_Latest", choices);
        Assert.DoesNotContain("RENDER_PRESET_DEFAULT", choices);
        Assert.DoesNotContain("RENDER_PRESET_LATEST", choices);
    }

    [Fact]
    public void OffersNothingBeyondTheDriverTable() =>
        Assert.Equal(28, SettingCatalog.Find(PresetSettings[0])!.Choices.Count);

    [Fact]
    public void KeepsAPresetTheCatalogDoesNotList()
    {
        // Newer presets appear in drivers before they appear here; selecting one elsewhere and
        // opening ProtonTune must not quietly discard it.
        var options = LaunchOptions.Parse(
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_ZZ %command%");

        Assert.Equal("RENDER_PRESET_ZZ", options.FindEnvironment(PresetSettings[0])?.Value);
        Assert.Equal(
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_ZZ %command%",
            options.Format());
    }
}
