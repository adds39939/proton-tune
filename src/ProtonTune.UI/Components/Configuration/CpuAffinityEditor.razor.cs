using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Cpu;
using ProtonTune.Services.Cpu;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Chooses which threads a game is pinned to.
/// </summary>
/// <remarks>
/// The presets come from the machine's own topology rather than a fixed list, because the useful
/// mask depends entirely on the processor. On a part where one die carries more cache than the
/// other, pinning a game to that die is the single most valuable thing this section does — and
/// working out which threads those are by hand means reading sysfs.
/// </remarks>
public partial class CpuAffinityEditor : ComponentBase
{
    [Inject]
    private ICpuTopologyService TopologyService { get; set; } = null!;

    /// <summary>The current mask, or <see langword="null"/> when the game is not pinned.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Raised with the new mask, or <see langword="null"/> to remove the pinning.</summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    private CpuTopology Topology => TopologyService.Get();

    private IReadOnlyList<int> SelectedThreads => CpuAffinityMask.Parse(Value);

    /// <summary>
    /// The masks worth offering as a single click, in the order they are most likely wanted.
    /// </summary>
    private IReadOnlyList<AffinityPreset> Presets
    {
        get
        {
            var presets = new List<AffinityPreset>
            {
                new("All threads", null, "No pinning; the scheduler decides.")
            };

            foreach (var group in Topology.CacheGroups)
            {
                var isLargest = Topology.LargestCacheGroup == group;

                presets.Add(new AffinityPreset(
                    isLargest ? "Largest cache group" : "Cache group",
                    group.Mask,
                    $"{group.Threads.Count} threads sharing {group.CacheBytes / 1024 / 1024} MiB" +
                    (isLargest ? " — usually the one games want" : string.Empty)));
            }

            if (Topology.HasSimultaneousMultithreading)
            {
                presets.Add(new AffinityPreset(
                    "Physical cores only",
                    CpuAffinityMask.Format(Topology.PhysicalCoreThreads),
                    "One thread per core, avoiding sibling threads."));
            }

            return presets;
        }
    }

    /// <summary>
    /// Whether a preset matches what is set. Compared as thread sets rather than as text, so a
    /// mask written differently but meaning the same still shows as selected.
    /// </summary>
    private bool IsSelected(AffinityPreset preset)
    {
        if (preset.Mask is null)
        {
            return string.IsNullOrWhiteSpace(Value);
        }

        return !string.IsNullOrWhiteSpace(Value) &&
               CpuAffinityMask.Parse(preset.Mask).SequenceEqual(SelectedThreads);
    }

    private Task Apply(string? mask) => ValueChanged.InvokeAsync(mask);

    private Task ToggleThread(int thread, bool isOn)
    {
        var threads = SelectedThreads.ToHashSet();

        if (isOn)
        {
            threads.Add(thread);
        }
        else
        {
            threads.Remove(thread);
        }

        return Apply(threads.Count == 0 || threads.Count == Topology.AllThreads.Count
            ? null
            : CpuAffinityMask.Format(threads));
    }

    private Task OnMaskChanged(ChangeEventArgs args)
    {
        var mask = args.Value?.ToString();

        return Apply(string.IsNullOrWhiteSpace(mask) ? null : mask.Trim());
    }

    /// <summary>A one-click mask offered alongside the thread picker.</summary>
    private sealed record AffinityPreset(string Label, string? Mask, string? Detail);
}
