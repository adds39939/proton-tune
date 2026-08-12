using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtonTune.Core.Hosting;
using ProtonTune.Services.Cpu;
using ProtonTune.Services.GameConfiguration;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Proton;
using ProtonTune.Services.Settings;
using ProtonTune.Services.Steam;
using ProtonTune.Services.Storage;

namespace ProtonTune.Services.DependencyExtensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtonTuneServices(this IServiceCollection services)
    {
        services.AddSingleton(provider => new YamlSettingCatalogReader(
                YamlSettingCatalogReader.DefaultDirectory,
                provider.GetRequiredService<ILogger<YamlSettingCatalogReader>>())
            .Read());

        services.AddSingleton<ISteamInstallLocator, SteamInstallLocator>();
        services.AddSingleton<ISteamLibraryService, SteamLibraryService>();
        services.AddSingleton<SteamLibraryCacheArtworkService>();
        services.AddSingleton<IGameArtworkService>(provider => new FallbackArtworkService(
        [
            provider.GetRequiredService<SteamLibraryCacheArtworkService>(),
            new SteamCdnArtworkService()
        ]));
        services.AddSingleton<ICustomSchemeHandler>(provider =>
            provider.GetRequiredService<SteamLibraryCacheArtworkService>());
        services.AddSingleton<IProtonToolService, ProtonToolService>();
        services.AddSingleton<ICpuTopologyService, LinuxCpuTopologyService>();
        services.AddSingleton<ProtonTuneStorage>();
        services.AddSingleton<IGlobalProfileService, GlobalProfileService>();
        services.AddSingleton<ISteamClient, SteamClient>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<ISteamConfigBackupService, SteamConfigBackupService>();
        services.AddSingleton<ISteamLaunchOptionsService, SteamLaunchOptionsService>();

        return services;
    }
}
