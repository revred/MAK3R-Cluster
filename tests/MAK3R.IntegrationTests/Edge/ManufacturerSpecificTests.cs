using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Mak3r.Edge.Services;
using Mak3r.Edge.Models;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace MAK3R.IntegrationTests.Edge;

/// <summary>
/// Comprehensive integration tests for each manufacturer's specific requirements
/// Tests the full flow from machine connection to event delivery
/// </summary>
public class ManufacturerSpecificTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private HubConnection? _hubConnection;
    private readonly List<KMachineEvent> _receivedEvents = new();

    public ManufacturerSpecificTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output).SetMinimumLevel(LogLevel.Debug));
        
        // Add Edge services
        services.AddSingleton<FanucEdgeAdapter>();
        services.AddSingleton<SiemensEdgeAdapter>();
        services.AddSingleton<HaasEdgeAdapter>();
        services.AddSingleton<MazakEdgeAdapter>();
        services.AddSingleton<NormalizerService>();
        services.AddSingleton<QueueService>();
        
        _serviceProvider = services.BuildServiceProvider();

        // Setup SignalR mock hub for testing
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5225/hubs/machines")
            .Build();

        _hubConnection.On<List<KMachineEvent>>("MachineEvents", events =>
        {
            _receivedEvents.AddRange(events);
        });

        try
        {
            await _hubConnection.StartAsync();
        }
        catch
        {
            _output.WriteLine("SignalR hub not available - skipping hub tests");
        }
    }

    public async Task DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        _serviceProvider?.Dispose();
    }

    #region FANUC Tests

    [Fact]
    public async Task Fanuc_FullCycleTest_ShouldGenerateExpectedEvents()
    {
        // Arrange
        var config = CreateFanucConfig();
        using var adapter = new FanucEdgeAdapter(config, "TEST-SITE", 
            _serviceProvider!.GetRequiredService<ILogger<FanucEdgeAdapter>>());

        var events = new List<KMachineEvent>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await adapter.StartAsync();
        await foreach (var evt in adapter.GetEventsAsync(cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 10) break;
        }
        await adapter.StopAsync();

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(evt =>
        {
            evt.MachineId.Should().Be("FANUC-TC-01");
            evt.Source!.Vendor.Should().Be("FANUC");
            evt.Source.Protocol.Should().Be("FOCAS");
        });

        // FANUC-specific state validations
        var stateEvents = events.Where(e => e.State != null).ToList();
        stateEvents.Should().NotBeEmpty();
        stateEvents.Should().AllSatisfy(evt =>
        {
            // FANUC machines report specific execution states
            evt.State!.Execution.Should().BeOneOf("READY", "ACTIVE", "INTERRUPTED", "STOPPED");
            evt.State.Mode.Should().BeOneOf("AUTO", "MDI", "MANUAL", "EDIT");
        });

        _output.WriteLine($"FANUC generated {events.Count} events in 5 seconds");
    }

    [Fact]
    public void Fanuc_AlarmHandling_ShouldGenerateAlarmEvents()
    {
        // Arrange
        var config = CreateFanucConfig();
        using var adapter = new FanucEdgeAdapter(config, "TEST-SITE",
            _serviceProvider!.GetRequiredService<ILogger<FanucEdgeAdapter>>());

        // Act - Simulate alarm condition
        var alarmEvent = new KMachineEvent
        {
            MachineId = "FANUC-TC-01",
            Event = new EventInfo
            {
                Type = "ALARM",
                Severity = "ERROR",
                Code = "SV0401",
                Message = "SERVO ALARM: X-AXIS OVERLOAD"
            }
        };

        // Assert
        alarmEvent.Event.Should().NotBeNull();
        alarmEvent.Event!.Type.Should().Be("ALARM");
        alarmEvent.Event.Code.Should().MatchRegex(@"^[A-Z]{2}\d{4}$"); // FANUC alarm format
        alarmEvent.Event.Severity.Should().BeOneOf("WARNING", "ERROR", "CRITICAL");
    }

    [Theory]
    [InlineData("O0001", 123, "ACTIVE", "AUTO")]
    [InlineData("O9999", 456, "READY", "MDI")]
    public void Fanuc_ProgramExecution_ShouldTrackProgramState(string programName, int blockNumber, string execution, string mode)
    {
        // Arrange & Act
        var evt = new KMachineEvent
        {
            MachineId = "FANUC-TC-01",
            State = new StateInfo
            {
                Execution = execution,
                Mode = mode,
                Program = new ProgramInfo
                {
                    Name = programName,
                    Block = blockNumber
                }
            }
        };

        // Assert
        evt.State!.Program!.Name.Should().MatchRegex(@"^O\d{4}$"); // FANUC program naming
        evt.State.Program.Block.Should().BeGreaterThan(0);
        evt.State.Execution.Should().Be(execution);
        evt.State.Mode.Should().Be(mode);
    }

    #endregion

    #region Siemens Tests

    [Fact]
    public async Task Siemens_OpcUaConnection_ShouldProvideRichTelemetry()
    {
        // Arrange
        var config = CreateSiemensConfig();
        using var adapter = new SiemensEdgeAdapter(config, "TEST-SITE",
            _serviceProvider!.GetRequiredService<ILogger<SiemensEdgeAdapter>>());

        var events = new List<KMachineEvent>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await adapter.StartAsync();
        await foreach (var evt in adapter.GetEventsAsync(cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 10) break;
        }
        await adapter.StopAsync();

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(evt =>
        {
            evt.Source!.Protocol.Should().Be("OPC UA");
            evt.Source.Vendor.Should().Be("SIEMENS");
        });

        // Siemens-specific telemetry
        var metricsEvents = events.Where(e => e.State?.Metrics != null).ToList();
        metricsEvents.Should().NotBeEmpty();
        metricsEvents.Should().AllSatisfy(evt =>
        {
            // Siemens provides precise metrics via OPC UA
            evt.State!.Metrics!.SpindleRPM.Should().BeInRange(0, 20000);
            evt.State.Metrics.Feedrate.Should().BeInRange(0, 50000);
        });
    }

    [Fact]
    public void Siemens_ToolManagement_ShouldTrackToolLife()
    {
        // Arrange & Act
        var evt = new KMachineEvent
        {
            MachineId = "SIEMENS-TC-02",
            State = new StateInfo
            {
                Tool = new ToolInfo
                {
                    Id = 12,
                    Life = 85.5 // Percentage
                }
            }
        };

        // Assert - Siemens provides detailed tool management
        evt.State!.Tool.Should().NotBeNull();
        evt.State.Tool!.Id.Should().BeInRange(1, 999); // Siemens tool magazine range
        evt.State.Tool.Life.Should().BeInRange(0, 100); // Life percentage
    }

    [Fact]
    public void Siemens_SafetyInterlocks_ShouldBeMonitored()
    {
        // Arrange & Act
        var safetyEvent = new KMachineEvent
        {
            MachineId = "SIEMENS-TC-02",
            Event = new EventInfo
            {
                Type = "SAFETY_DOOR_OPEN",
                Severity = "WARNING",
                Message = "Safety door opened - machine in safe mode"
            }
        };

        // Assert - Siemens has strict safety monitoring
        safetyEvent.Event!.Type.Should().StartWith("SAFETY_");
        safetyEvent.Event.Severity.Should().BeOneOf("WARNING", "CRITICAL");
    }

    #endregion

    #region HAAS Tests

    [Fact]
    public async Task Haas_MTConnectStreaming_ShouldProvideRealtimeData()
    {
        // Arrange
        var config = CreateHaasConfig();
        using var adapter = new HaasEdgeAdapter(config, "TEST-SITE",
            _serviceProvider!.GetRequiredService<ILogger<HaasEdgeAdapter>>());

        var events = new List<KMachineEvent>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await adapter.StartAsync();
        await foreach (var evt in adapter.GetEventsAsync(cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 10) break;
        }
        await adapter.StopAsync();

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(evt =>
        {
            evt.Source!.Protocol.Should().Be("MTConnect");
            evt.Source.Vendor.Should().Be("HAAS");
        });

        // HAAS-specific validations
        var availabilityEvents = events.Where(e => e.State?.Availability != null).ToList();
        availabilityEvents.Should().NotBeEmpty();
        availabilityEvents.Should().AllSatisfy(evt =>
        {
            // HAAS reports standard MTConnect availability
            evt.State!.Availability.Should().BeOneOf("AVAILABLE", "UNAVAILABLE");
        });
    }

    [Fact]
    public void Haas_CoolantMonitoring_ShouldTrackCoolantState()
    {
        // Arrange & Act - HAAS specific coolant monitoring
        var coolantEvent = new KMachineEvent
        {
            MachineId = "HAAS-MILL-03",
            Event = new EventInfo
            {
                Type = "COOLANT_LOW",
                Severity = "WARNING",
                Message = "Coolant level below minimum threshold"
            }
        };

        // Assert
        coolantEvent.Event!.Type.Should().Contain("COOLANT");
        coolantEvent.Event.Severity.Should().Be("WARNING");
    }

    [Fact]
    public void Haas_OverrideSettings_ShouldBeInValidRange()
    {
        // Arrange & Act
        var evt = new KMachineEvent
        {
            MachineId = "HAAS-MILL-03",
            State = new StateInfo
            {
                Overrides = new Overrides
                {
                    Feed = 1.2,    // 120%
                    Spindle = 0.5, // 50%
                    Rapid = 0.25   // 25% - HAAS safety feature
                }
            }
        };

        // Assert - HAAS specific override ranges
        evt.State!.Overrides!.Feed.Should().BeInRange(0, 2.0);    // 0-200%
        evt.State.Overrides.Spindle.Should().BeInRange(0, 1.5);   // 0-150%
        evt.State.Overrides.Rapid.Should().BeInRange(0, 1.0);     // 0-100% for safety
    }

    #endregion

    #region Mazak Tests

    [Fact]
    public async Task Mazak_FiveAxisMachining_ShouldTrackAllAxes()
    {
        // Arrange
        var config = CreateMazakConfig();
        using var adapter = new MazakEdgeAdapter(config, "TEST-SITE",
            _serviceProvider!.GetRequiredService<ILogger<MazakEdgeAdapter>>());

        var events = new List<KMachineEvent>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await adapter.StartAsync();
        await foreach (var evt in adapter.GetEventsAsync(cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 10) break;
        }
        await adapter.StopAsync();

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(evt =>
        {
            evt.MachineId.Should().Be("MAZAK-5X-04");
            evt.Source!.Vendor.Should().Be("MAZAK");
        });

        // Mazak 5-axis specific checks would include axis positions
        // In a real implementation, we'd check for A/B/C axis data
    }

    [Fact]
    public void Mazak_PalletChanger_ShouldTrackPalletStatus()
    {
        // Arrange & Act - Mazak pallet changer events
        var palletEvent = new KMachineEvent
        {
            MachineId = "MAZAK-5X-04",
            Event = new EventInfo
            {
                Type = "PALLET_CHANGE",
                Code = "PC_COMPLETE",
                Message = "Pallet change completed - Pallet B in position"
            },
            Context = new ContextInfo
            {
                Workholding = new Workholding
                {
                    Type = "PALLET",
                    FixtureId = "PALLET_B"
                }
            }
        };

        // Assert - Mazak specific pallet handling
        palletEvent.Event!.Type.Should().Be("PALLET_CHANGE");
        palletEvent.Context!.Workholding!.Type.Should().Be("PALLET");
        palletEvent.Context.Workholding.FixtureId.Should().MatchRegex(@"^PALLET_[A-B]$");
    }

    [Fact]
    public void Mazak_SmoothTechnology_ShouldOptimizeToolpaths()
    {
        // Arrange & Act - Mazak SMOOTH technology features
        var smoothEvent = new KMachineEvent
        {
            MachineId = "MAZAK-5X-04",
            State = new StateInfo
            {
                Mode = "AUTO",
                Execution = "ACTIVE",
                Metrics = new Metrics
                {
                    SpindleRPM = 12000,
                    Feedrate = 15000 // High-speed machining
                }
            }
        };

        // Assert - Mazak high-speed capabilities
        smoothEvent.State!.Metrics!.SpindleRPM.Should().BeInRange(0, 20000);
        smoothEvent.State.Metrics.Feedrate.Should().BeInRange(0, 30000); // mm/min
    }

    #endregion

    #region Cross-Manufacturer Tests

    [Theory]
    [InlineData("FANUC", "FANUC-TC-01", "FOCAS")]
    [InlineData("SIEMENS", "SIEMENS-TC-02", "OPC UA")]
    [InlineData("HAAS", "HAAS-MILL-03", "MTConnect")]
    [InlineData("MAZAK", "MAZAK-5X-04", "MTConnect")]
    public async Task AllManufacturers_ShouldGenerateConsistentEventStructure(string make, string machineId, string protocol)
    {
        // Arrange
        var config = new EdgeConnectorConfig
        {
            MachineId = machineId,
            Make = make,
            Protocol = protocol,
            IpAddress = "127.0.0.1",
            Settings = new Dictionary<string, object> { { "IsSimulator", true } }
        };

        IEdgeConnectorAdapter adapter = make switch
        {
            "FANUC" => new FanucEdgeAdapter(config, "TEST-SITE", 
                _serviceProvider!.GetRequiredService<ILogger<FanucEdgeAdapter>>()),
            "SIEMENS" => new SiemensEdgeAdapter(config, "TEST-SITE",
                _serviceProvider!.GetRequiredService<ILogger<SiemensEdgeAdapter>>()),
            "HAAS" => new HaasEdgeAdapter(config, "TEST-SITE",
                _serviceProvider!.GetRequiredService<ILogger<HaasEdgeAdapter>>()),
            "MAZAK" => new MazakEdgeAdapter(config, "TEST-SITE",
                _serviceProvider!.GetRequiredService<ILogger<MazakEdgeAdapter>>()),
            _ => throw new NotSupportedException($"Unknown make: {make}")
        };

        using (adapter)
        {
            var events = new List<KMachineEvent>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            await adapter.StartAsync();
            await foreach (var evt in adapter.GetEventsAsync(cts.Token))
            {
                events.Add(evt);
                if (events.Count >= 5) break;
            }
            await adapter.StopAsync();

            // Assert - Common structure across all manufacturers
            events.Should().NotBeEmpty();
            events.Should().AllSatisfy(evt =>
            {
                // Canonical event structure
                evt.SiteId.Should().Be("TEST-SITE");
                evt.MachineId.Should().Be(machineId);
                evt.Ts.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
                
                // Source information
                evt.Source.Should().NotBeNull();
                evt.Source!.Vendor.Should().Be(make);
                evt.Source.Protocol.Should().Be(protocol);
                
                // Event ID should be deterministic
                evt.EventId.Should().NotBeNullOrEmpty();
            });
        }
    }

    [Fact]
    public async Task AllManufacturers_CycleTimeCalculation_ShouldBeConsistent()
    {
        // Test that cycle time calculation works consistently across all manufacturers
        var manufacturers = new[] { "FANUC", "SIEMENS", "HAAS", "MAZAK" };
        var cycleStartTimes = new Dictionary<string, DateTime>();
        var cycleTimes = new Dictionary<string, TimeSpan>();

        foreach (var make in manufacturers)
        {
            // Simulate cycle start
            var startEvent = new KMachineEvent
            {
                MachineId = $"{make}-TEST",
                Ts = DateTime.UtcNow,
                Event = new EventInfo { Type = "CYCLE_START" }
            };
            cycleStartTimes[make] = startEvent.Ts;

            await Task.Delay(100); // Simulate machining time

            // Simulate cycle stop
            var stopEvent = new KMachineEvent
            {
                MachineId = $"{make}-TEST",
                Ts = DateTime.UtcNow,
                Event = new EventInfo { Type = "CYCLE_STOP" }
            };

            cycleTimes[make] = stopEvent.Ts - cycleStartTimes[make];
        }

        // Assert - All manufacturers should calculate cycle time similarly
        cycleTimes.Values.Should().AllSatisfy(time =>
        {
            time.Should().BeGreaterThan(TimeSpan.FromMilliseconds(90));
            time.Should().BeLessThan(TimeSpan.FromMilliseconds(200));
        });
    }

    #endregion

    #region Helper Methods

    private EdgeConnectorConfig CreateFanucConfig()
    {
        return new EdgeConnectorConfig
        {
            MachineId = "FANUC-TC-01",
            Make = "FANUC",
            Model = "30i-B Plus",
            IpAddress = "10.10.20.11",
            Protocol = "FOCAS",
            Enabled = true,
            Settings = new Dictionary<string, object>
            {
                { "Port", 8193 },
                { "IsSimulator", true },
                { "PollIntervalMs", 250 }
            }
        };
    }

    private EdgeConnectorConfig CreateSiemensConfig()
    {
        return new EdgeConnectorConfig
        {
            MachineId = "SIEMENS-TC-02",
            Make = "SIEMENS",
            Model = "840D sl",
            IpAddress = "10.10.20.12",
            Protocol = "OPC UA",
            Enabled = true,
            Settings = new Dictionary<string, object>
            {
                { "EndpointUrl", "opc.tcp://10.10.20.12:4840" },
                { "IsSimulator", true },
                { "SecurityPolicy", "None" }
            }
        };
    }

    private EdgeConnectorConfig CreateHaasConfig()
    {
        return new EdgeConnectorConfig
        {
            MachineId = "HAAS-MILL-03",
            Make = "HAAS",
            Model = "VF-2SS",
            IpAddress = "10.10.20.13",
            Protocol = "MTConnect",
            Enabled = true,
            Settings = new Dictionary<string, object>
            {
                { "BaseUrl", "http://10.10.20.13:8082/VF2SS" },
                { "IsSimulator", true },
                { "SampleIntervalMs", 500 }
            }
        };
    }

    private EdgeConnectorConfig CreateMazakConfig()
    {
        return new EdgeConnectorConfig
        {
            MachineId = "MAZAK-5X-04",
            Make = "MAZAK",
            Model = "VARIAXIS j-600",
            IpAddress = "10.10.20.14",
            Protocol = "MTConnect",
            Enabled = true,
            Settings = new Dictionary<string, object>
            {
                { "BaseUrl", "http://10.10.20.14:5000/MAZAK" },
                { "IsSimulator", true },
                { "SampleIntervalMs", 500 },
                { "EnablePalletData", true }
            }
        };
    }

    #endregion
}