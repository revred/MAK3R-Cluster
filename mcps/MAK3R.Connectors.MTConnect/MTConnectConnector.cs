using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace MAK3R.Connectors.MTConnect;

public class MTConnectConnector : IConnector, IDisposable
{
    private readonly ILogger<MTConnectConnector> _logger;
    private readonly MTConnectConfig _config;
    private readonly HttpClient _httpClient;
    private readonly Timer? _pollTimer;
    private readonly Queue<UpsertEvent> _eventQueue = new();
    private readonly object _queueLock = new();
    private readonly Random _random = new();

    public string Id => _config.ConnectorId;
    public string Name => $"MTConnect - {_config.MachineId}";
    public string Type => "mtconnect";

    public MTConnectConnector(ILogger<MTConnectConnector> logger, MTConnectConfig config)
    {
        _logger = logger;
        _config = config;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        if (_config.IsSimulator)
        {
            _pollTimer = new Timer(GenerateSimulatorData, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_config.SampleIntervalMs));
        }
    }

    public async ValueTask<ConnectorCheck> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Checking MTConnect connector health for {MachineId} (Simulator: {IsSimulator})", _config.MachineId, _config.IsSimulator);

            if (_config.IsSimulator)
            {
                return new ConnectorCheck(
                    true,
                    $"MTConnect Simulator - {_config.MachineId} - Healthy",
                    new Dictionary<string, object>
                    {
                        { "IsSimulator", true },
                        { "MachineId", _config.MachineId },
                        { "Make", _config.Make },
                        { "SampleInterval", _config.SampleIntervalMs }
                    }
                );
            }

            var probeUrl = $"{_config.BaseUrl}/probe";
            var response = await _httpClient.GetAsync(probeUrl, ct);
            
            if (response.IsSuccessStatusCode)
            {
                return new ConnectorCheck(
                    true,
                    $"Connected to MTConnect agent for {_config.MachineId}",
                    new Dictionary<string, object>
                    {
                        { "IsSimulator", false },
                        { "MachineId", _config.MachineId },
                        { "Make", _config.Make },
                        { "ProbeUrl", probeUrl },
                        { "ResponseStatus", (int)response.StatusCode }
                    }
                );
            }
            else
            {
                return new ConnectorCheck(false, $"MTConnect agent not reachable: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MTConnect health check for {MachineId}", _config.MachineId);
            return new ConnectorCheck(false, $"Health check error: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<UpsertEvent> PullAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starting MTConnect pull for {MachineId} since {Since} (Simulator: {IsSimulator})", _config.MachineId, since, _config.IsSimulator);

        if (_config.IsSimulator)
        {
            while (!ct.IsCancellationRequested)
            {
                UpsertEvent? eventToYield = null;
                
                lock (_queueLock)
                {
                    if (_eventQueue.Count > 0)
                    {
                        eventToYield = _eventQueue.Dequeue();
                    }
                }

                if (eventToYield != null && eventToYield.Timestamp >= since)
                {
                    yield return eventToYield;
                }

                await Task.Delay(100, ct);
            }
        }
        else
        {
            await foreach (var machineEvent in PullMTConnectDataAsync(since, ct))
            {
                if (ct.IsCancellationRequested)
                    yield break;
                    
                yield return machineEvent;
            }
        }
    }

    public ValueTask<ConnectorConfiguration> GetConfigurationSchemaAsync()
    {
        var config = new ConnectorConfiguration(
            Id,
            Type,
            new Dictionary<string, object>
            {
                { "MachineId", _config.MachineId },
                { "Make", _config.Make },
                { "BaseUrl", _config.BaseUrl },
                { "IsSimulator", _config.IsSimulator },
                { "SampleIntervalMs", _config.SampleIntervalMs }
            }
        );
        
        return ValueTask.FromResult(config);
    }

    private async IAsyncEnumerable<UpsertEvent> PullMTConnectDataAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var currentUrl = $"{_config.BaseUrl}/current";
        var response = await _httpClient.GetAsync(currentUrl, ct);
        
        if (response.IsSuccessStatusCode)
        {
            var xmlContent = await response.Content.ReadAsStringAsync(ct);
            var machineData = ParseMTConnectData(xmlContent);
            
            if (machineData.timestamp >= since)
            {
                yield return CreateUpsertEvent(machineData);
            }
        }
    }

    private (Dictionary<string, object> data, DateTime timestamp) ParseMTConnectData(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var timestamp = DateTime.UtcNow;
            
            // This would parse actual MTConnect XML structure
            // For now, return simulated data based on configuration
            return CreateSimulatedMachineData(timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing MTConnect XML for {MachineId}, returning simulated data", _config.MachineId);
            return CreateSimulatedMachineData(DateTime.UtcNow);
        }
    }

    private void GenerateSimulatorData(object? state)
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            var machineData = CreateSimulatedMachineData(timestamp);
            var upsertEvent = CreateUpsertEvent(machineData);

            lock (_queueLock)
            {
                _eventQueue.Enqueue(upsertEvent);
                
                while (_eventQueue.Count > 1000)
                {
                    _eventQueue.Dequeue();
                }
            }

            _logger.LogDebug("Generated MTConnect simulator data for machine {MachineId}", _config.MachineId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating MTConnect simulator data for {MachineId}", _config.MachineId);
        }
    }

    private (Dictionary<string, object> data, DateTime timestamp) CreateSimulatedMachineData(DateTime timestamp)
    {
        // Different characteristics based on machine make
        var (spindleMin, spindleMax, feedMax, partMax) = _config.Make.ToUpper() switch
        {
            "HAAS" => (800, 8000, 1500, 150), // Mill characteristics
            "MAZAK" => (300, 12000, 5000, 50), // 5-axis characteristics  
            _ => (500, 6000, 2000, 100)
        };
        
        var data = new Dictionary<string, object>
        {
            { "availability", "AVAILABLE" },
            { "execution", _random.Next(0, 3) switch { 0 => "READY", 1 => "ACTIVE", _ => "INTERRUPTED" } },
            { "controllerMode", _random.Next(0, 2) switch { 0 => "AUTOMATIC", _ => "MANUAL" } },
            { "program", $"PROG{_random.Next(100, 999)}" },
            { "line", _random.Next(1, 200) },
            { "pathFeedrate", Math.Round(_random.NextDouble() * feedMax + 100, 1) },
            { "spindleSpeed", _random.Next(spindleMin, spindleMax) },
            { "spindleOverride", Math.Round(0.8 + _random.NextDouble() * 0.4, 2) },
            { "feedOverride", Math.Round(0.75 + _random.NextDouble() * 0.5, 2) },
            { "rapidOverride", Math.Round(0.6 + _random.NextDouble() * 0.4, 2) },
            { "tool", _random.Next(1, 30) },
            { "toolLife", Math.Round(_random.NextDouble() * 100, 1) },
            { "partCount", _random.Next(0, partMax) },
            { "machineId", _config.MachineId },
            { "make", _config.Make },
            { "protocol", "MTConnect" }
        };
        
        // Occasionally add events
        if (_random.Next(0, 20) == 0)
        {
            data["eventType"] = _random.Next(0, 3) switch 
            { 
                0 => "TOOL_CHANGE", 
                1 => "PROGRAM_START", 
                _ => "PART_COMPLETED" 
            };
        }

        return (data, timestamp);
    }

    private UpsertEvent CreateUpsertEvent((Dictionary<string, object> data, DateTime timestamp) machineData)
    {
        return new UpsertEvent(
            "MachineData",
            $"{_config.MachineId}-{DateTime.UtcNow:HHmmss}",
            JsonSerializer.SerializeToElement(machineData.data),
            machineData.timestamp
        );
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _httpClient?.Dispose();
    }
}

public class MTConnectConfig
{
    public string ConnectorId { get; set; } = "mtconnect-connector";
    public string MachineId { get; set; } = "MACHINE-01";
    public string Make { get; set; } = "HAAS";
    public string BaseUrl { get; set; } = "http://10.10.20.13:8082/VF2SS";
    public bool IsSimulator { get; set; } = true;
    public int SampleIntervalMs { get; set; } = 500;
}