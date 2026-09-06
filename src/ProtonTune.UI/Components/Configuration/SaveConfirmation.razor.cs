using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Launch;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Asks to confirm a save, showing exactly what it would write.
/// </summary>
/// <remarks>
/// Saving closes Steam, writes to files it owns, and starts it again, so the line about to be
/// written is shown rather than assumed from the controls.
/// </remarks>
public partial class SaveConfirmation : ComponentBase
{
    /// <summary>What the save would write.</summary>
    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    /// <summary>What is stored now, so the difference can be marked.</summary>
    [Parameter]
    public string Saved { get; set; } = string.Empty;

    /// <summary>What is being saved — a game's name, or the profile.</summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>
    /// Anything else the save would do that the launch options do not show — a change of Proton
    /// build, or a profile applied across several games.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> AlsoChanging { get; set; } = [];

    /// <summary>Whether confirming would close and reopen Steam.</summary>
    [Parameter]
    public bool WillRestartSteam { get; set; }

    /// <summary>Whether the save is already under way, which locks both buttons.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback OnConfirm { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private string Title => Subject is { Length: > 0 } subject ? $"Save {subject}?" : "Save these changes?";

    private Task Confirm() => OnConfirm.InvokeAsync();

    private Task Cancel() => IsBusy ? Task.CompletedTask : OnCancel.InvokeAsync();

    /// <summary>
    /// Escape backs out, unless the save is already running and Steam is mid-restart.
    /// </summary>
    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" ? Cancel() : Task.CompletedTask;
}
