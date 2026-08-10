namespace ProtonTune.Core.Dlss;

/// <summary>What has been done to one of a game's DLSS libraries.</summary>
public enum DlssLinkState
{
    /// <summary>The file the game shipped with, untouched.</summary>
    Original,

    /// <summary>A link to the copy ProtonTune manages.</summary>
    Managed,

    /// <summary>
    /// A link to somewhere ProtonTune does not control — someone set this up by hand.
    /// </summary>
    Foreign
}

/// <summary>
/// One DLSS library inside a game's install.
/// </summary>
/// <param name="Path">The absolute path to the file.</param>
/// <param name="RelativePath">Where it sits inside the install, which is how it is identified.</param>
/// <param name="State">What has been done to it.</param>
/// <param name="LinkTarget">Where it points, when it is a link.</param>
public sealed record DlssLibrary(string Path, string RelativePath, DlssLinkState State, string? LinkTarget)
{
    /// <summary>The library's file name, such as <c>nvngx_dlss.dll</c>.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// The DLSS libraries a game has, and what ProtonTune has done to them.
/// </summary>
/// <remarks>
/// A game can carry several, in more than one directory — an Unreal title keeps them under
/// <c>Engine/Plugins</c>, several levels down — so this is always a list rather than a single
/// file.
/// </remarks>
public sealed record DlssGameStatus
{
    /// <summary>Every library found, ordered by where it sits in the install.</summary>
    public IReadOnlyList<DlssLibrary> Libraries { get; init; } = [];

    /// <summary>Whether the game has any DLSS libraries at all.</summary>
    public bool HasLibraries => Libraries.Count > 0;

    /// <summary>Whether every library ProtonTune can replace is currently linked to its copy.</summary>
    public bool IsManaged => HasLibraries && Libraries.All(library => library.State == DlssLinkState.Managed);

    /// <summary>
    /// Whether any library is linked somewhere ProtonTune did not put it. Worth saying out loud
    /// rather than quietly overwriting: it is someone's existing arrangement.
    /// </summary>
    public bool HasForeignLinks => Libraries.Any(library => library.State == DlssLinkState.Foreign);
}
