namespace ProtonTune.UI.Formatting;

/// <summary>
/// Formats filesystem paths for display.
/// </summary>
public static class PathDisplay
{
    private static readonly string HomeDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Shortens a path by collapsing the home directory to <c>~</c>. Steam lives under the home
    /// directory on Linux, so the prefix is repeated on every path and carries no information.
    /// </summary>
    public static string Abbreviate(string path) =>
        !string.IsNullOrEmpty(HomeDirectory) && path.StartsWith(HomeDirectory, StringComparison.Ordinal)
            ? string.Concat("~", path.AsSpan(HomeDirectory.Length))
            : path;
}
