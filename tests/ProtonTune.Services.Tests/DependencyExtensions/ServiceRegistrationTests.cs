using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Services.DependencyExtensions;

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

        // ValidateOnBuild walks every registration and reports what it cannot construct, rather
        // than waiting for the first screen that asks for one.
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

        // Open generics such as ILogger<> are resolved through a closed type rather than asked for
        // directly, so they are not something to ask the container for here.
        var resolvable = services.Where(service =>
            !service.ServiceType.IsGenericTypeDefinition &&
            (service.ServiceType.IsInterface || service.ImplementationFactory is not null));

        Assert.All(resolvable, service => Assert.NotNull(provider.GetService(service.ServiceType)));
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
