using ProtonTune.Core.Launch;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Profiles;

/// <summary>
/// A set of launch options kept once and applied to any game that wants them.
/// </summary>
/// <remarks>
/// Steam has no such concept, so this lives in ProtonTune's own directory. Applying it writes
/// ordinary per-game launch options, indistinguishable to Steam from ones set by hand.
/// </remarks>
public interface IGlobalProfileService
{
    /// <summary>The global launch options, empty when none have been set.</summary>
    Task<LaunchOptions> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores the global launch options.</summary>
    Task SaveAsync(LaunchOptions options, CancellationToken cancellationToken = default);

    /// <summary>Whether a game is following the global profile.</summary>
    Task<bool> IsLinkedAsync(uint appId, CancellationToken cancellationToken = default);

    /// <summary>Records whether a game follows the global profile.</summary>
    Task SetLinkedAsync(uint appId, bool linked, CancellationToken cancellationToken = default);

    /// <summary>Every game currently following the profile.</summary>
    Task<IReadOnlyList<uint>> GetLinkedAppsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the profile and writes it to every game following it.
    /// </summary>
    /// <remarks>
    /// All the games are written in one pass so Steam is restarted once.
    /// </remarks>
    Task<LaunchOptionsSaveResult> SaveAndApplyAsync(
        LaunchOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the profile and stops every game following it, leaving the games' own settings
    /// exactly as they are.
    /// </summary>
    Task ResetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops games whose stored launch options no longer match the profile.
    /// </summary>
    /// <remarks>
    /// Which games follow the profile is ProtonTune's own belief, held separately from the options
    /// themselves, so anything editing them behind its back can leave it false. A game shown as
    /// following a profile it does not match would be rewritten on the next save.
    /// </remarks>
    /// <returns>How many games stopped following the profile.</returns>
    Task<int> ReconcileLinksAsync(CancellationToken cancellationToken = default);
}
