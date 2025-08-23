using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MAK3R.Connectors.Shopify;

public static class ShopifyConnectorExtensions
{
    public static IServiceCollection AddShopifyConnector(this IServiceCollection services, IConfiguration configuration)
    {
        // Register the factory for dependency injection
        services.AddTransient<IConnectorFactory<ShopifyConnector>, ShopifyConnectorFactory>();
        
        // Register the connector type in the registry
        services.AddSingleton<IConnectorTypeRegistration>(provider => 
        {
            var registry = provider.GetRequiredService<IConnectorRegistry>();
            registry.RegisterConnectorType<ShopifyConnector>("shopify", "Shopify", "E-commerce platform connector for syncing products, orders, and customer data");
            return new ConnectorTypeRegistration("shopify");
        });
        
        return services;
    }
}

// Helper class for registration tracking
internal record ConnectorTypeRegistration(string TypeId) : IConnectorTypeRegistration;