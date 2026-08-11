namespace ProtonTune.UI.Formatting;

/// <summary>
/// Formats an app's last-played timestamp for display.
/// </summary>
public static class LastPlayedDisplay
{
    /// <summary>Once "n days ago" stops being easier to read than the date itself.</summary>
    private const int DaysBeforeShowingADate = 30;

    /// <summary>
    /// Describes when an app was last launched, in the largest unit that still says something
    /// useful: minutes within the hour, hours within the day, then days.
    /// </summary>
    /// <remarks>
    /// Elapsed rather than calendar based, and truncated rather than rounded — the convention for
    /// this kind of label, where "1 hour ago" is understood to mean at least an hour.
    /// </remarks>
    public static string Format(DateTimeOffset? lastPlayed)
    {
        if (lastPlayed is null)
        {
            return "Never";
        }

        var elapsed = DateTimeOffset.Now - lastPlayed.Value;

        // A timestamp in the future means the clock moved, not that the game is played tomorrow.
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return elapsed switch
        {
            { TotalMinutes: < 1 } => "Just now",
            { TotalHours: < 1 } => Count(elapsed.Minutes, "minute"),
            { TotalDays: < 1 } => Count(elapsed.Hours, "hour"),
            { TotalDays: < DaysBeforeShowingADate } => Count(elapsed.Days, "day"),
            _ => lastPlayed.Value.ToLocalTime().ToString("d MMM yyyy")
        };
    }

    private static string Count(int value, string unit) =>
        value == 1 ? $"1 {unit} ago" : $"{value} {unit}s ago";
}
