using System.Text.RegularExpressions;
using ProtonTune.Core.Proton;

namespace ProtonTune.Core.Launch;

/// <summary>How a setting is presented and edited.</summary>
public enum SettingKind
{
    /// <summary>On or off. Off removes the variable rather than setting it to zero.</summary>
    Toggle,

    /// <summary>One of a known set of values.</summary>
    Choice,

    /// <summary>Free text.</summary>
    Text,

    /// <summary>A number, such as a frame rate cap.</summary>
    Number
}

/// <summary>
/// What ProtonTune knows about one environment variable: where it belongs, what to call it, and
/// how it should be edited.
/// </summary>
/// <remarks>
/// Read from the setting definition files rather than written in code, so adding a variable does
/// not mean changing the application.
/// </remarks>
/// <param name="Variable">The environment variable name, exactly as it appears in launch options.</param>
/// <param name="Category">The section it is presented under.</param>
/// <param name="Label">A readable name for the setting.</param>
public sealed record SettingDefinition(string Variable, SettingCategory Category, string Label)
{
    private Regex[]? _buildPatterns;

    /// <summary>One line on what the variable actually does.</summary>
    public string? Description { get; init; }

    /// <summary>The control used to edit it.</summary>
    public SettingKind Kind { get; init; } = SettingKind.Text;

    /// <summary>
    /// The value written when a <see cref="SettingKind.Toggle" /> is switched on. Usually
    /// <c>1</c>, but some settings are a switch over a compound value — the DLSS debug overlay is
    /// turned on by writing <c>DLSSIndicator=1024</c>.
    /// </summary>
    public string OnValue { get; init; } = "1";

    /// <summary>
    /// The values offered for a <see cref="SettingKind.Choice" />. A value already set that is
    /// not in this list is still offered, so a preset ProtonTune has not heard of is never
    /// silently replaced.
    /// </summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Example text shown in an empty field.</summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// How the value is packed, for a variable that holds several settings in one string. Null for
    /// the ordinary case of a variable holding one value.
    /// </summary>
    /// <remarks>
    /// Where this is set the setting is edited option by option rather than as text, whatever
    /// <see cref="Kind" /> says.
    /// </remarks>
    public CompoundSchema? Compound { get; init; }

    /// <summary>
    /// Patterns naming the Proton builds this setting exists in, matched against a build's name
    /// and version. Empty means it is offered for every build.
    /// </summary>
    /// <remarks>
    /// A declaration made in the definition file, and the weaker of the two things ProtonTune
    /// knows: where a build's own launch script can be read, what it actually consults decides.
    /// This speaks for the variables that reading a script cannot settle — those implemented in
    /// the shipped renderer libraries, where the names are assembled at runtime and never appear
    /// whole.
    /// </remarks>
    public IReadOnlyList<string> ProtonBuilds { get; init; } = [];

    /// <summary>
    /// Whether to hide the setting outright on a build it does not apply to, rather than showing
    /// it greyed out.
    /// </summary>
    /// <remarks>
    /// For settings that exist in one family of builds and nowhere else. A GE-Proton feature shown
    /// against Valve's Proton is not a setting the user might reconsider — it is noise in a list
    /// they are trying to read. Left off, the setting stays visible and says why it does nothing,
    /// which is the better answer where the build might plausibly gain it.
    /// </remarks>
    public bool RestrictToProtonBuild { get; init; }

    /// <summary>Whether a stored value counts as this setting being on.</summary>
    public bool IsOn(string? value) =>
        value is not null && !string.Equals(value, "0", StringComparison.Ordinal) && value.Length > 0;

    /// <summary>
    /// Whether the definition file declares this setting to exist in a build.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when no builds are named, when the build is unknown, or when one of
    /// the patterns matches its name or version.
    /// </returns>
    public bool AppliesTo(ProtonBuild? build)
    {
        if (ProtonBuilds.Count == 0 || build is null)
        {
            return true;
        }

        // Compiled on first use. A malformed pattern is treated as matching nothing rather than
        // throwing out of a render: a typo in a data file should narrow a list, not break a page.
        _buildPatterns ??= ProtonBuilds.Select(Compile).OfType<Regex>().ToArray();

        return _buildPatterns.Any(pattern =>
            pattern.IsMatch(build.Name) || (build.Version is { } version && pattern.IsMatch(version)));
    }

    private static Regex? Compile(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
