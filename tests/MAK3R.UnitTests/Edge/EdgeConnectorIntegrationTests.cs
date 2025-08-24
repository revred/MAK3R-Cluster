using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mak3r.Edge.Services;
using Mak3r.Edge.Models;

namespace MAK3R.UnitTests.Edge;

public class EdgeConnectorIntegrationTests
{
    private readonly Mock<ILogger<FanucEdgeAdapter>> _fanucLoggerMock;
    private readonly Mock<ILogger<SiemensEdgeAdapter>> _siemensLoggerMock;
    private readonly Mock<ILogger<HaasEdgeAdapter>> _haasLoggerMock;
    private readonly Mock<ILogger<MazakEdgeAdapter>> _mazakLoggerMock;

    public EdgeConnectorIntegrationTests()
    {
        _fanucLoggerMock = new Mock<ILogger<FanucEdgeAdapter>>();
        _siemensLoggerMock = new Mock<ILogger<SiemensEdgeAdapter>>();
        _haasLoggerMock = new Mock<ILogger<HaasEdgeAdapter>>();
        _mazakLoggerMock = new Mock<ILogger<MazakEdgeAdapter>>();
    }

    [Fact]
    public void FanucEdgeAdapter_WithValidConfig_ShouldCreateSuccessfully()
    {
        // Arrange
        var config = new EdgeConnectorConfig
        {
            MachineId = "FANUC-TC-01",
            Make = "FANUC",
            IpAddress = "10.10.20.11",
            Protocol = "FOCAS",
            Settings = new Dictionary<string, object>
            {
                { "Port", 8193 },
                { "IsSimulator", true },
                { "PollIntervalMs", 250 }
            }
        };

        // Act
        using var adapter = new FanucEdgeAdapter(config, "BLR-Plant-01", _fanucLoggerMock.Object);

        // Assert
        adapter.Should().NotBeNull();
        adapter.Id.Should().Be("FANUC-TC-01");
        adapter.MachineId.Should().Be("FANUC-TC-01");
        adapter.Protocol.Should().Be("FOCAS");
    }

    [Fact]
    public void SiemensEdgeAdapter_WithValidConfig_ShouldCreateSuccessfully()
    {
        // Arrange
        var config = new EdgeConnectorConfig
        {
            MachineId = "SIEMENS-TC-02",
            Make = "SIEMENS",
            IpAddress = "10.10.20.12",
            Protocol = "OPC UA",
            Settings = new Dictionary<string, object>
            {
                { "EndpointUrl", "opc.tcp://10.10.20.12:4840" },
                { "IsSimulator", true },
                { "SecurityPolicy", "None" }
            }
        };

        // Act
        using var adapter = new SiemensEdgeAdapter(config, "BLR-Plant-01", _siemensLoggerMock.Object);

        // Assert
        adapter.Should().NotBeNull();
        adapter.Id.Should().Be("SIEMENS-TC-02");
        adapter.MachineId.Should().Be("SIEMENS-TC-02");
        adapter.Protocol.Should().Be("OPC UA");
    }

    [Fact]
    public void HaasEdgeAdapter_WithValidConfig_ShouldCreateSuccessfully()
    {
        // Arrange
        var config = new EdgeConnectorConfig
        {
            MachineId = "HAAS-MILL-03",
            Make = "HAAS",
            IpAddress = "10.10.20.13",
            Protocol = "MTConnect",
            Settings = new Dictionary<string, object>
            {
                { "BaseUrl", "http://10.10.20.13:8082/VF2SS" },
                { "IsSimulator", true },
                { "SampleIntervalMs", 500 }
            }
        };

        // Act
        using var adapter = new HaasEdgeAdapter(config, "BLR-Plant-01", _haasLoggerMock.Object);

        // Assert
        adapter.Should().NotBeNull();
        adapter.Id.Should().Be("HAAS-MILL-03");
        adapter.MachineId.Should().Be("HAAS-MILL-03");
        adapter.Protocol.Should().Be("MTConnect");
    }

    [Fact]
    public void MazakEdgeAdapter_WithValidConfig_ShouldCreateSuccessfully()
    {
        // Arrange
        var config = new EdgeConnectorConfig
        {
            MachineId = "MAZAK-5X-04",
            Make = "MAZAK",
            IpAddress = "10.10.20.14",
            Protocol = "MTConnect",
            Settings = new Dictionary<string, object>
            {
                { "BaseUrl", "http://10.10.20.14:5000/MAZAK" },
                { "IsSimulator", true },
                { "SampleIntervalMs", 500 }
            }
        };

        // Act
        using var adapter = new MazakEdgeAdapter(config, "BLR-Plant-01", _mazakLoggerMock.Object);

        // Assert
        adapter.Should().NotBeNull();
        adapter.Id.Should().Be("MAZAK-5X-04");
        adapter.MachineId.Should().Be("MAZAK-5X-04");
        adapter.Protocol.Should().Be("MTConnect");
    }

    [Fact]
    public async Task AllAdapters_StartAndStop_ShouldExecuteWithoutErrors()
    {
        // Arrange
        var adapters = CreateAllAdapters();

        try
        {
            // Act & Assert - Start all adapters
            foreach (var adapter in adapters)
            {
                var startAction = async () => await adapter.StartAsync();
                await startAction.Should().NotThrowAsync();
            }

            // Act & Assert - Stop all adapters
            foreach (var adapter in adapters)
            {
                var stopAction = async () => await adapter.StopAsync();
                await stopAction.Should().NotThrowAsync();
            }
        }
        finally
        {
            // Cleanup
            foreach (var adapter in adapters)
            {
                adapter.Dispose();
            }
        }
    }

    [Fact]
    public async Task AllAdapters_CheckHealth_ShouldReturnHealthyForSimulators()
    {
        // Arrange
        var adapters = CreateAllAdapters();

        try
        {
            foreach (var adapter in adapters)
            {
                await adapter.StartAsync();

                // Act
                var health = await adapter.CheckHealthAsync();

                // Assert
                health.Should().NotBeNull();
                health.IsHealthy.Should().BeTrue("Simulator should always be healthy");
                health.Message.Should().NotBeNullOrEmpty();
            }
        }
        finally
        {
            foreach (var adapter in adapters)
            {
                await adapter.StopAsync();
                adapter.Dispose();
            }
        }
    }

    [Theory]
    [InlineData("FANUC")]
    [InlineData("SIEMENS")]
    [InlineData("HAAS")]
    [InlineData("MAZAK")]
    public async Task EdgeAdapter_GetEvents_ShouldGenerateKMachineEvents(string make)
    {
        // Arrange
        using var adapter = CreateAdapterByMake(make);
        await adapter.StartAsync();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            // Act
            var events = new List<KMachineEvent>();
            await foreach (var evt in adapter.GetEventsAsync(cts.Token))
            {
                events.Add(evt);
                if (events.Count >= 2) break; // Collect a few events
            }

            // Assert
            events.Should().NotBeEmpty("Simulator should generate events");
            events.Should().AllSatisfy(evt =>
            {
                evt.SiteId.Should().Be("BLR-Plant-01");
                evt.MachineId.Should().NotBeNullOrEmpty();
                evt.Ts.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
                evt.Source.Should().NotBeNull();
                evt.Source!.Vendor.Should().Be(make);
            });
        }
        finally
        {
            await adapter.StopAsync();
        }
    }

    [Fact]
    public async Task KMachineEvent_Serialization_ShouldPreserveAllFields()
    {
        // Arrange
        var originalEvent = new KMachineEvent
        {
            SiteId = "TEST-SITE",
            MachineId = "TEST-MACHINE",
            Ts = DateTime.UtcNow,
            Source = new SourceInfo { Vendor = "TEST", Protocol = "TEST", Ip = "127.0.0.1" },
            State = new StateInfo
            {
                Execution = "ACTIVE",
                Mode = "AUTO",
                Program = new ProgramInfo { Name = "TEST-PROG", Block = 123 },
                Tool = new ToolInfo { Id = 5, Life = 75.5 },
                Overrides = new Overrides { Feed = 1.0, Spindle = 0.95, Rapid = 0.8 },
                Metrics = new Metrics { SpindleRPM = 2500, Feedrate = 1200, PartCount = 42 }
            },
            Event = new EventInfo { Type = "CYCLE_START", Severity = "INFO", Code = "OK" },
            Context = new ContextInfo
            {
                Job = new JobInfo { Id = "WO-123", Op = "10", Barcode = "WO-123-10" }
            }
        };

        // Act - Serialize and deserialize using MessagePack
        var serialized = MessagePack.MessagePackSerializer.Serialize(originalEvent);
        var deserialized = MessagePack.MessagePackSerializer.Deserialize<KMachineEvent>(serialized);

        // Assert
        deserialized.Should().BeEquivalentTo(originalEvent, options => options
            .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1)))
            .WhenTypeIs<DateTime>());
    }

    private List<IEdgeConnectorAdapter> CreateAllAdapters()
    {
        return new List<IEdgeConnectorAdapter>
        {
            CreateAdapterByMake("FANUC"),
            CreateAdapterByMake("SIEMENS"),
            CreateAdapterByMake("HAAS"),
            CreateAdapterByMake("MAZAK")
        };
    }

    private IEdgeConnectorAdapter CreateAdapterByMake(string make)
    {
        return make switch
        {
            "FANUC" => new FanucEdgeAdapter(
                new EdgeConnectorConfig
                {
                    MachineId = $"{make}-TEST",
                    Make = make,
                    IpAddress = "127.0.0.1",
                    Protocol = "FOCAS",
                    Settings = new Dictionary<string, object> 
                    { 
                        { "IsSimulator", true }, 
                        { "PollIntervalMs", 100 } 
                    }
                },
                "BLR-Plant-01",
                _fanucLoggerMock.Object
            ),
            "SIEMENS" => new SiemensEdgeAdapter(
                new EdgeConnectorConfig
                {
                    MachineId = $"{make}-TEST",
                    Make = make,
                    IpAddress = "127.0.0.1",
                    Protocol = "OPC UA",
                    Settings = new Dictionary<string, object> 
                    { 
                        { "IsSimulator", true } 
                    }
                },
                "BLR-Plant-01",
                _siemensLoggerMock.Object
            ),
            "HAAS" => new HaasEdgeAdapter(
                new EdgeConnectorConfig
                {
                    MachineId = $"{make}-TEST",
                    Make = make,
                    IpAddress = "127.0.0.1",
                    Protocol = "MTConnect",
                    Settings = new Dictionary<string, object> 
                    { 
                        { "IsSimulator", true }, 
                        { "SampleIntervalMs", 100 } 
                    }
                },
                "BLR-Plant-01",
                _haasLoggerMock.Object
            ),
            "MAZAK" => new MazakEdgeAdapter(
                new EdgeConnectorConfig
                {
                    MachineId = $"{make}-TEST",
                    Make = make,
                    IpAddress = "127.0.0.1",
                    Protocol = "MTConnect",
                    Settings = new Dictionary<string, object> 
                    { 
                        { "IsSimulator", true }, 
                        { "SampleIntervalMs", 100 } 
                    }
                },
                "BLR-Plant-01",
                _mazakLoggerMock.Object
            ),
            _ => throw new ArgumentException($"Unknown make: {make}")
        };
    }
}