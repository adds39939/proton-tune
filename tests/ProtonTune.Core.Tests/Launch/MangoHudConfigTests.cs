using ProtonTune.Core.Launch;

namespace ProtonTune.Core.Tests.Launch;

/// <summary>
/// MANGOHUD_CONFIG mixes bare flags with key=value settings, and ProtonTune only has controls for
/// some of them. Everything else has to survive being edited around.
/// </summary>
public class MangoHudConfigTests
{
    [Theory]
    [InlineData("")]
    [InlineData("fps")]
    [InlineData("fps_limit=224")]
    [InlineData("fps_limit=224,fps_limit_method=late")]
    [InlineData("fps,frametime,gpu_stats,cpu_stats")]
    [InlineData("fps_limit=0,30,60")]
    [InlineData("position=top-left,font_size=24,round_corners=5")]
    public void FormatRestoresTheOriginalValue(string original) =>
        Assert.Equal(original, MangoHudConfig.Parse(original).Format());

    [Fact]
    public void ReadsTheRealOverwatchConfiguration()
    {
        var config = MangoHudConfig.Parse("fps_limit=224,fps_limit_method=late");

        Assert.Equal("224", config.GetValue("fps_limit"));
        Assert.Equal("late", config.GetValue("fps_limit_method"));
    }

    [Fact]
    public void TreatsABareOptionAsAFlag()
    {
        var config = MangoHudConfig.Parse("fps");

        Assert.True(config.Contains("fps"));
        Assert.Null(config.GetValue("fps"));
    }

    [Fact]
    public void KeepsANumericListWithItsSetting()
    {
        // MangoHud cycles through fps_limit=0,30,60. Split naively, the 30 and 60 would read as
        // unknown flags and be shown as junk in the free-text field.
        var config = MangoHudConfig.Parse("fps_limit=0,30,60,fps");

        Assert.Equal("0,30,60", config.GetValue("fps_limit"));
        Assert.True(config.Contains("fps"));
        Assert.Empty(config.Unrecognised);
    }

    [Fact]
    public void SettingAnExistingOptionLeavesItWhereItWas()
    {
        var config = MangoHudConfig.Parse("fps,fps_limit=60,gpu_stats").Set("fps_limit", "144");

        Assert.Equal("fps,fps_limit=144,gpu_stats", config.Format());
    }

    [Fact]
    public void SettingANewOptionAppendsIt() =>
        Assert.Equal("fps,position=top-left", MangoHudConfig.Parse("fps").Set("position", "top-left").Format());

    [Fact]
    public void RemovingAnOptionLeavesTheRest() =>
        Assert.Equal("fps,gpu_stats", MangoHudConfig.Parse("fps,fps_limit=60,gpu_stats").Remove("fps_limit").Format());

    [Fact]
    public void SeparatesOptionsWithoutControlsFromThoseWithThem()
    {
        var config = MangoHudConfig.Parse("fps_limit=224,round_corners=5,engine_version");

        Assert.Equal(["round_corners=5", "engine_version"], config.Unrecognised.Select(o => o.ToString()));
    }

    [Fact]
    public void ReplacingTheExtrasKeepsTheRecognisedOptions()
    {
        var config = MangoHudConfig
            .Parse("fps_limit=224,round_corners=5")
            .ReplaceUnrecognised(MangoHudConfig.Parse("engine_version,wine").Options);

        Assert.Equal("fps_limit=224,engine_version,wine", config.Format());
    }

    [Fact]
    public void ToleratesUntidySpacingAndEmptyEntries() =>
        Assert.Equal("fps,fps_limit=60", MangoHudConfig.Parse(" fps , , fps_limit=60 ,").Format());
}
