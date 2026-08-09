namespace ProtonTune.Core.Launch;

/// <summary>
/// What ProtonTune knows about one environment variable: where it belongs and what to call it.
/// </summary>
/// <param name="Variable">The environment variable name, exactly as it appears in launch options.</param>
/// <param name="Category">The section it is presented under.</param>
/// <param name="Label">A readable name for the setting.</param>
public sealed record SettingDefinition(string Variable, SettingCategory Category, string Label)
{
    /// <summary>One line on what the variable actually does.</summary>
    public string? Description { get; init; }
}
