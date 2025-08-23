using MAK3R.Connectors.Abstractions;
using MAK3R.Core;

namespace MAK3R.Connectors;

public interface IConnectorHub
{
    /// <summary>
    /// Get all registered connectors
    /// </summary>
    IEnumerable<IConnector> GetConnectors();
    
    /// <summary>
    /// Get a specific connector by ID
    /// </summary>
    Result<IConnector> GetConnector(string connectorId);
    
    /// <summary>
    /// Register a new connector
    /// </summary>
    ValueTask<Result<bool>> RegisterConnectorAsync(IConnector connector);
    
    /// <summary>
    /// Check health of all connectors
    /// </summary>
    IAsyncEnumerable<ConnectorHealthStatus> CheckAllHealthAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Check health of a specific connector
    /// </summary>
    ValueTask<Result<ConnectorHealthStatus>> CheckHealthAsync(string connectorId, CancellationToken ct = default);
    
    /// <summary>
    /// Sync data from all enabled connectors since last sync
    /// </summary>
    IAsyncEnumerable<ConnectorSyncResult> SyncAllAsync(DateTime? since = null, CancellationToken ct = default);
    
    /// <summary>
    /// Sync data from a specific connector
    /// </summary>
    ValueTask<Result<ConnectorSyncResult>> SyncConnectorAsync(string connectorId, DateTime? since = null, CancellationToken ct = default);
}

public record ConnectorHealthStatus(
    string ConnectorId,
    string ConnectorName,
    string Type,
    bool IsHealthy,
    string? Message = null,
    Dictionary<string, object>? Metadata = null,
    DateTime CheckedAt = default
)
{
    public DateTime CheckedAt { get; init; } = CheckedAt == default ? DateTime.UtcNow : CheckedAt;
}

public record ConnectorSyncResult(
    string ConnectorId,
    string ConnectorName,
    bool IsSuccess,
    int EventsProcessed,
    TimeSpan Duration,
    string? Error = null,
    DateTime SyncedAt = default
)
{
    public DateTime SyncedAt { get; init; } = SyncedAt == default ? DateTime.UtcNow : SyncedAt;
}