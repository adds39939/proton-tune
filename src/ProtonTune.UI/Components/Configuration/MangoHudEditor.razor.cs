using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits <c>MANGOHUD_CONFIG</c> one option at a time, rather than as a single string.
/// </summary>
/// <remarks>
/// MangoHud has far more options than are worth listing, so the recognised ones get controls and
/// everything else stays editable as text. Unrecognised entries are preserved either way.
/// </remarks>
public partial class MangoHudEditor : ComponentBase
{
    /// <summary>The current <c>MANGOHUD_CONFIG</c> value, or null when it is not set.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// Raised with the new value, or <see langword="null"/> when nothing is left configured and
    /// the variable should be removed.
    /// </summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    private MangoHudConfig Config => MangoHudConfig.Parse(Value);

    /// <summary>The entries with no control, as the comma-separated text the user edits.</summary>
    private string AdditionalOptions => string.Join(',', Config.Unrecognised);

    /// <summary>
    /// The offered values, plus whatever is already set if it is not among them, so an option
    /// this build does not know cannot be dropped by opening a menu.
    /// </summary>
    private IEnumerable<string> ChoicesFor(MangoHudOptionDefinition definition) =>
        Config.GetValue(definition.Key) is { Length: > 0 } current && !definition.Choices.Contains(current)
            ? definition.Choices.Append(current)
            : definition.Choices;

    private Task ToggleOption(string key, bool isOn) =>
        Publish(isOn ? Config.Set(key, null) : Config.Remove(key));

    private Task SetOption(string key, string? value) =>
        Publish(string.IsNullOrWhiteSpace(value) ? Config.Remove(key) : Config.Set(key, value.Trim()));

    private Task OnAdditionalChanged(ChangeEventArgs args) =>
        Publish(Config.ReplaceUnrecognised(MangoHudConfig.Parse(args.Value?.ToString()).Options));

    /// <summary>
    /// Reports the new configuration, removing the variable entirely once nothing is left rather
    /// than leaving an empty <c>MANGOHUD_CONFIG=</c> behind.
    /// </summary>
    private Task Publish(MangoHudConfig config) =>
        ValueChanged.InvokeAsync(config.IsEmpty ? null : config.Format());
}
