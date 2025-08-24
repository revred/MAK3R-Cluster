using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Mak3r.Edge.Services;
using Mak3r.Edge.Models;

namespace MAK3R.UnitTests.Edge;

public class ConfigValidationTests
{
    private readonly ConfigValidationService _validator;
    private readonly Mock<ILogger<ConfigValidationService>> _loggerMock;

    public ConfigValidationTests()
    {
        _loggerMock = new Mock<ILogger<ConfigValidationService>>();
        _validator = new ConfigValidationService(_loggerMock.Object);
    }

    [Fact]
    public void ValidateEdgeConfig_WithValidConfig_ShouldPass()
    {
        // Arrange
        var config = CreateValidEdgeConfig();

        // Act
        var result = _validator.ValidateEdgeConfig(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateEdgeConfig_WithInvalidHubUrl_ShouldFail()
    {
        // Arrange
        var config = CreateValidEdgeConfig();
        config.Uplink.HubUrl = "invalid-url";

        // Act
        var result = _validator.ValidateEdgeConfig(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid Hub URL format"));
    }

    [Fact]
    public void ValidateEdgeConfig_WithShortReconnectDelay_ShouldFail()
    {
        // Arrange
        var config = CreateValidEdgeConfig();
        config.Uplink.ReconnectDelayMs = 500;

        // Act
        var result = _validator.ValidateEdgeConfig(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Reconnect delay should be at least 1000ms"));
    }

    [Fact]
    public void ValidateEdgeConfig_WithLoadGenEnabled_ShouldGenerateWarning()
    {
        // Arrange
        var config = CreateValidEdgeConfig();
        config.LoadGen.Enabled = true;

        // Act
        var result = _validator.ValidateEdgeConfig(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Load generator is enabled"));
    }

    [Fact]
    public void ValidateMachinesConfig_WithValidMachines_ShouldPass()
    {
        // Arrange
        var machines = CreateValidMachinesConfig();

        // Act
        var result = _validator.ValidateMachinesConfig(machines);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateMachinesConfig_WithDuplicateMachineIds_ShouldFail()
    {
        // Arrange
        var machines = CreateValidMachinesConfig();
        machines.Add(new EdgeConnectorConfig
        {
            MachineId = machines[0].MachineId, // Duplicate ID
            Make = "TEST",
            IpAddress = "10.10.20.99",
            Protocol = "MTConnect",
            Settings = new Dictionary<string, object> { { "BaseUrl", "http://test" } }
        });

        // Act
        var result = _validator.ValidateMachinesConfig(machines);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate machine ID"));
    }

    [Fact]
    public void ValidateMachinesConfig_WithInvalidIpAddress_ShouldFail()
    {
        // Arrange
        var machines = CreateValidMachinesConfig();
        machines[0].IpAddress = "invalid-ip";

        // Act
        var result = _validator.ValidateMachinesConfig(machines);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid IP address format"));
    }

    [Fact]
    public void ValidateMachinesConfig_WithMissingFocasPort_ShouldFail()
    {
        // Arrange
        var machines = new List<EdgeConnectorConfig>
        {
            new EdgeConnectorConfig
            {
                MachineId = "FANUC-01",
                Make = "FANUC",
                IpAddress = "10.10.20.11",
                Protocol = "FOCAS",
                Settings = new Dictionary<string, object>() // Missing Port
            }
        };

        // Act
        var result = _validator.ValidateMachinesConfig(machines);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("FOCAS protocol requires 'Port' setting"));
    }

    [Fact]
    public void ValidateMachinesConfig_WithMissingOpcUaEndpoint_ShouldFail()
    {
        // Arrange
        var machines = new List<EdgeConnectorConfig>
        {
            new EdgeConnectorConfig
            {
                MachineId = "SIEMENS-01",
                Make = "SIEMENS",
                IpAddress = "10.10.20.12",
                Protocol = "OPC UA",
                Settings = new Dictionary<string, object>() // Missing EndpointUrl
            }
        };

        // Act
        var result = _validator.ValidateMachinesConfig(machines);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("OPC UA protocol requires 'EndpointUrl' setting"));
    }

    [Fact]
    public void ValidateMachinesConfig_WithInvalidOpcUaEndpoint_ShouldFail()
    {
        // Arrange
        var machines = new List<EdgeConnectorConfig>
        {
            new EdgeConnectorConfig
            {
                MachineId = "SIEMENS-01",
                Make = "SIEMENS",
                IpAddress = "10.10.20.12",
                Protocol = "OPC UA",
                Settings = new Dictionary<string, object>
                {
                    { "EndpointUrl", "http://invalid-protocol" } // Should be opc.tcp://
                }
            }
        };

        // Act
        var result = _validator.ValidateMachinesConfig(machines);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid OPC UA endpoint URL format"));
    }

    [Fact]
    public void ValidateMachinesConfig_WithMissingMTConnectBaseUrl_ShouldFail()
    {
        // Arrange
        var machines = new List<EdgeConnectorConfig>
        {
            new EdgeConnectorConfig
            {
                MachineId = "HAAS-01",
                Make = "HAAS",
                IpAddress = "10.10.20.13",
                Protocol = "MTConnect",
                Settings = new Dictionary<string, object>() // Missing BaseUrl
            }
        };

        // Act
        var result = _validator.ValidateMachinesConfig(machines);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MTConnect protocol requires 'BaseUrl' setting"));
    }

    [Fact]
    public void ValidateMachinesConfig_WithFastPollInterval_ShouldGenerateWarning()
    {
        // Arrange
        var machines = new List<EdgeConnectorConfig>
        {
            new EdgeConnectorConfig
            {
                MachineId = "FANUC-01",
                Make = "FANUC",
                IpAddress = "10.10.20.11",
                Protocol = "FOCAS",
                Settings = new Dictionary<string, object>
                {
                    { "Port", 8193 },
                    { "PollIntervalMs", 50 } // Very fast polling
                }
            }
        };

        // Act
        var result = _validator.ValidateMachinesConfig(machines);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("poll interval below 100ms may cause performance issues"));
    }

    [Fact]
    public void ValidateJsonConfiguration_WithValidEdgeConfig_ShouldPass()
    {
        // Arrange
        var config = CreateValidEdgeConfig();
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Act
        var result = _validator.ValidateJsonConfiguration(json, "edge");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateJsonConfiguration_WithInvalidJson_ShouldFail()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var result = _validator.ValidateJsonConfiguration(invalidJson, "edge");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid JSON format"));
    }

    [Fact]
    public void ValidateJsonConfiguration_WithUnknownConfigType_ShouldFail()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = _validator.ValidateJsonConfiguration(json, "unknown");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Unknown configuration type"));
    }

    [Fact]
    public void ValidateJsonConfiguration_WithValidMachinesConfig_ShouldPass()
    {
        // Arrange
        var machines = CreateValidMachinesConfig();
        var wrapper = new { machines };
        var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Act
        var result = _validator.ValidateJsonConfiguration(json, "machines");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private EdgeConfig CreateValidEdgeConfig()
    {
        return new EdgeConfig
        {
            SiteId = "TEST-SITE",
            Timezone = "UTC",
            Uplink = new UplinkConfig
            {
                HubUrl = "https://localhost:7228/hubs/machines",
                ReconnectDelayMs = 5000,
                Batch = new BatchConfig
                {
                    MaxEvents = 50,
                    MaxSizeBytes = 32768,
                    FlushIntervalMs = 5000
                }
            },
            AdminApi = new AdminApiConfig
            {
                Listen = "http://localhost:9080"
            },
            Storage = new StorageConfig
            {
                Root = "./data",
                Sqlite = new SqliteConfig
                {
                    Path = "./data/test.db"
                }
            },
            Queue = new QueueConfig
            {
                Capacity = 10000
            },
            LoadGen = new LoadGenConfig
            {
                Enabled = false,
                Machines = 0
            }
        };
    }

    private List<EdgeConnectorConfig> CreateValidMachinesConfig()
    {
        return new List<EdgeConnectorConfig>
        {
            new EdgeConnectorConfig
            {
                MachineId = "FANUC-01",
                Make = "FANUC",
                IpAddress = "10.10.20.11",
                Protocol = "FOCAS",
                Settings = new Dictionary<string, object>
                {
                    { "Port", 8193 },
                    { "IsSimulator", true }
                }
            },
            new EdgeConnectorConfig
            {
                MachineId = "SIEMENS-01",
                Make = "SIEMENS",
                IpAddress = "10.10.20.12",
                Protocol = "OPC UA",
                Settings = new Dictionary<string, object>
                {
                    { "EndpointUrl", "opc.tcp://10.10.20.12:4840" },
                    { "IsSimulator", true }
                }
            },
            new EdgeConnectorConfig
            {
                MachineId = "HAAS-01",
                Make = "HAAS",
                IpAddress = "10.10.20.13",
                Protocol = "MTConnect",
                Settings = new Dictionary<string, object>
                {
                    { "BaseUrl", "http://10.10.20.13:8082/VF2SS" },
                    { "IsSimulator", true }
                }
            }
        };
    }
}