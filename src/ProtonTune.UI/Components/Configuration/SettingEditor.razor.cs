using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits one recognised setting with whichever control suits it.
/// </summary>
public partial class SettingEditor : ComponentBase
{
    /// <summary>What the setting is and how it should be edited.</summary>
    [Parameter]
    [EditorRequired]
    public required SettingDefinition Definition { get; set; }

    /// <summary>The current value, or <see langword="null"/> when the variable is not set.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// Raised with the new value, or <see langword="null"/> to remove the variable entirely.
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// Whether the Proton build the game runs under is known not to read this variable, in which
    /// case setting it does nothing at all.
    /// </summary>
    /// <remarks>
    /// The control stays usable. A false negative here would take away a setting that works, and
    /// the probe cannot see into every component — saying so and leaving the decision alone is
    /// safer than deciding for the user.
    /// </remarks>
    [Parameter]
    public bool IsIgnored { get; set; }

    /// <summary>The build doing the ignoring, so the note can name it.</summary>
    [Parameter]
    public string? BuildName { get; set; }

    private bool IsOn => Definition.IsOn(Value);

    /// <summary>
    /// The offered values, plus whatever is already set if it is not among them. A preset newer
    /// than this build must not disappear because the user opened a dropdown.
    /// </summary>
    private IEnumerable<string> Choices =>
        Value is { Length: > 0 } current && !Definition.Choices.Contains(current)
            ? Definition.Choices.Append(current)
            : Definition.Choices;

    private Task OnToggled(ChangeEventArgs args) =>
        ValueChanged.InvokeAsync(args.Value is true ? Definition.OnValue : null);

    private Task OnChoicePicked(ChangeEventArgs args) => Apply(args.Value?.ToString());

    private Task OnTextChanged(ChangeEventArgs args) => Apply(args.Value?.ToString());

    /// <summary>
    /// Applies a value, treating empty as unset so clearing a field removes the variable rather
    /// than writing <c>NAME=</c>.
    /// </summary>
    private Task Apply(string? value) =>
        ValueChanged.InvokeAsync(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
}
