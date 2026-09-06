using ProtonTune.Core.Launch;
using ProtonTune.UI.Components.Configuration;

namespace ProtonTune.UI.Tests.Components.Configuration;

/// <summary>
/// The line each game carries in the copy picker. It is the only thing distinguishing one row from
/// the next, so it has to say what taking that game's configuration would actually bring.
/// </summary>
public class CopyConfigSummaryTests
{
    private static string Describe(string launchOptions) =>
        CopyConfigDialog.Describe(LaunchOptions.Parse(launchOptions));

    [Fact]
    public void CountsTheVariables() =>
        Assert.Equal("2 variables", Describe("PROTON_ENABLE_WAYLAND=1 DXVK_HDR=1 %command%"));

    [Fact]
    public void SaysOneVariableInTheSingular() =>
        Assert.Equal("1 variable", Describe("PROTON_ENABLE_WAYLAND=1 %command%"));

    [Fact]
    public void MentionsTheLaunchChain() =>
        Assert.Equal(
            "1 variable, a launch chain",
            Describe("PROTON_ENABLE_WAYLAND=1 mangohud %command%"));

    [Fact]
    public void MentionsArgumentsPassedToTheGame() =>
        Assert.Equal("1 argument", Describe("%command% -dx11"));

    /// <summary>
    /// A game with nothing set is never offered, but the description must still read as a sentence
    /// rather than an empty string if one ever reaches it.
    /// </summary>
    [Fact]
    public void SaysSoWhenThereIsNothingToTake() => Assert.Equal("Nothing set", Describe(string.Empty));
}
