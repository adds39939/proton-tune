using ProtonTune.UI.Formatting;

namespace ProtonTune.UI.Tests.Formatting;

/// <summary>
/// The label under every game in the library. It reports elapsed time in the largest unit that
/// still says something useful, so the boundaries between those units are where it can go wrong.
/// </summary>
public class LastPlayedDisplayTests
{
    private static string Ago(TimeSpan elapsed) => LastPlayedDisplay.Format(DateTimeOffset.Now - elapsed);

    [Fact]
    public void SaysNeverWhenAGameHasNotBeenPlayed() =>
        Assert.Equal("Never", LastPlayedDisplay.Format(null));

    /// <summary>Below a minute there is no number worth showing.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(59)]
    public void SaysJustNowWithinTheFirstMinute(int seconds) =>
        Assert.Equal("Just now", Ago(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(1, "1 minute ago")]
    [InlineData(2, "2 minutes ago")]
    [InlineData(59, "59 minutes ago")]
    public void CountsMinutesWithinTheHour(int minutes, string expected) =>
        Assert.Equal(expected, Ago(TimeSpan.FromMinutes(minutes)));

    /// <summary>The minute the hour turns over, which is where the unit changes.</summary>
    [Theory]
    [InlineData(60, "1 hour ago")]
    [InlineData(61, "1 hour ago")]
    [InlineData(119, "1 hour ago")]
    [InlineData(120, "2 hours ago")]
    public void CountsHoursOncePastFiftyNineMinutes(int minutes, string expected) =>
        Assert.Equal(expected, Ago(TimeSpan.FromMinutes(minutes)));

    [Theory]
    [InlineData(23, "23 hours ago")]
    [InlineData(24, "1 day ago")]
    [InlineData(47, "1 day ago")]
    [InlineData(48, "2 days ago")]
    public void CountsDaysOncePastTwentyFourHours(int hours, string expected) =>
        Assert.Equal(expected, Ago(TimeSpan.FromHours(hours)));

    /// <summary>
    /// Far enough back that a count stops being easier to read than the date it stands for.
    /// </summary>
    [Fact]
    public void ShowsADateOnceTheCountStopsHelping()
    {
        var played = DateTimeOffset.Now - TimeSpan.FromDays(45);

        Assert.Equal(played.ToLocalTime().ToString("d MMM yyyy"), LastPlayedDisplay.Format(played));
    }

    [Fact]
    public void KeepsCountingDaysRightUpToThatPoint() =>
        Assert.Equal("29 days ago", Ago(TimeSpan.FromDays(29)));

    /// <summary>
    /// A timestamp in the future means the clock moved, not that the game is played tomorrow.
    /// Reporting a negative count would be worse than saying nothing.
    /// </summary>
    [Fact]
    public void TreatsAFutureTimestampAsNow() =>
        Assert.Equal("Just now", LastPlayedDisplay.Format(DateTimeOffset.Now.AddHours(2)));
}
