namespace ProtonTune.Core.Launch;

/// <summary>
/// Editing the flags of a wrapper command ProtonTune has a definition for.
/// </summary>
/// <remarks>
/// <para>
/// A command's arguments run from the word itself to its terminator — Gamescope's <c>--</c> — and
/// only tokens naming a declared flag are ever touched. A flag ProtonTune does not list, written
/// between the two by hand, survives every edit made here untouched.
/// </para>
/// <para>
/// Where the terminator has been left out the arguments end at the first token that is not a
/// recognised flag, so removing a command cannot swallow the one that follows it. That is the
/// reading Gamescope itself takes: without <c>--</c> it stops at the first thing that is not an
/// option and treats the rest as the command to run.
/// </para>
/// </remarks>
public sealed partial record LaunchOptions
{
    /// <summary>Where a flag sits in the chain, and how it was written.</summary>
    /// <param name="Index">The token naming the flag.</param>
    /// <param name="End">Where the command's arguments stop, which bounds the value.</param>
    /// <param name="Inline">
    /// The value written into the flag's own token as <c>--flag=value</c>, or
    /// <see langword="null"/> where the value is a token of its own.
    /// </param>
    private readonly record struct FlagLocation(int Index, int End, string? Inline);

    /// <summary>Whether the game is launched through a command.</summary>
    public bool HasCommand(CommandDefinition command) => IndexOfCommand(command.Command) >= 0;

    /// <summary>
    /// Returns a copy with the command added to or removed from the chain.
    /// </summary>
    /// <remarks>
    /// Added commands go first, so they wrap everything after them, and bring their terminator
    /// with them. Removing one takes its flags and its terminator too: they name nothing on their
    /// own, and leaving them behind would hand the next command in the chain arguments meant for
    /// something else.
    /// </remarks>
    public LaunchOptions WithCommand(CommandDefinition command, bool present)
    {
        var index = IndexOfCommand(command.Command);

        if (present == index >= 0)
        {
            return this;
        }

        var wasEmpty = IsEmpty;
        var wrapper = Wrapper.ToList();

        if (present)
        {
            Insert(command, wrapper);
        }
        else
        {
            wrapper.RemoveRange(index, EndOfCommand(command, index, wrapper) - index);
        }

        return this with { Wrapper = wrapper, HasCommandPlaceholder = HasCommandPlaceholder || wasEmpty };
    }

    /// <summary>Whether a flag is written on the command.</summary>
    public bool HasFlag(CommandDefinition command, CommandFlagDefinition flag) =>
        Locate(command, flag, Wrapper) is not null;

    /// <summary>
    /// The value a flag is set to, or <see langword="null"/> when it is not written, or is written
    /// with the value missing.
    /// </summary>
    public string? FindFlag(CommandDefinition command, CommandFlagDefinition flag)
    {
        if (Locate(command, flag, Wrapper) is not { } found)
        {
            return null;
        }

        if (found.Inline is { } inline)
        {
            return inline;
        }

        return flag.TakesValue && found.Index + 1 < found.End ? Wrapper[found.Index + 1] : null;
    }

    /// <summary>
    /// Returns a copy with a switch written on the command, or taken off it.
    /// </summary>
    /// <remarks>
    /// Switching one on adds the command where it is not already there. A flag outside the command
    /// it belongs to is not a setting waiting to take effect, it is a word handed to the game.
    /// </remarks>
    public LaunchOptions WithSwitch(CommandDefinition command, CommandFlagDefinition flag, bool present)
    {
        if (!present)
        {
            return Without(command, flag);
        }

        if (HasFlag(command, flag))
        {
            return this;
        }

        var (options, wrapper, index) = Ensure(command);

        wrapper.Insert(EndOfArguments(command, index, wrapper), flag.Flag);

        return options with { Wrapper = wrapper };
    }

    /// <summary>
    /// Returns a copy with a flag's value set, or the flag removed when the value is empty.
    /// </summary>
    /// <remarks>
    /// A flag already written keeps the spelling it was written with, so setting a width does not
    /// rewrite someone's <c>--output-width</c> as <c>-W</c> under them.
    /// </remarks>
    public LaunchOptions WithFlag(CommandDefinition command, CommandFlagDefinition flag, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Without(command, flag);
        }

        var trimmed = value.Trim();
        var (options, wrapper, index) = Ensure(command);

        if (Locate(command, flag, wrapper) is not { } found)
        {
            wrapper.InsertRange(EndOfArguments(command, index, wrapper), [flag.Flag, trimmed]);
        }
        else if (found.Inline is not null)
        {
            wrapper[found.Index] = $"{wrapper[found.Index].Split('=')[0]}={trimmed}";
        }
        else if (found.Index + 1 < found.End)
        {
            wrapper[found.Index + 1] = trimmed;
        }
        else
        {
            wrapper.Insert(found.Index + 1, trimmed);
        }

        return options with { Wrapper = wrapper };
    }

    /// <summary>Removes a flag, and the value token belonging to it.</summary>
    private LaunchOptions Without(CommandDefinition command, CommandFlagDefinition flag)
    {
        if (Locate(command, flag, Wrapper) is not { } found)
        {
            return this;
        }

        var wrapper = Wrapper.ToList();
        var hasValueToken = found.Inline is null && flag.TakesValue && found.Index + 1 < found.End;

        wrapper.RemoveRange(found.Index, hasValueToken ? 2 : 1);

        return this with { Wrapper = wrapper };
    }

    /// <summary>
    /// The chain with the command in it, ready to be edited, adding it where it is absent.
    /// </summary>
    private (LaunchOptions Options, List<string> Wrapper, int Index) Ensure(CommandDefinition command)
    {
        var wrapper = Wrapper.ToList();
        var index = IndexOfCommand(command.Command, wrapper);

        if (index >= 0)
        {
            return (this, wrapper, index);
        }

        Insert(command, wrapper);

        return (this with { HasCommandPlaceholder = HasCommandPlaceholder || IsEmpty }, wrapper, 0);
    }

    /// <summary>Puts a command at the front of the chain, with its terminator behind it.</summary>
    private static void Insert(CommandDefinition command, List<string> wrapper) =>
        wrapper.InsertRange(
            0,
            command.Terminator is { } terminator ? [command.Command, terminator] : [command.Command]);

    /// <summary>Finds a flag within the command's arguments.</summary>
    private static FlagLocation? Locate(
        CommandDefinition command,
        CommandFlagDefinition flag,
        IReadOnlyList<string> wrapper)
    {
        var index = IndexOfCommand(command.Command, wrapper);

        if (index < 0)
        {
            return null;
        }

        var end = EndOfArguments(command, index, wrapper);

        for (var i = index + 1; i < end; i++)
        {
            if (FlagAt(command, wrapper[i]) is { } found &&
                string.Equals(found.Flag.Flag, flag.Flag, StringComparison.Ordinal))
            {
                return new FlagLocation(i, end, found.Inline);
            }
        }

        return null;
    }

    /// <summary>
    /// The flag a token names, and the value packed into the token itself where the
    /// <c>--flag=value</c> form was used.
    /// </summary>
    private static (CommandFlagDefinition Flag, string? Inline)? FlagAt(CommandDefinition command, string token)
    {
        foreach (var flag in command.AllFlags)
        {
            if (flag.Matches(token))
            {
                return (flag, null);
            }

            if (!flag.TakesValue)
            {
                continue;
            }

            var separator = token.IndexOf('=');

            if (separator > 0 && flag.Matches(token[..separator]))
            {
                return (flag, token[(separator + 1)..]);
            }
        }

        return null;
    }

    /// <summary>Where the command's own arguments stop, exclusive of the terminator.</summary>
    private static int EndOfArguments(CommandDefinition command, int index, IReadOnlyList<string> wrapper)
    {
        var start = index + 1;

        if (command.Terminator is { } terminator)
        {
            for (var i = start; i < wrapper.Count; i++)
            {
                if (string.Equals(wrapper[i], terminator, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        var end = start;

        while (end < wrapper.Count && FlagAt(command, wrapper[end]) is { } found)
        {
            end += found.Flag.TakesValue && found.Inline is null && end + 1 < wrapper.Count ? 2 : 1;
        }

        return end;
    }

    /// <summary>Where everything belonging to the command stops, terminator included.</summary>
    private static int EndOfCommand(CommandDefinition command, int index, IReadOnlyList<string> wrapper)
    {
        var end = EndOfArguments(command, index, wrapper);

        return command.Terminator is { } terminator &&
               end < wrapper.Count &&
               string.Equals(wrapper[end], terminator, StringComparison.Ordinal)
            ? end + 1
            : end;
    }
}
