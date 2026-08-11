using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Hosting;
using ProtonTune.Services.DependencyExtensions;
using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.DependencyExtensions;

/// <summary>
/// The container is only exercised when the application runs, and a service that cannot be built
/// shows up as a blank screen rather than a failed build. Asking the container to prove itself
/// here turns that into a failing test.
/// </summary>
public class ServiceRegistrationTests
{
    /// <summary>
    /// Catches a circular dependency, which is easy to introduce and invisible to everything else:
    /// every other test constructs these services directly and never goes through the container.
    /// </summary>
    [Fact]
    public void EveryServiceCanBeBuilt()
    {
        var services = WithLogging().AddProtonTuneServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(provider);
    }

    /// <summary>
    /// And that each one actually comes out. Building validates the graph; this also covers a
    /// registration whose own construction throws on the way up.
    /// </summary>
    [Fact]
    public void EveryServiceResolves()
    {
        var services = WithLogging().AddProtonTuneServices();

        using var provider = services.BuildServiceProvider();

        var resolvable = services.Where(service =>
            !service.ServiceType.IsGenericTypeDefinition &&
            (service.ServiceType.IsInterface || service.ImplementationFactory is not null));

        Assert.All(resolvable, service => Assert.NotNull(provider.GetService(service.ServiceType)));
    }

    /// <summary>
    /// The host registers scheme handlers by asking the container for them and nothing else, so a
    /// handler that is written but never registered is a feature that silently does not work. The
    /// artwork one is the reason the extension point exists; if it stops arriving, covers stop
    /// loading and nothing else complains.
    /// </summary>
    [Fact]
    public void OffersItsCustomSchemeHandlersToTheHost()
    {
        using var provider = WithLogging().AddProtonTuneServices().BuildServiceProvider();

        Assert.Contains(
            provider.GetServices<ICustomSchemeHandler>(),
            handler => handler.Scheme == ArtworkScheme.Name);
    }

    /// <summary>
    /// The application registers logging separately, so the container needs it before any of these
    /// can be built. Silenced rather than wired to a console: the question here is only whether
    /// they can be constructed.
    /// </summary>
    private static ServiceCollection WithLogging()
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return services;
    }
}
