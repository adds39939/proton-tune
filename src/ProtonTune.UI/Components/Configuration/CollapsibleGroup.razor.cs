using Microsoft.AspNetCore.Components;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// One heading within a configuration section, and the controls listed under it.
/// </summary>
/// <remarks>
/// A <c>details</c> element rather than a button and a flag, so the open state is the browser's:
/// the render tree always says <c>open</c>, so Blazor never rewrites the attribute over someone
/// who closed one. Callers key the group where its position can change. Groups open by default,
/// and the count in the summary is what stops a closed one hiding a setting.
/// </remarks>
public partial class CollapsibleGroup : ComponentBase
{
    /// <summary>
    /// The heading. A group without one — the settings a file lists before its first heading —
    /// renders its contents alone, since there is nothing to collapse them into.
    /// </summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>How many of the controls inside are set, shown beside the heading when any are.</summary>
    [Parameter]
    public int SetCount { get; set; }

    [Parameter]
    [EditorRequired]
    public required RenderFragment ChildContent { get; set; }
}
