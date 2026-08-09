namespace ProtonTune.UI.Formatting;

/// <summary>
/// Formats an app's last-played timestamp for display.
/// </summary>
public static class LastPlayedDisplay
{
    /// <summary>
    /// Describes when an app was last launched, relative for the recent past and as a date once
    /// "n days ago" stops being easier to read than the date itself.
    /// </summary>
    public static string Format(DateTimeOffset? lastPlayed)
    {
        if (lastPlayed is null)
        {
            return "Never";
        }

        var days = (DateTimeOffset.Now - lastPlayed.Value).Days;

        return days switch
        {
            <= 0 => "Today",
            1 => "Yesterday",
            < 30 => $"{days} days ago",
            _ => lastPlayed.Value.ToLocalTime().ToString("d MMM yyyy")
        };
    }
}
