using ProtonTune.Core.Proton;

namespace ProtonTune.Core.Tests.Proton;

/// <summary>
/// Reporting a setting as unsupported is a claim that it does nothing, which is why this refuses
/// to make one it cannot back up.
/// </summary>
public class ProtonCapabilitiesTests
{
    private static ProtonCapabilities Reading(params string[] variables) => new()
    {
        Variables = variables.ToHashSet(StringComparer.Ordinal),
        IsKnown = true
    };

    [Fact]
    public void AnswersForAVariableTheBuildReads() =>
        Assert.True(Reading("PROTON_DLSS_UPGRADE").Reads("PROTON_DLSS_UPGRADE"));

    /// <summary>
    /// The case worth having: PROTON_ENABLE_NGX_UPDATER is read by no build installed here, so
    /// setting it is silent.
    /// </summary>
    [Fact]
    public void AnswersForAVariableTheBuildDoesNotRead()
    {
        var capabilities = Reading("PROTON_LOG");

        Assert.False(capabilities.Reads("PROTON_ENABLE_NGX_UPDATER"));
        Assert.True(capabilities.Ignores("PROTON_ENABLE_NGX_UPDATER"));
    }

    /// <summary>
    /// The renderer variables are implemented in shipped DLLs, where names are often assembled
    /// from a prefix at runtime — the DLSS preset overrides are built from DXVK_NVAPI_DRS_ and
    /// never appear whole. Since ProtonTune reads only the launch script, it has no opinion, and
    /// must not turn no opinion into "unsupported".
    /// </summary>
    [Theory]
    [InlineData("DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION")]
    [InlineData("DXVK_HDR")]
    [InlineData("VKD3D_CONFIG")]
    [InlineData("MANGOHUD_CONFIG")]
    public void HasNoOpinionOnVariablesItCannotSee(string variable)
    {
        var capabilities = Reading("PROTON_LOG");

        Assert.Null(capabilities.Reads(variable));
        Assert.False(capabilities.Ignores(variable));
    }

    /// <summary>
    /// A build that could not be read judges nothing. The same object stands in for the global
    /// profile, which belongs to no build at all.
    /// </summary>
    [Fact]
    public void JudgesNothingWhenTheBuildCouldNotBeRead()
    {
        Assert.Null(ProtonCapabilities.Unknown.Reads("PROTON_LOG"));
        Assert.False(ProtonCapabilities.Unknown.Ignores("PROTON_ENABLE_NGX_UPDATER"));
    }

    /// <summary>
    /// Steam's variables are case sensitive, and a near miss is a different variable rather than
    /// the same one spelled loosely.
    /// </summary>
    [Fact]
    public void MatchesNamesExactly() =>
        Assert.Null(Reading("PROTON_LOG").Reads("proton_log"));
}
