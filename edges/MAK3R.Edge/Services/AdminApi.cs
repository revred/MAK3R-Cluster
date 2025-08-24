using Mak3r.Edge.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Mak3r.Edge.Services;

public class AdminApi : BackgroundService
{
    private readonly EdgeConfig _cfg;
    private readonly QueueService _queue;
    private readonly NetDiagDb _db;
    private readonly ILogger<AdminApi> _log;
    private readonly IServiceProvider _serviceProvider;
    private IHost? _web;

    public AdminApi(IOptions<EdgeConfig> cfg, QueueService queue, NetDiagDb db, IServiceProvider serviceProvider, ILogger<AdminApi> log)
    { 
        _cfg = cfg.Value; 
        _queue = queue; 
        _db = db; 
        _serviceProvider = serviceProvider;
        _log = log; 
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(_cfg.AdminApi.Listen);
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });
        
        var app = builder.Build();
        app.UseCors();

        // Basic health and metrics endpoints
        app.MapGet("/health", () => Results.Ok(new { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            siteId = _cfg.SiteId,
            version = "v1.0"
        }));

        app.MapGet("/metrics", () => Results.Ok(new { 
            queueDepth = _queue.ApproxDepth,
            queueCapacity = _cfg.Queue.Capacity,
            uptimeSeconds = Environment.TickCount64 / 1000
        }));

        app.MapGet("/config", () => Results.Ok(new {
            siteId = _cfg.SiteId,
            timezone = _cfg.Timezone,
            uplink = new {
                hubUrl = _cfg.Uplink.HubUrl,
                batchSize = _cfg.Uplink.Batch.MaxEvents
            },
            loadGen = new {
                enabled = _cfg.LoadGen.Enabled,
                machines = _cfg.LoadGen.Machines
            }
        }));

        // Network diagnostics endpoints
        app.MapGet("/netdiag/phases", async () => 
        {
            var phases = await _db.GetRecentNetPhasesAsync(100);
            return Results.Ok(phases);
        });

        app.MapGet("/netdiag/batches", async () => 
        {
            var batches = await _db.GetRecentBatchesAsync(50);
            return Results.Ok(batches);
        });

        app.MapGet("/netdiag/stats", async () => 
        {
            var stats = await _db.GetNetworkStatsAsync();
            return Results.Ok(stats);
        });

        // Connector status endpoints
        app.MapGet("/connectors", async () => 
        {
            var discoveryService = _serviceProvider.GetService<ConnectorDiscoveryService>();
            var configService = _serviceProvider.GetService<ConfigService>();
            
            if (discoveryService != null && configService != null)
            {
                var machines = configService.GetMachinesConfig() ?? new List<EdgeConnectorConfig>();
                var statuses = await discoveryService.GetConnectorStatusAsync(machines);
                return Results.Ok(statuses);
            }
            
            return Results.Ok(new { 
                message = "Discovery service not available",
                loadedConnectors = 0,
                healthyConnectors = 0
            });
        });

        app.MapGet("/connectors/{machineId}/health", async (string machineId) => 
        {
            var discoveryService = _serviceProvider.GetService<ConnectorDiscoveryService>();
            var configService = _serviceProvider.GetService<ConfigService>();
            
            if (discoveryService != null && configService != null)
            {
                var machines = configService.GetMachinesConfig() ?? new List<EdgeConnectorConfig>();
                var machine = machines.FirstOrDefault(m => m.MachineId == machineId);
                
                if (machine != null)
                {
                    var connectivity = await discoveryService.ValidateConnectivityAsync(machine);
                    return Results.Ok(new {
                        machineId = connectivity.MachineId,
                        isHealthy = connectivity.ProtocolConnectivity == ConnectivityStatus.Connected,
                        lastCheck = DateTime.UtcNow,
                        protocol = connectivity.Protocol,
                        pingLatencyMs = connectivity.PingLatencyMs,
                        errorMessage = connectivity.ErrorMessage
                    });
                }
            }
            
            return Results.NotFound(new { message = $"Machine {machineId} not found" });
        });

        // Discovery endpoints
        app.MapPost("/discover", async (DiscoveryRequest request) =>
        {
            var discoveryService = _serviceProvider.GetService<ConnectorDiscoveryService>();
            if (discoveryService == null)
                return Results.ServiceUnavailable();

            var discovered = await discoveryService.DiscoverMachinesAsync(
                request.NetworkRange ?? "10.10.20.0/24");
            
            return Results.Ok(new { 
                networkRange = request.NetworkRange,
                discovered = discovered.Count,
                machines = discovered
            });
        });

        app.MapPost("/connectors/auto-register", async (AutoRegisterRequest request) =>
        {
            var discoveryService = _serviceProvider.GetService<ConnectorDiscoveryService>();
            if (discoveryService == null)
                return Results.ServiceUnavailable();

            var config = await discoveryService.AutoRegisterMachineAsync(request.IpAddress);
            if (config == null)
                return Results.NotFound(new { message = "No machine discovered at specified address" });

            return Results.Ok(new { 
                message = "Machine auto-registered successfully",
                machine = config,
                note = "Machine is disabled by default - enable after reviewing configuration"
            });
        });

        app.MapPost("/connectors/validate", async (ValidateConnectivityRequest request) =>
        {
            var discoveryService = _serviceProvider.GetService<ConnectorDiscoveryService>();
            if (discoveryService == null)
                return Results.ServiceUnavailable();

            var machine = new EdgeConnectorConfig
            {
                MachineId = request.MachineId,
                IpAddress = request.IpAddress,
                Protocol = request.Protocol,
                Settings = request.Settings
            };

            var result = await discoveryService.ValidateConnectivityAsync(machine);
            return Results.Ok(result);
        });

        // Event streaming endpoint for debugging
        app.MapGet("/events/recent", async () => 
        {
            var recentEvents = await _db.GetRecentEventsAsync(20);
            return Results.Ok(recentEvents);
        });

        // Configuration update endpoints
        app.MapPost("/config/sampling", async (SamplingConfigRequest request) => 
        {
            _log.LogInformation("Sampling configuration update requested: {MachineId} -> {IntervalMs}ms", 
                request.MachineId, request.IntervalMs);
            
            // Would forward to ConnectorManager to update sampling rates
            return Results.Ok(new { applied = true, timestamp = DateTime.UtcNow });
        });

        // Spool management endpoints
        app.MapGet("/spool", () => 
        {
            var spoolDir = Path.Combine(_cfg.Storage.Root, "spool");
            if (!Directory.Exists(spoolDir))
                return Results.Ok(new { spooledBatches = 0, totalSizeBytes = 0 });
            
            var files = Directory.GetFiles(spoolDir, "*.mpack");
            var totalSize = files.Sum(f => new FileInfo(f).Length);
            
            return Results.Ok(new { 
                spooledBatches = files.Length,
                totalSizeBytes = totalSize,
                oldestFile = files.Length > 0 ? Path.GetFileName(files.OrderBy(f => new FileInfo(f).CreationTime).First()) : null
            });
        });

        app.MapDelete("/spool", () => 
        {
            var spoolDir = Path.Combine(_cfg.Storage.Root, "spool");
            if (Directory.Exists(spoolDir))
            {
                var files = Directory.GetFiles(spoolDir, "*.mpack");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                _log.LogWarning("Cleared spool directory - {Count} files deleted", files.Length);
                return Results.Ok(new { deleted = files.Length });
            }
            return Results.Ok(new { deleted = 0 });
        });

        _web = app;
        _log.LogInformation("Edge Admin API listening on {url}", _cfg.AdminApi.Listen);
        await app.RunAsync(stoppingToken);
    }
}

public record SamplingConfigRequest(string MachineId, int IntervalMs);

public record DiscoveryRequest(string? NetworkRange = null);

public record AutoRegisterRequest(string IpAddress);

public record ValidateConnectivityRequest(
    string MachineId,
    string IpAddress, 
    string Protocol,
    Dictionary<string, object> Settings);
