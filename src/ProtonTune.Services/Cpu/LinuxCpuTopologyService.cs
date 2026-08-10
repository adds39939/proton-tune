using Microsoft.Extensions.Logging;
using ProtonTune.Core.Cpu;

namespace ProtonTune.Services.Cpu;

/// <inheritdoc cref="ICpuTopologyService" />
/// <remarks>
/// Everything comes from <c>/sys/devices/system/cpu</c>, which the kernel exports directly:
/// <c>cache/index3</c> gives the last-level cache each thread shares and how big it is, and
/// <c>topology/thread_siblings_list</c> gives which threads belong to the same physical core.
/// </remarks>
public sealed class LinuxCpuTopologyService(ILogger<LinuxCpuTopologyService> logger) : ICpuTopologyService
{
    private const string CpuRoot = "/sys/devices/system/cpu";

    /// <summary>
    /// The last-level cache index. Index 3 is L3 on every processor ProtonTune targets; where
    /// there is no L3 the cache grouping simply comes back empty and only the thread list is used.
    /// </summary>
    private const string LastLevelCache = "cache/index3";

    private CpuTopology? _topology;

    /// <inheritdoc />
    public CpuTopology Get() => _topology ??= Read();

    private CpuTopology Read()
    {
        try
        {
            var threads = Directory
                .EnumerateDirectories(CpuRoot, "cpu*")
                .Select(directory => Path.GetFileName(directory)[3..])
                .Where(name => name.All(char.IsAsciiDigit) && name.Length > 0)
                .Select(int.Parse)
                .Order()
                .ToList();

            if (threads.Count == 0)
            {
                logger.LogWarning("No processors were found under {CpuRoot}.", CpuRoot);

                return new CpuTopology();
            }

            var topology = new CpuTopology
            {
                AllThreads = threads,
                CacheGroups = ReadCacheGroups(threads),
                PhysicalCoreThreads = ReadPhysicalCoreThreads(threads)
            };

            logger.LogInformation(
                "Detected {ThreadCount} threads in {GroupCount} cache groups; asymmetric cache: {Asymmetric}.",
                topology.AllThreads.Count,
                topology.CacheGroups.Count,
                topology.HasAsymmetricCache);

            return topology;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            logger.LogWarning(e, "Could not read the processor topology.");

            return new CpuTopology();
        }
    }

    /// <summary>
    /// Groups threads by the last-level cache they share, keyed on the sharing list itself so
    /// each distinct cache appears once.
    /// </summary>
    private IReadOnlyList<CpuCacheGroup> ReadCacheGroups(IEnumerable<int> threads)
    {
        var groups = new Dictionary<string, CpuCacheGroup>(StringComparer.Ordinal);

        foreach (var thread in threads)
        {
            var shared = ReadValue($"{CpuRoot}/cpu{thread}/{LastLevelCache}/shared_cpu_list");

            if (shared is null || groups.ContainsKey(shared))
            {
                continue;
            }

            groups[shared] = new CpuCacheGroup(
                CpuAffinityMask.Parse(shared),
                ParseCacheSize(ReadValue($"{CpuRoot}/cpu{thread}/{LastLevelCache}/size")));
        }

        return groups.Values.OrderBy(group => group.Threads.FirstOrDefault()).ToList();
    }

    /// <summary>
    /// Takes the lowest thread of each physical core, which is the one to keep when avoiding
    /// sibling threads.
    /// </summary>
    private IReadOnlyList<int> ReadPhysicalCoreThreads(IEnumerable<int> threads)
    {
        var cores = new HashSet<string>(StringComparer.Ordinal);
        var primary = new List<int>();

        foreach (var thread in threads)
        {
            var siblings = ReadValue($"{CpuRoot}/cpu{thread}/topology/thread_siblings_list");

            // Without sibling information every thread has to count as its own core.
            if (siblings is null || cores.Add(siblings))
            {
                primary.Add(thread);
            }
        }

        return primary;
    }

    /// <summary>Reads a sysfs value, or null when it is not readable.</summary>
    private static string? ReadValue(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reads a cache size, which sysfs writes with a unit suffix such as <c>98304K</c>.</summary>
    private static long ParseCacheSize(string? size)
    {
        if (size is null || size.Length == 0)
        {
            return 0;
        }

        var multiplier = char.ToUpperInvariant(size[^1]) switch
        {
            'K' => 1024L,
            'M' => 1024L * 1024,
            'G' => 1024L * 1024 * 1024,
            _ => 1L
        };

        var digits = multiplier == 1 ? size : size[..^1];

        return long.TryParse(digits, out var value) ? value * multiplier : 0;
    }
}
