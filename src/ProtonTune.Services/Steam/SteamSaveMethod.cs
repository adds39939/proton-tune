namespace ProtonTune.Services.Steam;

/// <summary>
/// How a save made right now would reach Steam, which decides what the screen has to warn about.
/// </summary>
public enum SteamSaveMethod
{
    /// <summary>
    /// Steam is not running, so its files can simply be written. Nothing to warn about.
    /// </summary>
    Files,

    /// <summary>
    /// Steam is running and offering live editing, so the change goes straight into the client.
    /// Nothing is closed, and a game in progress is no obstacle.
    /// </summary>
    Live,

    /// <summary>
    /// Steam is running without live editing. It holds its configuration in memory and writes it
    /// out as it exits, so it has to be closed and started again for a change to survive.
    /// </summary>
    Restart,

    /// <summary>
    /// Steam is running a game, without live editing to reach around it. Saving would mean closing
    /// Steam out from under the game, so nothing can be saved until it is closed.
    /// </summary>
    Blocked
}
