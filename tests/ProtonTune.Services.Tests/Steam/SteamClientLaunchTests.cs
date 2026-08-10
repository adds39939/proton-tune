using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// How Steam is launched, which decides whether it outlives ProtonTune.
/// </summary>
/// <remarks>
/// Only the description of the launch is asserted here — actually starting Steam is not something
/// a test suite should do. The behaviour it stands for was measured separately: a child started
/// the plain way dies when ProtonTune's process group is signalled, and one in its own session
/// survives.
/// </remarks>
public class SteamClientLaunchTests
{
    /// <summary>
    /// Steam must not inherit ProtonTune's process group. A terminal closing, or a desktop session
    /// ending the app, signals the whole group — which would take down the Steam that ProtonTune
    /// had just restarted on the user's behalf.
    /// </summary>
    [Fact]
    public void StartsSteamInASessionOfItsOwn()
    {
        var startInfo = SteamClient.BuildStartInfo(detached: true, []);

        Assert.Equal("setsid", startInfo.FileName);
        Assert.Equal(["--fork", "steam"], startInfo.ArgumentList);
    }

    [Fact]
    public void PassesArgumentsThroughToSteam()
    {
        var startInfo = SteamClient.BuildStartInfo(detached: true, ["-shutdown"]);

        Assert.Equal(["--fork", "steam", "-shutdown"], startInfo.ArgumentList);
    }

    /// <summary>
    /// The output used to be captured into pipes nothing ever read, so Steam blocked once the
    /// buffer filled and took a broken pipe when ProtonTune exited. Steam logs for itself.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeverCapturesSteamsOutput(bool detached)
    {
        var startInfo = SteamClient.BuildStartInfo(detached, ["-shutdown"]);

        Assert.False(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    /// <summary>
    /// The fallback for a system without setsid runs Steam directly. Worse, but better than not
    /// starting Steam again at all after ProtonTune has just shut it down.
    /// </summary>
    [Fact]
    public void FallsBackToLaunchingSteamDirectly()
    {
        var startInfo = SteamClient.BuildStartInfo(detached: false, ["-shutdown"]);

        Assert.Equal("steam", startInfo.FileName);
        Assert.Equal(["-shutdown"], startInfo.ArgumentList);
    }
}
