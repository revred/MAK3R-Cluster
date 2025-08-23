using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MAK3R.Connectors.OPCUA;

public static class OpcUaConnectorExtensions
{
    public static IServiceCollection AddOpcUaConnector(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new OpcUaConfig();
        configuration.GetSection("Connectors:OpcUa").Bind(config);
        
        services.AddSingleton(config);
        services.AddTransient<OpcUaConnector>();
        
        return services;
    }
}