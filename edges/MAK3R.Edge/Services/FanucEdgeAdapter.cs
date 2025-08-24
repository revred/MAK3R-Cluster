using Mak3r.Edge.Models;
using MAK3R.Connectors.Abstractions;
using MAK3R.Connectors.FANUC;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Mak3r.Edge.Services;

public class FanucEdgeAdapter : IEdgeConnectorAdapter
{
    private readonly ILogger<FanucEdgeAdapter> _logger;
    private readonly FanucConnector _connector;
    private readonly EdgeConnectorConfig _config;
    private readonly string _siteId;
    private bool _isRunning;

    public string Id => _config.MachineId;
    public string MachineId => _config.MachineId;
    public string Protocol => "FOCAS";
    public bool IsConnected => _connector != null;

    public FanucEdgeAdapter(EdgeConnectorConfig config, string siteId, ILogger<FanucEdgeAdapter> logger)
    {
        _config = config;
        _siteId = siteId;
        _logger = logger;

        // Create FANUC connector configuration
        var fanucConfig = new FanucConfig
        {
            ConnectorId = config.MachineId,
            MachineId = config.MachineId,
            IpAddress = config.IpAddress,
            Port = GetSetting<int>("Port", 8193),
            IsSimulator = GetSetting<bool>("IsSimulator", false),
            PollIntervalMs = GetSetting<int>("PollIntervalMs", 250)
        };

        _connector = new FanucConnector(
            logger as ILogger<FanucConnector> ?? 
            new LoggerFactory().CreateLogger<FanucConnector>(), 
            fanucConfig);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting FANUC Edge adapter for machine {MachineId}", MachineId);
        _isRunning = true;
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping FANUC Edge adapter for machine {MachineId}", MachineId);
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
                    Vendor = "FANUC",
                    Protocol = "FOCAS",
                    Ip = _config.IpAddress
                },
                State = new StateInfo
                {
                    Execution = GetStringValue(machineData, "execution"),
                    Mode = GetStringValue(machineData, "mode"),
                    Program = new ProgramInfo
                    {
                        Name = GetStringValue(machineData, "program"),
                        Block = GetIntValue(machineData, "block")
                    },
                    Tool = new ToolInfo
                    {
                        Id = GetIntValue(machineData, "tool"),
                        Life = GetDoubleValue(machineData, "toolLife")
                    },
                    Overrides = new Overrides
                    {
                        Feed = GetDoubleValue(machineData, "feedOverride"),
                        Spindle = GetDoubleValue(machineData, "spindleOverride")
                    },
                    Metrics = new Metrics
                    {
                        SpindleRPM = GetDoubleValue(machineData, "spindleRpm"),
                        Feedrate = GetDoubleValue(machineData, "feedrate"),
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

    private string? DetermineEventType(Dictionary<string, object> data)
    {
        var execution = GetStringValue(data, "execution");
        
        return execution switch
        {
            "ACTIVE" => "CYCLE_START",
            "READY" => "CYCLE_STOP",
            "FEED_HOLD" => "FEED_HOLD",
            _ => null
        };
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