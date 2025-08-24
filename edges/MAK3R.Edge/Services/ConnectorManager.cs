using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Mak3r.Edge.Services;

public class ConnectorManager : BackgroundService
{
    private readonly EdgeConfig _cfg;
    private readonly NormalizerService _normalizer;
    private readonly ILogger<ConnectorManager> _log;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, IEdgeConnectorAdapter> _connectors = new();
    private readonly CancellationTokenSource _managerCts = new();

    public ConnectorManager(IOptions<EdgeConfig> cfg, NormalizerService normalizer, IServiceProvider serviceProvider, ILogger<ConnectorManager> log)
    {
        _cfg = cfg.Value; 
        _normalizer = normalizer;
        _serviceProvider = serviceProvider;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ConnectorManager starting - loading real machine connectors");
        
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _managerCts.Token);
        
        try
        {
            // Load connectors from configuration
            await LoadConnectorsFromConfig(linkedCts.Token);
            
            // Start all connectors
            await StartAllConnectors(linkedCts.Token);
            
            // Monitor connector health
            await MonitorConnectorHealth(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("ConnectorManager shutdown requested");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ConnectorManager execution failed");
        }
        finally
        {
            await StopAllConnectors();
        }
    }

    private async Task LoadConnectorsFromConfig(CancellationToken ct)
    {
        // For now, create sample machine configurations
        // In production, this would load from appsettings.json or external config
        var machines = new[]
        {
            new EdgeConnectorConfig
            {
                MachineId = "FANUC-TC-01",
                Make = "FANUC",
                Model = "Series-0i-TF",
                IpAddress = "10.10.20.11",
                Protocol = "FOCAS",
                Settings = new Dictionary<string, object>
                {
                    { "Port", 8193 },
                    { "IsSimulator", true },
                    { "PollIntervalMs", 250 }
                }
            },
            new EdgeConnectorConfig
            {
                MachineId = "SIEMENS-TC-02", 
                Make = "SIEMENS",
                Model = "SINUMERIK-840D",
                IpAddress = "10.10.20.12",
                Protocol = "OPC UA",
                Settings = new Dictionary<string, object>
                {
                    { "EndpointUrl", "opc.tcp://10.10.20.12:4840" },
                    { "IsSimulator", true },
                    { "SecurityPolicy", "None" }
                }
            },
            new EdgeConnectorConfig
            {
                MachineId = "HAAS-MILL-03",
                Make = "HAAS", 
                Model = "VF-2SS",
                IpAddress = "10.10.20.13",
                Protocol = "MTConnect",
                Settings = new Dictionary<string, object>
                {
                    { "BaseUrl", "http://10.10.20.13:8082/VF2SS" },
                    { "IsSimulator", true },
                    { "SampleIntervalMs", 500 }
                }
            },
            new EdgeConnectorConfig
            {
                MachineId = "MAZAK-5X-04",
                Make = "MAZAK",
                Model = "VARIAXIS i-700", 
                IpAddress = "10.10.20.14",
                Protocol = "MTConnect",
                Settings = new Dictionary<string, object>
                {
                    { "BaseUrl", "http://10.10.20.14:5000/MAZAK" },
                    { "IsSimulator", true },
                    { "SampleIntervalMs", 500 }
                }
            }
        };

        foreach (var machineConfig in machines)
        {
            try
            {
                var connector = CreateConnector(machineConfig);
                if (connector != null)
                {
                    _connectors[machineConfig.MachineId] = connector;
                    _log.LogInformation("Loaded connector for {MachineId} ({Protocol})", 
                        machineConfig.MachineId, machineConfig.Protocol);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to create connector for {MachineId}", machineConfig.MachineId);
            }
        }
        
        _log.LogInformation("Loaded {Count} connectors", _connectors.Count);
    }

    private IEdgeConnectorAdapter? CreateConnector(EdgeConnectorConfig config)
    {
        var loggerFactory = _serviceProvider.GetService<ILoggerFactory>() ?? new LoggerFactory();
        
        return config.Make.ToUpperInvariant() switch
        {
            "FANUC" => new FanucEdgeAdapter(config, _cfg.SiteId, 
                loggerFactory.CreateLogger<FanucEdgeAdapter>()),
            "SIEMENS" => new SiemensEdgeAdapter(config, _cfg.SiteId,
                loggerFactory.CreateLogger<SiemensEdgeAdapter>()),
            "HAAS" => new HaasEdgeAdapter(config, _cfg.SiteId,
                loggerFactory.CreateLogger<HaasEdgeAdapter>()),
            "MAZAK" => new MazakEdgeAdapter(config, _cfg.SiteId,
                loggerFactory.CreateLogger<MazakEdgeAdapter>()),
            _ => null
        };
    }

    private async Task StartAllConnectors(CancellationToken ct)
    {
        var startTasks = _connectors.Values.Select(c => StartConnectorWithEventProcessing(c, ct));
        await Task.WhenAll(startTasks);
        
        _log.LogInformation("Started {Count} connectors", _connectors.Count);
    }

    private async Task StartConnectorWithEventProcessing(IEdgeConnectorAdapter connector, CancellationToken ct)
    {
        try
        {
            await connector.StartAsync(ct);
            
            // Start event processing for this connector
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var machineEvent in connector.GetEventsAsync(ct))
                    {
                        await _normalizer.EnqueueAsync(machineEvent, ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error processing events from {MachineId}", connector.MachineId);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to start connector {MachineId}", connector.MachineId);
        }
    }

    private async Task MonitorConnectorHealth(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var kvp in _connectors)
            {
                try
                {
                    var health = await kvp.Value.CheckHealthAsync(ct);
                    if (!health.IsHealthy)
                    {
                        _log.LogWarning("Connector {MachineId} unhealthy: {Message}", 
                            kvp.Key, health.Message);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Health check failed for {MachineId}", kvp.Key);
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private async Task StopAllConnectors()
    {
        var stopTasks = _connectors.Values.Select(async c =>
        {
            try
            {
                await c.StopAsync(CancellationToken.None);
                c.Dispose();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error stopping connector {MachineId}", c.MachineId);
            }
        });
        
        await Task.WhenAll(stopTasks);
        _connectors.Clear();
        _log.LogInformation("Stopped all connectors");
    }

    public override void Dispose()
    {
        _managerCts?.Cancel();
        _managerCts?.Dispose();
        base.Dispose();
    }
}
