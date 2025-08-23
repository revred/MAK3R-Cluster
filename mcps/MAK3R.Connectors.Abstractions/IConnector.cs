using System.Text.Json;

namespace MAK3R.Connectors.Abstractions;

public interface IConnector
{
    string Id { get; }
    string Name { get; }
    string Type { get; }
    
    ValueTask<ConnectorCheck> CheckAsync(CancellationToken ct = default);
    IAsyncEnumerable<UpsertEvent> PullAsync(DateTime since, CancellationToken ct = default);
    ValueTask<ConnectorConfiguration> GetConfigurationSchemaAsync();
}

public record ConnectorCheck(
    bool IsHealthy,
    string? Message,
    Dictionary<string, object>? Metadata = null
);

public record UpsertEvent(
    string EntityType,
    string ExternalId,
    JsonElement Payload,
    DateTime Timestamp
);

public record ConnectorConfiguration(
    string ConnectorId,
    string Type,
    Dictionary<string, object> Settings,
    bool IsEnabled = true
);

public record ExternalRef(
    string ConnectorId,
    string ExternalId,
    string EntityType
);

