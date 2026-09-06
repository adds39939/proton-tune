namespace ProtonTune.DebugServer;

/// <summary>
/// Wraps an already registered service in another that takes it as its own dependency.
/// </summary>
/// <remarks>
/// How this host adjusts one thing the application does without the application knowing there is
/// a second host: the registration it made stands, and what asks for the service gets the wrapper.
/// </remarks>
internal static class ServiceDecoration
{
    public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var registered = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(TService))
                         ?? throw new InvalidOperationException($"{typeof(TService).Name} is not registered.");

        services.Remove(registered);

        services.Add(new ServiceDescriptor(
            typeof(TService),
            provider => ActivatorUtilities.CreateInstance<TDecorator>(
                provider,
                (TService)Instantiate(provider, registered)),
            registered.Lifetime));

        return services;
    }

    /// <summary>Builds whatever the original registration described.</summary>
    private static object Instantiate(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is { } instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return factory(provider);
        }

        return ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }
}
