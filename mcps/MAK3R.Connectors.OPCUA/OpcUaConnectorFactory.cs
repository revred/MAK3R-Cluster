using MAK3R.Connectors.Abstractions;
using MAK3R.Core;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MAK3R.Connectors.OPCUA;

public class OpcUaConnectorFactory : ConnectorFactoryBase<OpcUaConnector>
{
    private readonly ILogger<OpcUaConnector> _logger;

    public OpcUaConnectorFactory(ILogger<OpcUaConnector> logger)
    {
        _logger = logger;
    }

    public override async ValueTask<Result<OpcUaConnector>> CreateAsync(ConnectorConfiguration configuration, CancellationToken ct = default)
    {
        var validationResult = ValidateConfiguration(configuration);
        if (!validationResult.IsSuccess)
        {
            return Result<OpcUaConnector>.Failure(validationResult.Error!);
        }

        var config = new OpcUaConfig
        {
            EndpointUrl = GetSettingValue<string>(configuration, "EndpointUrl") ?? "opc.tcp://localhost:4840",
            IsSimulator = GetSettingValue<bool>(configuration, "IsSimulator"),
            SecurityPolicy = GetSettingValue<string>(configuration, "SecurityPolicy") ?? "None",
            NodeIds = GetSettingValue<List<string>>(configuration, "NodeIds") ?? new List<string>
            {
                "ns=2;i=1001", "ns=2;i=1002", "ns=2;i=1003", "ns=2;i=1004",
                "ns=2;i=1005", "ns=2;i=1006", "ns=2;i=1007", "ns=2;i=1008"
            }
        };

        var connector = new OpcUaConnector(_logger, config);
        
        return Result<OpcUaConnector>.Success(connector);
    }

    public override ValueTask<ConnectorConfigurationSchema> GetConfigurationSchemaAsync()
    {
        var schema = new ConnectorConfigurationSchema(
            "opcua",
            """
            {
              "type": "object",
              "properties": {
                "EndpointUrl": {
                  "type": "string",
                  "description": "OPC UA server endpoint URL",
                  "default": "opc.tcp://localhost:4840"
                },
                "IsSimulator": {
                  "type": "boolean",
                  "description": "Use built-in simulator instead of connecting to real OPC UA server",
                  "default": true
                },
                "SecurityPolicy": {
                  "type": "string",
                  "description": "OPC UA security policy to use",
                  "default": "None"
                },
                "NodeIds": {
                  "type": "array",
                  "description": "List of OPC UA node IDs to monitor for telemetry data"
                }
              },
              "required": ["EndpointUrl"]
            }
            """,
            new Dictionary<string, ConfigurationField>
            {
                {
                    "EndpointUrl", 
                    new ConfigurationField(
                        "EndpointUrl", 
                        "string", 
                        "Endpoint URL", 
                        "OPC UA server endpoint URL",
                        "opc.tcp://localhost:4840",
                        true,
                        false,
                        null,
                        new Dictionary<string, object> { { "pattern", @"^opc\.tcp:\/\/[\w\-\.]+:\d+\/?.*$" } }
                    )
                },
                {
                    "IsSimulator", 
                    new ConfigurationField(
                        "IsSimulator", 
                        "boolean", 
                        "Simulator Mode", 
                        "Use built-in simulator instead of connecting to real OPC UA server",
                        true,
                        false
                    )
                },
                {
                    "SecurityPolicy", 
                    new ConfigurationField(
                        "SecurityPolicy", 
                        "string", 
                        "Security Policy", 
                        "OPC UA security policy to use",
                        "None",
                        false,
                        false,
                        new[] { "None", "Basic128Rsa15", "Basic256", "Basic256Sha256", "Aes128_Sha256_RsaOaep", "Aes256_Sha256_RsaPss" }
                    )
                },
                {
                    "NodeIds", 
                    new ConfigurationField(
                        "NodeIds", 
                        "array", 
                        "Node IDs", 
                        "List of OPC UA node IDs to monitor for telemetry data",
                        new[] { "ns=2;i=1001", "ns=2;i=1002", "ns=2;i=1003", "ns=2;i=1004", "ns=2;i=1005", "ns=2;i=1006", "ns=2;i=1007", "ns=2;i=1008" },
                        false
                    )
                }
            },
            new[] { "EndpointUrl" }
        );

        return ValueTask.FromResult(schema);
    }

    public override Result<bool> ValidateConfiguration(ConnectorConfiguration configuration)
    {
        var errors = new List<string>();

        var endpointUrl = GetSettingValue<string>(configuration, "EndpointUrl");
        if (string.IsNullOrWhiteSpace(endpointUrl))
        {
            errors.Add("EndpointUrl is required");
        }
        else if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) || !uri.Scheme.Equals("opc.tcp", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("EndpointUrl must be a valid OPC UA endpoint URL (opc.tcp://...)");
        }

        var securityPolicy = GetSettingValue<string>(configuration, "SecurityPolicy");
        if (!string.IsNullOrEmpty(securityPolicy))
        {
            var validPolicies = new[] { "None", "Basic128Rsa15", "Basic256", "Basic256Sha256", "Aes128_Sha256_RsaOaep", "Aes256_Sha256_RsaPss" };
            if (!validPolicies.Contains(securityPolicy))
            {
                errors.Add($"SecurityPolicy must be one of: {string.Join(", ", validPolicies)}");
            }
        }

        var nodeIds = GetSettingValue<List<string>>(configuration, "NodeIds");
        if (nodeIds != null && nodeIds.Count == 0)
        {
            errors.Add("At least one NodeId is required for telemetry collection");
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