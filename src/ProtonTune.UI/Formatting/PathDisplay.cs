namespace ProtonTune.UI.Formatting;

/// <summary>
/// Formats filesystem paths for display.
/// </summary>
public static class PathDisplay
{
    private static readonly string HomeDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Shortens a path by collapsing the home directory to <c>~</c>, since Steam lives under it on
    /// Linux and the prefix repeats on every path.
    /// </summary>
    public static string Abbreviate(string path) =>
        !string.IsNullOrEmpty(HomeDirectory) && path.StartsWith(HomeDirectory, StringComparison.Ordinal)
            ? string.Concat("~", path.AsSpan(HomeDirectory.Length))
            : path;
}
