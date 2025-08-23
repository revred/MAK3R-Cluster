using MAK3R.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MAK3R.Connectors;

public class ConnectorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConnectorHostedService> _logger;
    private readonly PeriodicTimer _syncTimer;
    private readonly PeriodicTimer _healthTimer;

    public ConnectorHostedService(
        IServiceProvider serviceProvider,
        ILogger<ConnectorHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Sync every 5 minutes, health check every 1 minute
        _syncTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        _healthTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Connector hosted service started");
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var connectorHub = scope.ServiceProvider.GetRequiredService<IConnectorHub>();

            // Start both timers concurrently
            var syncTask = RunSyncLoop(connectorHub, stoppingToken);
            var healthTask = RunHealthCheckLoop(connectorHub, stoppingToken);

            await Task.WhenAny(syncTask, healthTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in connector hosted service");
        }
    }


    private async Task RunSyncLoop(IConnectorHub connectorHub, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _syncTimer.WaitForNextTickAsync(stoppingToken);
                
                var connectors = connectorHub.GetConnectors().ToArray();
                if (connectors.Length == 0)
                {
                    _logger.LogDebug("No connectors registered, skipping sync");
                    continue;
                }

                _logger.LogDebug("Starting periodic sync for {ConnectorCount} connectors", connectors.Length);
                
                await foreach (var result in connectorHub.SyncAllAsync(DateTime.UtcNow.AddMinutes(-10), stoppingToken))
                {
                    if (result.IsSuccess)
                    {
                        _logger.LogDebug("Sync completed for {ConnectorName}: {EventCount} events in {Duration}ms",
                            result.ConnectorName, result.EventsProcessed, result.Duration.TotalMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning("Sync failed for {ConnectorName}: {Error}",
                            result.ConnectorName, result.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic sync");
            }
        }
    }

    private async Task RunHealthCheckLoop(IConnectorHub connectorHub, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _healthTimer.WaitForNextTickAsync(stoppingToken);
                
                var connectors = connectorHub.GetConnectors().ToArray();
                if (connectors.Length == 0)
                {
                    continue;
                }

                _logger.LogDebug("Starting periodic health check for {ConnectorCount} connectors", connectors.Length);
                
                await foreach (var status in connectorHub.CheckAllHealthAsync(stoppingToken))
                {
                    if (!status.IsHealthy)
                    {
                        _logger.LogWarning("Connector {ConnectorName} is unhealthy: {Message}",
                            status.ConnectorName, status.Message);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic health check");
            }
        }
    }

    public override void Dispose()
    {
        _syncTimer?.Dispose();
        _healthTimer?.Dispose();
        base.Dispose();
    }
}