namespace ProtonTune.Core.Launch;

/// <summary>How a setting is presented and edited.</summary>
public enum SettingKind
{
    /// <summary>On or off. Off removes the variable rather than setting it to zero.</summary>
    Toggle,

    /// <summary>One of a known set of values.</summary>
    Choice,

    /// <summary>Free text.</summary>
    Text
}

/// <summary>
/// What ProtonTune knows about one environment variable: where it belongs, what to call it, and
/// how it should be edited.
/// </summary>
/// <param name="Variable">The environment variable name, exactly as it appears in launch options.</param>
/// <param name="Category">The section it is presented under.</param>
/// <param name="Label">A readable name for the setting.</param>
public sealed record SettingDefinition(string Variable, SettingCategory Category, string Label)
{
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

    /// <summary>Example text shown in an empty <see cref="SettingKind.Text" /> field.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Whether a stored value counts as this setting being on.</summary>
    public bool IsOn(string? value) =>
        value is not null && !string.Equals(value, "0", StringComparison.Ordinal) && value.Length > 0;
}
