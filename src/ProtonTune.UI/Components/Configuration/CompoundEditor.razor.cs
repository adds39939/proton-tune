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

    /// <summary>
    /// Whether to list only the options this value actually carries.
    /// </summary>
    /// <remarks>
    /// For the tab answering "what is set", where a variable holding four of a hundred options has
    /// to show the four. Everywhere else the whole list is the point, since an option cannot be
    /// turned on from a list it is missing from.
    /// </remarks>
    [Parameter]
    public bool SetOnly { get; set; }

    private CompoundValue Current => CompoundValue.Parse(Schema, Value);

    /// <summary>
    /// The groups worth drawing, with the options they should carry. A heading whose every option
    /// went is dropped rather than left standing over nothing.
    /// </summary>
    private IEnumerable<CompoundOptionGroup> ListedGroups =>
        SetOnly ? Current.GroupsWithValues() : Schema.Groups;

    /// <summary>
    /// Whether the free-text field belongs on screen. Listing only what is set means listing it
    /// only when it holds something, since there is nothing to add to here.
    /// </summary>
    private bool ShowsAdditional => !SetOnly || AdditionalCount > 0;

    /// <summary>The entries with no control, as the text the user edits.</summary>
    private string AdditionalOptions =>
        string.Join(Schema.Separator, Current.Unrecognised.Select(entry => entry.Render(Schema)));

    /// <summary>
    /// How many of a group's options are set, shown beside its heading so a closed group still
    /// says whether there is anything inside it.
    /// </summary>
    private int SetCountIn(CompoundOptionGroup group) =>
        group.Options.Count(option => Current.Contains(option.Key));

    /// <summary>The same, for the entries no group claims and the free-text field holds.</summary>
    private int AdditionalCount => Current.Unrecognised.Count;

    /// <summary>An example of the format, built from the separator this variable actually uses.</summary>
    private string AdditionalPlaceholder =>
        string.Join(Schema.Separator, "round_corners" + Schema.Assignment + "5", "engine_version");

    /// <summary>
    /// The offered values, plus whatever is already set if it is not among them, so opening a menu
    /// cannot drop an option ProtonTune does not know.
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
