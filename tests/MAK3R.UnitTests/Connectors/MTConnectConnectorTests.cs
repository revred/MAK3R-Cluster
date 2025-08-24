using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MAK3R.Connectors.Abstractions;
using MAK3R.Connectors.MTConnect;
using System.Text.Json;

namespace MAK3R.UnitTests.Connectors;

public class MTConnectConnectorTests
{
    private readonly Mock<ILogger<MTConnectConnector>> _loggerMock;
    private readonly MTConnectConfig _config;

    public MTConnectConnectorTests()
    {
        _loggerMock = new Mock<ILogger<MTConnectConnector>>();
        _config = new MTConnectConfig
        {
            ConnectorId = "test-mtconnect",
            MachineId = "HAAS-TEST-01",
            Make = "HAAS",
            BaseUrl = "http://localhost:5000/test",
            IsSimulator = true,
            SampleIntervalMs = 1000
        };
    }

    [Fact]
    public void Constructor_WithValidConfig_ShouldCreateConnector()
    {
        // Act
        using var connector = new MTConnectConnector(_loggerMock.Object, _config);

        // Assert
        connector.Should().NotBeNull();
        connector.Id.Should().Be(_config.ConnectorId);
        connector.Name.Should().Contain(_config.MachineId);
        connector.Type.Should().Be("mtconnect");
    }

    [Fact]
    public async Task CheckAsync_WithSimulator_ShouldReturnHealthy()
    {
        // Arrange
        using var connector = new MTConnectConnector(_loggerMock.Object, _config);

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
        result.Metadata.Should().ContainKey("Make");
        result.Metadata["Make"].Should().Be(_config.Make);
    }

    [Fact]
    public async Task CheckAsync_WithRealConnection_ShouldHandleConnectionFailure()
    {
        // Arrange
        var realConfig = new MTConnectConfig
        {
            ConnectorId = _config.ConnectorId,
            MachineId = _config.MachineId,
            Make = _config.Make,
            BaseUrl = _config.BaseUrl,
            IsSimulator = false,
            SampleIntervalMs = _config.SampleIntervalMs
        };
        using var connector = new MTConnectConnector(_loggerMock.Object, realConfig);

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
        using var connector = new MTConnectConnector(_loggerMock.Object, _config);
        var since = DateTime.UtcNow.AddMinutes(-1);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        var events = new List<UpsertEvent>();
        await foreach (var evt in connector.PullAsync(since, cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 3) break;
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
        using var connector = new MTConnectConnector(_loggerMock.Object, _config);

        // Act
        var result = await connector.GetConfigurationSchemaAsync();

        // Assert
        result.Should().NotBeNull();
        result.ConnectorId.Should().Be(_config.ConnectorId);
        result.Type.Should().Be("mtconnect");
        result.Settings.Should().ContainKey("MachineId");
        result.Settings.Should().ContainKey("Make");
        result.Settings.Should().ContainKey("BaseUrl");
        result.Settings.Should().ContainKey("IsSimulator");
        result.Settings.Should().ContainKey("SampleIntervalMs");
    }

    [Theory]
    [InlineData("HAAS", 800, 8000)]
    [InlineData("MAZAK", 300, 12000)]
    [InlineData("OTHER", 500, 6000)]
    public async Task PullAsync_WithDifferentMakes_ShouldGenerateAppropriateData(string make, int minSpindle, int maxSpindle)
    {
        // Arrange
        var config = new MTConnectConfig
        {
            ConnectorId = _config.ConnectorId,
            MachineId = _config.MachineId,
            Make = make,
            BaseUrl = _config.BaseUrl,
            IsSimulator = _config.IsSimulator,
            SampleIntervalMs = _config.SampleIntervalMs
        };
        using var connector = new MTConnectConnector(_loggerMock.Object, config);
        var since = DateTime.UtcNow.AddMinutes(-1);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        var events = new List<UpsertEvent>();
        await foreach (var evt in connector.PullAsync(since, cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 1) break;
        }

        // Assert
        events.Should().NotBeEmpty();
        var eventData = events.First();
        eventData.EntityType.Should().Be("MachineData");
        
        // Parse the JSON payload to verify machine-specific data
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(eventData.Payload);
        payload.Should().ContainKey("make");
        payload!["make"].ToString().Should().Be(make);
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var connector = new MTConnectConnector(_loggerMock.Object, _config);

        // Act & Assert - Should not throw
        var action = () => connector.Dispose();
        action.Should().NotThrow();
    }
}

public class MTConnectConnectorFactoryTests
{
    private readonly Mock<ILogger<MTConnectConnector>> _loggerMock;
    private readonly MTConnectConnectorFactory _factory;

    public MTConnectConnectorFactoryTests()
    {
        _loggerMock = new Mock<ILogger<MTConnectConnector>>();
        _factory = new MTConnectConnectorFactory(_loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidConfiguration_ShouldCreateConnector()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-mtconnect",
            "mtconnect",
            new Dictionary<string, object>
            {
                { "MachineId", "HAAS-TEST-01" },
                { "Make", "HAAS" },
                { "BaseUrl", "http://localhost:5000/test" },
                { "IsSimulator", true },
                { "SampleIntervalMs", 500 }
            }
        );

        // Act
        var result = await _factory.CreateAsync(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be("test-mtconnect");
        result.Value.Type.Should().Be("mtconnect");
        
        // Cleanup
        result.Value.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithMissingBaseUrl_ShouldUseDefault()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-mtconnect",
            "mtconnect",
            new Dictionary<string, object>
            {
                { "MachineId", "HAAS-TEST-01" },
                { "Make", "HAAS" }
                // Missing BaseUrl - should use default
            }
        );

        // Act
        var result = await _factory.CreateAsync(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be("test-mtconnect");
        
        // Cleanup
        result.Value.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithInvalidSampleInterval_ShouldFail()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-mtconnect",
            "mtconnect",
            new Dictionary<string, object>
            {
                { "MachineId", "HAAS-TEST-01" },
                { "BaseUrl", "http://localhost:5000" },
                { "SampleIntervalMs", 50 } // Too low
            }
        );

        // Act
        var result = await _factory.CreateAsync(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("SampleIntervalMs");
    }

    [Fact]
    public async Task GetConfigurationSchemaAsync_ShouldReturnValidSchema()
    {
        // Act
        var result = await _factory.GetConfigurationSchemaAsync();

        // Assert
        result.Should().NotBeNull();
        result.ConnectorTypeId.Should().Be("mtconnect");
        result.JsonSchema.Should().NotBeEmpty();
        result.Fields.Should().ContainKey("MachineId");
        result.Fields.Should().ContainKey("Make");
        result.Fields.Should().ContainKey("BaseUrl");
        result.Fields.Should().ContainKey("IsSimulator");
        result.Fields.Should().ContainKey("SampleIntervalMs");
        result.RequiredFields.Should().Contain("MachineId");
        result.RequiredFields.Should().Contain("BaseUrl");
        
        // Verify Make field has enum values
        result.Fields["Make"].EnumValues.Should().NotBeNull();
        result.Fields["Make"].EnumValues.Should().Contain("HAAS");
        result.Fields["Make"].EnumValues.Should().Contain("MAZAK");
    }

    [Fact]
    public void ValidateConfiguration_WithWrongType_ShouldFail()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-mtconnect",
            "fanuc", // Wrong type
            new Dictionary<string, object>()
        );

        // Act
        var result = _factory.ValidateConfiguration(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid connector type");
    }

    [Fact]
    public void ValidateConfiguration_WithNullSettings_ShouldFail()
    {
        // Arrange
        var config = new ConnectorConfiguration(
            "test-mtconnect",
            "mtconnect",
            null! // Null settings
        );

        // Act
        var result = _factory.ValidateConfiguration(config);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Settings");
    }
}