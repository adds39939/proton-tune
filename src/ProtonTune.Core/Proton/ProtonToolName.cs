using System.Text.RegularExpressions;

namespace ProtonTune.Core.Proton;

/// <summary>
/// Infers the internal name Steam gives a Valve Proton build from the name it displays.
/// </summary>
/// <remarks>
/// Valve builds do not state their internal name on disk; Steam records it in plain text in its
/// compatibility log, and this is the fallback for when that log has been rotated away. The rule
/// is a pattern observed rather than one Valve publishes, so a derived name is a guess.
/// </remarks>
public static partial class ProtonToolName
{
    /// <summary>
    /// Derives the internal name for a Valve build from its Steam app name.
    /// </summary>
    /// <example>
    /// <c>Proton 5.13</c> becomes <c>proton_513</c>, <c>Proton 9.0</c> becomes <c>proton_9</c>,
    /// and <c>Proton Experimental</c> becomes <c>proton_experimental</c>.
    /// </example>
    public static string Derive(string appName)
    {
        var name = appName.Trim();
        var version = VersionedName().Match(name);

        if (version.Success)
        {
            var major = version.Groups["major"].Value;
            var minor = version.Groups["minor"].Value;

            return minor == "0" ? $"proton_{major}" : $"proton_{major}{minor}";
        }

        return Separators().Replace(name.ToLowerInvariant(), "_").Trim('_');
    }

    [GeneratedRegex(@"^proton\s+(?<major>\d+)\.(?<minor>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionedName();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Separators();
}
