using Microsoft.AspNetCore.Components;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// One heading within a configuration section, and the controls listed under it.
/// </summary>
/// <remarks>
/// <para>
/// A <c>details</c> element rather than a button and a flag, so the open state is the browser's to
/// keep. That is what makes it survive a re-render: the render tree always says <c>open</c>, so
/// Blazor never writes the attribute again and never argues with a person who has closed one.
/// The caller keys the group where its position can change, which is what stops a closed group in
/// one section reappearing closed as a different group in the next.
/// </para>
/// <para>
/// Groups open by default. A closed group hides what is set inside it, so the count in the summary
/// is not decoration — without it, closing a group would be a way to lose track of a setting.
/// </para>
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
