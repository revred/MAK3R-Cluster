using MAK3R.Connectors.Abstractions;
using MAK3R.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace MAK3R.Connectors;

/// <summary>
/// Registry for managing connector types and creating instances
/// </summary>
public class ConnectorRegistry : IConnectorRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConnectorRegistry> _logger;
    private readonly ConcurrentDictionary<string, ConnectorTypeRegistration> _registrations = new();

    public ConnectorRegistry(IServiceProvider serviceProvider, ILogger<ConnectorRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Result<bool> RegisterConnectorType<T>(string connectorTypeId, string displayName, string description) where T : class, IConnector
    {
        try
        {
            var connectorType = typeof(T);
            var factoryType = typeof(IConnectorFactory<T>);
            
            var registration = new ConnectorTypeRegistration(
                connectorTypeId,
                displayName,
                description,
                connectorType,
                factoryType,
                GetConnectorCategory(connectorType),
                GetConnectorVersion(connectorType)
            );

            if (_registrations.TryAdd(connectorTypeId, registration))
            {
                _logger.LogInformation("Registered connector type: {ConnectorTypeId} ({DisplayName})", 
                    connectorTypeId, displayName);
                return Result<bool>.Success(true);
            }
            else
            {
                _logger.LogWarning("Connector type already registered: {ConnectorTypeId}", connectorTypeId);
                return Result<bool>.Failure($"Connector type '{connectorTypeId}' is already registered");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register connector type: {ConnectorTypeId}", connectorTypeId);
            return Result<bool>.Failure($"Registration failed: {ex.Message}");
        }
    }

    public IEnumerable<ConnectorTypeInfo> GetAvailableConnectorTypes()
    {
        return _registrations.Values.Select(reg => new ConnectorTypeInfo(
            reg.TypeId,
            reg.DisplayName,
            reg.Description,
            reg.Category,
            reg.Version,
            IsConnectorAvailable(reg),
            new Dictionary<string, object>
            {
                { "ConnectorType", reg.ConnectorType.Name },
                { "Assembly", reg.ConnectorType.Assembly.GetName().Name ?? "Unknown" }
            }
        ));
    }

    public async ValueTask<Result<IConnector>> CreateConnectorAsync(string connectorTypeId, ConnectorConfiguration configuration, CancellationToken ct = default)
    {
        if (!_registrations.TryGetValue(connectorTypeId, out var registration))
        {
            return Result<IConnector>.Failure($"Connector type '{connectorTypeId}' is not registered");
        }

        try
        {
            _logger.LogDebug("Creating connector instance: {ConnectorTypeId}", connectorTypeId);

            // Get the factory from DI container
            var factory = _serviceProvider.GetService(registration.FactoryType);
            if (factory == null)
            {
                return Result<IConnector>.Failure($"Factory not registered for connector type '{connectorTypeId}'");
            }

            // Validate configuration
            var validateMethod = registration.FactoryType.GetMethod("ValidateConfiguration");
            if (validateMethod != null)
            {
                var validationResult = (Result<bool>?)validateMethod.Invoke(factory, new object[] { configuration });
                if (validationResult?.IsSuccess == false)
                {
                    return Result<IConnector>.Failure($"Configuration validation failed: {validationResult.Error}");
                }
            }

            // Create connector instance
            var createMethod = registration.FactoryType.GetMethod("CreateAsync");
            if (createMethod == null)
            {
                return Result<IConnector>.Failure($"CreateAsync method not found on factory for '{connectorTypeId}'");
            }

            var task = (ValueTask<Result<IConnector>>?)createMethod.Invoke(factory, new object[] { configuration, ct });
            if (task == null)
            {
                return Result<IConnector>.Failure($"Failed to invoke CreateAsync for '{connectorTypeId}'");
            }

            var result = await task.Value;
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully created connector: {ConnectorId} (Type: {ConnectorTypeId})", 
                    configuration.ConnectorId, connectorTypeId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connector: {ConnectorTypeId}", connectorTypeId);
            return Result<IConnector>.Failure($"Connector creation failed: {ex.Message}");
        }
    }

    public async ValueTask<Result<ConnectorConfigurationSchema>> GetConfigurationSchemaAsync(string connectorTypeId, CancellationToken ct = default)
    {
        if (!_registrations.TryGetValue(connectorTypeId, out var registration))
        {
            return Result<ConnectorConfigurationSchema>.Failure($"Connector type '{connectorTypeId}' is not registered");
        }

        try
        {
            var factory = _serviceProvider.GetService(registration.FactoryType);
            if (factory == null)
            {
                return Result<ConnectorConfigurationSchema>.Failure($"Factory not registered for connector type '{connectorTypeId}'");
            }

            var getSchemaMethod = registration.FactoryType.GetMethod("GetConfigurationSchemaAsync");
            if (getSchemaMethod == null)
            {
                return Result<ConnectorConfigurationSchema>.Failure($"GetConfigurationSchemaAsync method not found on factory for '{connectorTypeId}'");
            }

            var task = (ValueTask<ConnectorConfigurationSchema>?)getSchemaMethod.Invoke(factory, Array.Empty<object>());
            if (task == null)
            {
                return Result<ConnectorConfigurationSchema>.Failure($"Failed to invoke GetConfigurationSchemaAsync for '{connectorTypeId}'");
            }

            var schema = await task.Value;
            return Result<ConnectorConfigurationSchema>.Success(schema);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration schema: {ConnectorTypeId}", connectorTypeId);
            return Result<ConnectorConfigurationSchema>.Failure($"Schema retrieval failed: {ex.Message}");
        }
    }

    private string GetConnectorCategory(Type connectorType)
    {
        // Fallback to name-based categorization
        var typeName = connectorType.Name.ToLower();
        return typeName switch
        {
            var n when n.Contains("shopify") => "shopify",
            var n when n.Contains("netsuite") => "netsuite",
            var n when n.Contains("opcua") || n.Contains("opc") => "opcua",
            var n when n.Contains("file") => "file",
            var n when n.Contains("rest") || n.Contains("api") => "rest-api",
            var n when n.Contains("database") || n.Contains("db") => "database",
            _ => "unknown"
        };
    }

    private string GetConnectorVersion(Type connectorType)
    {
        try
        {
            var assembly = connectorType.Assembly;
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    private bool IsConnectorAvailable(ConnectorTypeRegistration registration)
    {
        try
        {
            // Check if the factory is registered in DI
            var factory = _serviceProvider.GetService(registration.FactoryType);
            return factory != null;
        }
        catch
        {
            return false;
        }
    }

    private record ConnectorTypeRegistration(
        string TypeId,
        string DisplayName,
        string Description,
        Type ConnectorType,
        Type FactoryType,
        string Category,
        string Version
    );
}