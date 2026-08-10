namespace ProtonTune.Services.Steam;

/// <summary>
/// Where Steam records a deliberate choice of compatibility tool, and what such a choice has to
/// look like once written.
/// </summary>
/// <remarks>
/// These live in <c>config/config.vdf</c>, which is per install rather than per user — unlike
/// launch options, which are in each account's <c>localconfig.vdf</c>.
/// </remarks>
internal static class SteamCompatTools
{
    /// <summary>
    /// The priority Steam gives a tool chosen in its own interface.
    /// </summary>
    /// <remarks>
    /// Steam settles competing mappings for one app by taking the highest priority, and the
    /// mappings that come from app metadata sit at 90. Writing a name without a priority would
    /// leave the entry at zero, so the game would carry on under whatever Steam picked for it and
    /// the change would look as though it had done nothing.
    /// </remarks>
    private const int ChosenPriority = 250;

    /// <summary>
    /// The priority left behind when a choice is cleared. Nothing should win with no tool named,
    /// so it goes back to the bottom rather than outranking Steam's own mapping with a blank.
    /// </summary>
    private const int ClearedPriority = 0;

    /// <summary>The object holding every mapping, keyed by app id.</summary>
    /// <remarks>
    /// Also used for reading, so the two directions cannot drift apart. The reader drops the first
    /// segment: the VDF parser hands back the root object's contents rather than the root itself.
    /// </remarks>
    public static readonly string[] MappingRoot =
        ["InstallConfigStore", "Software", "Valve", "Steam", "CompatToolMapping"];

    /// <summary>The key naming the tool, which is what a mapping means.</summary>
    public const string NameKey = "name";

    /// <summary>Steam's path to <c>config/config.vdf</c> under an installation root.</summary>
    public static string ConfigPathIn(string steamRoot) => Path.Combine(steamRoot, "config", "config.vdf");

    /// <summary>
    /// The complete set of values that record one app's choice.
    /// </summary>
    /// <param name="toolName">
    /// The tool's internal name, or an empty string to clear the choice and let Steam decide.
    /// </param>
    /// <remarks>
    /// All three keys are written every time. Steam's own entries always carry them, and an entry
    /// holding only a name would be read back with a priority of zero.
    /// </remarks>
    public static IReadOnlyList<(string[] Path, string Value)> Assignment(uint appId, string toolName) =>
    [
        (PathTo(appId, NameKey), toolName),
        (PathTo(appId, "config"), string.Empty),
        (PathTo(appId, "priority"), (toolName.Length == 0 ? ClearedPriority : ChosenPriority).ToString())
    ];

    /// <summary>The key path one field of one app's mapping lives at.</summary>
    public static string[] PathTo(uint appId, string key) => [.. MappingRoot, appId.ToString(), key];
}
