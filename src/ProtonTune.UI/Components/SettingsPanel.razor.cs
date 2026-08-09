using Microsoft.AspNetCore.Components;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components;

/// <summary>
/// Shows how ProtonTune is reading the local Steam installation.
/// </summary>
public partial class SettingsPanel : ComponentBase
{
    [Inject]
    private ISteamInstallLocator InstallLocator { get; set; } = null!;

    /// <summary>The detected Steam root, or <see langword="null"/> when Steam is not installed.</summary>
    private string? SteamRoot { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => SteamRoot = InstallLocator.Locate();
}
