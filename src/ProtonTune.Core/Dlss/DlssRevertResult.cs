namespace ProtonTune.Core.Dlss;

/// <summary>
/// What putting a game's own DLSS libraries back actually achieved.
/// </summary>
/// <remarks>
/// Reverting can succeed partially. A link can outlive the backup that would undo it — the
/// original is moved aside on the first swap and moved back on the first revert, so anything that
/// re-creates the link afterwards leaves one with nothing behind it. Reporting that is the point
/// of this type: the alternative is telling someone their game is back to normal when a file
/// inside it still points at ProtonTune.
/// </remarks>
/// <param name="Restored">Libraries put back exactly as the game shipped them.</param>
/// <param name="Replaced">
/// Libraries whose original was gone, left as a real copy of the version that had been linked in.
/// The game works and nothing points at ProtonTune any more, but the file is not the one it
/// shipped with — only Steam can supply that, by verifying the install.
/// </param>
public sealed record DlssRevertResult(
    IReadOnlyList<string> Restored,
    IReadOnlyList<string> Replaced)
{
    /// <summary>A revert that had nothing to do.</summary>
    public static DlssRevertResult Nothing { get; } = new([], []);

    /// <summary>Whether every library came back as the game shipped it.</summary>
    public bool IsComplete => Replaced.Count == 0;
}
