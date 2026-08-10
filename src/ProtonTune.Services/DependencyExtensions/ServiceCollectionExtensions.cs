using Microsoft.Extensions.DependencyInjection;
using ProtonTune.Services.Cpu;
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
        services.AddSingleton<ISteamClient, SteamClient>();
        services.AddSingleton<ISteamLaunchOptionsService, SteamLaunchOptionsService>();

        return services;
    }
}
