using ProtonTune.Core.Launch;

namespace ProtonTune.Core.Tests.Launch;

/// <summary>
/// Reading and writing the flags of a wrapper command, which is the only way a tool like Gamescope
/// can be configured at all — none of what it does is reachable through the environment.
/// </summary>
public class CommandFlagTests
{
    private static readonly CommandFlagDefinition Width = new("-W", "Output width")
    {
        Kind = SettingKind.Number,
        Aliases = ["--output-width"]
    };

    private static readonly CommandFlagDefinition Height = new("-H", "Output height")
    {
        Kind = SettingKind.Number
    };

    private static readonly CommandFlagDefinition Fullscreen = new("-f", "Fullscreen")
    {
        Aliases = ["--fullscreen"]
    };

    private static readonly CommandFlagDefinition AdaptiveSync = new("--adaptive-sync", "Adaptive sync");

    private static readonly CommandDefinition Gamescope = new("gamescope", "Launch through Gamescope")
    {
        Terminator = "--",
        Groups =
        [
            new CommandFlagGroup("Output", [Width, Height]),
            new CommandFlagGroup("Window", [Fullscreen, AdaptiveSync])
        ]
    };

    /// <summary>A command that takes no arguments of its own, which most wrappers are.</summary>
    private static readonly CommandDefinition MangoHud = new("mangohud", "Launch through MangoHud");

    [Fact]
    public void ReadsAFlagThatTakesAValue()
    {
        var options = LaunchOptions.Parse("gamescope -W 3840 -H 2160 -- %command%");

        Assert.Equal("3840", options.FindFlag(Gamescope, Width));
        Assert.Equal("2160", options.FindFlag(Gamescope, Height));
    }

    [Fact]
    public void ReadsASwitchAsPresentOrAbsent()
    {
        var options = LaunchOptions.Parse("gamescope -f -- %command%");

        Assert.True(options.HasFlag(Gamescope, Fullscreen));
        Assert.False(options.HasFlag(Gamescope, AdaptiveSync));
    }

    /// <summary>The long spellings are what the documentation uses, so both have to be read.</summary>
    [Fact]
    public void ReadsAFlagWrittenOutInFull() =>
        Assert.Equal(
            "3840",
            LaunchOptions.Parse("gamescope --output-width 3840 -- %command%").FindFlag(Gamescope, Width));

    /// <summary><c>getopt_long</c> accepts a value joined to its flag, so it turns up in real strings.</summary>
    [Fact]
    public void ReadsAValueJoinedToItsFlag() =>
        Assert.Equal(
            "3840",
            LaunchOptions.Parse("gamescope --output-width=3840 -- %command%").FindFlag(Gamescope, Width));

    /// <summary>Nothing after the terminator belongs to the command, however it is spelled.</summary>
    [Fact]
    public void ReadsNothingPastTheTerminator() =>
        Assert.Null(LaunchOptions.Parse("gamescope -- mangohud -W 3840 %command%").FindFlag(Gamescope, Width));

    [Fact]
    public void ReadsNothingFromACommandThatIsNotThere() =>
        Assert.Null(LaunchOptions.Parse("mangohud %command%").FindFlag(Gamescope, Width));

    [Fact]
    public void AddsAFlagInsideTheCommandsOwnArguments()
    {
        var edited = LaunchOptions.Parse("gamescope -W 3840 -- %command%").WithFlag(Gamescope, Height, "2160");

        Assert.Equal("gamescope -W 3840 -H 2160 -- %command%", edited.Format());
    }

    [Fact]
    public void AddsASwitchInsideTheCommandsOwnArguments()
    {
        var edited = LaunchOptions.Parse("gamescope -W 3840 -- %command%")
            .WithSwitch(Gamescope, Fullscreen, true);

        Assert.Equal("gamescope -W 3840 -f -- %command%", edited.Format());
    }

    [Fact]
    public void ChangesAValueWhereItStands()
    {
        var edited = LaunchOptions.Parse("gamescope -W 1920 -H 1080 -- %command%")
            .WithFlag(Gamescope, Width, "3840");

        Assert.Equal("gamescope -W 3840 -H 1080 -- %command%", edited.Format());
    }

    /// <summary>
    /// Rewriting someone's <c>--output-width</c> as <c>-W</c> because they typed in the box beside
    /// it is a change they did not ask for, in a string they have to be able to read.
    /// </summary>
    [Fact]
    public void KeepsTheSpellingAFlagWasWrittenWith()
    {
        var edited = LaunchOptions.Parse("gamescope --output-width 1920 -- %command%")
            .WithFlag(Gamescope, Width, "3840");

        Assert.Equal("gamescope --output-width 3840 -- %command%", edited.Format());
    }

    [Fact]
    public void KeepsAValueJoinedToItsFlagJoined()
    {
        var edited = LaunchOptions.Parse("gamescope --output-width=1920 -- %command%")
            .WithFlag(Gamescope, Width, "3840");

        Assert.Equal("gamescope --output-width=3840 -- %command%", edited.Format());
    }

    [Fact]
    public void RemovingAFlagTakesItsValueWithIt()
    {
        var edited = LaunchOptions.Parse("gamescope -W 3840 -H 2160 -- %command%")
            .WithFlag(Gamescope, Width, null);

        Assert.Equal("gamescope -H 2160 -- %command%", edited.Format());
    }

    [Fact]
    public void ClearingAFieldRemovesTheFlagRatherThanWritingNothing()
    {
        var edited = LaunchOptions.Parse("gamescope -W 3840 -- %command%").WithFlag(Gamescope, Width, "  ");

        Assert.Equal("gamescope -- %command%", edited.Format());
    }

    [Fact]
    public void RemovingASwitchLeavesTheFlagsAroundItAlone()
    {
        var edited = LaunchOptions.Parse("gamescope -W 3840 -f -H 2160 -- %command%")
            .WithSwitch(Gamescope, Fullscreen, false);

        Assert.Equal("gamescope -W 3840 -H 2160 -- %command%", edited.Format());
    }

    /// <summary>
    /// A flag with no command to belong to is not a setting waiting to take effect — it is a word
    /// handed to the game. Setting one has to bring the command with it.
    /// </summary>
    [Fact]
    public void SettingAFlagAddsTheCommandItBelongsTo()
    {
        var edited = LaunchOptions.Parse("mangohud %command%").WithFlag(Gamescope, Width, "3840");

        Assert.Equal("gamescope -W 3840 -- mangohud %command%", edited.Format());
    }

    [Fact]
    public void SettingAFlagOnNothingAtAllWritesAWholeString()
    {
        var edited = LaunchOptions.Parse(string.Empty).WithSwitch(Gamescope, AdaptiveSync, true);

        Assert.Equal("gamescope --adaptive-sync -- %command%", edited.Format());
    }

    [Fact]
    public void AddingTheCommandWrapsWhatIsAlreadyThere()
    {
        var edited = LaunchOptions.Parse("mangohud taskset -c 0-7 %command%").WithCommand(Gamescope, true);

        Assert.Equal("gamescope -- mangohud taskset -c 0-7 %command%", edited.Format());
    }

    /// <summary>
    /// Its flags mean nothing without it, and leaving them behind would hand the next command in
    /// the chain arguments meant for something else.
    /// </summary>
    [Fact]
    public void RemovingTheCommandTakesItsFlagsAndItsTerminator()
    {
        var edited = LaunchOptions.Parse("gamescope -W 3840 -f -- mangohud %command%")
            .WithCommand(Gamescope, false);

        Assert.Equal("mangohud %command%", edited.Format());
    }

    /// <summary>
    /// Without a terminator there is no telling where one command's arguments stop, so the
    /// arguments end at the first token that is not a flag — which is how Gamescope itself reads a
    /// command line missing its <c>--</c>.
    /// </summary>
    [Fact]
    public void RemovingACommandWrittenWithoutItsTerminatorSparesTheNextOne()
    {
        var edited = LaunchOptions.Parse("gamescope -W 3840 mangohud %command%").WithCommand(Gamescope, false);

        Assert.Equal("mangohud %command%", edited.Format());
    }

    [Fact]
    public void ACommandThatTakesNoArgumentsClaimsNothingAfterIt()
    {
        var edited = LaunchOptions.Parse("mangohud taskset -c 0-7 %command%").WithCommand(MangoHud, false);

        Assert.Equal("taskset -c 0-7 %command%", edited.Format());
    }

    /// <summary>
    /// The lists are partial by design. A flag ProtonTune has never heard of is not a mistake, and
    /// editing the ones beside it must leave it exactly where it was written.
    /// </summary>
    [Fact]
    public void LeavesAFlagItDoesNotKnowUntouched()
    {
        var edited = LaunchOptions.Parse("gamescope --reshade-effect crt.fx -W 1920 -- %command%")
            .WithFlag(Gamescope, Width, "3840")
            .WithSwitch(Gamescope, AdaptiveSync, true);

        Assert.Equal("gamescope --reshade-effect crt.fx -W 3840 --adaptive-sync -- %command%", edited.Format());
    }

    [Fact]
    public void EditingFlagsNeverDisturbsTheEnvironment()
    {
        var edited = LaunchOptions.Parse("DXVK_HDR=1 gamescope -f -- %command%")
            .WithFlag(Gamescope, Width, "3840");

        Assert.Equal("DXVK_HDR=1 gamescope -f -W 3840 -- %command%", edited.Format());
    }

    /// <summary>
    /// Matched on file name at both ends, the same as every other command in the chain, so an
    /// absolute path to the same binary is still recognised.
    /// </summary>
    [Fact]
    public void FindsTheCommandBehindAnAbsolutePath()
    {
        var options = LaunchOptions.Parse("/usr/bin/gamescope -W 3840 -- %command%");

        Assert.True(options.HasCommand(Gamescope));
        Assert.Equal("3840", options.FindFlag(Gamescope, Width));
    }

    [Fact]
    public void SwitchingSomethingOnThatIsAlreadyOnChangesNothing()
    {
        const string original = "gamescope -f -- %command%";

        Assert.Equal(original, LaunchOptions.Parse(original).WithSwitch(Gamescope, Fullscreen, true).Format());
    }

    [Fact]
    public void RemovingSomethingThatIsNotThereChangesNothing()
    {
        const string original = "gamescope -W 3840 -- %command%";

        Assert.Equal(original, LaunchOptions.Parse(original).WithSwitch(Gamescope, Fullscreen, false).Format());
    }

    /// <summary>A whole configuration, read back one flag at a time.</summary>
    [Fact]
    public void ReadsEveryFlagOfARealString()
    {
        var options = LaunchOptions.Parse(
            "ENABLE_GAMESCOPE_WSI=1 gamescope -W 3840 -H 2160 -r 240 -f --adaptive-sync -- %command%");

        Assert.Equal("3840", options.FindFlag(Gamescope, Width));
        Assert.Equal("2160", options.FindFlag(Gamescope, Height));
        Assert.True(options.HasFlag(Gamescope, Fullscreen));
        Assert.True(options.HasFlag(Gamescope, AdaptiveSync));
        Assert.Equal("1", options.FindEnvironment("ENABLE_GAMESCOPE_WSI")?.Value);
    }
}
