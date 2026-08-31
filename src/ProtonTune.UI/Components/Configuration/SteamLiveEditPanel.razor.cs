using Microsoft.AspNetCore.Components;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Switches on the Steam interface that lets a save take effect without closing Steam.
/// </summary>
/// <remarks>
/// Worth a group of its own rather than a line in a list. It changes a file inside the Steam
/// directory, it restarts Steam to take effect, and it opens a port other programs on the machine
/// can reach — none of which should be discovered after the fact, so the screen says all three
/// before the switch is touched.
/// </remarks>
public partial class SteamLiveEditPanel : ComponentBase
{
    [Inject]
    private ISteamLiveEditService LiveEdit { get; set; } = null!;

    private SteamLiveEditState State { get; set; } = new();

    private bool IsLoading { get; set; } = true;

    private bool IsBusy { get; set; }

    private string? Message { get; set; }

    private bool Failed { get; set; }

    /// <summary>What the switch reads as, which is what Steam is doing rather than what it was asked.</summary>
    private string StatusLabel => (State.IsEnabled, State.IsActive) switch
    {
        (true, true) => "On",
        (true, false) => "On, once Steam restarts",
        (false, true) => "Off, once Steam restarts",
        (false, false) => "Off"
    };

    /// <summary>
    /// Said when what was asked for and what Steam is doing have come apart, so a switch reading
    /// "on" beside launch options that still close Steam is explained rather than puzzling.
    /// </summary>
    private string? Unsettled
    {
        get
        {
            if (IsLoading || IsBusy || !State.IsSteamInstalled || State.IsSettled)
            {
                return null;
            }

            return State.IsEnabled
                ? "Steam has not been restarted since this was switched on, so saves still close " +
                  "and reopen it. Restart Steam to finish."
                : "Steam is still offering its debugging port from before this was switched off. " +
                  "It closes the next time Steam restarts.";
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            State = await LiveEdit.GetStateAsync();
        }
        catch (Exception e)
        {
            Failed = true;
            Message = $"Could not tell whether live editing is on: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Applies the change straight away rather than waiting for a save button. There is nothing
    /// else on this screen to save it alongside, and the switch reads as done the moment it moves.
    /// </summary>
    private async Task OnToggledAsync(ChangeEventArgs args)
    {
        if (args.Value is not bool enabled)
        {
            return;
        }

        IsBusy = true;
        Message = null;

        try
        {
            var result = await LiveEdit.SetEnabledAsync(enabled);

            State = result.State;
            Failed = !result.IsSuccess;

            Message = result.IsSuccess
                ? Describe(enabled, result.Restart)
                : result.Message ?? "Live editing could not be changed.";
        }
        catch (Exception e)
        {
            Failed = true;
            Message = $"Live editing could not be changed: {e.Message}";

            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Re-reads the state after a failure, so the switch shows where things were actually left
    /// rather than where the click was headed.
    /// </summary>
    private async Task RefreshAsync()
    {
        try
        {
            State = await LiveEdit.GetStateAsync();
        }
        catch
        {
            // Already reporting the failure that brought us here; a second one adds nothing.
        }
    }

    /// <summary>Says what was done, and what is still owed when Steam could not be restarted.</summary>
    private static string Describe(bool enabled, SteamRestartOutcome restart)
    {
        var opening = enabled ? "Live editing is on." : "Live editing is off.";

        return restart switch
        {
            SteamRestartOutcome.Restarted =>
                $"{opening} Steam was closed and started again.",
            SteamRestartOutcome.NotNeeded =>
                $"{opening} It applies the next time Steam starts.",
            SteamRestartOutcome.DeferredGameRunning =>
                $"{opening} A game is running, so Steam was left alone — it applies the next time " +
                "Steam restarts.",
            SteamRestartOutcome.RestartFailed =>
                $"{opening} Steam did not close in time and is still running as it was. Restart " +
                "Steam to finish.",
            _ => opening
        };
    }
}
