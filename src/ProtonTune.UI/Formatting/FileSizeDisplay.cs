namespace ProtonTune.UI.Formatting;

/// <summary>
/// Formats byte counts for display.
/// </summary>
public static class FileSizeDisplay
{
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB", "TiB"];

    /// <summary>
    /// Scales a byte count to its largest sensible binary unit. Steam reports install sizes in
    /// binary units, so this deliberately matches rather than using decimal GB.
    /// </summary>
    /// <returns>The formatted size, or "Unknown" when the manifest reported no size.</returns>
    public static string Format(long bytes)
    {
        if (bytes <= 0)
        {
            return "Unknown";
        }

        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {Units[unit]}";
    }
}
