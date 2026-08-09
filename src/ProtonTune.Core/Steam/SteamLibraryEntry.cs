namespace ProtonTune.Core.Steam;

/// <summary>
/// A single app installed into a Steam library folder, as described by its
/// <c>appmanifest_&lt;appid&gt;.acf</c> file.
/// </summary>
public sealed record SteamLibraryEntry
{
    /// <summary>The Steam application id. Unique across the whole store.</summary>
    public required uint AppId { get; init; }

    /// <summary>The display name Steam shows for the app.</summary>
    public required string Name { get; init; }

    /// <summary>Absolute path to the app's folder inside <c>steamapps/common</c>.</summary>
    public required string InstallDirectory { get; init; }

    /// <summary>Absolute path to the library folder the app lives in.</summary>
    public required string LibraryPath { get; init; }

    /// <summary>Whether this is a playable game or a Proton/runtime tool.</summary>
    public required SteamAppKind Kind { get; init; }

    /// <summary>Installed size in bytes, or 0 when the manifest does not report one.</summary>
    public long SizeOnDisk { get; init; }

    /// <summary>
    /// When the app was last launched, or <see langword="null"/> if it has never been played.
    /// </summary>
    public DateTimeOffset? LastPlayed { get; init; }

    /// <summary>
    /// Whether the install completed. Apps that are still downloading, updating, or that need
    /// repair appear in the library but are not yet runnable.
    /// </summary>
    public bool IsFullyInstalled { get; init; }
}
