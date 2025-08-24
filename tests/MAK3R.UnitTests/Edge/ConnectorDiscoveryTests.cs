using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Mak3r.Edge.Services;
using Mak3r.Edge.Models;

namespace MAK3R.UnitTests.Edge;

public class ConnectorDiscoveryTests
{
    private readonly ConnectorDiscoveryService _discoveryService;
    private readonly Mock<ILogger<ConnectorDiscoveryService>> _loggerMock;
    private readonly EdgeConfig _edgeConfig;

    public ConnectorDiscoveryTests()
    {
        _loggerMock = new Mock<ILogger<ConnectorDiscoveryService>>();
        _edgeConfig = new EdgeConfig { SiteId = "TEST-SITE", Timezone = "UTC" };
        var configOptions = Options.Create(_edgeConfig);
        
        _discoveryService = new ConnectorDiscoveryService(configOptions, _loggerMock.Object);
    }

    [Fact]
    public async Task ValidateConnectivityAsync_WithValidFocasConfig_ShouldTestTcpPort()
    {
        // Arrange
        var machine = new EdgeConnectorConfig
        {
            MachineId = "FANUC-01",
            Make = "FANUC",
            IpAddress = "127.0.0.1", // Localhost for testing
            Protocol = "FOCAS",
            Settings = new Dictionary<string, object>
            {
                { "Port", 8193 }
            }
        };

        // Act
        var result = await _discoveryService.ValidateConnectivityAsync(machine);

        // Assert
        result.Should().NotBeNull();
        result.MachineId.Should().Be("FANUC-01");
        result.IpAddress.Should().Be("127.0.0.1");
        result.Protocol.Should().Be("FOCAS");
        // Note: PingSuccessful should be true for localhost, but ProtocolConnectivity will likely fail unless port is open
    }

    [Fact]
    public async Task ValidateConnectivityAsync_WithValidOpcUaConfig_ShouldTestEndpoint()
    {
        // Arrange
        var machine = new EdgeConnectorConfig
        {
            MachineId = "SIEMENS-01", 
            Make = "SIEMENS",
            IpAddress = "127.0.0.1",
            Protocol = "OPC UA",
            Settings = new Dictionary<string, object>
            {
                { "EndpointUrl", "opc.tcp://127.0.0.1:4840" }
            }
        };

        // Act
        var result = await _discoveryService.ValidateConnectivityAsync(machine);

        // Assert
        result.Should().NotBeNull();
        result.MachineId.Should().Be("SIEMENS-01");
        result.Protocol.Should().Be("OPC UA");
    }

    [Fact]
    public async Task ValidateConnectivityAsync_WithValidMTConnectConfig_ShouldTestHttpEndpoint()
    {
        // Arrange
        var machine = new EdgeConnectorConfig
        {
            MachineId = "HAAS-01",
            Make = "HAAS", 
            IpAddress = "127.0.0.1",
            Protocol = "MTConnect",
            Settings = new Dictionary<string, object>
            {
                { "BaseUrl", "http://127.0.0.1:8082/VF2SS" }
            }
        };

        // Act
        var result = await _discoveryService.ValidateConnectivityAsync(machine);

        // Assert
        result.Should().NotBeNull();
        result.MachineId.Should().Be("HAAS-01");
        result.Protocol.Should().Be("MTConnect");
    }

    [Fact]
    public async Task ValidateConnectivityAsync_WithMissingFocasPort_ShouldReturnFailed()
    {
        // Arrange
        var machine = new EdgeConnectorConfig
        {
            MachineId = "FANUC-01",
            Make = "FANUC",
            IpAddress = "127.0.0.1",
            Protocol = "FOCAS",
            Settings = new Dictionary<string, object>() // Missing Port
        };

        // Act
        var result = await _discoveryService.ValidateConnectivityAsync(machine);

        // Assert
        result.ProtocolConnectivity.Should().Be(ConnectivityStatus.Failed);
    }

    [Fact]
    public async Task ValidateConnectivityAsync_WithMissingOpcUaEndpoint_ShouldReturnFailed()
    {
        // Arrange
        var machine = new EdgeConnectorConfig
        {
            MachineId = "SIEMENS-01",
            Make = "SIEMENS",
            IpAddress = "127.0.0.1",
            Protocol = "OPC UA",
            Settings = new Dictionary<string, object>() // Missing EndpointUrl
        };

        // Act
        var result = await _discoveryService.ValidateConnectivityAsync(machine);

        // Assert
        result.ProtocolConnectivity.Should().Be(ConnectivityStatus.Failed);
    }

    [Fact]
    public async Task ValidateConnectivityAsync_WithMissingMTConnectBaseUrl_ShouldReturnFailed()
    {
        // Arrange
        var machine = new EdgeConnectorConfig
        {
            MachineId = "HAAS-01",
            Make = "HAAS",
            IpAddress = "127.0.0.1", 
            Protocol = "MTConnect",
            Settings = new Dictionary<string, object>() // Missing BaseUrl
        };

        // Act
        var result = await _discoveryService.ValidateConnectivityAsync(machine);

        // Assert
        result.ProtocolConnectivity.Should().Be(ConnectivityStatus.Failed);
    }

    [Fact]
    public async Task GetConnectorStatusAsync_WithMultipleMachines_ShouldReturnStatuses()
    {
        // Arrange
        var machines = new List<EdgeConnectorConfig>
        {
            new EdgeConnectorConfig
            {
                MachineId = "FANUC-01",
                Make = "FANUC",
                IpAddress = "127.0.0.1",
                Protocol = "FOCAS",
                Enabled = true,
                Settings = new Dictionary<string, object> { { "Port", 8193 } }
            },
            new EdgeConnectorConfig
            {
                MachineId = "SIEMENS-01",
                Make = "SIEMENS", 
                IpAddress = "127.0.0.1",
                Protocol = "OPC UA",
                Enabled = false,
                Settings = new Dictionary<string, object> { { "EndpointUrl", "opc.tcp://127.0.0.1:4840" } }
            }
        };

        // Act
        var statuses = await _discoveryService.GetConnectorStatusAsync(machines);

        // Assert
        statuses.Should().HaveCount(2);
        statuses.Should().AllSatisfy(status =>
        {
            status.MachineId.Should().NotBeNullOrEmpty();
            status.Make.Should().NotBeNullOrEmpty();
            status.Protocol.Should().NotBeNullOrEmpty(); 
            status.IpAddress.Should().NotBeNullOrEmpty();
            status.LastCheck.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        });

        var fanucStatus = statuses.First(s => s.MachineId == "FANUC-01");
        fanucStatus.Enabled.Should().BeTrue();
        fanucStatus.Make.Should().Be("FANUC");

        var siemensStatus = statuses.First(s => s.MachineId == "SIEMENS-01");
        siemensStatus.Enabled.Should().BeFalse();
        siemensStatus.Make.Should().Be("SIEMENS");
    }

    [Fact]
    public async Task AutoRegisterMachineAsync_WithNonExistentAddress_ShouldReturnNull()
    {
        // Arrange
        var nonExistentIp = "192.168.99.99"; // Unlikely to exist

        // Act
        var result = await _discoveryService.AutoRegisterMachineAsync(nonExistentIp);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DiscoverMachinesAsync_WithNetworkRange_ShouldReturnDiscoveredMachines()
    {
        // Arrange
        var networkRange = "127.0.0.0/24"; // Localhost range for testing

        // Act
        var discovered = await _discoveryService.DiscoverMachinesAsync(networkRange);

        // Assert
        discovered.Should().NotBeNull();
        // Note: This test may not find actual machines but should complete without error
        discovered.Should().BeOfType<List<DiscoveredMachine>>();
    }

    [Theory]
    [InlineData("FOCAS")]
    [InlineData("OPC UA")]
    [InlineData("MTConnect")]
    public async Task ValidateConnectivityAsync_WithDifferentProtocols_ShouldHandleCorrectly(string protocol)
    {
        // Arrange
        var settings = protocol switch
        {
            "FOCAS" => new Dictionary<string, object> { { "Port", 8193 } },
            "OPC UA" => new Dictionary<string, object> { { "EndpointUrl", "opc.tcp://127.0.0.1:4840" } },
            "MTConnect" => new Dictionary<string, object> { { "BaseUrl", "http://127.0.0.1:8082" } },
            _ => new Dictionary<string, object>()
        };

        var machine = new EdgeConnectorConfig
        {
            MachineId = $"TEST-{protocol}",
            Make = "TEST",
            IpAddress = "127.0.0.1",
            Protocol = protocol,
            Settings = settings
        };

        // Act
        var result = await _discoveryService.ValidateConnectivityAsync(machine);

        // Assert
        result.Should().NotBeNull();
        result.Protocol.Should().Be(protocol);
        result.ProtocolConnectivity.Should().BeOneOf(
            ConnectivityStatus.Connected, 
            ConnectivityStatus.Failed); // Either works or fails, but should not be Unknown
    }

    [Fact]
    public async Task ValidateConnectivityAsync_WithInvalidIpAddress_ShouldHandleGracefully()
    {
        // Arrange
        var machine = new EdgeConnectorConfig
        {
            MachineId = "TEST-01",
            Make = "TEST",
            IpAddress = "invalid.ip.address",
            Protocol = "FOCAS",
            Settings = new Dictionary<string, object> { { "Port", 8193 } }
        };

        // Act
        var result = await _discoveryService.ValidateConnectivityAsync(machine);

        // Assert
        result.Should().NotBeNull();
        result.ProtocolConnectivity.Should().Be(ConnectivityStatus.Failed);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}