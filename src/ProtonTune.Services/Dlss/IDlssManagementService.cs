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

    /// <summary>Restores the libraries the game shipped with.</summary>
    Task RevertAsync(SteamLibraryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>The launch script for a game, whether or not it exists yet.</summary>
    string ScriptPathFor(uint appId);
}
