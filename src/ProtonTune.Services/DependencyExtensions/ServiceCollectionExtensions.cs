using Microsoft.Extensions.DependencyInjection;
using ProtonTune.Services.Cpu;
using ProtonTune.Services.Dlss;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.DependencyExtensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtonTuneServices(this IServiceCollection services)
    {
        services.AddSingleton<ISteamInstallLocator, SteamInstallLocator>();
        services.AddSingleton<ISteamLibraryService, SteamLibraryService>();
        services.AddSingleton<IGameArtworkService, SteamCdnArtworkService>();
        services.AddSingleton<ICpuTopologyService, LinuxCpuTopologyService>();
        services.AddSingleton<ProtonTuneStorage>();
        services.AddSingleton<IDlssRuntimeProvider, ShippedDlssRuntimeProvider>();
        services.AddSingleton<IDlssManagementService, DlssManagementService>();
        services.AddSingleton<IGlobalProfileService, GlobalProfileService>();
        services.AddSingleton<ISteamClient, SteamClient>();
        services.AddSingleton<ISteamLaunchOptionsService, SteamLaunchOptionsService>();

        return services;
    }
}
