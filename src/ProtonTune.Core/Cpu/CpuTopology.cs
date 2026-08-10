namespace ProtonTune.Core.Cpu;

/// <summary>
/// A set of threads that share a last-level cache — one CCD on AMD, one cache domain elsewhere.
/// </summary>
/// <param name="Threads">The thread numbers, ascending.</param>
/// <param name="CacheBytes">The size of the shared cache.</param>
public sealed record CpuCacheGroup(IReadOnlyList<int> Threads, long CacheBytes)
{
    /// <summary>The threads as a <c>taskset</c> mask.</summary>
    public string Mask => CpuAffinityMask.Format(Threads);
}

/// <summary>
/// What the machine's processor looks like, in the terms that matter for pinning a game to part
/// of it.
/// </summary>
/// <remarks>
/// The useful distinction on a modern desktop is which threads share which cache. On AMD's X3D
/// parts one die carries stacked cache and the other does not, and games generally want the one
/// that does; the split is visible as two cache groups of very different sizes.
/// </remarks>
public sealed record CpuTopology
{
    /// <summary>Every thread on the machine, ascending.</summary>
    public IReadOnlyList<int> AllThreads { get; init; } = [];

    /// <summary>The cache groups, ordered by their lowest thread.</summary>
    public IReadOnlyList<CpuCacheGroup> CacheGroups { get; init; } = [];

    /// <summary>
    /// One thread per physical core, so a game can be kept off sibling threads.
    /// </summary>
    public IReadOnlyList<int> PhysicalCoreThreads { get; init; } = [];

    /// <summary>Whether the machine has more than one thread per core.</summary>
    public bool HasSimultaneousMultithreading => PhysicalCoreThreads.Count < AllThreads.Count;

    /// <summary>
    /// Whether the cache groups differ in size, which is what makes one of them worth singling
    /// out. On a uniform processor every group is equivalent and the distinction is noise.
    /// </summary>
    public bool HasAsymmetricCache =>
        CacheGroups.Count > 1 &&
        CacheGroups.Select(group => group.CacheBytes).Distinct().Count() > 1;

    /// <summary>
    /// The cache group with the most cache, when that is meaningfully more than the others.
    /// </summary>
    public CpuCacheGroup? LargestCacheGroup =>
        HasAsymmetricCache ? CacheGroups.MaxBy(group => group.CacheBytes) : null;

    /// <summary>Whether anything was detected at all.</summary>
    public bool IsEmpty => AllThreads.Count == 0;
}
