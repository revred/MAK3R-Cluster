using MAK3R.Connectors.Abstractions;
using MAK3R.Core;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MAK3R.Connectors.NetSuite;

public class NetSuiteConnectorFactory : ConnectorFactoryBase<NetSuiteConnector>
{
    private readonly ILogger<NetSuiteConnector> _logger;

    public NetSuiteConnectorFactory(ILogger<NetSuiteConnector> logger)
    {
        _logger = logger;
    }

    public override async ValueTask<Result<NetSuiteConnector>> CreateAsync(ConnectorConfiguration configuration, CancellationToken ct = default)
    {
        var validationResult = ValidateConfiguration(configuration);
        if (!validationResult.IsSuccess)
        {
            return Result<NetSuiteConnector>.Failure(validationResult.Error!);
        }

        var config = new NetSuiteConfig
        {
            AccountId = GetSettingValue<string>(configuration, "AccountId") ?? "",
            AccessToken = GetSettingValue<string>(configuration, "AccessToken") ?? "",
            ApiVersion = GetSettingValue<string>(configuration, "ApiVersion") ?? "v1",
            IsMockMode = GetSettingValue<bool>(configuration, "IsMockMode")
        };

        var connector = new NetSuiteConnector(_logger, config);
        
        return Result<NetSuiteConnector>.Success(connector);
    }

    public override ValueTask<ConnectorConfigurationSchema> GetConfigurationSchemaAsync()
    {
        var schema = new ConnectorConfigurationSchema(
            "netsuite",
            """
            {
              "type": "object",
              "properties": {
                "AccountId": {
                  "type": "string",
                  "description": "NetSuite Account ID (e.g., 123456_SB1)"
                },
                "AccessToken": {
                  "type": "string",
                  "description": "OAuth 2.0 access token for NetSuite API"
                },
                "ApiVersion": {
                  "type": "string",
                  "description": "NetSuite SuiteQL API version",
                  "default": "v1"
                },
                "IsMockMode": {
                  "type": "boolean",
                  "description": "Use mock data instead of connecting to real NetSuite API",
                  "default": true
                }
              }
            }
            """,
            new Dictionary<string, ConfigurationField>
            {
                {
                    "AccountId", 
                    new ConfigurationField(
                        "AccountId", 
                        "string", 
                        "Account ID", 
                        "NetSuite Account ID (e.g., 123456_SB1)",
                        null,
                        false,
                        false,
                        null,
                        new Dictionary<string, object> { { "pattern", "^[0-9]+(_[A-Z0-9]+)?$" } }
                    )
                },
                {
                    "AccessToken", 
                    new ConfigurationField(
                        "AccessToken", 
                        "string", 
                        "Access Token", 
                        "OAuth 2.0 access token for NetSuite API",
                        null,
                        false,
                        true
                    )
                },
                {
                    "ApiVersion", 
                    new ConfigurationField(
                        "ApiVersion", 
                        "string", 
                        "API Version", 
                        "NetSuite SuiteQL API version",
                        "v1",
                        false,
                        false,
                        new[] { "v1" }
                    )
                },
                {
                    "IsMockMode", 
                    new ConfigurationField(
                        "IsMockMode", 
                        "boolean", 
                        "Mock Mode", 
                        "Use mock data instead of connecting to real NetSuite API",
                        true,
                        false
                    )
                }
            },
            Array.Empty<string>()
        );

        return ValueTask.FromResult(schema);
    }

    public override Result<bool> ValidateConfiguration(ConnectorConfiguration configuration)
    {
        var errors = new List<string>();

        var isMockMode = GetSettingValue<bool>(configuration, "IsMockMode");
        
        // In production mode, validate required fields
        if (!isMockMode)
        {
            var accountId = GetSettingValue<string>(configuration, "AccountId");
            if (string.IsNullOrWhiteSpace(accountId))
            {
                errors.Add("AccountId is required when not in mock mode");
            }

            var accessToken = GetSettingValue<string>(configuration, "AccessToken");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                errors.Add("AccessToken is required when not in mock mode");
            }
        }

        var apiVersion = GetSettingValue<string>(configuration, "ApiVersion");
        if (!string.IsNullOrEmpty(apiVersion) && apiVersion != "v1")
        {
            errors.Add("Only API version 'v1' is currently supported");
        }

        return errors.Count == 0 
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(string.Join("; ", errors));
    }

    private T? GetSettingValue<T>(ConnectorConfiguration configuration, string key)
    {
        if (configuration.Settings.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                catch (JsonException)
                {
                    return default;
                }
            }

            if (value is T directValue)
            {
                return directValue;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        return default;
    }
}