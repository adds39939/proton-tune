namespace ProtonTune.Core.Launch;

/// <summary>
/// The environment variables ProtonTune recognises, and where each belongs.
/// </summary>
/// <remarks>
/// Loaded from the setting definition files, so the set of variables can grow without changing the
/// application. Deliberately partial: anything absent still parses and is written back, appearing
/// under custom variables rather than in a named section.
/// </remarks>
public sealed class SettingCatalog
{
    private readonly Dictionary<string, SettingDefinition> _byVariable;

    public SettingCatalog(
        IEnumerable<SettingCategory> categories,
        IEnumerable<SettingDefinition> definitions)
    {
        Categories = categories.OrderBy(category => category.Order).ToList();
        All = [.. definitions];

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

    /// <summary>
    /// A section's settings as the headings its file declares, in the order it declares them.
    /// </summary>
    /// <remarks>
    /// A run rather than a lookup: a new group starts wherever the heading changes, so a heading
    /// used twice stays two runs. A section declaring no headings comes back as one unnamed group.
    /// </remarks>
    public IReadOnlyList<SettingGroup> GroupsIn(SettingCategory category) =>
        Group(In(category));

    /// <summary>
    /// The same, over a list already narrowed down: the editor hides settings that do not apply to
    /// the build in force, and a heading whose settings were all hidden goes with them.
    /// </summary>
    public static IReadOnlyList<SettingGroup> Group(IEnumerable<SettingDefinition> definitions)
    {
        var groups = new List<SettingGroup>();
        var current = new List<SettingDefinition>();
        string? name = null;

        foreach (var definition in definitions)
        {
            if (current.Count > 0 && !string.Equals(definition.Group, name, StringComparison.Ordinal))
            {
                groups.Add(new SettingGroup(name, current));
                current = [];
            }

            name = definition.Group;
            current.Add(definition);
        }

        if (current.Count > 0)
        {
            groups.Add(new SettingGroup(name, current));
        }

        return groups;
    }

    /// <summary>Finds a section by its identifier.</summary>
    public SettingCategory? FindCategory(string id) =>
        Categories.FirstOrDefault(category => category.Is(id));
}
