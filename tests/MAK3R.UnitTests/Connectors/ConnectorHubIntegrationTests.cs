using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MAK3R.Connectors;
using MAK3R.Connectors.Abstractions;
using MAK3R.Connectors.FANUC;
using MAK3R.Connectors.MTConnect;
using MAK3R.Connectors.OPCUA;

namespace MAK3R.UnitTests.Connectors;

public class ConnectorHubIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectorHub _connectorHub;

    public ConnectorHubIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddSingleton(Mock.Of<ILogger<ConnectorRegistry>>());
        services.AddSingleton(Mock.Of<ILogger<ConnectorHub>>());
        services.AddSingleton(Mock.Of<ILogger<FanucConnector>>());
        services.AddSingleton(Mock.Of<ILogger<MTConnectConnector>>());
        services.AddSingleton(Mock.Of<ILogger<OpcUaConnector>>());
        
        // Add connector hub
        services.AddConnectorHubCore();
        
        _serviceProvider = services.BuildServiceProvider();
        _connectorHub = _serviceProvider.GetRequiredService<IConnectorHub>();
    }

    [Fact]
    public void GetConnectors_WithNoConnectors_ShouldReturnEmpty()
    {
        // Act
        var connectors = _connectorHub.GetConnectors();

        // Assert
        connectors.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterConnectorAsync_WithValidConnector_ShouldSucceed()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<ILogger<FanucConnector>>();
        var config = new FanucConfig
        {
            ConnectorId = "test-fanuc",
            MachineId = "FANUC-01",
            IpAddress = "192.168.1.100",
            IsSimulator = true
        };
        var connector = new FanucConnector(logger, config);

        // Act
        var result = await _connectorHub.RegisterConnectorAsync(connector);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        // Verify registration
        var connectors = _connectorHub.GetConnectors();
        connectors.Should().HaveCount(1);
        connectors.First().Id.Should().Be("test-fanuc");

        // Cleanup
        connector.Dispose();
    }

    [Fact]
    public async Task RegisterConnectorAsync_WithDuplicateId_ShouldFail()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<ILogger<FanucConnector>>();
        var config = new FanucConfig { ConnectorId = "duplicate-id", MachineId = "FANUC-01", IsSimulator = true };
        var connector1 = new FanucConnector(logger, config);
        var connector2 = new FanucConnector(logger, config);

        // Act
        var result1 = await _connectorHub.RegisterConnectorAsync(connector1);
        var result2 = await _connectorHub.RegisterConnectorAsync(connector2);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeFalse();
        result2.Error.Should().Contain("already registered");

        // Cleanup
        connector1.Dispose();
        connector2.Dispose();
    }

    [Fact]
    public async Task GetConnector_WithExistingId_ShouldReturnConnector()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<ILogger<FanucConnector>>();
        var config = new FanucConfig { ConnectorId = "get-test", MachineId = "FANUC-01", IsSimulator = true };
        var connector = new FanucConnector(logger, config);
        await _connectorHub.RegisterConnectorAsync(connector);

        // Act
        var result = _connectorHub.GetConnector("get-test");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be("get-test");

        // Cleanup
        connector.Dispose();
    }

    [Fact]
    public void GetConnector_WithNonExistentId_ShouldFail()
    {
        // Act
        var result = _connectorHub.GetConnector("non-existent");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task CheckHealthAsync_WithHealthyConnector_ShouldReturnHealthy()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<ILogger<FanucConnector>>();
        var config = new FanucConfig { ConnectorId = "health-test", MachineId = "FANUC-01", IsSimulator = true };
        var connector = new FanucConnector(logger, config);
        await _connectorHub.RegisterConnectorAsync(connector);

        // Act
        var result = await _connectorHub.CheckHealthAsync("health-test");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsHealthy.Should().BeTrue();
        result.Value.ConnectorId.Should().Be("health-test");

        // Cleanup
        connector.Dispose();
    }

    [Fact]
    public async Task CheckAllHealthAsync_WithMultipleConnectors_ShouldReturnAllStatuses()
    {
        // Arrange
        var fanucLogger = _serviceProvider.GetRequiredService<ILogger<FanucConnector>>();
        var mtconnectLogger = _serviceProvider.GetRequiredService<ILogger<MTConnectConnector>>();

        var fanucConfig = new FanucConfig { ConnectorId = "fanuc-multi", MachineId = "FANUC-01", IsSimulator = true };
        var mtconnectConfig = new MTConnectConfig { ConnectorId = "mtconnect-multi", MachineId = "HAAS-01", IsSimulator = true };

        var fanucConnector = new FanucConnector(fanucLogger, fanucConfig);
        var mtconnectConnector = new MTConnectConnector(mtconnectLogger, mtconnectConfig);

        await _connectorHub.RegisterConnectorAsync(fanucConnector);
        await _connectorHub.RegisterConnectorAsync(mtconnectConnector);

        // Act
        var results = new List<ConnectorHealthStatus>();
        await foreach (var status in _connectorHub.CheckAllHealthAsync())
        {
            results.Add(status);
        }

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(s => s.IsHealthy.Should().BeTrue());
        results.Should().Contain(s => s.ConnectorId == "fanuc-multi");
        results.Should().Contain(s => s.ConnectorId == "mtconnect-multi");

        // Cleanup
        fanucConnector.Dispose();
        mtconnectConnector.Dispose();
    }

    [Fact]
    public async Task SyncConnectorAsync_WithValidConnector_ShouldReturnSyncResult()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<ILogger<FanucConnector>>();
        var config = new FanucConfig { ConnectorId = "sync-test", MachineId = "FANUC-01", IsSimulator = true };
        var connector = new FanucConnector(logger, config);
        await _connectorHub.RegisterConnectorAsync(connector);

        // Wait a bit for simulator to generate data
        await Task.Delay(1100);

        // Act
        var result = await _connectorHub.SyncConnectorAsync("sync-test", DateTime.UtcNow.AddMinutes(-1));

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsSuccess.Should().BeTrue();
        result.Value.ConnectorId.Should().Be("sync-test");
        result.Value.EventsProcessed.Should().BeGreaterOrEqualTo(0);
        result.Value.Duration.Should().BePositive();

        // Cleanup
        connector.Dispose();
    }

    [Fact]
    public async Task SyncAllAsync_WithMultipleConnectors_ShouldSyncAll()
    {
        // Arrange
        var fanucLogger = _serviceProvider.GetRequiredService<ILogger<FanucConnector>>();
        var mtconnectLogger = _serviceProvider.GetRequiredService<ILogger<MTConnectConnector>>();

        var fanucConfig = new FanucConfig { ConnectorId = "fanuc-sync-all", MachineId = "FANUC-01", IsSimulator = true };
        var mtconnectConfig = new MTConnectConfig { ConnectorId = "mtconnect-sync-all", MachineId = "HAAS-01", IsSimulator = true };

        var fanucConnector = new FanucConnector(fanucLogger, fanucConfig);
        var mtconnectConnector = new MTConnectConnector(mtconnectLogger, mtconnectConfig);

        await _connectorHub.RegisterConnectorAsync(fanucConnector);
        await _connectorHub.RegisterConnectorAsync(mtconnectConnector);

        // Wait for simulators to generate data
        await Task.Delay(1100);

        // Act
        var results = new List<ConnectorSyncResult>();
        await foreach (var syncResult in _connectorHub.SyncAllAsync(DateTime.UtcNow.AddMinutes(-1)))
        {
            results.Add(syncResult);
        }

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        results.Should().Contain(r => r.ConnectorId == "fanuc-sync-all");
        results.Should().Contain(r => r.ConnectorId == "mtconnect-sync-all");

        // Cleanup
        fanucConnector.Dispose();
        mtconnectConnector.Dispose();
    }

    [Fact]
    public void RegisterConnectorAsync_WithNullConnector_ShouldFail()
    {
        // Act
        var result = _connectorHub.RegisterConnectorAsync(null!);

        // Assert
        result.Result.IsSuccess.Should().BeFalse();
        result.Result.Error.Should().Contain("cannot be null");
    }
}