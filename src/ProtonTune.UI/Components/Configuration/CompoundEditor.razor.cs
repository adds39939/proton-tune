using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits a variable that packs several settings into one string — <c>MANGOHUD_CONFIG</c>,
/// <c>DXVK_HUD</c> — one option at a time rather than as a single line of text.
/// </summary>
/// <remarks>
/// These formats always have far more options than are worth listing, so the ones described in
/// the definition files get controls and everything else stays editable as text. Unrecognised
/// entries are preserved either way.
/// </remarks>
public partial class CompoundEditor : ComponentBase
{
    /// <summary>How the variable is packed, and which options are offered.</summary>
    [Parameter]
    [EditorRequired]
    public required CompoundSchema Schema { get; set; }

    /// <summary>The current value, or <see langword="null"/> when the variable is not set.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// Raised with the new value, or <see langword="null"/> when nothing is left configured and
    /// the variable should be removed.
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    private CompoundValue Current => CompoundValue.Parse(Schema, Value);

    /// <summary>The entries with no control, as the text the user edits.</summary>
    private string AdditionalOptions =>
        string.Join(Schema.Separator, Current.Unrecognised.Select(entry => entry.Render(Schema)));

    /// <summary>An example of the format, built from the separator this variable actually uses.</summary>
    private string AdditionalPlaceholder =>
        string.Join(Schema.Separator, "round_corners" + Schema.Assignment + "5", "engine_version");

    /// <summary>
    /// The offered values, plus whatever is already set if it is not among them, so an option this
    /// build does not know cannot be dropped by opening a menu.
    /// </summary>
    private IEnumerable<string> ChoicesFor(CompoundOptionDefinition option) =>
        Current.GetValue(option.Key) is { Length: > 0 } current && !option.Choices.Contains(current)
            ? option.Choices.Append(current)
            : option.Choices;

    private Task ToggleOption(string key, bool isOn) =>
        Publish(isOn ? Current.Set(key, null) : Current.Remove(key));

    private Task SetOption(string key, string? value) =>
        Publish(string.IsNullOrWhiteSpace(value) ? Current.Remove(key) : Current.Set(key, value.Trim()));

    private Task OnAdditionalChanged(ChangeEventArgs args) =>
        Publish(Current.ReplaceUnrecognised(CompoundValue.Parse(Schema, args.Value?.ToString()).Entries));

    /// <summary>
    /// Reports the new value, removing the variable entirely once nothing is left rather than
    /// leaving an empty assignment behind.
    /// </summary>
    private Task Publish(CompoundValue value) =>
        ValueChanged.InvokeAsync(value.IsEmpty ? null : value.Format());
}
