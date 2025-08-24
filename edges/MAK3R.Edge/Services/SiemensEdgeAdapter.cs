using Mak3r.Edge.Models;
using MAK3R.Connectors.Abstractions;
using MAK3R.Connectors.OPCUA;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Mak3r.Edge.Services;

public class SiemensEdgeAdapter : IEdgeConnectorAdapter
{
    private readonly ILogger<SiemensEdgeAdapter> _logger;
    private readonly OpcUaConnector _connector;
    private readonly EdgeConnectorConfig _config;
    private readonly string _siteId;
    private bool _isRunning;

    public string Id => _config.MachineId;
    public string MachineId => _config.MachineId;
    public string Protocol => "OPC UA";
    public bool IsConnected => _connector != null;

    public SiemensEdgeAdapter(EdgeConnectorConfig config, string siteId, ILogger<SiemensEdgeAdapter> logger)
    {
        _config = config;
        _siteId = siteId;
        _logger = logger;

        // Create OPC UA connector configuration
        var opcuaConfig = new OpcUaConfig
        {
            MachineId = config.MachineId,
            EndpointUrl = GetSetting<string>("EndpointUrl") ?? $"opc.tcp://{config.IpAddress}:4840",
            IsSimulator = GetSetting<bool>("IsSimulator", false),
            SecurityPolicy = GetSetting<string>("SecurityPolicy", "None"),
            NodeIds = GetSetting<List<string>>("NodeIds") ?? new List<string>
            {
                "ns=3;s=Channel/State",
                "ns=3;s=Program/Name", 
                "ns=3;s=Program/Block",
                "ns=3;s=Spindle/Speed",
                "ns=3;s=Feed/Rate",
                "ns=3;s=Tool/Number",
                "ns=3;s=Machine/Mode",
                "ns=3;s=PartCount"
            }
        };

        _connector = new OpcUaConnector(
            logger as ILogger<OpcUaConnector> ?? 
            new LoggerFactory().CreateLogger<OpcUaConnector>(), 
            opcuaConfig);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Siemens OPC UA Edge adapter for machine {MachineId}", MachineId);
        _isRunning = true;
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping Siemens OPC UA Edge adapter for machine {MachineId}", MachineId);
        _isRunning = false;
        await Task.CompletedTask;
    }

    public async Task<ConnectorCheck> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return await _connector.CheckAsync(cancellationToken);
    }

    public async IAsyncEnumerable<KMachineEvent> GetEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddMinutes(-1);
        
        await foreach (var upsertEvent in _connector.PullAsync(since, cancellationToken))
        {
            if (!_isRunning) yield break;
            
            // Convert UpsertEvent to KMachineEvent
            var kmachineEvent = ConvertToKMachineEvent(upsertEvent);
            if (kmachineEvent != null)
            {
                yield return kmachineEvent;
            }
            
            // Small delay to prevent overwhelming the system
            await Task.Delay(100, cancellationToken);
        }
    }

    private KMachineEvent? ConvertToKMachineEvent(UpsertEvent upsertEvent)
    {
        try
        {
            // Parse the JSON payload from the UpsertEvent
            var payloadJson = upsertEvent.Payload.GetRawText();
            var telemetryData = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
            
            if (telemetryData == null) return null;

            return new KMachineEvent
            {
                SiteId = _siteId,
                MachineId = MachineId,
                Ts = upsertEvent.Timestamp,
                Source = new SourceInfo
                {
                    Vendor = "SIEMENS",
                    Protocol = "OPC UA",
                    Ip = _config.IpAddress
                },
                State = new StateInfo
                {
                    Execution = MapSiemensExecutionState(GetStringValue(telemetryData, "value")),
                    Mode = GetStringValue(telemetryData, "vendor") == "SIEMENS" ? "AUTO" : null,
                    Program = new ProgramInfo
                    {
                        Name = GetStringValue(telemetryData, "protocol") == "OPC UA" ? "SIEMENS_PROG" : null
                    },
                    Metrics = new Metrics
                    {
                        SpindleRPM = GetDoubleValue(telemetryData, "value"),
                        PartCount = GetIntValue(telemetryData, "partCount")
                    }
                },
                Event = new EventInfo
                {
                    Type = DetermineEventTypeFromOpcUa(telemetryData),
                    Severity = "INFO"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert UpsertEvent to KMachineEvent for machine {MachineId}", MachineId);
            return null;
        }
    }

    private string? MapSiemensExecutionState(string? opcuaValue)
    {
        if (string.IsNullOrEmpty(opcuaValue)) return null;
        
        // Map common Siemens SINUMERIK states to canonical execution states
        return opcuaValue.ToUpperInvariant() switch
        {
            "AUTOMATIC" or "AUTO" => "ACTIVE",
            "MANUAL" or "MDI" => "READY", 
            "RESET" => "STOPPED",
            "ALARM" => "ALARM",
            _ => "READY"
        };
    }

    private string? DetermineEventTypeFromOpcUa(Dictionary<string, object> data)
    {
        var nodeId = GetStringValue(data, "nodeId");
        var value = GetStringValue(data, "value");
        
        if (nodeId?.Contains("Channel/State") == true)
        {
            return value switch
            {
                "ACTIVE" => "CYCLE_START",
                "READY" => "CYCLE_STOP", 
                "ALARM" => "ALARM",
                _ => null
            };
        }
        
        if (nodeId?.Contains("Program/Name") == true)
        {
            return "PROG_START";
        }
        
        return null;
    }

    private string? GetStringValue(Dictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private int? GetIntValue(Dictionary<string, object> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value != null)
        {
            if (int.TryParse(value.ToString(), out var result))
                return result;
        }
        return null;
    }

    private double? GetDoubleValue(Dictionary<string, object> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value != null)
        {
            if (double.TryParse(value.ToString(), out var result))
                return result;
        }
        return null;
    }

    private T GetSetting<T>(string key, T defaultValue = default!)
    {
        if (_config.Settings.TryGetValue(key, out var value))
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

    public void Dispose()
    {
        _connector?.Dispose();
        GC.SuppressFinalize(this);
    }
}