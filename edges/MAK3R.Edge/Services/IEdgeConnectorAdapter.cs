using Mak3r.Edge.Models;
using MAK3R.Connectors.Abstractions;

namespace Mak3r.Edge.Services;

/// <summary>
/// Adapter interface to bridge existing MCP connectors with Edge runtime
/// </summary>
public interface IEdgeConnectorAdapter : IDisposable
{
    string Id { get; }
    string MachineId { get; }
    string Protocol { get; }
    bool IsConnected { get; }
    
    /// <summary>
    /// Initialize and start the connector
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop and clean up the connector
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check connector health status
    /// </summary>
    Task<ConnectorCheck> CheckHealthAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stream of normalized KMachineEvents from this machine
    /// </summary>
    IAsyncEnumerable<KMachineEvent> GetEventsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for an Edge connector adapter
/// </summary>
public class EdgeConnectorConfig
{
    public string MachineId { get; set; } = "";
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string Protocol { get; set; } = "";
    public Dictionary<string, object> Settings { get; set; } = new();
}