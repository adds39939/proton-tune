using ProtonTune.Core.Dlss;

namespace ProtonTune.Services.Dlss;

/// <summary>
/// The DLSS library versions shipped with ProtonTune.
/// </summary>
public interface IDlssRuntimeProvider
{
    /// <summary>Every shipped version, newest first.</summary>
    IReadOnlyList<DlssRuntime> GetAll();

    /// <summary>The newest shipped version, or null when none were found.</summary>
    DlssRuntime? Latest { get; }
}
