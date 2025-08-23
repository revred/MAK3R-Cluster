using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MAK3R.Connectors;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add the MAK3R Connector Hub to the service collection
    /// </summary>
    public static IServiceCollection AddConnectorHub(this IServiceCollection services)
    {
        // Register core connector services
        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.AddSingleton<IConnectorHub, ConnectorHub>();
        services.AddHostedService<ConnectorHostedService>();
        
        return services;
    }
    
    /// <summary>
    /// Add the MAK3R Connector Hub without background services (useful for testing)
    /// </summary>
    public static IServiceCollection AddConnectorHubCore(this IServiceCollection services)
    {
        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.AddSingleton<IConnectorHub, ConnectorHub>();
        
        return services;
    }
}