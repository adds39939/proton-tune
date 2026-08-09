using Microsoft.AspNetCore.Components;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// One setting as it is currently configured. Read-only: the variable name is shown next to the
/// value so what ProtonTune understood can be checked against what Steam actually holds.
/// </summary>
public partial class SettingRow : ComponentBase
{
    /// <summary>The readable name of the setting.</summary>
    [Parameter]
    [EditorRequired]
    public required string Label { get; set; }

    /// <summary>The environment variable behind it.</summary>
    [Parameter]
    [EditorRequired]
    public required string Variable { get; set; }

    /// <summary>The value currently set.</summary>
    [Parameter]
    [EditorRequired]
    public required string Value { get; set; }

    /// <summary>Optional explanation of what the setting does.</summary>
    [Parameter]
    public string? Description { get; set; }
}
