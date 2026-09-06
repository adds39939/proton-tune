namespace ProtonTune.Services.Steam;

/// <summary>
/// What the running Steam client holds for a game, as opposed to what is on disk.
/// </summary>
/// <param name="LaunchOptions">The launch options Steam would use for the next launch.</param>
/// <param name="CompatToolName">
/// The Proton build the game runs under, named as Steam knows it. The build in effect rather than
/// the one chosen: a game left to Steam reports what Steam picked, not the stored empty string.
/// </param>
public sealed record SteamAppDetails(string LaunchOptions, string CompatToolName);
