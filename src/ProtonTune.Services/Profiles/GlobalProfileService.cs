using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ProtonTune.Core.Launch;
using ProtonTune.Services.Storage;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Profiles;

/// <inheritdoc cref="IGlobalProfileService" />
public sealed class GlobalProfileService(
    ProtonTuneStorage storage,
    ISteamLaunchOptionsService launchOptions,
    ILogger<GlobalProfileService> logger) : IGlobalProfileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    private StoredProfile? _profile;

    /// <inheritdoc />
    public async Task<LaunchOptions> GetAsync(CancellationToken cancellationToken = default) =>
        LaunchOptions.Parse((await LoadAsync(cancellationToken).ConfigureAwait(false)).LaunchOptions);

    /// <inheritdoc />
    public async Task SaveAsync(LaunchOptions options, CancellationToken cancellationToken = default)
    {
        var profile = await LoadAsync(cancellationToken).ConfigureAwait(false);

        await StoreAsync(profile with { LaunchOptions = options.Format() }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsLinkedAsync(uint appId, CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false)).LinkedApps.Contains(appId);

    /// <inheritdoc />
    public async Task SetLinkedAsync(uint appId, bool linked, CancellationToken cancellationToken = default)
    {
        var profile = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var apps = profile.LinkedApps.ToHashSet();

        if (linked ? !apps.Add(appId) : !apps.Remove(appId))
        {
            return;
        }

        await StoreAsync(profile with { LinkedApps = apps.Order().ToList() }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<uint>> GetLinkedAppsAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false)).LinkedApps;

    /// <inheritdoc />
    public async Task<LaunchOptionsSaveResult> SaveAndApplyAsync(
        LaunchOptions options,
        CancellationToken cancellationToken = default)
    {
        var linked = await GetLinkedAppsAsync(cancellationToken).ConfigureAwait(false);

        if (linked.Count == 0)
        {
            await SaveAsync(options, cancellationToken).ConfigureAwait(false);

            return new LaunchOptionsSaveResult(LaunchOptionsSaveStatus.Saved);
        }

        var byApp = linked.ToDictionary(appId => appId, _ => options.Format());

        var result = await launchOptions.SaveManyAsync(byApp, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await SaveAsync(options, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Applied the global profile to {AppCount} games.", linked.Count);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task ResetAsync(CancellationToken cancellationToken = default) =>
        await StoreAsync(new StoredProfile(), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> ReconcileLinksAsync(CancellationToken cancellationToken = default)
    {
        var stored = await LoadAsync(cancellationToken).ConfigureAwait(false);

        if (stored.LinkedApps.Count == 0)
        {
            return 0;
        }

        var expected = LaunchOptions.Parse(stored.LaunchOptions);
        var stillFollowing = new List<uint>();

        foreach (var appId in stored.LinkedApps)
        {
            var actual = await launchOptions.GetAsync(appId, cancellationToken).ConfigureAwait(false);

            if (string.Equals(actual.Format(), expected.Format(), StringComparison.Ordinal))
            {
                stillFollowing.Add(appId);
            }
            else
            {
                logger.LogInformation(
                    "{AppId} no longer matches the global profile, so it no longer follows it.",
                    appId);
            }
        }

        var dropped = stored.LinkedApps.Count - stillFollowing.Count;

        if (dropped > 0)
        {
            await StoreAsync(stored with { LinkedApps = stillFollowing }, cancellationToken).ConfigureAwait(false);
        }

        return dropped;
    }

    /// <summary>
    /// Reads the stored profile, treating anything unreadable as empty. A profile is a
    /// convenience rather than a record of the user's games — losing it costs a retype, so
    /// failing loudly would be worse than starting fresh.
    /// </summary>
    private async Task<StoredProfile> LoadAsync(CancellationToken cancellationToken)
    {
        if (_profile is not null)
        {
            return _profile;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_profile is not null)
            {
                return _profile;
            }

            if (!File.Exists(storage.ProfileFile))
            {
                return _profile = new StoredProfile();
            }

            var json = await File.ReadAllTextAsync(storage.ProfileFile, cancellationToken).ConfigureAwait(false);

            return _profile = JsonSerializer.Deserialize<StoredProfile>(json, SerializerOptions)
                              ?? new StoredProfile();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(e, "Could not read the global profile at {ProfilePath}.", storage.ProfileFile);

            return _profile = new StoredProfile();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task StoreAsync(StoredProfile profile, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(storage.Root);

            await File.WriteAllTextAsync(
                storage.ProfileFile,
                JsonSerializer.Serialize(profile, SerializerOptions),
                cancellationToken).ConfigureAwait(false);

            _profile = profile;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not write the global profile to {ProfilePath}.", storage.ProfileFile);

            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>What is kept on disk.</summary>
    private sealed record StoredProfile
    {
        /// <summary>The global launch options, as the string Steam would store.</summary>
        public string LaunchOptions { get; init; } = string.Empty;

        /// <summary>The app ids currently following the profile.</summary>
        public IReadOnlyList<uint> LinkedApps { get; init; } = [];
    }
}
