using Mak3r.Edge.Models;
using MAK3R.Connectors.Abstractions;
using MAK3R.Connectors.MTConnect;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Mak3r.Edge.Services;

public class HaasEdgeAdapter : IEdgeConnectorAdapter
{
    private readonly ILogger<HaasEdgeAdapter> _logger;
    private readonly MTConnectConnector _connector;
    private readonly EdgeConnectorConfig _config;
    private readonly string _siteId;
    private bool _isRunning;

    public string Id => _config.MachineId;
    public string MachineId => _config.MachineId;
    public string Protocol => "MTConnect";
    public bool IsConnected => _connector != null;

    public HaasEdgeAdapter(EdgeConnectorConfig config, string siteId, ILogger<HaasEdgeAdapter> logger)
    {
        _config = config;
        _siteId = siteId;
        _logger = logger;

        // Create MTConnect connector configuration for HAAS
        var mtconnectConfig = new MTConnectConfig
        {
            ConnectorId = config.MachineId,
            MachineId = config.MachineId,
            Make = "HAAS",
            BaseUrl = GetSetting<string>("BaseUrl") ?? $"http://{config.IpAddress}:8082/VF2SS",
            IsSimulator = GetSetting<bool>("IsSimulator", false),
            SampleIntervalMs = GetSetting<int>("SampleIntervalMs", 500)
        };

        _connector = new MTConnectConnector(
            logger as ILogger<MTConnectConnector> ?? 
            new LoggerFactory().CreateLogger<MTConnectConnector>(), 
            mtconnectConfig);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting HAAS MTConnect Edge adapter for machine {MachineId}", MachineId);
        _isRunning = true;
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping HAAS MTConnect Edge adapter for machine {MachineId}", MachineId);
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
            var machineData = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
            
            if (machineData == null) return null;

            return new KMachineEvent
            {
                SiteId = _siteId,
                MachineId = MachineId,
                Ts = upsertEvent.Timestamp,
                Source = new SourceInfo
                {
                    Vendor = "HAAS",
                    Protocol = "MTConnect",
                    Ip = _config.IpAddress
                },
                State = new StateInfo
                {
                    Power = "ON",
                    Availability = GetStringValue(machineData, "availability"),
                    Execution = GetStringValue(machineData, "execution"),
                    Mode = GetStringValue(machineData, "controllerMode"),
                    Program = new ProgramInfo
                    {
                        Name = GetStringValue(machineData, "program"),
                        Block = GetIntValue(machineData, "line")
                    },
                    Tool = new ToolInfo
                    {
                        Id = GetIntValue(machineData, "tool"),
                        Life = GetDoubleValue(machineData, "toolLife")
                    },
                    Overrides = new Overrides
                    {
                        Feed = GetDoubleValue(machineData, "feedOverride"),
                        Spindle = GetDoubleValue(machineData, "spindleOverride"),
                        Rapid = GetDoubleValue(machineData, "rapidOverride")
                    },
                    Metrics = new Metrics
                    {
                        SpindleRPM = GetDoubleValue(machineData, "spindleSpeed"),
                        Feedrate = GetDoubleValue(machineData, "pathFeedrate"),
                        PartCount = GetIntValue(machineData, "partCount")
                    }
                },
                Event = new EventInfo
                {
                    Type = DetermineEventType(machineData),
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

    private string? DetermineEventType(Dictionary<string, object> data)
    {
        var execution = GetStringValue(data, "execution");
        var eventType = GetStringValue(data, "eventType");
        
        // Check for explicit event types first
        if (!string.IsNullOrEmpty(eventType))
        {
            return eventType switch
            {
                "TOOL_CHANGE" => "TOOL_CHANGE",
                "PROGRAM_START" => "PROG_START", 
                "PART_COMPLETED" => "PART_COMPLETED",
                _ => eventType
            };
        }
        
        // Infer event types from execution state transitions
        return execution switch
        {
            "ACTIVE" => "CYCLE_START",
            "READY" => "CYCLE_STOP",
            "INTERRUPTED" => "FEED_HOLD",
            _ => null
        };
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