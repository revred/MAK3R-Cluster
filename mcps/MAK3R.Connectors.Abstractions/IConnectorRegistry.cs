using MAK3R.Core;

namespace MAK3R.Connectors.Abstractions;

/// <summary>
/// Registry for discovering and managing available connector types
/// </summary>
public interface IConnectorRegistry
{
    /// <summary>
    /// Registers a connector type that can be instantiated
    /// </summary>
    Result<bool> RegisterConnectorType<T>(string connectorTypeId, string displayName, string description) where T : class, IConnector;
    
    /// <summary>
    /// Gets all available connector types
    /// </summary>
    IEnumerable<ConnectorTypeInfo> GetAvailableConnectorTypes();
    
    /// <summary>
    /// Creates a connector instance from configuration
    /// </summary>
    ValueTask<Result<IConnector>> CreateConnectorAsync(string connectorTypeId, ConnectorConfiguration configuration, CancellationToken ct = default);
    
    /// <summary>
    /// Gets the configuration schema for a connector type
    /// </summary>
    ValueTask<Result<ConnectorConfigurationSchema>> GetConfigurationSchemaAsync(string connectorTypeId, CancellationToken ct = default);
}

/// <summary>
/// Information about an available connector type
/// </summary>
public record ConnectorTypeInfo(
    string TypeId,
    string DisplayName, 
    string Description,
    string Category,
    string Version,
    bool IsAvailable,
    Dictionary<string, object>? Metadata = null
);

/// <summary>
/// JSON schema for connector configuration
/// </summary>
public record ConnectorConfigurationSchema(
    string ConnectorTypeId,
    string JsonSchema,
    Dictionary<string, ConfigurationField> Fields,
    string[] RequiredFields
);

/// <summary>
/// Configuration field definition
/// </summary>
public record ConfigurationField(
    string Name,
    string Type, // "string", "number", "boolean", "array", "object"
    string DisplayName,
    string? Description = null,
    object? DefaultValue = null,
    bool IsRequired = false,
    bool IsSecret = false,
    string[]? EnumValues = null,
    Dictionary<string, object>? Validation = null
);