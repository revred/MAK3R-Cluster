using MAK3R.Connectors.Abstractions;
using MAK3R.Core;
using Microsoft.Extensions.Logging;

namespace MAK3R.Connectors.MTConnect;

public class MTConnectConnectorFactory : ConnectorFactoryBase<MTConnectConnector>
{
    private readonly ILogger<MTConnectConnector> _logger;

    public MTConnectConnectorFactory(ILogger<MTConnectConnector> logger)
    {
        _logger = logger;
    }

    public override async ValueTask<Result<MTConnectConnector>> CreateAsync(ConnectorConfiguration configuration, CancellationToken ct = default)
    {
        try
        {
            var validationResult = ValidateConfiguration(configuration);
            if (!validationResult.IsSuccess)
            {
                return Result<MTConnectConnector>.Failure(validationResult.Error!);
            }

            var config = new MTConnectConfig
            {
                ConnectorId = configuration.ConnectorId,
                MachineId = GetSettingValue<string>(configuration, "MachineId") ?? "MACHINE-01",
                Make = GetSettingValue<string>(configuration, "Make") ?? "HAAS",
                BaseUrl = GetSettingValue<string>(configuration, "BaseUrl") ?? "http://localhost:5000",
                IsSimulator = GetSettingValue<bool>(configuration, "IsSimulator", true),
                SampleIntervalMs = GetSettingValue<int>(configuration, "SampleIntervalMs", 500)
            };

            if (string.IsNullOrWhiteSpace(config.MachineId))
                return Result<MTConnectConnector>.Failure("MachineId is required");
                
            if (string.IsNullOrWhiteSpace(config.BaseUrl))
                return Result<MTConnectConnector>.Failure("BaseUrl is required");

            if (config.SampleIntervalMs < 100 || config.SampleIntervalMs > 10000)
                return Result<MTConnectConnector>.Failure("SampleIntervalMs must be between 100 and 10000");

            var connector = new MTConnectConnector(_logger, config);
            
            _logger.LogInformation("Created MTConnect connector for machine: {MachineId} ({Make})", config.MachineId, config.Make);
            
            return Result<MTConnectConnector>.Success(connector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MTConnect connector");
            return Result<MTConnectConnector>.Failure($"Creation failed: {ex.Message}");
        }
    }

    public override ValueTask<ConnectorConfigurationSchema> GetConfigurationSchemaAsync()
    {
        var schema = new ConnectorConfigurationSchema(
            "mtconnect",
            """
            {
              "type": "object",
              "properties": {
                "MachineId": {
                  "type": "string",
                  "minLength": 1,
                  "description": "Unique identifier for the machine"
                },
                "Make": {
                  "type": "string",
                  "enum": ["HAAS", "MAZAK", "OKUMA", "DMG_MORI", "OTHER"],
                  "description": "Machine manufacturer"
                },
                "BaseUrl": {
                  "type": "string",
                  "pattern": "^https?://[^\\s/$.?#].[^\\s]*$",
                  "description": "Base URL of the MTConnect agent"
                },
                "IsSimulator": {
                  "type": "boolean",
                  "description": "Enable simulator mode for testing"
                },
                "SampleIntervalMs": {
                  "type": "integer",
                  "minimum": 100,
                  "maximum": 10000,
                  "description": "Sample interval in milliseconds"
                }
              },
              "required": ["MachineId", "BaseUrl"]
            }
            """,
            new Dictionary<string, ConfigurationField>
            {
                {
                    "MachineId", 
                    new ConfigurationField(
                        "MachineId", 
                        "string", 
                        "Machine ID", 
                        "Unique identifier for the machine (e.g., HAAS-MILL-03)",
                        null,
                        true
                    )
                },
                {
                    "Make", 
                    new ConfigurationField(
                        "Make", 
                        "string", 
                        "Machine Make", 
                        "Manufacturer of the machine",
                        "HAAS",
                        false,
                        false,
                        new[] { "HAAS", "MAZAK", "OKUMA", "DMG_MORI", "OTHER" }
                    )
                },
                {
                    "BaseUrl", 
                    new ConfigurationField(
                        "BaseUrl", 
                        "string", 
                        "MTConnect Agent URL", 
                        "Base URL of the MTConnect agent (e.g., http://10.10.20.13:8082/VF2SS)",
                        "http://localhost:5000",
                        true
                    )
                },
                {
                    "IsSimulator", 
                    new ConfigurationField(
                        "IsSimulator", 
                        "boolean", 
                        "Simulator Mode", 
                        "Enable simulator mode for testing without real hardware",
                        true,
                        false
                    )
                },
                {
                    "SampleIntervalMs", 
                    new ConfigurationField(
                        "SampleIntervalMs", 
                        "number", 
                        "Sample Interval (ms)", 
                        "How frequently to sample data from the MTConnect agent",
                        500,
                        false
                    )
                }
            },
            new[] { "MachineId", "BaseUrl" }
        );

        return ValueTask.FromResult(schema);
    }

    public override Result<bool> ValidateConfiguration(ConnectorConfiguration configuration)
    {
        var baseResult = base.ValidateConfiguration(configuration);
        if (!baseResult.IsSuccess)
            return baseResult;

        if (configuration.Type != "mtconnect")
            return Result<bool>.Failure("Invalid connector type for MTConnect factory");

        return Result<bool>.Success(true);
    }

    private T GetSettingValue<T>(ConnectorConfiguration configuration, string key, T defaultValue = default!)
    {
        if (configuration.Settings.TryGetValue(key, out var value))
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T))!;
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
}