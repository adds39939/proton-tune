namespace ProtonTune.Core.Proton;

/// <summary>
/// The environment variables one Proton build actually reads.
/// </summary>
/// <remarks>
/// Builds differ enormously — GE-Proton11-3 reads thirty-one variables Proton Experimental does
/// not — and setting one a build ignores fails silently. Discovered by reading the build's own
/// <c>proton</c> script rather than by version number, since the list changes with every release.
/// </remarks>
public sealed record ProtonCapabilities
{
    /// <summary>
    /// The prefix of the variables this can speak to. The <c>proton</c> script is Python source,
    /// so every variable it reads appears in it literally and the answer is exact.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow. The renderer variables — <c>DXVK_*</c>, <c>VKD3D_*</c> — live in
    /// shipped DLLs with names assembled at runtime, so probing them would report working settings
    /// as unsupported. Those answer "cannot say" instead.
    /// </remarks>
    private const string ReadablePrefix = "PROTON_";

    /// <summary>
    /// Used where a build could not be read at all, and for the global profile, which is not
    /// attached to any build. Nothing is judged.
    /// </summary>
    public static ProtonCapabilities Unknown { get; } = new()
    {
        Variables = new HashSet<string>(StringComparer.Ordinal),
        IsKnown = false
    };

    /// <summary>Every variable found in the build's launch script.</summary>
    public required IReadOnlySet<string> Variables { get; init; }

    /// <summary>Whether the build was read successfully. Nothing is judged when it was not.</summary>
    public required bool IsKnown { get; init; }

    /// <summary>
    /// Whether the build reads a variable.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> or <see langword="false"/> where there is a reliable answer, and
    /// <see langword="null"/> where there is not — an unread build, or a variable belonging to a
    /// component this cannot see into. A caller should treat <see langword="null"/> as "carry on
    /// as normal", never as "unsupported".
    /// </returns>
    public bool? Reads(string variable) =>
        IsKnown && variable.StartsWith(ReadablePrefix, StringComparison.Ordinal)
            ? Variables.Contains(variable)
            : null;

    /// <summary>Whether the build is known not to read a variable.</summary>
    public bool Ignores(string variable) => Reads(variable) is false;
}
