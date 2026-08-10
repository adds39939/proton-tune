namespace ProtonTune.Core.Launch;

/// <summary>
/// The environment variables ProtonTune recognises, and where each belongs.
/// </summary>
/// <remarks>
/// <para>
/// Loaded from the setting definition files rather than written in code, so the set of variables
/// the application offers can grow without it changing.
/// </para>
/// <para>
/// Deliberately partial. Anything absent from it still parses and is still written back — it
/// simply appears under custom variables rather than in a named section, so an unknown variable
/// costs the user presentation and never data.
/// </para>
/// </remarks>
public sealed class SettingCatalog
{
    private readonly Dictionary<string, SettingDefinition> _byVariable;

    public SettingCatalog(
        IEnumerable<SettingCategory> categories,
        IEnumerable<SettingDefinition> definitions)
    {
        Categories = categories.OrderBy(category => category.Order).ToList();
        All = definitions.ToList();

        // First definition wins. A variable declared twice is a mistake in the files, and taking
        // the first keeps the catalogue usable rather than failing the whole load over it.
        _byVariable = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);

        foreach (var definition in All)
        {
            _byVariable.TryAdd(definition.Variable, definition);
        }
    }

    /// <summary>A catalogue with nothing in it, for when no definitions could be read.</summary>
    public static SettingCatalog Empty { get; } = new([], []);

    /// <summary>The sections, in the order they should be listed.</summary>
    public IReadOnlyList<SettingCategory> Categories { get; }

    /// <summary>Every recognised variable.</summary>
    public IReadOnlyList<SettingDefinition> All { get; }

    /// <summary>
    /// Looks up a variable, or returns <see langword="null"/> when ProtonTune has no opinion
    /// about it.
    /// </summary>
    public SettingDefinition? Find(string variable) => _byVariable.GetValueOrDefault(variable);

    /// <summary>The settings belonging to a section, in the order the file declares them.</summary>
    public IReadOnlyList<SettingDefinition> In(SettingCategory category) =>
        All.Where(definition => definition.Category.Is(category.Id)).ToList();

    /// <summary>Finds a section by its identifier.</summary>
    public SettingCategory? FindCategory(string id) =>
        Categories.FirstOrDefault(category => category.Is(id));
}
