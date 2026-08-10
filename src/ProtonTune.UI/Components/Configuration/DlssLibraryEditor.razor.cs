using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Dlss;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Dlss;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Replaces a game's DLSS libraries with newer ones, and puts them back.
/// </summary>
/// <remarks>
/// Unlike the settings around it, this changes files on disk the moment it is switched — there is
/// nothing to preview and no string to write. What it does leave for the save is the launch
/// script entry, because Steam restores a game's own libraries whenever it verifies or updates
/// it, and only re-applying the links on every launch keeps the swap in place.
/// </remarks>
public partial class DlssLibraryEditor : ComponentBase
{
    [Inject]
    private IDlssManagementService Dlss { get; set; } = null!;

    [Inject]
    private IDlssRuntimeProvider Runtimes { get; set; } = null!;

    /// <summary>The game whose libraries are being managed.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }

    /// <summary>
    /// Raised with the launch script's path and whether it should be in the launch chain.
    /// </summary>
    [Parameter]
    public EventCallback<(string ScriptPath, bool Present)> ScriptChanged { get; set; }

    private DlssGameStatus Status { get; set; } = new();

    private bool IsBusy { get; set; }

    private string? Error { get; set; }

    private DlssRuntime? Latest => Runtimes.Latest;

    /// <inheritdoc />
    protected override void OnParametersSet() => Status = Dlss.Inspect(Entry);

    private static string StateLabel(DlssLinkState state) => state switch
    {
        DlssLinkState.Managed => "Tuned",
        DlssLinkState.Foreign => "Linked elsewhere",
        _ => "Game's own"
    };

    private async Task OnToggled(ChangeEventArgs args)
    {
        if (Latest is null)
        {
            return;
        }

        IsBusy = true;
        Error = null;

        try
        {
            var shouldUpgrade = args.Value is true;

            if (shouldUpgrade)
            {
                var scriptPath = await Dlss.ApplyAsync(Entry, Latest);

                await ScriptChanged.InvokeAsync((scriptPath, true));
            }
            else
            {
                var scriptPath = Dlss.ScriptPathFor(Entry.AppId);

                await Dlss.RevertAsync(Entry);
                await ScriptChanged.InvokeAsync((scriptPath, false));
            }
        }
        catch (Exception e)
        {
            Error = $"The DLSS libraries could not be changed: {e.Message}";
        }
        finally
        {
            Status = Dlss.Inspect(Entry);
            IsBusy = false;
        }
    }
}
