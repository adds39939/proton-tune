using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Finds artwork in the cache Steam fills as it browses the library.
/// </summary>
/// <remarks>
/// The layout is <c>appcache/librarycache/&lt;appid&gt;/…</c>, and an upgraded install holds both
/// of Steam's arrangements at once: assets sit either in the app's directory or one level down in
/// a content-hashed directory. The app id is a directory in both, so the hash is enumerated rather
/// than derived. File names moved too — <c>library_600x900.jpg</c> or <c>library_capsule.jpg</c>,
/// <c>header.jpg</c> or <c>library_header.jpg</c> — so both are searched for each shape.
/// </remarks>
internal static class SteamLibraryCache
{
    /// <summary>The cache root inside a Steam installation.</summary>
    public static string RootIn(string steamRoot) => Path.Combine(steamRoot, "appcache", "librarycache");

    /// <summary>
    /// The file names an artwork shape is stored under, in the order they should be preferred.
    /// </summary>
    public static IReadOnlyList<string> FileNamesFor(GameArtworkKind kind) => kind switch
    {
        GameArtworkKind.Capsule => ["library_600x900.jpg", "library_capsule.jpg"],
        GameArtworkKind.Header => ["header.jpg", "library_header.jpg"],
        _ => []
    };

    /// <summary>
    /// Returns the path to an app's cached artwork, or <see langword="null"/> when Steam has not
    /// cached that shape for it.
    /// </summary>
    public static string? Find(string steamRoot, uint appId, GameArtworkKind kind)
    {
        var names = FileNamesFor(kind);

        if (names.Count == 0)
        {
            return null;
        }

        var appDirectory = Path.Combine(RootIn(steamRoot), appId.ToString());

        try
        {
            if (!Directory.Exists(appDirectory))
            {
                return null;
            }

            foreach (var directory in Directories(appDirectory))
            {
                foreach (var name in names)
                {
                    var candidate = Path.Combine(directory, name);

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The app's own directory, then each hashed directory beneath it.</summary>
    private static IEnumerable<string> Directories(string appDirectory) =>
        new[] { appDirectory }.Concat(Directory.EnumerateDirectories(appDirectory));
}
