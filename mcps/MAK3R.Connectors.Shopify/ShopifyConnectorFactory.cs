using MAK3R.Connectors.Abstractions;
using MAK3R.Core;
using Microsoft.Extensions.Logging;

namespace MAK3R.Connectors.Shopify;

public class ShopifyConnectorFactory : ConnectorFactoryBase<ShopifyConnector>
{
    private readonly ILogger<ShopifyConnector> _logger;

    public ShopifyConnectorFactory(ILogger<ShopifyConnector> logger)
    {
        _logger = logger;
    }

    public override async ValueTask<Result<ShopifyConnector>> CreateAsync(ConnectorConfiguration configuration, CancellationToken ct = default)
    {
        try
        {
            // Validate configuration first
            var validationResult = ValidateConfiguration(configuration);
            if (!validationResult.IsSuccess)
            {
                return Result<ShopifyConnector>.Failure(validationResult.Error!);
            }

            // Extract Shopify-specific configuration
            var config = new ShopifyConfig
            {
                ShopUrl = GetSettingValue<string>(configuration, "ShopUrl") ?? "",
                AccessToken = GetSettingValue<string>(configuration, "AccessToken") ?? "",
                ApiVersion = GetSettingValue<string>(configuration, "ApiVersion") ?? "2023-10"
            };

            // Validate Shopify-specific settings
            if (string.IsNullOrWhiteSpace(config.ShopUrl))
                return Result<ShopifyConnector>.Failure("ShopUrl is required");
                
            if (string.IsNullOrWhiteSpace(config.AccessToken))
                return Result<ShopifyConnector>.Failure("AccessToken is required");

            if (!config.ShopUrl.StartsWith("https://") || !config.ShopUrl.Contains(".myshopify.com"))
                return Result<ShopifyConnector>.Failure("ShopUrl must be a valid Shopify store URL");

            var connector = new ShopifyConnector(_logger, config);
            
            _logger.LogInformation("Created Shopify connector for shop: {ShopUrl}", config.ShopUrl);
            
            return Result<ShopifyConnector>.Success(connector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Shopify connector");
            return Result<ShopifyConnector>.Failure($"Creation failed: {ex.Message}");
        }
    }

    public override ValueTask<ConnectorConfigurationSchema> GetConfigurationSchemaAsync()
    {
        var schema = new ConnectorConfigurationSchema(
            "shopify",
            """
            {
              "type": "object",
              "properties": {
                "ShopUrl": {
                  "type": "string",
                  "pattern": "^https://[a-zA-Z0-9-]+\\.myshopify\\.com$",
                  "description": "Your Shopify store URL (e.g., https://mystore.myshopify.com)"
                },
                "AccessToken": {
                  "type": "string",
                  "minLength": 1,
                  "description": "Private app access token"
                },
                "ApiVersion": {
                  "type": "string",
                  "description": "Shopify API version",
                  "default": "2023-10"
                }
              },
              "required": ["ShopUrl", "AccessToken"]
            }
            """,
            new Dictionary<string, ConfigurationField>
            {
                {
                    "ShopUrl", 
                    new ConfigurationField(
                        "ShopUrl", 
                        "string", 
                        "Shop URL", 
                        "Your Shopify store URL (e.g., https://mystore.myshopify.com)",
                        null,
                        true,
                        false,
                        null,
                        new Dictionary<string, object> { { "pattern", "^https://[a-zA-Z0-9-]+\\.myshopify\\.com$" } }
                    )
                },
                {
                    "AccessToken", 
                    new ConfigurationField(
                        "AccessToken", 
                        "string", 
                        "Access Token", 
                        "Private app access token from Shopify Admin",
                        null,
                        true,
                        true
                    )
                },
                {
                    "ApiVersion", 
                    new ConfigurationField(
                        "ApiVersion", 
                        "string", 
                        "API Version", 
                        "Shopify API version to use",
                        "2023-10",
                        false,
                        false,
                        new[] { "2023-10", "2023-07", "2023-04" }
                    )
                }
            },
            new[] { "ShopUrl", "AccessToken" }
        );

        return ValueTask.FromResult(schema);
    }

    public override Result<bool> ValidateConfiguration(ConnectorConfiguration configuration)
    {
        var baseResult = base.ValidateConfiguration(configuration);
        if (!baseResult.IsSuccess)
            return baseResult;

        if (configuration.Type != "shopify")
            return Result<bool>.Failure("Invalid connector type for Shopify factory");

        return Result<bool>.Success(true);
    }

    private T? GetSettingValue<T>(ConnectorConfiguration configuration, string key)
    {
        if (configuration.Settings.TryGetValue(key, out var value))
        {
            try
            {
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        return default;
    }
}