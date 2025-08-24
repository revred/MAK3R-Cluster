using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MAK3R.Connectors.FANUC;

public class FanucConnector : IConnector, IDisposable
{
    private readonly ILogger<FanucConnector> _logger;
    private readonly FanucConfig _config;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly Timer? _pollTimer;
    private readonly Queue<UpsertEvent> _eventQueue = new();
    private readonly object _queueLock = new();
    private readonly Random _random = new();

    public string Id => _config.ConnectorId;
    public string Name => $"FANUC FOCAS - {_config.MachineId}";
    public string Type => "fanuc";

    public FanucConnector(ILogger<FanucConnector> logger, FanucConfig config)
    {
        _logger = logger;
        _config = config;

        if (_config.IsSimulator)
        {
            _pollTimer = new Timer(GenerateSimulatorData, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_config.PollIntervalMs));
        }
    }

    public async ValueTask<ConnectorCheck> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Checking FANUC connector health for {MachineId} (Simulator: {IsSimulator})", _config.MachineId, _config.IsSimulator);

            if (_config.IsSimulator)
            {
                return new ConnectorCheck(
                    true,
                    $"FANUC FOCAS Simulator - {_config.MachineId} - Healthy",
                    new Dictionary<string, object>
                    {
                        { "IsSimulator", true },
                        { "MachineId", _config.MachineId },
                        { "PollInterval", _config.PollIntervalMs }
                    }
                );
            }

            await EnsureConnectedAsync(ct);
            
            if (_tcpClient?.Connected == true)
            {
                return new ConnectorCheck(
                    true,
                    $"Connected to FANUC controller {_config.MachineId}",
                    new Dictionary<string, object>
                    {
                        { "IsSimulator", false },
                        { "MachineId", _config.MachineId },
                        { "EndPoint", $"{_config.IpAddress}:{_config.Port}" }
                    }
                );
            }
            else
            {
                return new ConnectorCheck(false, "Failed to connect to FANUC controller");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FANUC health check for {MachineId}", _config.MachineId);
            return new ConnectorCheck(false, $"Health check error: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<UpsertEvent> PullAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starting FANUC pull for {MachineId} since {Since} (Simulator: {IsSimulator})", _config.MachineId, since, _config.IsSimulator);

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
            await foreach (var machineEvent in PullMachineDataAsync(since, ct))
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
                { "IpAddress", _config.IpAddress },
                { "Port", _config.Port },
                { "IsSimulator", _config.IsSimulator },
                { "PollIntervalMs", _config.PollIntervalMs }
            }
        );
        
        return ValueTask.FromResult(config);
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_tcpClient?.Connected == true)
            return;

        try
        {
            _logger.LogDebug("Connecting to FANUC controller: {IpAddress}:{Port}", _config.IpAddress, _config.Port);

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_config.IpAddress, _config.Port, ct);
            _stream = _tcpClient.GetStream();
            
            _logger.LogInformation("Successfully connected to FANUC controller {MachineId}", _config.MachineId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to FANUC controller {MachineId}", _config.MachineId);
            throw;
        }
    }

    private async IAsyncEnumerable<UpsertEvent> PullMachineDataAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        
        if (_tcpClient?.Connected != true)
            yield break;

        // Simulate reading FOCAS data structure
        var focasData = await ReadFocasDataAsync();
        
        if (focasData.timestamp >= since)
        {
            yield return CreateUpsertEvent(focasData);
        }
    }

    private async Task<(Dictionary<string, object> data, DateTime timestamp)> ReadFocasDataAsync()
    {
        // This would implement actual FOCAS protocol communication
        await Task.Delay(50);
        
        var timestamp = DateTime.UtcNow;
        var data = new Dictionary<string, object>
        {
            { "execution", "ACTIVE" },
            { "mode", "AUTO" },
            { "program", $"O{_random.Next(1000, 9999)}" },
            { "block", _random.Next(1, 500) },
            { "spindleRpm", _random.Next(1000, 5000) },
            { "feedrate", Math.Round(_random.NextDouble() * 2000 + 500, 1) },
            { "tool", _random.Next(1, 20) },
            { "partCount", _random.Next(0, 100) }
        };
        
        return (data, timestamp);
    }

    private void GenerateSimulatorData(object? state)
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            var focasData = new Dictionary<string, object>
            {
                { "execution", _random.Next(0, 3) switch { 0 => "READY", 1 => "ACTIVE", _ => "FEED_HOLD" } },
                { "mode", _random.Next(0, 3) switch { 0 => "AUTO", 1 => "MDI", _ => "MANUAL" } },
                { "program", $"O{_random.Next(1000, 9999)}" },
                { "block", _random.Next(1, 500) },
                { "spindleRpm", _random.Next(1000, 5000) },
                { "feedrate", Math.Round(_random.NextDouble() * 2000 + 500, 1) },
                { "feedOverride", Math.Round(0.8 + _random.NextDouble() * 0.4, 2) },
                { "spindleOverride", Math.Round(0.9 + _random.NextDouble() * 0.2, 2) },
                { "tool", _random.Next(1, 20) },
                { "toolLife", Math.Round(_random.NextDouble() * 100, 1) },
                { "partCount", _random.Next(0, 100) },
                { "machineId", _config.MachineId },
                { "vendor", "FANUC" },
                { "protocol", "FOCAS" }
            };

            var upsertEvent = CreateUpsertEvent((focasData, timestamp));

            lock (_queueLock)
            {
                _eventQueue.Enqueue(upsertEvent);
                
                while (_eventQueue.Count > 1000)
                {
                    _eventQueue.Dequeue();
                }
            }

            _logger.LogDebug("Generated FANUC simulator data for machine {MachineId}", _config.MachineId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating FANUC simulator data for {MachineId}", _config.MachineId);
        }
    }

    private UpsertEvent CreateUpsertEvent((Dictionary<string, object> data, DateTime timestamp) focasData)
    {
        return new UpsertEvent(
            "MachineData",
            $"{_config.MachineId}-{DateTime.UtcNow:HHmmss}",
            JsonSerializer.SerializeToElement(focasData.data),
            focasData.timestamp
        );
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
    }
}

public class FanucConfig
{
    public string ConnectorId { get; set; } = "fanuc-connector";
    public string MachineId { get; set; } = "FANUC-TC-01";
    public string IpAddress { get; set; } = "10.10.20.11";
    public int Port { get; set; } = 8193;
    public bool IsSimulator { get; set; } = true;
    public int PollIntervalMs { get; set; } = 250;
}