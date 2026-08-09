using Microsoft.Extensions.DependencyInjection;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.DependencyExtensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtonTuneServices(this IServiceCollection services)
    {
        services.AddSingleton<ISteamInstallLocator, SteamInstallLocator>();
        services.AddSingleton<ISteamLibraryService, SteamLibraryService>();

        return services;
    }
}
