using System.Text.RegularExpressions;

namespace ProtonTune.Core.Proton;

/// <summary>
/// Infers the internal name Steam gives a Valve Proton build from the name it displays.
/// </summary>
/// <remarks>
/// <para>
/// Builds installed by hand state their internal name in <c>compatibilitytool.vdf</c>, but
/// Valve's do not: they ship only a <c>toolmanifest.vdf</c> and a <c>version</c> file, and the
/// name Steam knows them by lives in a binary metadata cache. Steam does record it in plain text
/// in its compatibility log, which is the preferred source; this is the fallback for when that
/// log has been rotated away.
/// </para>
/// <para>
/// The rule below reproduces all fourteen names Steam has registered on the development machine —
/// <c>proton_experimental</c>, <c>proton_hotfix</c>, and <c>proton_37</c> through
/// <c>proton_11</c> — but it is a pattern observed rather than a contract Valve publishes, so
/// callers should treat a derived name as a guess.
/// </para>
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
