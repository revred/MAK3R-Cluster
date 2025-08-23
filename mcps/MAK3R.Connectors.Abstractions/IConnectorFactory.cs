using MAK3R.Core;

namespace MAK3R.Connectors.Abstractions;

/// <summary>
/// Factory interface for creating connector instances
/// </summary>
public interface IConnectorFactory<T> where T : class, IConnector
{
    /// <summary>
    /// Creates a connector instance from configuration
    /// </summary>
    ValueTask<Result<T>> CreateAsync(ConnectorConfiguration configuration, CancellationToken ct = default);
    
    /// <summary>
    /// Gets the configuration schema for this connector type
    /// </summary>
    ValueTask<ConnectorConfigurationSchema> GetConfigurationSchemaAsync();
    
    /// <summary>
    /// Validates a configuration before creating the connector
    /// </summary>
    Result<bool> ValidateConfiguration(ConnectorConfiguration configuration);
}

/// <summary>
/// Base factory implementation with common functionality
/// </summary>
public abstract class ConnectorFactoryBase<T> : IConnectorFactory<T> where T : class, IConnector
{
    public abstract ValueTask<Result<T>> CreateAsync(ConnectorConfiguration configuration, CancellationToken ct = default);
    
    public abstract ValueTask<ConnectorConfigurationSchema> GetConfigurationSchemaAsync();
    
    public virtual Result<bool> ValidateConfiguration(ConnectorConfiguration configuration)
    {
        if (configuration == null)
            return Result<bool>.Failure("Configuration cannot be null");
            
        if (string.IsNullOrWhiteSpace(configuration.ConnectorId))
            return Result<bool>.Failure("ConnectorId is required");
            
        if (configuration.Settings == null)
            return Result<bool>.Failure("Settings cannot be null");
            
        return Result<bool>.Success(true);
    }
}