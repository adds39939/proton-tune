namespace ProtonTune.Core.Launch;

/// <summary>How a token in a pending change relates to what is already saved.</summary>
public enum LaunchDiffKind
{
    /// <summary>Present in both, and staying.</summary>
    Unchanged,

    /// <summary>Not currently saved; will be written.</summary>
    Added,

    /// <summary>Currently saved; will be gone.</summary>
    Removed
}

/// <summary>One token of a pending change.</summary>
/// <param name="Text">The token as it appears in the launch options.</param>
/// <param name="Kind">Whether it is being added, removed, or left alone.</param>
public sealed record LaunchDiffToken(string Text, LaunchDiffKind Kind);

/// <summary>
/// Compares what is saved against what would be written, token by token.
/// </summary>
/// <remarks>
/// Launch options are a single long line, and telling what changed by reading two of them is
/// genuinely hard. Comparing tokens rather than characters keeps the result meaningful: a setting
/// is added or it is not, rather than a run of letters differing somewhere in the middle.
/// </remarks>
public static class LaunchOptionsDiff
{
    /// <summary>Compares two sets of launch options.</summary>
    public static IReadOnlyList<LaunchDiffToken> Compare(LaunchOptions saved, LaunchOptions pending) =>
        Compare(saved.FormatTokens(), pending.FormatTokens());

    /// <summary>
    /// Compares two token sequences, keeping the longest run they have in common so that
    /// everything else falls out as an addition or a removal.
    /// </summary>
    public static IReadOnlyList<LaunchDiffToken> Compare(
        IReadOnlyList<string> saved,
        IReadOnlyList<string> pending)
    {
        // Longest common subsequence lengths, filled from the end backwards. These sequences are
        // a few dozen tokens at most, so the quadratic table costs nothing.
        var common = new int[saved.Count + 1, pending.Count + 1];

        for (var i = saved.Count - 1; i >= 0; i--)
        {
            for (var j = pending.Count - 1; j >= 0; j--)
            {
                common[i, j] = string.Equals(saved[i], pending[j], StringComparison.Ordinal)
                    ? common[i + 1, j + 1] + 1
                    : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        var tokens = new List<LaunchDiffToken>();
        var savedIndex = 0;
        var pendingIndex = 0;

        while (savedIndex < saved.Count && pendingIndex < pending.Count)
        {
            if (string.Equals(saved[savedIndex], pending[pendingIndex], StringComparison.Ordinal))
            {
                tokens.Add(new LaunchDiffToken(pending[pendingIndex], LaunchDiffKind.Unchanged));
                savedIndex++;
                pendingIndex++;
            }
            else if (common[savedIndex + 1, pendingIndex] >= common[savedIndex, pendingIndex + 1])
            {
                tokens.Add(new LaunchDiffToken(saved[savedIndex], LaunchDiffKind.Removed));
                savedIndex++;
            }
            else
            {
                tokens.Add(new LaunchDiffToken(pending[pendingIndex], LaunchDiffKind.Added));
                pendingIndex++;
            }
        }

        while (savedIndex < saved.Count)
        {
            tokens.Add(new LaunchDiffToken(saved[savedIndex++], LaunchDiffKind.Removed));
        }

        while (pendingIndex < pending.Count)
        {
            tokens.Add(new LaunchDiffToken(pending[pendingIndex++], LaunchDiffKind.Added));
        }

        return tokens;
    }
}
