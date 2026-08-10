namespace ProtonTune.Core.Launch;

/// <summary>A named set of options, shown together under a heading.</summary>
/// <param name="Name">
/// The heading, or <see langword="null"/> for a set that needs none — a short compound variable
/// with no natural divisions is one unnamed group.
/// </param>
/// <param name="Options">The options in the order they should be shown.</param>
public sealed record CompoundOptionGroup(string? Name, IReadOnlyList<CompoundOptionDefinition> Options);

/// <summary>
/// How one variable packs several settings into a single string, and which of them ProtonTune
/// offers as controls.
/// </summary>
/// <remarks>
/// <para>
/// Several of the variables worth setting are really lists — <c>MANGOHUD_CONFIG</c>,
/// <c>DXVK_HUD</c>, <c>VKD3D_CONFIG</c>. Describing the shape here rather than in code means a
/// variable of that sort is added by writing it down, the same as any other.
/// </para>
/// <para>
/// Always partial. MangoHud alone has well over a hundred options; listing the ones worth reaching
/// for while tuning a game is the point, and everything else stays editable as text so nothing is
/// out of reach.
/// </para>
/// </remarks>
/// <param name="Separator">What sits between entries, usually a comma.</param>
/// <param name="Assignment">What joins a key to its value, usually an equals sign.</param>
/// <param name="Groups">The options ProtonTune offers as controls.</param>
public sealed record CompoundSchema(
    string Separator,
    string Assignment,
    IReadOnlyList<CompoundOptionGroup> Groups)
{
    /// <summary>What these formats use unless a definition says otherwise.</summary>
    public const string DefaultSeparator = ",";

    public const string DefaultAssignment = "=";

    private Dictionary<string, CompoundOptionDefinition>? _byKey;

    /// <summary>Every offered option, across all groups, in display order.</summary>
    public IEnumerable<CompoundOptionDefinition> AllOptions => Groups.SelectMany(group => group.Options);

    /// <summary>
    /// Looks up an option, or returns <see langword="null"/> when it is not one ProtonTune offers.
    /// </summary>
    public CompoundOptionDefinition? Find(string key)
    {
        _byKey ??= AllOptions
            .GroupBy(option => option.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return _byKey.GetValueOrDefault(key);
    }
}
