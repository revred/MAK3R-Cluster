using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MAK3R.Connectors.Abstractions;
using MAK3R.Connectors.FANUC;

namespace MAK3R.UnitTests.Connectors;

public class FanucConnectorTests
{
    private readonly Mock<ILogger<FanucConnector>> _loggerMock;
    private readonly FanucConfig _config;

    public FanucConnectorTests()
    {
        _loggerMock = new Mock<ILogger<FanucConnector>>();
        _config = new FanucConfig
        {
            ConnectorId = "test-fanuc",
            MachineId = "FANUC-TEST-01",
            IpAddress = "192.168.1.100",
            Port = 8193,
            IsSimulator = true,
            PollIntervalMs = 1000
        };
    }

    [Fact]
    public void Constructor_WithValidConfig_ShouldCreateConnector()
    {
        // Act
        using var connector = new FanucConnector(_loggerMock.Object, _config);

        // Assert
        connector.Should().NotBeNull();
        connector.Id.Should().Be(_config.ConnectorId);
        connector.Name.Should().Contain(_config.MachineId);
        connector.Type.Should().Be("fanuc");
    }

    [Fact]
    public async Task CheckAsync_WithSimulator_ShouldReturnHealthy()
    {
        // Arrange
        using var connector = new FanucConnector(_loggerMock.Object, _config);

        // Act
        var result = await connector.CheckAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeTrue();
        result.Message.Should().Contain("Healthy");
        result.Metadata.Should().ContainKey("IsSimulator");
        result.Metadata!["IsSimulator"].Should().Be(true);
        result.Metadata.Should().ContainKey("MachineId");
        result.Metadata["MachineId"].Should().Be(_config.MachineId);
    }

    [Fact]
    public async Task CheckAsync_WithRealConnection_ShouldHandleConnectionFailure()
    {
        // Arrange
        var realConfig = new FanucConfig
        {
            ConnectorId = _config.ConnectorId,
            MachineId = _config.MachineId,
            IpAddress = _config.IpAddress,
            Port = _config.Port,
            IsSimulator = false,
            PollIntervalMs = _config.PollIntervalMs
        };
        using var connector = new FanucConnector(_loggerMock.Object, realConfig);

        // Act
        var result = await connector.CheckAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("Health check error");
    }

    [Fact]
    public async Task PullAsync_WithSimulator_ShouldGenerateEvents()
    {
        // Arrange
        using var connector = new FanucConnector(_loggerMock.Object, _config);
        var since = DateTime.UtcNow.AddMinutes(-1);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        var events = new List<UpsertEvent>();
        await foreach (var evt in connector.PullAsync(since, cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 3) break; // Collect a few events
        }

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(e =>
        {
            e.EntityType.Should().Be("MachineData");
            e.ExternalId.Should().Contain(_config.MachineId);
            e.Timestamp.Should().BeAfter(since);
        });
    }

    [Fact]
    public async Task GetConfigurationSchemaAsync_ShouldReturnValidConfiguration()
    {
        // Arrange
        using var connector = new FanucConnector(_loggerMock.Object, _config);

        // Act
        var result = await connector.GetConfigurationSchemaAsync();

        // Assert
        result.Should().NotBeNull();
        result.ConnectorId.Should().Be(_config.ConnectorId);
        result.Type.Should().Be("fanuc");
        result.Settings.Should().ContainKey("MachineId");
        result.Settings.Should().ContainKey("IpAddress");
        result.Settings.Should().ContainKey("Port");
        result.Settings.Should().ContainKey("IsSimulator");
        result.Settings.Should().ContainKey("PollIntervalMs");
    }

    [Theory]
    [InlineData("", "IpAddress", 8193, true, 250)]
    [InlineData("FANUC-01", "", 8193, true, 250)]
    [InlineData("FANUC-01", "192.168.1.100", 0, true, 250)]
    [InlineData("FANUC-01", "192.168.1.100", 65536, true, 250)]
    public void Constructor_WithInvalidConfig_ShouldNotThrow(string machineId, string ipAddress, int port, bool isSimulator, int pollInterval)
    {
        // Arrange
        var invalidConfig = new FanucConfig
        {
            ConnectorId = "test",
            MachineId = machineId,
            IpAddress = ipAddress,
            Port = port,
            IsSimulator = isSimulator,
            PollIntervalMs = pollInterval
        };

        // Act & Assert - Constructor should not throw, validation happens at factory level
        var action = () => new FanucConnector(_loggerMock.Object, invalidConfig);
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var connector = new FanucConnector(_loggerMock.Object, _config);

        // Act & Assert - Should not throw
        var action = () => connector.Dispose();
        action.Should().NotThrow();
    }
}

public class FanucConnectorFactoryTests
{
    private readonly Mock<ILogger<FanucConnector>> _loggerMock;
    private readonly FanucConnectorFactory _factory;

    public FanucConnectorFactoryTests()
    {
        _loggerMock = new Mock<ILogger<FanucConnector>>();
        _factory = new FanucConnectorFactory(_loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidConfiguration_ShouldCreateConnector()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-fanuc",
            "fanuc",
            new Dictionary<string, object>
            {
                { "MachineId", "FANUC-TEST-01" },
                { "IpAddress", "192.168.1.100" },
                { "Port", 8193 },
                { "IsSimulator", true },
                { "PollIntervalMs", 250 }
            }
        );

        // Act
        var result = await _factory.CreateAsync(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be("test-fanuc");
        result.Value.Type.Should().Be("fanuc");
        
        // Cleanup
        result.Value.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithMissingMachineId_ShouldUseDefault()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-fanuc",
            "fanuc",
            new Dictionary<string, object>
            {
                { "IpAddress", "192.168.1.100" },
                { "Port", 8193 }
            }
        );

        // Act
        var result = await _factory.CreateAsync(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Contain("FANUC-01"); // Default machine ID
        
        // Cleanup
        result.Value.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithInvalidPort_ShouldFail()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-fanuc",
            "fanuc",
            new Dictionary<string, object>
            {
                { "MachineId", "FANUC-TEST-01" },
                { "IpAddress", "192.168.1.100" },
                { "Port", 70000 } // Invalid port
            }
        );

        // Act
        var result = await _factory.CreateAsync(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Port");
    }

    [Fact]
    public async Task CreateAsync_WithWrongType_ShouldFail()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-fanuc",
            "opcua", // Wrong type
            new Dictionary<string, object>
            {
                { "MachineId", "FANUC-TEST-01" },
                { "IpAddress", "192.168.1.100" }
            }
        );

        // Act
        var result = await _factory.CreateAsync(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid connector type");
    }

    [Fact]
    public async Task GetConfigurationSchemaAsync_ShouldReturnValidSchema()
    {
        // Act
        var result = await _factory.GetConfigurationSchemaAsync();

        // Assert
        result.Should().NotBeNull();
        result.ConnectorTypeId.Should().Be("fanuc");
        result.JsonSchema.Should().NotBeEmpty();
        result.Fields.Should().ContainKey("MachineId");
        result.Fields.Should().ContainKey("IpAddress");
        result.Fields.Should().ContainKey("Port");
        result.Fields.Should().ContainKey("IsSimulator");
        result.Fields.Should().ContainKey("PollIntervalMs");
        result.RequiredFields.Should().Contain("MachineId");
        result.RequiredFields.Should().Contain("IpAddress");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateConfiguration_WithNullOrEmptyConnectorId_ShouldFail(string? connectorId)
    {
        // Arrange
        var config = new ConnectorConfiguration(
            connectorId!,
            "fanuc",
            new Dictionary<string, object>()
        );

        // Act
        var result = _factory.ValidateConfiguration(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ConnectorId");
    }
}