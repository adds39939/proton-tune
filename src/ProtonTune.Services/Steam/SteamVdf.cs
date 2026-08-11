using Gameloop.Vdf;
using Gameloop.Vdf.Linq;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Helpers for reading Valve's KeyValues files — the <c>.vdf</c> and <c>.acf</c> documents Steam
/// keeps its local state in.
/// </summary>
internal static class SteamVdf
{
    /// <summary>
    /// Reader settings sized for the largest of these files rather than the smallest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Steam caches JSON blobs inside <c>localconfig.vdf</c> — notification and store preferences
    /// run to tens of thousands of characters in a single token. The library's 4096-character
    /// default is far too small for those, and it overruns its own buffer rather than reporting a
    /// clean error, so the symptom is an <see cref="IndexOutOfRangeException" /> from deep inside
    /// the reader.
    /// </para>
    /// <para>
    /// Those same blobs contain escaped quotes, so escape sequences have to be honoured or the
    /// document ends early and parses as truncated. That is safe here because ProtonTune is Linux
    /// only: the backslash-heavy Windows paths that make escape handling ambiguous never appear.
    /// </para>
    /// </remarks>
    private static readonly VdfSerializerSettings ReaderSettings = new()
    {
        MaximumTokenSize = 1 << 18,
        UsesEscapeSequences = true
    };

    /// <summary>
    /// Reads a KeyValues file and returns its single root object.
    /// </summary>
    /// <returns>
    /// The root object, or <see langword="null"/> when the file is missing, unreadable, or
    /// malformed. Steam rewrites these files in place while it runs, so a torn or half-written
    /// document is an expected outcome rather than an error worth failing the whole scan over.
    /// </returns>
    /// <remarks>
    /// A token longer than <see cref="VdfSerializerSettings.MaximumTokenSize" /> surfaces from the
    /// reader as an <see cref="IndexOutOfRangeException" /> rather than a <c>VdfException</c>,
    /// which is why both are caught.
    /// </remarks>
    public static async Task<VObject?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            return VdfConvert.Deserialize(text, ReaderSettings).Value as VObject;
        }
        catch (Exception e) when (e is IOException
                                      or UnauthorizedAccessException
                                      or VdfException
                                      or IndexOutOfRangeException)
        {
            return null;
        }
    }

    extension(VObject owner)
    {
        /// <summary>
        /// Reads a scalar child by key. Valve is inconsistent about the casing of keys between
        /// client versions, so lookups ignore case.
        /// </summary>
        public string? GetString(string key)
        {
            foreach (var property in owner.Properties())
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    property.Value is VValue value)
                {
                    return value.Value?.ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// Reads a nested object by key, ignoring case for the same reason
        /// <see cref="GetString" /> does.
        /// </summary>
        public VObject? GetObject(string key)
        {
            foreach (var property in owner.Properties())
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    property.Value is VObject nested)
                {
                    return nested;
                }
            }

            return null;
        }

        /// <summary>Reads a scalar child by key and parses it as a 64-bit integer.</summary>
        public long GetInt64(string key) =>
            long.TryParse(owner.GetString(key), out var parsed) ? parsed : 0;

        /// <summary>
        /// Reads a scalar child by key and parses it as a Unix timestamp. Steam writes <c>0</c> for
        /// "never", which maps to <see langword="null"/>.
        /// </summary>
        public DateTimeOffset? GetUnixTime(string key)
        {
            var seconds = owner.GetInt64(key);

            return seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
        }
    }
}
