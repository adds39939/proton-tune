namespace ProtonTune.Core.Launch;

/// <summary>
/// A run of settings shown together under one heading inside a section.
/// </summary>
/// <remarks>
/// Sections had grown long enough that a flat list of twenty variables was the whole problem —
/// Nvidia's driver overrides and its DLSS library swaps read as one list when they are two
/// unrelated decisions. Which settings belong together is declared in the definition files
/// alongside the settings themselves, so a new heading is an edit to data rather than a branch in
/// the editor.
/// </remarks>
/// <param name="Name">
/// The heading, or <see langword="null"/> for the run a file declares before any group. That run
/// is shown without a heading rather than under one invented for it.
/// </param>
/// <param name="Settings">The settings in the order the file declares them.</param>
public sealed record SettingGroup(string? Name, IReadOnlyList<SettingDefinition> Settings);
