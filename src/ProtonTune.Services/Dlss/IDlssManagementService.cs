using ProtonTune.Core.Dlss;
using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Dlss;

/// <summary>
/// Replaces the DLSS libraries a game ships with newer ones, and puts them back.
/// </summary>
public interface IDlssManagementService
{
    /// <summary>Finds a game's DLSS libraries and reports what has been done to them.</summary>
    DlssGameStatus Inspect(SteamLibraryEntry entry);

    /// <summary>
    /// Points a game's DLSS libraries at a shipped version, keeping the originals.
    /// </summary>
    /// <returns>The path of the launch script that keeps the links applied.</returns>
    Task<string> ApplyAsync(SteamLibraryEntry entry, DlssRuntime runtime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the libraries the game shipped with, and unpicks any link left without one.
    /// </summary>
    /// <returns>
    /// What was achieved. Reverting can succeed only partially — a link outlives its backup if
    /// anything re-creates it after a previous revert — and a caller that reports success
    /// regardless would be telling someone their game is untouched while a file inside it still
    /// points at ProtonTune.
    /// </returns>
    Task<DlssRevertResult> RevertAsync(SteamLibraryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>The launch script for a game, whether or not it exists yet.</summary>
    string ScriptPathFor(uint appId);
}
