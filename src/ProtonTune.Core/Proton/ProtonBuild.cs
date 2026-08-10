namespace ProtonTune.Core.Proton;

/// <summary>
/// One Proton build installed on this machine.
/// </summary>
public sealed record ProtonBuild
{
    /// <summary>
    /// The internal name Steam identifies the build by, such as <c>proton_experimental</c> or
    /// <c>GE-Proton11-3</c>. This is the string that appears in <c>CompatToolMapping</c>, so it is
    /// what has to be written to change a game's Proton — the display name will not do.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The name to show a person, such as <c>Proton Experimental</c>.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Absolute path to the directory holding the build's <c>proton</c> script.</summary>
    public required string InstallPath { get; init; }

    /// <summary>Whether the build came from Valve or was installed by hand.</summary>
    public required ProtonBuildKind Kind { get; init; }

    /// <summary>
    /// The label from the build's <c>version</c> file, such as <c>experimental-11.0-20260805</c>,
    /// or <see langword="null"/> when there is no readable version file.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// The Steam app id for a Valve build. Custom builds are not apps and report <c>0</c>.
    /// </summary>
    public uint AppId { get; init; }

    /// <summary>
    /// Whether <see cref="Name" /> was inferred from the display name rather than read from a
    /// file Steam wrote.
    /// </summary>
    /// <remarks>
    /// Only Valve builds can be in this state, and only when Steam's compatibility log has been
    /// rotated away. The inference is reliable enough to display, but writing an inferred name
    /// into <c>CompatToolMapping</c> would silently do nothing if it were wrong, so anything that
    /// writes should treat this as a reason to warn first.
    /// </remarks>
    public bool NameIsDerived { get; init; }

    /// <summary>
    /// The variables this build honours, read from its own launch script. Defaults to
    /// <see cref="ProtonCapabilities.Unknown" />, which judges nothing.
    /// </summary>
    public ProtonCapabilities Capabilities { get; init; } = ProtonCapabilities.Unknown;
}
