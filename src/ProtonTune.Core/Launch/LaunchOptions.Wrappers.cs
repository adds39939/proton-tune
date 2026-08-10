namespace ProtonTune.Core.Launch;

/// <summary>
/// Editing the chain of commands a game is launched through.
/// </summary>
/// <remarks>
/// The chain is a flat list of tokens because each command consumes its own arguments and there
/// is no way to know how many without knowing the command. So rather than parsing it into a tree,
/// these operate on the commands ProtonTune actually understands and step over everything else
/// untouched.
/// </remarks>
public sealed partial record LaunchOptions
{
    /// <summary>The command that pins a process to a set of threads.</summary>
    public const string TasksetCommand = "taskset";

    /// <summary>The flags <c>taskset</c> accepts for a thread list.</summary>
    private static readonly string[] TasksetListFlags = ["-c", "--cpu-list"];

    /// <summary>
    /// The CPU affinity mask the game is launched with, or <see langword="null"/> when it is not
    /// pinned.
    /// </summary>
    public string? CpuAffinity
    {
        get
        {
            var index = IndexOfCommand(TasksetCommand);

            return index >= 0 && index + 2 < Wrapper.Count && TasksetListFlags.Contains(Wrapper[index + 1])
                ? Wrapper[index + 2]
                : null;
        }
    }

    /// <summary>
    /// Returns a copy pinned to a set of threads, or unpinned when the mask is
    /// <see langword="null"/> or empty.
    /// </summary>
    /// <remarks>
    /// A new <c>taskset</c> goes last in the chain, immediately before the game, so it applies to
    /// the game rather than to a wrapper that would go on to launch it differently.
    /// </remarks>
    public LaunchOptions WithCpuAffinity(string? mask)
    {
        var wrapper = Wrapper.ToList();
        var index = IndexOfCommand(TasksetCommand, wrapper);
        var hasList = index >= 0 && index + 2 < wrapper.Count && TasksetListFlags.Contains(wrapper[index + 1]);

        if (string.IsNullOrWhiteSpace(mask))
        {
            if (index >= 0)
            {
                wrapper.RemoveRange(index, hasList ? 3 : 1);
            }

            return this with { Wrapper = wrapper };
        }

        if (hasList)
        {
            wrapper[index + 2] = mask.Trim();
        }
        else
        {
            // Replace a bare taskset that has no list rather than leaving it stranded.
            if (index >= 0)
            {
                wrapper.RemoveAt(index);
            }

            wrapper.AddRange([TasksetCommand, "-c", mask.Trim()]);
        }

        return this with { Wrapper = wrapper };
    }

    /// <summary>
    /// Whether the game is launched through a command. Matched on the file name, so an absolute
    /// path to the same tool counts.
    /// </summary>
    public bool HasWrapperCommand(string command) => IndexOfCommand(command) >= 0;

    /// <summary>
    /// Returns a copy with a bare command added to or removed from the chain.
    /// </summary>
    /// <remarks>
    /// Added commands go first, so they wrap everything after them. Only the command itself is
    /// removed — anything following it belongs to whatever comes next in the chain.
    /// </remarks>
    public LaunchOptions WithWrapperCommand(string command, bool present)
    {
        var index = IndexOfCommand(command);

        if (present == index >= 0)
        {
            return this;
        }

        var wrapper = Wrapper.ToList();

        if (present)
        {
            wrapper.Insert(0, command);
        }
        else
        {
            wrapper.RemoveAt(index);
        }

        return this with { Wrapper = wrapper };
    }

    /// <summary>Finds a command in the chain by file name.</summary>
    private int IndexOfCommand(string command) => IndexOfCommand(command, Wrapper);

    private static int IndexOfCommand(string command, IReadOnlyList<string> wrapper)
    {
        for (var i = 0; i < wrapper.Count; i++)
        {
            if (string.Equals(Path.GetFileName(wrapper[i]), command, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
