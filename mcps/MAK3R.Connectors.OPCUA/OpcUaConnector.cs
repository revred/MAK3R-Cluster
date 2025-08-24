using MAK3R.Connectors.Abstractions;
using MAK3R.Core;
using MAK3R.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace MAK3R.Connectors.OPCUA;

public class OpcUaConnector : IConnector, IDisposable
{
    private readonly ILogger<OpcUaConnector> _logger;
    private readonly OpcUaConfig _config;
    private Session? _session;
    private ApplicationConfiguration? _appConfig;
    private readonly Timer? _subscriptionTimer;
    private readonly Dictionary<string, MonitoredItem> _monitoredItems = new();
    private readonly Queue<UpsertEvent> _eventQueue = new();
    private readonly object _queueLock = new();

    public string Id => "opcua-connector";
    public string Name => "OPC UA";
    public string Type => "opcua";
    public Dictionary<string, object> Metadata { get; } = new();

    public OpcUaConnector(ILogger<OpcUaConnector> logger, OpcUaConfig config)
    {
        _logger = logger;
        _config = config;
        
        Metadata["EndpointUrl"] = _config.EndpointUrl;
        Metadata["IsSimulator"] = _config.IsSimulator;
        Metadata["SecurityPolicy"] = _config.SecurityPolicy;

        if (_config.IsSimulator)
        {
            // Start simulator timer to generate mock telemetry data
            _subscriptionTimer = new Timer(GenerateSimulatorData, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }
    }

    public async ValueTask<ConnectorCheck> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Checking OPC UA connector health (Simulator: {IsSimulator})", _config.IsSimulator);

            if (_config.IsSimulator)
            {
                // Simulate health check for simulator mode
                await Task.Delay(50, ct);
                return new ConnectorCheck(
                    true,
                    "OPC UA Simulator - Healthy",
                    new Dictionary<string, object>
                    {
                        { "IsSimulator", true },
                        { "ResponseTime", 50 },
                        { "ActiveNodes", _config.NodeIds.Count }
                    }
                );
            }

            // Real OPC UA server health check
            await EnsureConnectedAsync(ct);
            
            if (_session?.Connected == true)
            {
                return new ConnectorCheck(
                    true,
                    "Connected to OPC UA server",
                    new Dictionary<string, object>
                    {
                        { "IsSimulator", false },
                        { "SessionId", _session.SessionId.ToString() },
                        { "ServerUri", _session.ConfiguredEndpoint?.EndpointUrl?.ToString() ?? "" }
                    }
                );
            }
            else
            {
                return new ConnectorCheck(false, "Failed to connect to OPC UA server");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OPC UA health check");
            return new ConnectorCheck(false, $"Health check error: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<UpsertEvent> PullAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starting OPC UA pull since {Since} (Simulator: {IsSimulator})", since, _config.IsSimulator);

        if (_config.IsSimulator)
        {
            // Return queued simulator events
            while (!ct.IsCancellationRequested)
            {
                UpsertEvent? eventToYield = null;
                
                lock (_queueLock)
                {
                    if (_eventQueue.Count > 0)
                    {
                        eventToYield = _eventQueue.Dequeue();
                    }
                }

                if (eventToYield != null && eventToYield.Timestamp >= since)
                {
                    yield return eventToYield;
                }

                await Task.Delay(100, ct); // Check for new events every 100ms
            }
        }
        else
        {
            // Pull from real OPC UA server
            await foreach (var telemetryEvent in PullTelemetryDataAsync(since, ct))
            {
                if (ct.IsCancellationRequested)
                    yield break;
                    
                yield return telemetryEvent;
            }
        }
    }

    public ValueTask<ConnectorConfiguration> GetConfigurationSchemaAsync()
    {
        var config = new ConnectorConfiguration(
            Id,
            "opcua",
            new Dictionary<string, object>
            {
                { "EndpointUrl", _config.EndpointUrl },
                { "IsSimulator", _config.IsSimulator },
                { "SecurityPolicy", _config.SecurityPolicy },
                { "NodeIds", _config.NodeIds }
            }
        );
        
        return ValueTask.FromResult(config);
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_session?.Connected == true)
            return;

        try
        {
            _logger.LogDebug("Connecting to OPC UA server: {EndpointUrl}", _config.EndpointUrl);

            // Create application configuration
            _appConfig = new ApplicationConfiguration()
            {
                ApplicationName = "MAK3R OPC UA Client",
                ApplicationUri = Utils.Format(@"urn:{0}:MAK3R:OPCUAClient", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = Utils.Format(@"CN={0}, DC={1}", "MAK3R OPC UA Client", System.Net.Dns.GetHostName()) },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TraceConfiguration = new TraceConfiguration()
            };

            await _appConfig.Validate(ApplicationType.Client).ConfigureAwait(false);

            if (_appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                _appConfig.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
            }

            var selectedEndpoint = CoreClientUtils.SelectEndpoint(_config.EndpointUrl, useSecurity: false);
            
            var endpointConfiguration = EndpointConfiguration.Create(_appConfig);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            _session = await Session.Create(_appConfig, endpoint, false, "MAK3R OPC UA Session", 60000, new UserIdentity(new AnonymousIdentityToken()), null).ConfigureAwait(false);
            
            _logger.LogInformation("Successfully connected to OPC UA server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to OPC UA server");
            throw;
        }
    }

    private async IAsyncEnumerable<UpsertEvent> PullTelemetryDataAsync(DateTime since, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await EnsureConnectedAsync(ct);
        
        if (_session?.Connected != true)
            yield break;

        var nodesToRead = new ReadValueIdCollection();
        
        foreach (var nodeId in _config.NodeIds)
        {
            nodesToRead.Add(new ReadValueId()
            {
                NodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value
            });
        }

        DataValueCollection? results = null;
        DiagnosticInfoCollection? diagnosticInfos = null;
        
        try
        {
            _session.Read(null, 0, TimestampsToReturn.Both, nodesToRead, out results, out diagnosticInfos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading OPC UA telemetry data");
            yield break;
        }

        if (results != null)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (ct.IsCancellationRequested)
                    yield break;

                var result = results[i];
                if (StatusCode.IsGood(result.StatusCode))
                {
                    var telemetryData = new Dictionary<string, object>
                    {
                        { "nodeId", _config.NodeIds[i] },
                        { "value", result.Value ?? DBNull.Value },
                        { "quality", result.StatusCode.ToString() },
                        { "timestamp", result.ServerTimestamp },
                        { "sourceTimestamp", result.SourceTimestamp }
                    };

                    yield return new UpsertEvent(
                        "Telemetry",
                        _config.NodeIds[i],
                        JsonSerializer.SerializeToElement(telemetryData),
                        result.ServerTimestamp
                    );
                }
            }
        }
    }

    private void GenerateSimulatorData(object? state)
    {
        if (_config.NodeIds.Count == 0)
            return;

        var random = new Random();
        var timestamp = DateTime.UtcNow;

        foreach (var nodeId in _config.NodeIds)
        {
            var value = (object)(nodeId.ToLower() switch
            {
                var n when n.Contains("temperature") => Math.Round(20 + random.NextDouble() * 60, 2), // 20-80°C
                var n when n.Contains("pressure") => Math.Round(1000 + random.NextDouble() * 500, 1), // 1000-1500 Pa
                var n when n.Contains("rpm") || n.Contains("spindle") => random.Next(800, 2000), // 800-2000 RPM
                var n when n.Contains("vibration") => Math.Round(random.NextDouble() * 2, 3), // 0-2 mm/s
                var n when n.Contains("current") => Math.Round(5 + random.NextDouble() * 15, 2), // 5-20A
                var n when n.Contains("voltage") => Math.Round(220 + random.NextDouble() * 20, 1), // 220-240V
                var n when n.Contains("power") => Math.Round(1000 + random.NextDouble() * 3000, 0), // 1-4kW
                var n when n.Contains("flow") => Math.Round(10 + random.NextDouble() * 40, 2), // 10-50 L/min
                var n when n.Contains("execution") => random.Next(0, 3) switch { 0 => "READY", 1 => "ACTIVE", _ => "FEED_HOLD" },
                var n when n.Contains("mode") => random.Next(0, 3) switch { 0 => "AUTO", 1 => "MDI", _ => "MANUAL" },
                var n when n.Contains("program") => $"MAIN{random.Next(100, 999)}",
                var n when n.Contains("block") => random.Next(1, 1000),
                var n when n.Contains("tool") => random.Next(1, 25),
                var n when n.Contains("partcount") => random.Next(0, 50),
                _ => Math.Round(random.NextDouble() * 100, 2) // Default 0-100
            });

            var telemetryData = new Dictionary<string, object>
            {
                { "nodeId", nodeId },
                { "value", value },
                { "quality", "Good" },
                { "timestamp", timestamp },
                { "sourceTimestamp", timestamp.AddMilliseconds(-random.Next(0, 100)) },
                { "machineId", _config.MachineId },
                { "vendor", "SIEMENS" },
                { "protocol", "OPC UA" }
            };

            var upsertEvent = new UpsertEvent(
                "MachineData",
                $"{_config.MachineId}-{nodeId}",
                JsonSerializer.SerializeToElement(telemetryData),
                timestamp
            );

            lock (_queueLock)
            {
                _eventQueue.Enqueue(upsertEvent);
                
                // Keep queue size manageable
                while (_eventQueue.Count > 1000)
                {
                    _eventQueue.Dequeue();
                }
            }
        }

        _logger.LogDebug("Generated OPC UA simulator data for {NodeCount} nodes", _config.NodeIds.Count);
    }

    public void Dispose()
    {
        _subscriptionTimer?.Dispose();
        
        try
        {
            _session?.Close();
            _session?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing OPC UA session");
        }
    }
}

public class OpcUaConfig
{
    public string MachineId { get; set; } = "MACHINE-01";
    public string EndpointUrl { get; set; } = "opc.tcp://localhost:4840";
    public bool IsSimulator { get; set; } = true;
    public string SecurityPolicy { get; set; } = "None";
    public List<string> NodeIds { get; set; } = new()
    {
        "ns=3;s=Channel/State",     // execution state
        "ns=3;s=Program/Name",      // program name
        "ns=3;s=Program/Block",     // block number
        "ns=3;s=Spindle/Speed",     // spindle RPM
        "ns=3;s=Feed/Rate",         // feed rate
        "ns=3;s=Tool/Number",       // tool number
        "ns=3;s=Machine/Mode",      // machine mode
        "ns=3;s=PartCount"          // part count
    };
}