using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MAK3R.Connectors.NetSuite;

public static class NetSuiteConnectorExtensions
{
    public static IServiceCollection AddNetSuiteConnector(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new NetSuiteConfig();
        configuration.GetSection("Connectors:NetSuite").Bind(config);
        
        services.AddSingleton(config);
        services.AddTransient<NetSuiteConnector>();
        
        return services;
    }
}