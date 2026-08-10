namespace ProtonTune.Services.Steam;

/// <summary>
/// A copy of a Steam configuration file, taken before ProtonTune changed it.
/// </summary>
/// <remarks>
/// Kept beside the file it came from, named after the moment it was taken. These files are edited
/// by splicing single values into text Steam owns; a backup is what makes a splice that goes
/// wrong recoverable rather than final.
/// </remarks>
public sealed record SteamConfigBackup
{
    /// <summary>The suffix that marks a file as one of ProtonTune's backups.</summary>
    public const string Extension = ".bak";

    /// <summary>What goes between the file's name and its timestamp.</summary>
    public const string Marker = ".protontune-";

    /// <summary>How the moment is written into the name.</summary>
    public const string TimestampFormat = "yyyyMMdd-HHmmss";

    /// <summary>The backup file itself.</summary>
    public required string Path { get; init; }

    /// <summary>The file it would be restored over.</summary>
    public required string TargetPath { get; init; }

    /// <summary>When it was taken, read from its name.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>The name of the file this belongs to, such as <c>localconfig.vdf</c>.</summary>
    public string TargetName => System.IO.Path.GetFileName(TargetPath);

    /// <summary>The name a backup of a file taken now would have.</summary>
    public static string NameFor(string targetPath, DateTimeOffset moment) =>
        $"{targetPath}{Marker}{moment.ToString(TimestampFormat, System.Globalization.CultureInfo.InvariantCulture)}{Extension}";

    /// <summary>Every backup of a file, whatever their age.</summary>
    public static string SearchPatternFor(string targetPath) =>
        $"{System.IO.Path.GetFileName(targetPath)}{Marker}*{Extension}";
}
