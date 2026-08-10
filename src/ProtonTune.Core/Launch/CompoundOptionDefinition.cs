namespace ProtonTune.Core.Launch;

/// <summary>
/// What ProtonTune knows about one option inside a compound variable — a single entry of
/// something like <c>MANGOHUD_CONFIG</c>, which packs many settings into one string.
/// </summary>
/// <param name="Key">The option name as it appears inside the variable.</param>
/// <param name="Label">A readable name.</param>
public sealed record CompoundOptionDefinition(string Key, string Label)
{
    /// <summary>
    /// The control used to edit it. A <see cref="SettingKind.Toggle" /> writes the key on its own
    /// rather than a value, which is how these formats express a flag.
    /// </summary>
    public SettingKind Kind { get; init; } = SettingKind.Toggle;

    /// <summary>The values offered for a <see cref="SettingKind.Choice" />.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Example text for an empty field.</summary>
    public string? Placeholder { get; init; }

    /// <summary>What the option does, where it is not obvious from the name.</summary>
    public string? Description { get; init; }
}
