using ProtonTune.Core.Launch;

namespace ProtonTune.Core.Tests.Launch;

/// <summary>
/// What the configuration screen's search box finds. Someone who remembers the variable and
/// someone who only remembers what it does have to arrive at the same row.
/// </summary>
public class SettingSearchTests
{
    private static readonly SettingCategory Display = new("display", "Display", 4);

    private static readonly SettingDefinition Wayland =
        new("PROTON_ENABLE_WAYLAND", Display, "Run natively on Wayland")
        {
            Description = "Skips XWayland. Required before HDR will do anything."
        };

    private static readonly CommandFlagDefinition HdrFlag =
        new("--hdr-enabled", "Output HDR")
        {
            Description = "Hands the display an HDR signal.",
            Aliases = ["--hdr-enable"]
        };

    private static readonly CommandDefinition Gamescope =
        new("gamescope", "Launch through Gamescope")
        {
            Description = "A nested compositor the game renders into.",
            Groups = [new CommandFlagGroup("HDR", [HdrFlag])]
        };

    [Theory]
    [InlineData("PROTON_ENABLE_WAYLAND")]
    [InlineData("ENABLE_WAY")]
    [InlineData("Run natively")]
    [InlineData("XWayland")]
    public void FindsASettingByItsVariableItsLabelOrItsDescription(string term) =>
        Assert.True(SettingSearch.For(term).Matches(Wayland));

    [Theory]
    [InlineData("wayland")]
    [InlineData("WAYLAND")]
    [InlineData("WaYlAnD")]
    public void IgnoresCase(string term) => Assert.True(SettingSearch.For(term).Matches(Wayland));

    /// <summary>
    /// Trimmed rather than taken literally: a space typed after a word must not empty the results
    /// someone is halfway through reading.
    /// </summary>
    [Fact]
    public void IgnoresTheWhitespaceAroundTheTerm() =>
        Assert.True(SettingSearch.For("  wayland  ").Matches(Wayland));

    [Fact]
    public void FindsNothingItDoesNotName() =>
        Assert.False(SettingSearch.For("mangohud").Matches(Wayland));

    /// <summary>
    /// An empty search is the unfiltered view rather than an empty one, so the same code path
    /// serves both.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MatchesEverythingUntilSomethingIsTyped(string? term)
    {
        var search = SettingSearch.For(term);

        Assert.False(search.IsActive);
        Assert.True(search.Matches(Wayland));
        Assert.True(search.Matches(HdrFlag));
        Assert.True(search.MatchesVariable("ANYTHING_AT_ALL"));
    }

    [Fact]
    public void HasNoActiveTermByDefault() => Assert.False(SettingSearch.None.IsActive);

    [Theory]
    [InlineData("--hdr-enabled")]
    [InlineData("hdr")]
    [InlineData("Output HDR")]
    [InlineData("HDR signal")]
    public void FindsACommandFlag(string term) => Assert.True(SettingSearch.For(term).Matches(HdrFlag));

    /// <summary>
    /// A flag written out under another of its spellings is still that flag, so the spelling
    /// someone read in a guide finds the control that edits it.
    /// </summary>
    [Fact]
    public void FindsACommandFlagUnderAnAlias() =>
        Assert.True(SettingSearch.For("--hdr-enable").Matches(HdrFlag));

    [Fact]
    public void FindsACommandByItsOwnName() =>
        Assert.True(SettingSearch.For("gamescope").Matches(Gamescope));

    /// <summary>
    /// What decides whether a command is drawn at all: its flags are searched beside the variables
    /// in the same section, so a term naming one puts the command on screen.
    /// </summary>
    [Fact]
    public void FindsACommandThroughAFlagOfIts()
    {
        Assert.True(SettingSearch.For("--hdr-enabled").MatchesAnythingIn(Gamescope));
        Assert.False(SettingSearch.For("--hdr-enabled").Matches(Gamescope));
    }

    [Fact]
    public void FindsNoCommandWhereNeitherItNorItsFlagsAreNamed() =>
        Assert.False(SettingSearch.For("esync").MatchesAnythingIn(Gamescope));

    /// <summary>
    /// A variable ProtonTune has no definition for has only its name to be found by, and it still
    /// has to be findable.
    /// </summary>
    [Theory]
    [InlineData("PROTON_VKD3D_HEAP", true)]
    [InlineData("vkd3d", true)]
    [InlineData("mangohud", false)]
    public void FindsAVariableItHasNoDefinitionFor(string term, bool expected) =>
        Assert.Equal(expected, SettingSearch.For(term).MatchesVariable("PROTON_VKD3D_HEAP"));
}
