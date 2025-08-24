using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Mak3r.Edge.Models;

namespace Mak3r.Edge.Services;

public class ConnectorDiscoveryService
{
    private readonly EdgeConfig _cfg;
    private readonly ILogger<ConnectorDiscoveryService> _log;
    private readonly HttpClient _httpClient;

    public ConnectorDiscoveryService(IOptions<EdgeConfig> cfg, ILogger<ConnectorDiscoveryService> log)
    {
        _cfg = cfg.Value;
        _log = log;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// Discovers machines on the network using various protocols
    /// </summary>
    public async Task<List<DiscoveredMachine>> DiscoverMachinesAsync(string networkRange = "10.10.20.0/24", CancellationToken ct = default)
    {
        _log.LogInformation("Starting machine discovery on network range: {NetworkRange}", networkRange);
        
        var discovered = new List<DiscoveredMachine>();
        var ipAddresses = GetNetworkAddresses(networkRange);
        
        var discoveryTasks = ipAddresses.Select(ip => DiscoverMachineAtAddressAsync(ip, ct)).ToList();
        var results = await Task.WhenAll(discoveryTasks);
        
        discovered.AddRange(results.Where(r => r != null).Cast<DiscoveredMachine>());
        
        _log.LogInformation("Discovery completed: found {Count} machines", discovered.Count);
        return discovered;
    }

    /// <summary>
    /// Validates connectivity to a specific machine configuration
    /// </summary>
    public async Task<ConnectivityResult> ValidateConnectivityAsync(EdgeConnectorConfig machine, CancellationToken ct = default)
    {
        _log.LogDebug("Validating connectivity to {MachineId} at {IpAddress}", machine.MachineId, machine.IpAddress);

        var result = new ConnectivityResult
        {
            MachineId = machine.MachineId,
            IpAddress = machine.IpAddress,
            Protocol = machine.Protocol
        };

        try
        {
            // Basic ping test
            using var ping = new Ping();
            var pingReply = await ping.SendPingAsync(machine.IpAddress, 3000);
            result.PingSuccessful = pingReply.Status == IPStatus.Success;
            result.PingLatencyMs = pingReply.Status == IPStatus.Success ? (int)pingReply.RoundtripTime : null;

            if (result.PingSuccessful)
            {
                // Protocol-specific connectivity tests
                switch (machine.Protocol.ToUpper())
                {
                    case "FOCAS":
                        result.ProtocolConnectivity = await TestFocasConnectivity(machine, ct);
                        break;
                    case "OPC UA":
                        result.ProtocolConnectivity = await TestOpcUaConnectivity(machine, ct);
                        break;
                    case "MTCONNECT":
                        result.ProtocolConnectivity = await TestMTConnectConnectivity(machine, ct);
                        break;
                    default:
                        result.ProtocolConnectivity = ConnectivityStatus.Unknown;
                        break;
                }
            }
            else
            {
                result.ProtocolConnectivity = ConnectivityStatus.Failed;
                result.ErrorMessage = $"Ping failed: {pingReply.Status}";
            }
        }
        catch (Exception ex)
        {
            result.ProtocolConnectivity = ConnectivityStatus.Failed;
            result.ErrorMessage = ex.Message;
            _log.LogWarning(ex, "Connectivity test failed for {MachineId}", machine.MachineId);
        }

        return result;
    }

    /// <summary>
    /// Registers a machine automatically by probing its capabilities
    /// </summary>
    public async Task<EdgeConnectorConfig?> AutoRegisterMachineAsync(string ipAddress, CancellationToken ct = default)
    {
        _log.LogInformation("Attempting auto-registration for machine at {IpAddress}", ipAddress);

        var discovered = await DiscoverMachineAtAddressAsync(ipAddress, ct);
        if (discovered == null)
        {
            _log.LogWarning("No machine discovered at {IpAddress}", ipAddress);
            return null;
        }

        var machineId = $"{discovered.Make}-{GenerateShortId()}";
        var config = new EdgeConnectorConfig
        {
            MachineId = machineId,
            Make = discovered.Make,
            Model = discovered.Model ?? "Unknown",
            IpAddress = ipAddress,
            Protocol = discovered.Protocol,
            Enabled = false, // Start disabled for manual review
            Settings = discovered.Settings
        };

        _log.LogInformation("Auto-registered machine: {MachineId} ({Make} - {Protocol})", 
            machineId, discovered.Make, discovered.Protocol);

        return config;
    }

    /// <summary>
    /// Discovers all active connectors and their health status
    /// </summary>
    public async Task<List<ConnectorStatus>> GetConnectorStatusAsync(List<EdgeConnectorConfig> machines, CancellationToken ct = default)
    {
        var statuses = new List<ConnectorStatus>();
        
        var tasks = machines.Select(async machine =>
        {
            var connectivity = await ValidateConnectivityAsync(machine, ct);
            return new ConnectorStatus
            {
                MachineId = machine.MachineId,
                Make = machine.Make,
                Protocol = machine.Protocol,
                IpAddress = machine.IpAddress,
                Enabled = machine.Enabled,
                IsConnected = connectivity.ProtocolConnectivity == ConnectivityStatus.Connected,
                LastCheck = DateTime.UtcNow,
                PingLatencyMs = connectivity.PingLatencyMs,
                ErrorMessage = connectivity.ErrorMessage
            };
        });

        statuses.AddRange(await Task.WhenAll(tasks));
        return statuses;
    }

    private async Task<DiscoveredMachine?> DiscoverMachineAtAddressAsync(string ipAddress, CancellationToken ct)
    {
        // Try multiple discovery methods in parallel
        var discoveryTasks = new[]
        {
            DiscoverMTConnect(ipAddress, ct),
            DiscoverOpcUa(ipAddress, ct),
            DiscoverFocas(ipAddress, ct)
        };

        var results = await Task.WhenAll(discoveryTasks);
        return results.FirstOrDefault(r => r != null);
    }

    private async Task<DiscoveredMachine?> DiscoverMTConnect(string ipAddress, CancellationToken ct)
    {
        try
        {
            // Common MTConnect ports and paths
            var testUrls = new[]
            {
                $"http://{ipAddress}:8082/current",
                $"http://{ipAddress}:5000/current", 
                $"http://{ipAddress}/MTConnect/current",
                $"http://{ipAddress}:8080/current"
            };

            foreach (var url in testUrls)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        if (content.Contains("MTConnectStreams", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("Device", StringComparison.OrdinalIgnoreCase))
                        {
                            var make = ExtractMakeFromMTConnect(content);
                            var model = ExtractModelFromMTConnect(content);

                            return new DiscoveredMachine
                            {
                                IpAddress = ipAddress,
                                Protocol = "MTConnect",
                                Make = make ?? "Unknown",
                                Model = model,
                                Settings = new Dictionary<string, object>
                                {
                                    { "BaseUrl", url.Replace("/current", "") },
                                    { "IsSimulator", false },
                                    { "SampleIntervalMs", 1000 }
                                }
                            };
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    // Continue to next URL
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "MTConnect discovery failed for {IpAddress}", ipAddress);
        }

        return null;
    }

    private async Task<DiscoveredMachine?> DiscoverOpcUa(string ipAddress, CancellationToken ct)
    {
        try
        {
            // Common OPC UA ports
            var testPorts = new[] { 4840, 48010, 62541 };
            
            foreach (var port in testPorts)
            {
                if (await TestTcpPort(ipAddress, port, ct))
                {
                    // If port is open, assume OPC UA (more detailed discovery would require OPC UA client)
                    return new DiscoveredMachine
                    {
                        IpAddress = ipAddress,
                        Protocol = "OPC UA",
                        Make = "SIEMENS", // Common assumption - could be improved with actual OPC UA discovery
                        Settings = new Dictionary<string, object>
                        {
                            { "EndpointUrl", $"opc.tcp://{ipAddress}:{port}" },
                            { "IsSimulator", false },
                            { "SecurityPolicy", "None" }
                        }
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "OPC UA discovery failed for {IpAddress}", ipAddress);
        }

        return null;
    }

    private async Task<DiscoveredMachine?> DiscoverFocas(string ipAddress, CancellationToken ct)
    {
        try
        {
            // Common FOCAS ports
            var testPorts = new[] { 8193, 8194, 8195 };
            
            foreach (var port in testPorts)
            {
                if (await TestTcpPort(ipAddress, port, ct))
                {
                    return new DiscoveredMachine
                    {
                        IpAddress = ipAddress,
                        Protocol = "FOCAS",
                        Make = "FANUC",
                        Settings = new Dictionary<string, object>
                        {
                            { "Port", port },
                            { "IsSimulator", false },
                            { "PollIntervalMs", 500 }
                        }
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "FOCAS discovery failed for {IpAddress}", ipAddress);
        }

        return null;
    }

    private async Task<bool> TestTcpPort(string ipAddress, int port, CancellationToken ct)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(ipAddress, port).WaitAsync(TimeSpan.FromSeconds(2), ct);
            return tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }

    private async Task<ConnectivityStatus> TestFocasConnectivity(EdgeConnectorConfig machine, CancellationToken ct)
    {
        if (!machine.Settings.TryGetValue("Port", out var portObj) || portObj is not int port)
            return ConnectivityStatus.Failed;

        return await TestTcpPort(machine.IpAddress, port, ct) 
            ? ConnectivityStatus.Connected 
            : ConnectivityStatus.Failed;
    }

    private async Task<ConnectivityStatus> TestOpcUaConnectivity(EdgeConnectorConfig machine, CancellationToken ct)
    {
        if (!machine.Settings.TryGetValue("EndpointUrl", out var endpointObj) || endpointObj is not string endpoint)
            return ConnectivityStatus.Failed;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return ConnectivityStatus.Failed;

        return await TestTcpPort(uri.Host, uri.Port, ct) 
            ? ConnectivityStatus.Connected 
            : ConnectivityStatus.Failed;
    }

    private async Task<ConnectivityStatus> TestMTConnectConnectivity(EdgeConnectorConfig machine, CancellationToken ct)
    {
        if (!machine.Settings.TryGetValue("BaseUrl", out var baseUrlObj) || baseUrlObj is not string baseUrl)
            return ConnectivityStatus.Failed;

        try
        {
            var response = await _httpClient.GetAsync($"{baseUrl}/current", ct);
            return response.IsSuccessStatusCode ? ConnectivityStatus.Connected : ConnectivityStatus.Failed;
        }
        catch
        {
            return ConnectivityStatus.Failed;
        }
    }

    private string? ExtractMakeFromMTConnect(string content)
    {
        // Simple regex-based extraction - could be improved with proper XML parsing
        if (content.Contains("HAAS", StringComparison.OrdinalIgnoreCase)) return "HAAS";
        if (content.Contains("MAZAK", StringComparison.OrdinalIgnoreCase)) return "MAZAK";
        if (content.Contains("DMG", StringComparison.OrdinalIgnoreCase)) return "DMG";
        if (content.Contains("OKUMA", StringComparison.OrdinalIgnoreCase)) return "OKUMA";
        return null;
    }

    private string? ExtractModelFromMTConnect(string content)
    {
        // Could be enhanced with actual XML parsing to extract device model
        return null;
    }

    private List<string> GetNetworkAddresses(string networkRange)
    {
        // Simple implementation for /24 networks
        // For production, would use proper CIDR parsing
        var addresses = new List<string>();
        
        if (networkRange.EndsWith("/24"))
        {
            var baseIp = networkRange.Replace("/24", "");
            var parts = baseIp.Split('.');
            if (parts.Length == 4 && parts[3] == "0")
            {
                var networkBase = string.Join(".", parts.Take(3));
                for (int i = 1; i <= 254; i++)
                {
                    addresses.Add($"{networkBase}.{i}");
                }
            }
        }

        return addresses;
    }

    private string GenerateShortId()
    {
        return Guid.NewGuid().ToString("N")[..6].ToUpper();
    }
}

public class DiscoveredMachine
{
    public string IpAddress { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string Make { get; set; } = "";
    public string? Model { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
}

public class ConnectivityResult
{
    public string MachineId { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string Protocol { get; set; } = "";
    public bool PingSuccessful { get; set; }
    public int? PingLatencyMs { get; set; }
    public ConnectivityStatus ProtocolConnectivity { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ConnectorStatus
{
    public string MachineId { get; set; } = "";
    public string Make { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public bool Enabled { get; set; }
    public bool IsConnected { get; set; }
    public DateTime LastCheck { get; set; }
    public int? PingLatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ConnectivityStatus
{
    Unknown,
    Connected,
    Failed
}