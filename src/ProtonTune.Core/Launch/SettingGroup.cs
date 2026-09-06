namespace ProtonTune.Core.Launch;

/// <summary>
/// A run of settings shown together under one heading inside a section.
/// </summary>
/// <remarks>
/// Which settings belong together is declared in the definition files alongside the settings, so a
/// new heading is an edit to data rather than a branch in the editor.
/// </remarks>
/// <param name="Name">
/// The heading, or <see langword="null"/> for the run a file declares before any group. That run
/// is shown without a heading rather than under one invented for it.
/// </param>
/// <param name="Settings">The settings in the order the file declares them.</param>
public sealed record SettingGroup(string? Name, IReadOnlyList<SettingDefinition> Settings);
