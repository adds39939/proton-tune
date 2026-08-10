using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtonTune.Core.Launch;
using ProtonTune.Services.Cpu;
using ProtonTune.Services.GameConfiguration;
using ProtonTune.Services.Dlss;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Proton;
using ProtonTune.Services.Settings;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.DependencyExtensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtonTuneServices(this IServiceCollection services)
    {
        // Read once at startup. The definitions do not change while the application runs, and a
        // screen that re-read them mid-render would be reading a file the user might be editing.
        services.AddSingleton(provider => new YamlSettingCatalogReader(
                YamlSettingCatalogReader.DefaultDirectory,
                provider.GetRequiredService<ILogger<YamlSettingCatalogReader>>())
            .Read());

        services.AddSingleton<ISteamInstallLocator, SteamInstallLocator>();
        services.AddSingleton<ISteamLibraryService, SteamLibraryService>();
        services.AddSingleton<IGameArtworkService, SteamCdnArtworkService>();
        services.AddSingleton<IProtonToolService, ProtonToolService>();
        services.AddSingleton<ICpuTopologyService, LinuxCpuTopologyService>();
        services.AddSingleton<ProtonTuneStorage>();
        services.AddSingleton<IDlssRuntimeProvider, ShippedDlssRuntimeProvider>();
        services.AddSingleton<IDlssManagementService, DlssManagementService>();
        services.AddSingleton<IGlobalProfileService, GlobalProfileService>();
        services.AddSingleton<ISteamClient, SteamClient>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<ISteamConfigBackupService, SteamConfigBackupService>();
        services.AddSingleton<ISteamLaunchOptionsService, SteamLaunchOptionsService>();

        return services;
    }
}
