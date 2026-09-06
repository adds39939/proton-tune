using Microsoft.AspNetCore.Components;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// One variable ProtonTune has no definition for: its name, its value, and a way to be rid of it.
/// </summary>
/// <remarks>
/// A component rather than markup in a tab, because two tabs list these — the custom variables
/// section and the one showing everything that is set — and a variable that is edited differently
/// depending on where it was found would be a variable nobody trusts.
/// </remarks>
public partial class CustomVariableRow : ComponentBase
{
    /// <summary>The variable name, shown as written since there is no readable name for it.</summary>
    [Parameter]
    [EditorRequired]
    public required string Name { get; set; }

    [Parameter]
    public string Value { get; set; } = string.Empty;

    /// <summary>Raised with the new value when the field is committed.</summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Raised when the variable should be taken out of the launch options.</summary>
    [Parameter]
    public EventCallback OnRemove { get; set; }
}
