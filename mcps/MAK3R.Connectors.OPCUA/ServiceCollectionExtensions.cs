using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MAK3R.Connectors.OPCUA;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add OPC UA connector factory to the service collection
    /// </summary>
    public static IServiceCollection AddOpcUaConnector(this IServiceCollection services, IConfiguration configuration)
    {
        // Register the factory
        services.AddTransient<IConnectorFactory<OpcUaConnector>, OpcUaConnectorFactory>();
        
        // Register the connector type with the registry
        services.AddSingleton(serviceProvider =>
        {
            var registry = serviceProvider.GetRequiredService<IConnectorRegistry>();
            var logger = serviceProvider.GetRequiredService<ILogger<OpcUaConnector>>();
            
            registry.RegisterConnectorType<OpcUaConnector>(
                "opcua",
                "OPC UA Server",
                "Connect to OPC UA servers to collect real-time machine telemetry data"
            );
            
            return registry;
        });
        
        return services;
    }
}