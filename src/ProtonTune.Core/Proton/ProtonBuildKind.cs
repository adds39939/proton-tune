namespace ProtonTune.Core.Proton;

/// <summary>
/// Where a Proton build came from. The two kinds are installed and described differently, and
/// they support different settings, so the distinction is worth keeping.
/// </summary>
public enum ProtonBuildKind
{
    /// <summary>
    /// Shipped by Valve and installed through Steam like any other app, so it has an app id and
    /// an entry in a library folder.
    /// </summary>
    Valve,

    /// <summary>
    /// Unpacked into <c>compatibilitytools.d</c> by hand — GE-Proton and its relatives. These
    /// have no app id and describe themselves in a <c>compatibilitytool.vdf</c>.
    /// </summary>
    Custom
}
