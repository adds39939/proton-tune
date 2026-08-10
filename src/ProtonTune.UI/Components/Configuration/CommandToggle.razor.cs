using Microsoft.AspNetCore.Components;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Adds or removes a command from the chain the game is launched through.
/// </summary>
/// <remarks>
/// Distinct from a setting: this writes a word into the launch chain rather than an environment
/// variable, so it is shown with the command it inserts rather than a variable name.
/// </remarks>
public partial class CommandToggle : ComponentBase
{
    /// <summary>The readable name of what the command does.</summary>
    [Parameter]
    [EditorRequired]
    public required string Label { get; set; }

    /// <summary>The command inserted into the launch chain.</summary>
    [Parameter]
    [EditorRequired]
    public required string Command { get; set; }

    /// <summary>Whether the command is currently in the chain.</summary>
    [Parameter]
    public bool IsOn { get; set; }

    /// <summary>What the command does.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>Raised with whether the command should be in the chain.</summary>
    [Parameter]
    public EventCallback<bool> IsOnChanged { get; set; }

    private Task OnToggled(ChangeEventArgs args) => IsOnChanged.InvokeAsync(args.Value is true);
}
