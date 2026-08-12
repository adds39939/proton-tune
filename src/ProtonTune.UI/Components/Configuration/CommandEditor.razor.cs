using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits a wrapper command and its flags — Gamescope, which is configured entirely on its own
/// command line rather than through the environment.
/// </summary>
/// <remarks>
/// Setting any flag adds the command where it is not already there, and the toggle at the top says
/// so as it happens. The alternative was to lock the flags until the command was switched on, which
/// makes the first thing anyone tries do nothing at all.
/// </remarks>
public partial class CommandEditor : ComponentBase
{
    /// <summary>The command and the flags on offer, read from the definition file.</summary>
    [Parameter]
    [EditorRequired]
    public required CommandDefinition Command { get; set; }

    /// <summary>The options being edited.</summary>
    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    /// <summary>Raised with the options after any change.</summary>
    [Parameter]
    public EventCallback<LaunchOptions> OptionsChanged { get; set; }

    private bool IsOn => Options.HasCommand(Command);

    /// <summary>
    /// The offered values, plus whatever is already set if it is not among them, so a value this
    /// build of the command knows and ProtonTune does not is never dropped by opening a menu.
    /// </summary>
    private IEnumerable<string> ChoicesFor(CommandFlagDefinition flag) =>
        Options.FindFlag(Command, flag) is { Length: > 0 } current && !flag.Choices.Contains(current)
            ? flag.Choices.Append(current)
            : flag.Choices;

    private Task OnCommandToggled(bool present) =>
        OptionsChanged.InvokeAsync(Options.WithCommand(Command, present));

    private Task ToggleFlag(CommandFlagDefinition flag, bool isOn) =>
        OptionsChanged.InvokeAsync(Options.WithSwitch(Command, flag, isOn));

    private Task SetFlag(CommandFlagDefinition flag, string? value) =>
        OptionsChanged.InvokeAsync(Options.WithFlag(Command, flag, value));
}
