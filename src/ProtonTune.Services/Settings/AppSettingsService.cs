using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProtonTune.Core.Settings;
using ProtonTune.Services.Dlss;

namespace ProtonTune.Services.Settings;

/// <inheritdoc cref="IAppSettingsService" />
public sealed class AppSettingsService(
    ProtonTuneStorage storage,
    ILogger<AppSettingsService> logger) : IAppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _lock = new(1, 1);

    private AppSettings? _settings;

    /// <inheritdoc />
    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_settings is not null)
        {
            return _settings;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_settings is not null)
            {
                return _settings;
            }

            if (!File.Exists(storage.SettingsFile))
            {
                return _settings = new AppSettings();
            }

            var json = await File.ReadAllTextAsync(storage.SettingsFile, cancellationToken).ConfigureAwait(false);

            return _settings = (JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings())
                .Sanitised();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(e, "Could not read settings at {SettingsPath}; using the defaults.", storage.SettingsFile);

            return _settings = new AppSettings();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var sanitised = settings.Sanitised();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(storage.Root);

            await File.WriteAllTextAsync(
                storage.SettingsFile,
                JsonSerializer.Serialize(sanitised, SerializerOptions),
                cancellationToken).ConfigureAwait(false);

            _settings = sanitised;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not write settings to {SettingsPath}.", storage.SettingsFile);

            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}
