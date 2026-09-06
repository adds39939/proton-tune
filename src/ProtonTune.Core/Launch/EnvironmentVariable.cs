namespace ProtonTune.Core.Launch;

/// <summary>
/// A single <c>NAME=value</c> assignment from the front of a launch options string.
/// </summary>
/// <param name="Name">The variable name.</param>
/// <param name="Value">
/// The value with any quoting resolved. It may itself contain <c>=</c> and <c>,</c>, as compound
/// settings like <c>MANGOHUD_CONFIG=fps_limit=224,fps_limit_method=late</c> do.
/// </param>
public sealed record EnvironmentVariable(string Name, string Value)
{
    /// <summary>
    /// The assignment exactly as it was written, when it came from parsing. Used only while it
    /// still spells out this exact name and value, so copying with a new <see cref="Value" />
    /// discards it and the assignment is re-quoted.
    /// </summary>
    public string? OriginalText { get; init; }
}
