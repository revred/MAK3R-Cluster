using MAK3R.Connectors.Abstractions;
using MAK3R.Core;
using Microsoft.Extensions.Logging;

namespace MAK3R.Connectors.FANUC;

public class FanucConnectorFactory : ConnectorFactoryBase<FanucConnector>
{
    private readonly ILogger<FanucConnector> _logger;

    public FanucConnectorFactory(ILogger<FanucConnector> logger)
    {
        _logger = logger;
    }

    public override async ValueTask<Result<FanucConnector>> CreateAsync(ConnectorConfiguration configuration, CancellationToken ct = default)
    {
        try
        {
            var validationResult = ValidateConfiguration(configuration);
            if (!validationResult.IsSuccess)
            {
                return Result<FanucConnector>.Failure(validationResult.Error!);
            }

            var config = new FanucConfig
            {
                ConnectorId = configuration.ConnectorId,
                MachineId = GetSettingValue<string>(configuration, "MachineId") ?? "FANUC-01",
                IpAddress = GetSettingValue<string>(configuration, "IpAddress") ?? "10.10.20.11",
                Port = GetSettingValue<int>(configuration, "Port", 8193),
                IsSimulator = GetSettingValue<bool>(configuration, "IsSimulator", true),
                PollIntervalMs = GetSettingValue<int>(configuration, "PollIntervalMs", 250)
            };

            if (string.IsNullOrWhiteSpace(config.MachineId))
                return Result<FanucConnector>.Failure("MachineId is required");
                
            if (string.IsNullOrWhiteSpace(config.IpAddress))
                return Result<FanucConnector>.Failure("IpAddress is required");

            if (config.Port <= 0 || config.Port > 65535)
                return Result<FanucConnector>.Failure("Port must be between 1 and 65535");

            var connector = new FanucConnector(_logger, config);
            
            _logger.LogInformation("Created FANUC connector for machine: {MachineId}", config.MachineId);
            
            return Result<FanucConnector>.Success(connector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create FANUC connector");
            return Result<FanucConnector>.Failure($"Creation failed: {ex.Message}");
        }
    }

    public override ValueTask<ConnectorConfigurationSchema> GetConfigurationSchemaAsync()
    {
        var schema = new ConnectorConfigurationSchema(
            "fanuc",
            """
            {
              "type": "object",
              "properties": {
                "MachineId": {
                  "type": "string",
                  "minLength": 1,
                  "description": "Unique identifier for the FANUC machine"
                },
                "IpAddress": {
                  "type": "string",
                  "pattern": "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
                  "description": "IP address of the FANUC controller"
                },
                "Port": {
                  "type": "integer",
                  "minimum": 1,
                  "maximum": 65535,
                  "description": "FOCAS TCP port (default: 8193)"
                },
                "IsSimulator": {
                  "type": "boolean",
                  "description": "Enable simulator mode for testing"
                },
                "PollIntervalMs": {
                  "type": "integer",
                  "minimum": 100,
                  "maximum": 5000,
                  "description": "Polling interval in milliseconds"
                }
              },
              "required": ["MachineId", "IpAddress"]
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
                        "Unique identifier for the FANUC machine (e.g., FANUC-TC-01)",
                        null,
                        true
                    )
                },
                {
                    "IpAddress", 
                    new ConfigurationField(
                        "IpAddress", 
                        "string", 
                        "IP Address", 
                        "IP address of the FANUC controller",
                        "10.10.20.11",
                        true
                    )
                },
                {
                    "Port", 
                    new ConfigurationField(
                        "Port", 
                        "number", 
                        "FOCAS Port", 
                        "TCP port for FOCAS communication",
                        8193,
                        false
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
                    "PollIntervalMs", 
                    new ConfigurationField(
                        "PollIntervalMs", 
                        "number", 
                        "Poll Interval (ms)", 
                        "How frequently to poll the controller for data",
                        250,
                        false
                    )
                }
            },
            new[] { "MachineId", "IpAddress" }
        );

        return ValueTask.FromResult(schema);
    }

    public override Result<bool> ValidateConfiguration(ConnectorConfiguration configuration)
    {
        var baseResult = base.ValidateConfiguration(configuration);
        if (!baseResult.IsSuccess)
            return baseResult;

        if (configuration.Type != "fanuc")
            return Result<bool>.Failure("Invalid connector type for FANUC factory");

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