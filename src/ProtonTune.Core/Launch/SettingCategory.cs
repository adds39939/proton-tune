namespace ProtonTune.Core.Launch;

/// <summary>
/// A section of the configuration screen, as declared by one of the setting definition files.
/// </summary>
/// <remarks>
/// These are data rather than an enumeration, so a new section is a new file rather than a change
/// to the application. That is also why the identifier matters: it is the stable name, while the
/// title is free to be reworded.
/// </remarks>
/// <param name="Id">
/// The stable key, lowercase. Three of these are known to the application by name — see
/// <see cref="SettingCategoryIds" />.
/// </param>
/// <param name="Title">The heading shown to a person.</param>
/// <param name="Order">Where the section sits in the list, lowest first.</param>
public sealed record SettingCategory(string Id, string Title, int Order)
{
    /// <summary>
    /// The command this section puts in the launch chain, where it configures one rather than a
    /// set of variables. Null for the sections that are variables alone.
    /// </summary>
    /// <remarks>
    /// Gamescope is the reason this exists: none of what it does is reachable through the
    /// environment, so a section able to describe only variables could not describe it.
    /// </remarks>
    public CommandDefinition? Command { get; init; }

    /// <summary>Whether this is the section with the given identifier.</summary>
    public bool Is(string id) => string.Equals(Id, id, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The section identifiers the application looks for by name.
/// </summary>
/// <remarks>
/// Each of these is presented as more than a list of variables — the CPU affinity picker,
/// MangoHud's option-by-option editor, and the headings Nvidia's settings are grouped under. A
/// section can be renamed in its file freely; renaming one of these identifiers removes the extra
/// presentation rather than the section, so it has to be changed here at the same time.
/// </remarks>
public static class SettingCategoryIds
{
    public const string Nvidia = "nvidia";

    public const string Cpu = "cpu";

    public const string MangoHud = "mangohud";
}
