using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits a wrapper command and its flags — Gamescope, which is configured entirely on its own
/// command line rather than through the environment.
/// </summary>
/// <remarks>
/// Setting any flag adds the command where it is not already there, rather than locking the flags
/// until the command is switched on.
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

    /// <summary>
    /// The search narrowing what is listed, if one is running.
    /// </summary>
    /// <remarks>
    /// Flags are searched beside the variables in the same section, so a term naming one finds it
    /// here rather than only in a section made of environment variables.
    /// </remarks>
    [Parameter]
    public SettingSearch Search { get; set; } = SettingSearch.None;

    private bool IsOn => Options.HasCommand(Command);

    /// <summary>
    /// Whether to offer the switch that puts the command in the chain. Held back while a search
    /// names only some of the flags, where it is not one of the answers.
    /// </summary>
    private bool ShowsToggle => !Search.IsActive || Search.Matches(Command);

    /// <summary>
    /// The flag groups worth drawing: those with a flag the search names, with the flags it does
    /// not left out. A heading whose every flag went goes with them.
    /// </summary>
    private IEnumerable<CommandFlagGroup> ListedGroups => Search.IsActive
        ? Command.Groups
            .Select(group => group with { Flags = group.Flags.Where(Search.Matches).ToList() })
            .Where(group => group.Flags.Count > 0)
        : Command.Groups;

    /// <summary>
    /// How many of a group's flags are set, shown beside its heading so a closed group still says
    /// whether there is anything inside it.
    /// </summary>
    private int SetCountIn(CommandFlagGroup group) =>
        group.Flags.Count(flag => Options.HasFlag(Command, flag));

    /// <summary>
    /// The offered values, plus whatever is already set if it is not among them, so opening a menu
    /// cannot drop a value ProtonTune does not know.
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
