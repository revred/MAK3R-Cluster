using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Mak3r.Edge.Models;

namespace Mak3r.Edge.Services;

public class ConfigValidationService
{
    private readonly ILogger<ConfigValidationService> _log;

    public ConfigValidationService(ILogger<ConfigValidationService> log)
    {
        _log = log;
    }

    public ValidationResult ValidateEdgeConfig(EdgeConfig config)
    {
        var context = new ValidationContext(config);
        var results = new List<ValidationResult>();
        
        bool isValid = Validator.TryValidateObject(config, context, results, true);

        // Additional business logic validations
        ValidateUplinkConfiguration(config.Uplink, results);
        ValidateStorageConfiguration(config.Storage, results);
        ValidateQueueConfiguration(config.Queue, results);
        ValidateAdminApiConfiguration(config.AdminApi, results);

        return new ValidationResult
        {
            IsValid = isValid && results.Count == 0,
            Errors = results.Select(r => r.ErrorMessage ?? "Unknown validation error").ToList(),
            Warnings = GetConfigurationWarnings(config)
        };
    }

    public ValidationResult ValidateMachinesConfig(List<EdgeConnectorConfig> machines)
    {
        var results = new List<ValidationResult>();
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!machines.Any())
        {
            errors.Add("No machines configured - at least one machine is required");
            return new ValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }

        // Check for duplicate machine IDs
        var duplicateIds = machines.GroupBy(m => m.MachineId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicateId in duplicateIds)
        {
            errors.Add($"Duplicate machine ID found: {duplicateId}");
        }

        // Validate each machine configuration
        foreach (var machine in machines)
        {
            var context = new ValidationContext(machine);
            var machineResults = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(machine, context, machineResults, true);

            if (!isValid)
            {
                errors.AddRange(machineResults.Select(r => $"Machine {machine.MachineId}: {r.ErrorMessage}"));
            }

            // Protocol-specific validation
            ValidateMachineProtocolConfig(machine, errors, warnings);
        }

        // Check IP address conflicts
        ValidateIpAddresses(machines, errors, warnings);

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    public ValidationResult ValidateJsonConfiguration(string configJson, string configType)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(configJson);
            
            switch (configType.ToLower())
            {
                case "edge":
                    var edgeConfig = JsonSerializer.Deserialize<EdgeConfig>(configJson);
                    if (edgeConfig != null)
                    {
                        var result = ValidateEdgeConfig(edgeConfig);
                        return result;
                    }
                    break;

                case "machines":
                    var machinesWrapper = JsonSerializer.Deserialize<MachinesConfigWrapper>(configJson);
                    if (machinesWrapper?.Machines != null)
                    {
                        var result = ValidateMachinesConfig(machinesWrapper.Machines);
                        return result;
                    }
                    break;

                default:
                    errors.Add($"Unknown configuration type: {configType}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Configuration parsing error: {ex.Message}");
        }

        return new ValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
    }

    private void ValidateUplinkConfiguration(UplinkConfig uplink, List<ValidationResult> results)
    {
        if (!Uri.TryCreate(uplink.HubUrl, UriKind.Absolute, out var uri))
        {
            results.Add(new ValidationResult("Invalid Hub URL format"));
        }
        else if (uri.Scheme != "https" && uri.Scheme != "http")
        {
            results.Add(new ValidationResult("Hub URL must use HTTP or HTTPS protocol"));
        }

        if (uplink.ReconnectDelayMs < 1000)
        {
            results.Add(new ValidationResult("Reconnect delay should be at least 1000ms"));
        }

        if (uplink.Batch.MaxEvents < 1 || uplink.Batch.MaxEvents > 1000)
        {
            results.Add(new ValidationResult("Batch MaxEvents must be between 1 and 1000"));
        }

        if (uplink.Batch.FlushIntervalMs < 100)
        {
            results.Add(new ValidationResult("Batch flush interval should be at least 100ms"));
        }
    }

    private void ValidateStorageConfiguration(StorageConfig storage, List<ValidationResult> results)
    {
        if (string.IsNullOrEmpty(storage.Root))
        {
            results.Add(new ValidationResult("Storage root path is required"));
        }

        if (string.IsNullOrEmpty(storage.Sqlite.Path))
        {
            results.Add(new ValidationResult("SQLite database path is required"));
        }
    }

    private void ValidateQueueConfiguration(QueueConfig queue, List<ValidationResult> results)
    {
        if (queue.Capacity < 1000)
        {
            results.Add(new ValidationResult("Queue capacity should be at least 1000"));
        }

        if (queue.Capacity > 1000000)
        {
            results.Add(new ValidationResult("Queue capacity should not exceed 1,000,000 for memory usage"));
        }
    }

    private void ValidateAdminApiConfiguration(AdminApiConfig adminApi, List<ValidationResult> results)
    {
        if (!Uri.TryCreate(adminApi.Listen, UriKind.Absolute, out var uri))
        {
            results.Add(new ValidationResult("Invalid Admin API listen URL format"));
        }
    }

    private void ValidateMachineProtocolConfig(EdgeConnectorConfig machine, List<string> errors, List<string> warnings)
    {
        switch (machine.Protocol.ToUpper())
        {
            case "FOCAS":
                ValidateFocasConfig(machine, errors, warnings);
                break;
            case "OPC UA":
                ValidateOpcUaConfig(machine, errors, warnings);
                break;
            case "MTCONNECT":
                ValidateMTConnectConfig(machine, errors, warnings);
                break;
            default:
                errors.Add($"Machine {machine.MachineId}: Unknown protocol '{machine.Protocol}'");
                break;
        }
    }

    private void ValidateFocasConfig(EdgeConnectorConfig machine, List<string> errors, List<string> warnings)
    {
        if (!machine.Settings.ContainsKey("Port"))
        {
            errors.Add($"Machine {machine.MachineId}: FOCAS protocol requires 'Port' setting");
        }
        else if (machine.Settings["Port"] is not int port || port < 1 || port > 65535)
        {
            errors.Add($"Machine {machine.MachineId}: Invalid FOCAS port number");
        }

        if (machine.Settings.ContainsKey("PollIntervalMs"))
        {
            if (machine.Settings["PollIntervalMs"] is int interval && interval < 100)
            {
                warnings.Add($"Machine {machine.MachineId}: FOCAS poll interval below 100ms may cause performance issues");
            }
        }
    }

    private void ValidateOpcUaConfig(EdgeConnectorConfig machine, List<string> errors, List<string> warnings)
    {
        if (!machine.Settings.ContainsKey("EndpointUrl"))
        {
            errors.Add($"Machine {machine.MachineId}: OPC UA protocol requires 'EndpointUrl' setting");
        }
        else if (machine.Settings["EndpointUrl"] is string endpointUrl)
        {
            if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) || uri.Scheme != "opc.tcp")
            {
                errors.Add($"Machine {machine.MachineId}: Invalid OPC UA endpoint URL format (should be opc.tcp://...)");
            }
        }

        if (machine.Settings.ContainsKey("SecurityPolicy"))
        {
            var policy = machine.Settings["SecurityPolicy"]?.ToString();
            if (policy != null && !new[] { "None", "Basic128Rsa15", "Basic256", "Basic256Sha256" }.Contains(policy))
            {
                warnings.Add($"Machine {machine.MachineId}: Unknown OPC UA security policy '{policy}'");
            }
        }
    }

    private void ValidateMTConnectConfig(EdgeConnectorConfig machine, List<string> errors, List<string> warnings)
    {
        if (!machine.Settings.ContainsKey("BaseUrl"))
        {
            errors.Add($"Machine {machine.MachineId}: MTConnect protocol requires 'BaseUrl' setting");
        }
        else if (machine.Settings["BaseUrl"] is string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                errors.Add($"Machine {machine.MachineId}: Invalid MTConnect base URL format");
            }
        }

        if (machine.Settings.ContainsKey("SampleIntervalMs"))
        {
            if (machine.Settings["SampleIntervalMs"] is int interval && interval < 500)
            {
                warnings.Add($"Machine {machine.MachineId}: MTConnect sample interval below 500ms may cause performance issues");
            }
        }
    }

    private void ValidateIpAddresses(List<EdgeConnectorConfig> machines, List<string> errors, List<string> warnings)
    {
        var ipGroups = machines.GroupBy(m => m.IpAddress).Where(g => g.Count() > 1);
        
        foreach (var group in ipGroups)
        {
            var machineIds = string.Join(", ", group.Select(m => m.MachineId));
            warnings.Add($"Multiple machines share IP address {group.Key}: {machineIds}");
        }

        foreach (var machine in machines)
        {
            if (!System.Net.IPAddress.TryParse(machine.IpAddress, out _))
            {
                errors.Add($"Machine {machine.MachineId}: Invalid IP address format '{machine.IpAddress}'");
            }
        }
    }

    private List<string> GetConfigurationWarnings(EdgeConfig config)
    {
        var warnings = new List<string>();

        if (config.LoadGen.Enabled)
        {
            warnings.Add("Load generator is enabled - this should be disabled in production");
        }

        if (config.Queue.Capacity > 50000)
        {
            warnings.Add("Large queue capacity configured - monitor memory usage");
        }

        if (config.Uplink.Batch.FlushIntervalMs > 30000)
        {
            warnings.Add("Long batch flush interval may delay event delivery");
        }

        return warnings;
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class MachinesConfigWrapper
{
    public List<EdgeConnectorConfig> Machines { get; set; } = new();
}