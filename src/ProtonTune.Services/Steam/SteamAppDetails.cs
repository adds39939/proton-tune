namespace ProtonTune.Services.Steam;

/// <summary>
/// What the running Steam client holds for a game, as opposed to what is on disk.
/// </summary>
/// <param name="LaunchOptions">The launch options Steam would use for the next launch.</param>
/// <param name="CompatToolName">
/// The Proton build the game runs under, named as Steam knows it. This is the build in effect
/// rather than the one chosen — a game left to Steam's judgement reports whatever Steam picked,
/// not the empty string that was stored.
/// </param>
public sealed record SteamAppDetails(string LaunchOptions, string CompatToolName);
