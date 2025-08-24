using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;
using Mak3r.Edge.Services;

namespace MAK3R.IntegrationTests.Edge;

/// <summary>
/// Integration tests for the Edge Admin API endpoints
/// Tests the full API surface across all manufacturers
/// </summary>
public class EdgeApiIntegrationTests : IClassFixture<EdgeApiTestFixture>
{
    private readonly EdgeApiTestFixture _fixture;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public EdgeApiIntegrationTests(EdgeApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _client = _fixture.CreateClient();
        _output = output;
    }

    #region Health and Basic Endpoints

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var healthData = JsonSerializer.Deserialize<HealthResponse>(content, JsonOptions);
        
        healthData.Should().NotBeNull();
        healthData!.Status.Should().Be("healthy");
        healthData.SiteId.Should().NotBeNullOrEmpty();
        healthData.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldReturnQueueMetrics()
    {
        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var metrics = JsonSerializer.Deserialize<MetricsResponse>(content, JsonOptions);

        metrics.Should().NotBeNull();
        metrics!.QueueDepth.Should().BeGreaterOrEqualTo(0);
        metrics.QueueCapacity.Should().BeGreaterThan(0);
        metrics.UptimeSeconds.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task ConfigEndpoint_ShouldReturnValidConfiguration()
    {
        // Act
        var response = await _client.GetAsync("/config");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var config = JsonSerializer.Deserialize<ConfigResponse>(content, JsonOptions);

        config.Should().NotBeNull();
        config!.SiteId.Should().NotBeNullOrEmpty();
        config.Uplink.Should().NotBeNull();
        config.Uplink.HubUrl.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Connector Status Tests

    [Fact]
    public async Task ConnectorsEndpoint_ShouldReturnAllConfiguredMachines()
    {
        // Act
        var response = await _client.GetAsync("/connectors");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var connectors = JsonSerializer.Deserialize<ConnectorStatus[]>(content, JsonOptions);

        connectors.Should().NotBeNull();
        connectors!.Should().HaveCountGreaterOrEqualTo(4); // FANUC, Siemens, HAAS, Mazak

        // Validate manufacturer representation
        var manufacturers = connectors.Select(c => c.Make).Distinct().ToList();
        manufacturers.Should().Contain(new[] { "FANUC", "SIEMENS", "HAAS", "MAZAK" });

        foreach (var connector in connectors)
        {
            connector.MachineId.Should().NotBeNullOrEmpty();
            connector.Make.Should().NotBeNullOrEmpty();
            connector.Protocol.Should().NotBeNullOrEmpty();
            connector.IpAddress.Should().NotBeNullOrEmpty();
            connector.LastCheck.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }
    }

    [Theory]
    [InlineData("FANUC-TC-01", "FANUC", "FOCAS")]
    [InlineData("SIEMENS-TC-02", "SIEMENS", "OPC UA")]
    [InlineData("HAAS-MILL-03", "HAAS", "MTConnect")]
    [InlineData("MAZAK-5X-04", "MAZAK", "MTConnect")]
    public async Task ConnectorHealthEndpoint_ShouldReturnManufacturerSpecificHealth(string machineId, string expectedMake, string expectedProtocol)
    {
        // Act
        var response = await _client.GetAsync($"/connectors/{machineId}/health");

        // Assert
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var health = JsonSerializer.Deserialize<ConnectorHealthResponse>(content, JsonOptions);

            health.Should().NotBeNull();
            health!.MachineId.Should().Be(machineId);
            health.Protocol.Should().Be(expectedProtocol);
            health.LastCheck.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }
        else
        {
            // Machine might not be configured - check that error is appropriate
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }
    }

    #endregion

    #region Discovery Tests

    [Fact]
    public async Task DiscoveryEndpoint_ShouldScanNetworkRange()
    {
        // Arrange
        var discoveryRequest = new
        {
            NetworkRange = "127.0.0.0/24" // Use localhost for testing
        };

        // Act
        var response = await _client.PostAsJsonAsync("/discover", discoveryRequest);

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<DiscoveryResponse>(content, JsonOptions);

        result.Should().NotBeNull();
        result!.NetworkRange.Should().Be("127.0.0.0/24");
        result.Discovered.Should().BeGreaterOrEqualTo(0);
        result.Machines.Should().NotBeNull();

        _output.WriteLine($"Discovery found {result.Discovered} machines in range {result.NetworkRange}");
    }

    [Fact]
    public async Task AutoRegisterEndpoint_ShouldHandleValidIpAddress()
    {
        // Arrange
        var autoRegisterRequest = new
        {
            IpAddress = "127.0.0.1"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/connectors/auto-register", autoRegisterRequest);

        // Assert
        // Either succeeds (if a protocol is detected) or returns 404 (no machine found)
        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.OK,
            System.Net.HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AutoRegisterResponse>(content, JsonOptions);
            
            result.Should().NotBeNull();
            result!.Message.Should().Contain("auto-registered");
            result.Machine.Should().NotBeNull();
            result.Machine.MachineId.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData("FOCAS", 8193)]
    [InlineData("OPC UA", 4840)]
    [InlineData("MTConnect", 8082)]
    public async Task ValidateConnectivityEndpoint_ShouldTestProtocolConnectivity(string protocol, int port)
    {
        // Arrange
        var validateRequest = new
        {
            MachineId = $"TEST-{protocol.Replace(" ", "")}",
            IpAddress = "127.0.0.1",
            Protocol = protocol,
            Settings = protocol switch
            {
                "FOCAS" => new Dictionary<string, object> { { "Port", port } },
                "OPC UA" => new Dictionary<string, object> { { "EndpointUrl", $"opc.tcp://127.0.0.1:{port}" } },
                "MTConnect" => new Dictionary<string, object> { { "BaseUrl", $"http://127.0.0.1:{port}" } },
                _ => new Dictionary<string, object>()
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/connectors/validate", validateRequest);

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ConnectivityResult>(content, JsonOptions);

        result.Should().NotBeNull();
        result!.MachineId.Should().Be($"TEST-{protocol.Replace(" ", "")}");
        result.IpAddress.Should().Be("127.0.0.1");
        result.Protocol.Should().Be(protocol);
        result.PingSuccessful.Should().BeTrue(); // Localhost should always ping
        result.ProtocolConnectivity.Should().BeOneOf("Connected", "Failed", "Unknown");
    }

    #endregion

    #region Network Diagnostics Tests

    [Fact]
    public async Task NetDiagPhasesEndpoint_ShouldReturnNetworkPhases()
    {
        // Act
        var response = await _client.GetAsync("/netdiag/phases");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var phases = JsonSerializer.Deserialize<object[]>(content, JsonOptions);

        phases.Should().NotBeNull();
        // Phases might be empty initially but endpoint should work
    }

    [Fact]
    public async Task NetDiagStatsEndpoint_ShouldReturnNetworkStatistics()
    {
        // Act
        var response = await _client.GetAsync("/netdiag/stats");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<NetworkStatsResponse>(content, JsonOptions);

        stats.Should().NotBeNull();
        stats!.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task NetDiagBatchesEndpoint_ShouldReturnBatchInformation()
    {
        // Act
        var response = await _client.GetAsync("/netdiag/batches");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var batches = JsonSerializer.Deserialize<object[]>(content, JsonOptions);

        batches.Should().NotBeNull();
    }

    #endregion

    #region Spool Management Tests

    [Fact]
    public async Task SpoolEndpoint_ShouldReturnSpoolStatus()
    {
        // Act
        var response = await _client.GetAsync("/spool");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var spool = JsonSerializer.Deserialize<SpoolStatusResponse>(content, JsonOptions);

        spool.Should().NotBeNull();
        spool!.SpooledBatches.Should().BeGreaterOrEqualTo(0);
        spool.TotalSizeBytes.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task ClearSpoolEndpoint_ShouldClearSpooledFiles()
    {
        // Act
        var response = await _client.DeleteAsync("/spool");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ClearSpoolResponse>(content, JsonOptions);

        result.Should().NotBeNull();
        result!.Deleted.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region Configuration Management Tests

    [Fact]
    public async Task SamplingConfigEndpoint_ShouldAcceptValidConfiguration()
    {
        // Arrange
        var samplingRequest = new
        {
            MachineId = "FANUC-TC-01",
            IntervalMs = 1000
        };

        // Act
        var response = await _client.PostAsJsonAsync("/config/sampling", samplingRequest);

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<SamplingConfigResponse>(content, JsonOptions);

        result.Should().NotBeNull();
        result!.Applied.Should().BeTrue();
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Load Testing and Performance

    [Fact]
    public async Task ConcurrentRequests_ShouldHandleMultipleClients()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Create multiple concurrent health check requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/health"));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response =>
        {
            response.Should().BeSuccessful();
        });

        // Verify all responses contain valid data
        foreach (var response in responses)
        {
            var content = await response.Content.ReadAsStringAsync();
            var health = JsonSerializer.Deserialize<HealthResponse>(content, JsonOptions);
            health.Should().NotBeNull();
            health!.Status.Should().Be("healthy");
        }
    }

    [Fact]
    public async Task LargeDiscoveryRequest_ShouldHandleWithinTimeout()
    {
        // Arrange
        var discoveryRequest = new
        {
            NetworkRange = "10.0.0.0/24" // Large range - 254 addresses
        };

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var response = await _client.PostAsJsonAsync("/discover", discoveryRequest, cts.Token);

        // Assert
        response.Should().BeSuccessful();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<DiscoveryResponse>(content, JsonOptions);
        
        result.Should().NotBeNull();
        _output.WriteLine($"Large discovery completed in time, found {result!.Discovered} machines");
    }

    #endregion

    #region Data Models

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public class HealthResponse
    {
        public string Status { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string SiteId { get; set; } = "";
        public string Version { get; set; } = "";
    }

    public class MetricsResponse
    {
        public int QueueDepth { get; set; }
        public int QueueCapacity { get; set; }
        public long UptimeSeconds { get; set; }
    }

    public class ConfigResponse
    {
        public string SiteId { get; set; } = "";
        public string Timezone { get; set; } = "";
        public UplinkInfo Uplink { get; set; } = new();
    }

    public class UplinkInfo
    {
        public string HubUrl { get; set; } = "";
        public int BatchSize { get; set; }
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

    public class ConnectorHealthResponse
    {
        public string MachineId { get; set; } = "";
        public bool IsHealthy { get; set; }
        public DateTime LastCheck { get; set; }
        public string Protocol { get; set; } = "";
        public int? PingLatencyMs { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class DiscoveryResponse
    {
        public string NetworkRange { get; set; } = "";
        public int Discovered { get; set; }
        public object[] Machines { get; set; } = Array.Empty<object>();
    }

    public class AutoRegisterResponse
    {
        public string Message { get; set; } = "";
        public MachineConfig Machine { get; set; } = new();
        public string Note { get; set; } = "";
    }

    public class MachineConfig
    {
        public string MachineId { get; set; } = "";
        public string Make { get; set; } = "";
        public string Protocol { get; set; } = "";
    }

    public class ConnectivityResult
    {
        public string MachineId { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Protocol { get; set; } = "";
        public bool PingSuccessful { get; set; }
        public int? PingLatencyMs { get; set; }
        public string ProtocolConnectivity { get; set; } = "";
    }

    public class NetworkStatsResponse
    {
        public DateTime Timestamp { get; set; }
        public object? Phases { get; set; }
        public object? Batches { get; set; }
    }

    public class SpoolStatusResponse
    {
        public int SpooledBatches { get; set; }
        public long TotalSizeBytes { get; set; }
        public string? OldestFile { get; set; }
    }

    public class ClearSpoolResponse
    {
        public int Deleted { get; set; }
    }

    public class SamplingConfigResponse
    {
        public bool Applied { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}

/// <summary>
/// Test fixture for Edge API integration tests
/// </summary>
public class EdgeApiTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Add test-specific services or override production services
            services.AddSingleton<ConnectorDiscoveryService>();
        });

        builder.UseEnvironment("Testing");
    }
}