using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MAK3R.Connectors.Abstractions;
using MAK3R.Core;

namespace MAK3R.Connectors;

public class ConnectorHub : IConnectorHub
{
    private readonly ConcurrentDictionary<string, IConnector> _connectors = new();
    private readonly IConnectorRegistry _registry;
    private readonly ILogger<ConnectorHub> _logger;

    public ConnectorHub(IConnectorRegistry registry, ILogger<ConnectorHub> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public IEnumerable<IConnector> GetConnectors()
    {
        return _connectors.Values.ToArray();
    }

    public Result<IConnector> GetConnector(string connectorId)
    {
        if (string.IsNullOrWhiteSpace(connectorId))
        {
            return Result<IConnector>.Failure("Connector ID cannot be empty");
        }

        return _connectors.TryGetValue(connectorId, out var connector)
            ? Result<IConnector>.Success(connector)
            : Result<IConnector>.Failure($"Connector '{connectorId}' not found");
    }

    public ValueTask<Result<bool>> RegisterConnectorAsync(IConnector connector)
    {
        if (connector == null)
        {
            return ValueTask.FromResult(Result<bool>.Failure("Connector cannot be null"));
        }

        if (string.IsNullOrWhiteSpace(connector.Id))
        {
            return ValueTask.FromResult(Result<bool>.Failure("Connector ID cannot be empty"));
        }

        try
        {
            var added = _connectors.TryAdd(connector.Id, connector);
            if (added)
            {
                _logger.LogInformation("Registered connector {ConnectorId} ({ConnectorName}) of type {ConnectorType}",
                    connector.Id, connector.Name, connector.Type);
                return ValueTask.FromResult(Result<bool>.Success(true));
            }
            else
            {
                _logger.LogWarning("Connector {ConnectorId} is already registered", connector.Id);
                return ValueTask.FromResult(Result<bool>.Failure($"Connector '{connector.Id}' is already registered"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register connector {ConnectorId}", connector.Id);
            return ValueTask.FromResult(Result<bool>.Failure($"Failed to register connector: {ex.Message}", ex));
        }
    }

    public async IAsyncEnumerable<ConnectorHealthStatus> CheckAllHealthAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var connectors = GetConnectors().ToArray();
        _logger.LogDebug("Checking health of {ConnectorCount} connectors", connectors.Length);

        foreach (var connector in connectors)
        {
            if (ct.IsCancellationRequested)
                yield break;

            var healthResult = await CheckHealthAsync(connector.Id, ct);
            if (healthResult.IsSuccess)
            {
                yield return healthResult.Value!;
            }
            else
            {
                yield return new ConnectorHealthStatus(
                    connector.Id,
                    connector.Name,
                    connector.Type,
                    IsHealthy: false,
                    Message: healthResult.Error
                );
            }
        }
    }

    public async ValueTask<Result<ConnectorHealthStatus>> CheckHealthAsync(string connectorId, CancellationToken ct = default)
    {
        var connectorResult = GetConnector(connectorId);
        if (!connectorResult.IsSuccess)
        {
            return Result<ConnectorHealthStatus>.Failure(connectorResult.Error!);
        }

        var connector = connectorResult.Value!;
        
        try
        {
            _logger.LogDebug("Checking health of connector {ConnectorId}", connectorId);
            var checkResult = await connector.CheckAsync(ct);
            
            var status = new ConnectorHealthStatus(
                connector.Id,
                connector.Name,
                connector.Type,
                checkResult.IsHealthy,
                checkResult.Message,
                checkResult.Metadata
            );

            _logger.LogDebug("Connector {ConnectorId} health check completed. Healthy: {IsHealthy}",
                connectorId, checkResult.IsHealthy);

            return Result<ConnectorHealthStatus>.Success(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for connector {ConnectorId}", connectorId);
            
            var status = new ConnectorHealthStatus(
                connector.Id,
                connector.Name,
                connector.Type,
                IsHealthy: false,
                Message: $"Health check failed: {ex.Message}"
            );

            return Result<ConnectorHealthStatus>.Success(status);
        }
    }

    public async IAsyncEnumerable<ConnectorSyncResult> SyncAllAsync(DateTime? since = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var connectors = GetConnectors().ToArray();
        var syncSince = since ?? DateTime.UtcNow.AddHours(-24); // Default to last 24 hours
        
        _logger.LogInformation("Starting sync for {ConnectorCount} connectors since {SyncSince}", 
            connectors.Length, syncSince);

        foreach (var connector in connectors)
        {
            if (ct.IsCancellationRequested)
                yield break;

            var syncResult = await SyncConnectorAsync(connector.Id, syncSince, ct);
            if (syncResult.IsSuccess)
            {
                yield return syncResult.Value!;
            }
            else
            {
                yield return new ConnectorSyncResult(
                    connector.Id,
                    connector.Name,
                    IsSuccess: false,
                    EventsProcessed: 0,
                    Duration: TimeSpan.Zero,
                    Error: syncResult.Error
                );
            }
        }
    }

    public async ValueTask<Result<ConnectorSyncResult>> SyncConnectorAsync(string connectorId, DateTime? since = null, CancellationToken ct = default)
    {
        var connectorResult = GetConnector(connectorId);
        if (!connectorResult.IsSuccess)
        {
            return Result<ConnectorSyncResult>.Failure(connectorResult.Error!);
        }

        var connector = connectorResult.Value!;
        var syncSince = since ?? DateTime.UtcNow.AddHours(-24);
        var stopwatch = Stopwatch.StartNew();
        int eventCount = 0;

        try
        {
            _logger.LogInformation("Starting sync for connector {ConnectorId} since {SyncSince}", 
                connectorId, syncSince);

            await foreach (var upsertEvent in connector.PullAsync(syncSince, ct))
            {
                if (ct.IsCancellationRequested)
                    break;

                // Process the event (in a real implementation, this would integrate with the digital twin)
                _logger.LogDebug("Processing {EntityType} event from {ConnectorId}: {ExternalId}",
                    upsertEvent.EntityType, connectorId, upsertEvent.ExternalId);
                
                eventCount++;
            }

            stopwatch.Stop();
            var result = new ConnectorSyncResult(
                connector.Id,
                connector.Name,
                IsSuccess: true,
                EventsProcessed: eventCount,
                Duration: stopwatch.Elapsed
            );

            _logger.LogInformation("Sync completed for connector {ConnectorId}. Events processed: {EventCount}, Duration: {Duration}ms",
                connectorId, eventCount, stopwatch.ElapsedMilliseconds);

            return Result<ConnectorSyncResult>.Success(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Sync failed for connector {ConnectorId} after processing {EventCount} events",
                connectorId, eventCount);

            var result = new ConnectorSyncResult(
                connector.Id,
                connector.Name,
                IsSuccess: false,
                EventsProcessed: eventCount,
                Duration: stopwatch.Elapsed,
                Error: ex.Message
            );

            return Result<ConnectorSyncResult>.Success(result);
        }
    }
}