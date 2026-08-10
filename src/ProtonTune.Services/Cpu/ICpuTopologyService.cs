using ProtonTune.Core.Cpu;

namespace ProtonTune.Services.Cpu;

/// <summary>
/// Describes the processor this machine has.
/// </summary>
public interface ICpuTopologyService
{
    /// <summary>
    /// Reads the topology, or returns an empty one when it cannot be determined.
    /// </summary>
    /// <remarks>
    /// The result does not change while the machine is running, so it is read once and reused.
    /// </remarks>
    CpuTopology Get();
}
