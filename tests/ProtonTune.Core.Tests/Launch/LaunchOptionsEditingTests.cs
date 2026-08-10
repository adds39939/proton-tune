using ProtonTune.Core.Launch;

namespace ProtonTune.Core.Tests.Launch;

/// <summary>
/// Editing one setting must leave the rest of a string exactly as it was — including its order,
/// which is how a user finds their way around a line they wrote by hand.
/// </summary>
public class LaunchOptionsEditingTests
{
    private const string Options = "PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 mangohud %command%";

    [Fact]
    public void SettingAnExistingVariableLeavesItWhereItWas()
    {
        var edited = LaunchOptions.Parse(Options).SetEnvironment("PROTON_ENABLE_WAYLAND", "0");

        Assert.Equal("PROTON_ENABLE_WAYLAND=0 PROTON_ENABLE_HDR=1 mangohud %command%", edited.Format());
    }

    [Fact]
    public void SettingANewVariableAppendsIt()
    {
        var edited = LaunchOptions.Parse(Options).SetEnvironment("DXVK_HDR", "1");

        Assert.Equal("PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 DXVK_HDR=1 mangohud %command%", edited.Format());
    }

    [Fact]
    public void RemovingAVariableLeavesTheOthersAlone()
    {
        var edited = LaunchOptions.Parse(Options).RemoveEnvironment("PROTON_ENABLE_HDR");

        Assert.Equal("PROTON_ENABLE_WAYLAND=1 mangohud %command%", edited.Format());
    }

    [Fact]
    public void RemovingSomethingThatIsNotThereChangesNothing() =>
        Assert.Equal(Options, LaunchOptions.Parse(Options).RemoveEnvironment("DXVK_HDR").Format());

    [Fact]
    public void EditingNeverDisturbsTheLaunchChain()
    {
        var edited = LaunchOptions
            .Parse("A=1 /home/adam/bin/ow-dlss mangohud taskset -c 0-7 %command%")
            .SetEnvironment("B", "2")
            .RemoveEnvironment("A");

        Assert.Equal("B=2 /home/adam/bin/ow-dlss mangohud taskset -c 0-7 %command%", edited.Format());
    }
}

/// <summary>
/// Warnings for combinations that are individually valid and together do nothing.
/// </summary>
public class LaunchOptionsValidatorTests
{
    [Fact]
    public void WarnsWhenHdrIsEnabledWithoutWayland()
    {
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse("PROTON_ENABLE_HDR=1 %command%"));

        Assert.Contains(warnings, warning => warning.Contains("Wayland", StringComparison.Ordinal));
    }

    [Fact]
    public void StaysQuietWhenHdrHasWayland()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 DXVK_HDR=1 %command%"));

        Assert.DoesNotContain(warnings, warning => warning.Contains("Wayland", StringComparison.Ordinal));
    }

    [Fact]
    public void SaysNothingAboutADlssOverrideWithoutNvapi()
    {
        // Proton enables NVAPI by default, so an override on its own is the normal setup. A real
        // working configuration tripped this rule, which is how it was caught.
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse(
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_L %command%"));

        Assert.Empty(warnings);
    }

    [Fact]
    public void WarnsWhenMangoHudIsConfiguredButNotLaunched()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("MANGOHUD_CONFIG=fps_limit=60 %command%"));

        Assert.Contains(warnings, warning => warning.Contains("mangohud", StringComparison.Ordinal));
    }

    [Fact]
    public void RecognisesMangoHudBehindAnAbsolutePath()
    {
        var warnings = LaunchOptionsValidator.Validate(
            LaunchOptions.Parse("MANGOHUD_CONFIG=fps_limit=60 /usr/bin/mangohud %command%"));

        Assert.DoesNotContain(warnings, warning => warning.Contains("mangohud", StringComparison.Ordinal));
    }

    [Fact]
    public void HasNothingToSayAboutARealConfiguration()
    {
        // The real Overwatch configuration: HDR with Wayland, a DLSS preset override, and
        // mangohud in the chain. Nothing here is a mistake.
        var warnings = LaunchOptionsValidator.Validate(LaunchOptions.Parse(
            "PROTON_ENABLE_WAYLAND=1 PROTON_ENABLE_HDR=1 DXVK_HDR=1 " +
            "DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION=RENDER_PRESET_L " +
            "MANGOHUD_CONFIG=fps_limit=224 mangohud %command%"));

        Assert.Empty(warnings);
    }
}
