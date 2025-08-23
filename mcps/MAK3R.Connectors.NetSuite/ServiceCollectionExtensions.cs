using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MAK3R.Connectors.NetSuite;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add NetSuite connector factory to the service collection
    /// </summary>
    public static IServiceCollection AddNetSuiteConnector(this IServiceCollection services, IConfiguration configuration)
    {
        // Register the factory
        services.AddTransient<IConnectorFactory<NetSuiteConnector>, NetSuiteConnectorFactory>();
        
        // Register the connector type with the registry
        services.AddSingleton(serviceProvider =>
        {
            var registry = serviceProvider.GetRequiredService<IConnectorRegistry>();
            var logger = serviceProvider.GetRequiredService<ILogger<NetSuiteConnector>>();
            
            registry.RegisterConnectorType<NetSuiteConnector>(
                "netsuite",
                "NetSuite ERP",
                "Connect to NetSuite ERP system to sync customers, items, and transactions"
            );
            
            return registry;
        });
        
        return services;
    }
}