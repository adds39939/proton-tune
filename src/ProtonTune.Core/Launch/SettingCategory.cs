namespace ProtonTune.Core.Launch;

/// <summary>
/// A section of the configuration screen, as declared by one of the setting definition files.
/// </summary>
/// <remarks>
/// Data rather than an enumeration, so a new section is a new file. The identifier is the stable
/// name; the title is free to be reworded.
/// </remarks>
/// <param name="Id">
/// The stable key, lowercase. Two of these are known to the application by name — see
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
    /// environment.
    /// </remarks>
    public CommandDefinition? Command { get; init; }

    /// <summary>Whether this is the section with the given identifier.</summary>
    public bool Is(string id) => string.Equals(Id, id, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The section identifiers the application looks for by name.
/// </summary>
/// <remarks>
/// Each carries a control that is more than a list of variables — the CPU affinity picker and
/// MangoHud's launch-chain toggle. Renaming one of these identifiers removes that control rather
/// than the section, so it has to be changed here at the same time.
/// </remarks>
public static class SettingCategoryIds
{
    public const string Cpu = "cpu";

    public const string MangoHud = "mangohud";
}
