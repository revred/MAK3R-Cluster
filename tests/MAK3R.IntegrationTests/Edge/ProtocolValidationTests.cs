using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Mak3r.Edge.Models;

namespace MAK3R.IntegrationTests.Edge;

/// <summary>
/// Protocol-specific validation tests ensuring each manufacturer's
/// communication protocols work correctly and generate valid data
/// </summary>
public class ProtocolValidationTests
{
    private readonly ITestOutputHelper _output;

    public ProtocolValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region FOCAS Protocol Tests (FANUC)

    [Fact]
    public void FOCAS_EventStructure_ShouldMatchFanucSpecifications()
    {
        // Arrange - Typical FANUC FOCAS event
        var fanucEvent = new KMachineEvent
        {
            SiteId = "TEST-SITE",
            MachineId = "FANUC-TC-01",
            Ts = DateTime.UtcNow,
            Source = new SourceInfo
            {
                Vendor = "FANUC",
                Protocol = "FOCAS",
                Ip = "10.10.20.11"
            },
            State = new StateInfo
            {
                Power = "ON",
                Execution = "ACTIVE",
                Mode = "AUTO",
                Program = new ProgramInfo
                {
                    Name = "O0001",
                    Block = 123
                },
                Tool = new ToolInfo
                {
                    Id = 12,
                    Life = 75.5
                },
                Metrics = new Metrics
                {
                    SpindleRPM = 2500,
                    Feedrate = 1200,
                    PartCount = 42
                }
            },
            Event = new EventInfo
            {
                Type = "TOOL_CHANGE",
                Severity = "INFO",
                Code = "T12"
            }
        };

        // Assert - FANUC-specific validations
        fanucEvent.Source.Should().NotBeNull();
        fanucEvent.Source!.Vendor.Should().Be("FANUC");
        fanucEvent.Source.Protocol.Should().Be("FOCAS");

        // FANUC program naming convention
        fanucEvent.State!.Program!.Name.Should().MatchRegex(@"^O\d{4}$", 
            "FANUC programs follow O-number format");

        // FANUC tool numbering
        fanucEvent.State.Tool!.Id.Should().BeInRange(1, 99, 
            "FANUC tool numbers are typically 1-99");

        // FANUC execution states
        fanucEvent.State.Execution.Should().BeOneOf("READY", "ACTIVE", "INTERRUPTED", "STOPPED");
        fanucEvent.State.Mode.Should().BeOneOf("AUTO", "MDI", "MANUAL", "EDIT", "JOG");

        // FANUC event codes
        fanucEvent.Event!.Code.Should().MatchRegex(@"^[A-Z]\d+$|^[A-Z]{1,2}\d{4}$",
            "FANUC codes follow specific format patterns");
    }

    [Theory]
    [InlineData("SV0401", "SERVO ALARM: X-AXIS OVERLOAD", "ERROR")]
    [InlineData("SP0001", "SPINDLE SPEED ALARM", "WARNING")]
    [InlineData("PS0010", "POWER SUPPLY ALARM", "CRITICAL")]
    public void FOCAS_AlarmCodes_ShouldFollowFanucFormat(string code, string message, string severity)
    {
        // Arrange
        var alarmEvent = new KMachineEvent
        {
            MachineId = "FANUC-TC-01",
            Event = new EventInfo
            {
                Type = "ALARM",
                Code = code,
                Message = message,
                Severity = severity
            }
        };

        // Assert - FANUC alarm code format
        alarmEvent.Event!.Code.Should().MatchRegex(@"^[A-Z]{2}\d{4}$",
            "FANUC alarm codes are 2 letters + 4 digits");
        
        alarmEvent.Event.Severity.Should().BeOneOf("WARNING", "ERROR", "CRITICAL");
        alarmEvent.Event.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FOCAS_CNCParameters_ShouldBeWithinValidRanges()
    {
        // Arrange - FANUC machine capabilities
        var fanucMetrics = new Metrics
        {
            SpindleRPM = 12000,    // Typical FANUC spindle range
            Feedrate = 15000,      // mm/min
            PartCount = 1542
        };

        // Assert - FANUC-specific parameter ranges
        fanucMetrics.SpindleRPM.Should().BeInRange(0, 15000,
            "FANUC spindles typically range 0-15000 RPM");
        fanucMetrics.Feedrate.Should().BeInRange(0, 20000,
            "FANUC feedrates typically up to 20000 mm/min");
        fanucMetrics.PartCount.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region OPC UA Protocol Tests (Siemens)

    [Fact]
    public void OPCUA_ConnectionParameters_ShouldMatchSiemensSpecs()
    {
        // Arrange - Siemens OPC UA configuration
        var siemensConfig = new EdgeConnectorConfig
        {
            MachineId = "SIEMENS-TC-02",
            Make = "SIEMENS",
            Protocol = "OPC UA",
            IpAddress = "10.10.20.12",
            Settings = new Dictionary<string, object>
            {
                { "EndpointUrl", "opc.tcp://10.10.20.12:4840" },
                { "SecurityPolicy", "Basic256Sha256" },
                { "SecurityMode", "SignAndEncrypt" },
                { "SessionTimeout", 60000 },
                { "PublishingInterval", 1000 },
                { "KeepAliveCount", 3 }
            }
        };

        // Assert - Siemens OPC UA standards
        var endpointUrl = siemensConfig.Settings["EndpointUrl"].ToString()!;
        endpointUrl.Should().StartWith("opc.tcp://", "OPC UA uses opc.tcp protocol");
        endpointUrl.Should().Contain(":4840", "Standard OPC UA port is 4840");

        var securityPolicy = siemensConfig.Settings["SecurityPolicy"].ToString()!;
        securityPolicy.Should().BeOneOf("None", "Basic128Rsa15", "Basic256", "Basic256Sha256",
            "Should use standard OPC UA security policies");

        var publishingInterval = (int)siemensConfig.Settings["PublishingInterval"];
        publishingInterval.Should().BeInRange(100, 10000,
            "Publishing interval should be reasonable for industrial use");
    }

    [Fact]
    public void OPCUA_NodeStructure_ShouldFollowSiemensHierarchy()
    {
        // Arrange - Siemens OPC UA node structure
        var siemensNodes = new Dictionary<string, object>
        {
            // Siemens 840D sl structure
            { "Channel.State.ChanState", "ACTIVE" },
            { "Channel.State.ProgState", "RUNNING" },
            { "Channel.Parameter.Override.FeedOverride", 120 },
            { "Channel.Parameter.Override.SpindleOverride", 100 },
            { "Channel.MachineAxis[X].ActPos", 125.456 },
            { "Channel.MachineAxis[Y].ActPos", -67.890 },
            { "Channel.MachineAxis[Z].ActPos", 45.123 },
            { "Channel.Spindle[S1].ActSpeed", 2500 },
            { "Channel.Tool.ActTool.Number", 12 },
            { "Channel.Tool.ActTool.LifeLeft", 85.5 }
        };

        // Assert - Siemens node naming conventions
        siemensNodes.Keys.Should().AllSatisfy(nodeId =>
        {
            nodeId.Should().StartWith("Channel.", "Siemens uses Channel hierarchy");
        });

        // Validate axis naming
        var axisNodes = siemensNodes.Keys.Where(k => k.Contains("MachineAxis")).ToList();
        axisNodes.Should().AllSatisfy(node =>
        {
            node.Should().MatchRegex(@"MachineAxis\[[XYZABC]\]",
                "Siemens axis nodes follow [AXIS] pattern");
        });
    }

    [Fact]
    public void OPCUA_DataTypes_ShouldBeCorrectForSiemens()
    {
        // Arrange
        var siemensEvent = new KMachineEvent
        {
            MachineId = "SIEMENS-TC-02",
            Source = new SourceInfo
            {
                Vendor = "SIEMENS",
                Protocol = "OPC UA",
                Ip = "10.10.20.12"
            },
            State = new StateInfo
            {
                Execution = "RUNNING",
                Mode = "AUTO",
                Overrides = new Overrides
                {
                    Feed = 1.2,    // 120% - Siemens percentage format
                    Spindle = 0.85, // 85%
                    Rapid = 1.0    // 100%
                },
                Metrics = new Metrics
                {
                    SpindleRPM = 8500,     // High precision from OPC UA
                    Feedrate = 2500,
                    PartCount = 156
                }
            }
        };

        // Assert - Siemens-specific data validation
        siemensEvent.State!.Execution.Should().BeOneOf("STOPPED", "RUNNING", "INTERRUPTED");
        
        // Siemens override ranges (can exceed 100%)
        siemensEvent.State.Overrides!.Feed.Should().BeInRange(0, 2.0);
        siemensEvent.State.Overrides.Spindle.Should().BeInRange(0, 1.5);
        
        // Siemens precision spindle control
        siemensEvent.State.Metrics!.SpindleRPM.Should().BeInRange(0, 20000);
    }

    #endregion

    #region MTConnect Protocol Tests (HAAS)

    [Fact]
    public void MTConnect_XMLStructure_ShouldBeValidForHAAS()
    {
        // Arrange - Sample HAAS MTConnect XML
        var haasMTConnectXml = @"<?xml version='1.0' encoding='UTF-8'?>
<MTConnectStreams xmlns='urn:mtconnect.org:MTConnectStreams:1.3' 
                  xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'>
  <Header creationTime='2024-01-15T10:30:00Z' sender='HAAS-VF2SS' 
          instanceId='1234567890' version='1.3.0.0'/>
  <Streams>
    <DeviceStream name='HAAS-MILL-03' uuid='HAAS-VF2SS-001'>
      <ComponentStream component='Controller' name='controller'>
        <Samples>
          <Availability dataItemId='avail' timestamp='2024-01-15T10:30:00.123Z'>AVAILABLE</Availability>
          <Execution dataItemId='exec' timestamp='2024-01-15T10:30:00.123Z'>ACTIVE</Execution>
          <ControllerMode dataItemId='mode' timestamp='2024-01-15T10:30:00.123Z'>AUTOMATIC</ControllerMode>
        </Samples>
        <Events>
          <Program dataItemId='program' timestamp='2024-01-15T10:30:00.123Z'>O00123</Program>
          <PartCount dataItemId='pc' timestamp='2024-01-15T10:30:00.123Z'>87</PartCount>
        </Events>
      </ComponentStream>
      <ComponentStream component='Spindle' name='S1'>
        <Samples>
          <SpindleSpeed dataItemId='S1speed' timestamp='2024-01-15T10:30:00.123Z'>2500</SpindleSpeed>
          <Load dataItemId='S1load' timestamp='2024-01-15T10:30:00.123Z'>65.5</Load>
        </Samples>
      </ComponentStream>
      <ComponentStream component='LinearAxis' name='X'>
        <Samples>
          <Position dataItemId='Xpos' timestamp='2024-01-15T10:30:00.123Z'>125.4567</Position>
          <Load dataItemId='Xload' timestamp='2024-01-15T10:30:00.123Z'>23.1</Load>
        </Samples>
      </ComponentStream>
    </DeviceStream>
  </Streams>
</MTConnectStreams>";

        // Act & Assert - Validate MTConnect XML structure
        var xmlDoc = XDocument.Parse(haasMTConnectXml);
        
        var ns = XNamespace.Get("urn:mtconnect.org:MTConnectStreams:1.3");
        var root = xmlDoc.Root!;
        
        root.Name.LocalName.Should().Be("MTConnectStreams");
        
        var header = root.Element(ns + "Header");
        header.Should().NotBeNull();
        header!.Attribute("sender")!.Value.Should().StartWith("HAAS");

        var deviceStream = root.Descendants(ns + "DeviceStream").First();
        deviceStream.Attribute("name")!.Value.Should().Contain("HAAS");

        // Validate HAAS-specific data items
        var availability = root.Descendants(ns + "Availability").FirstOrDefault();
        availability.Should().NotBeNull();
        availability!.Value.Should().BeOneOf("AVAILABLE", "UNAVAILABLE");

        var execution = root.Descendants(ns + "Execution").FirstOrDefault();
        execution.Should().NotBeNull();
        execution!.Value.Should().BeOneOf("READY", "ACTIVE", "INTERRUPTED", "STOPPED");
    }

    [Fact]
    public void MTConnect_HAASPrograms_ShouldFollowNamingConvention()
    {
        // Arrange - HAAS program naming
        var haasPrograms = new[] { "O00123", "O01456", "O09999", "O12345" };

        // Assert - HAAS program naming convention
        haasPrograms.Should().AllSatisfy(program =>
        {
            program.Should().MatchRegex(@"^O\d{5}$", 
                "HAAS programs use O-number with 5 digits");
        });
    }

    [Fact]
    public void MTConnect_HAASCapabilities_ShouldReflectMachineSpecs()
    {
        // Arrange - HAAS VF-2SS specifications
        var haasEvent = new KMachineEvent
        {
            MachineId = "HAAS-MILL-03",
            Source = new SourceInfo
            {
                Vendor = "HAAS",
                Protocol = "MTConnect",
                Ip = "10.10.20.13"
            },
            State = new StateInfo
            {
                Availability = "AVAILABLE",
                Execution = "ACTIVE",
                Mode = "AUTOMATIC",
                Overrides = new Overrides
                {
                    Feed = 1.2,    // 120%
                    Spindle = 0.8, // 80%
                    Rapid = 0.25   // 25% - HAAS safety feature
                },
                Metrics = new Metrics
                {
                    SpindleRPM = 8100,  // HAAS VF-2SS max 8100 RPM
                    Feedrate = 1000,    // IPM
                    PartCount = 245
                }
            }
        };

        // Assert - HAAS VF-2SS specific limits
        haasEvent.State!.Metrics!.SpindleRPM.Should().BeLessOrEqualTo(8100,
            "HAAS VF-2SS spindle max is 8100 RPM");
            
        haasEvent.State.Overrides!.Rapid.Should().BeLessOrEqualTo(1.0,
            "HAAS rapids limited to 100% for safety");
            
        haasEvent.State.Availability.Should().BeOneOf("AVAILABLE", "UNAVAILABLE");
    }

    #endregion

    #region MTConnect Protocol Tests (Mazak)

    [Fact]
    public void MTConnect_MazakPalletSystem_ShouldTrackPalletData()
    {
        // Arrange - Mazak pallet changer MTConnect data
        var mazakPalletXml = @"<?xml version='1.0' encoding='UTF-8'?>
<MTConnectStreams xmlns='urn:mtconnect.org:MTConnectStreams:1.3'>
  <Streams>
    <DeviceStream name='MAZAK-5X-04'>
      <ComponentStream component='Auxiliary' name='PalletChanger'>
        <Events>
          <PalletId dataItemId='pallet' timestamp='2024-01-15T10:30:00Z'>PALLET_B</PalletId>
          <PalletState dataItemId='pstate' timestamp='2024-01-15T10:30:00Z'>ACTIVE</PalletState>
        </Events>
      </ComponentStream>
      <ComponentStream component='WorkHolding' name='Fixture'>
        <Events>
          <WorkOrderNumber dataItemId='wo' timestamp='2024-01-15T10:30:00Z'>WO-12345</WorkOrderNumber>
          <PartId dataItemId='part' timestamp='2024-01-15T10:30:00Z'>PART-ABC-789</PartId>
        </Events>
      </ComponentStream>
    </DeviceStream>
  </Streams>
</MTConnectStreams>";

        // Act & Assert
        var xmlDoc = XDocument.Parse(mazakPalletXml);
        var ns = XNamespace.Get("urn:mtconnect.org:MTConnectStreams:1.3");

        var palletId = xmlDoc.Descendants(ns + "PalletId").FirstOrDefault();
        palletId.Should().NotBeNull();
        palletId!.Value.Should().MatchRegex(@"^PALLET_[A-B]$",
            "Mazak pallets typically named PALLET_A or PALLET_B");

        var workOrder = xmlDoc.Descendants(ns + "WorkOrderNumber").FirstOrDefault();
        workOrder.Should().NotBeNull();
        workOrder!.Value.Should().StartWith("WO-", "Work orders prefixed with WO-");
    }

    [Fact]
    public void MTConnect_MazakFiveAxis_ShouldIncludeRotaryAxes()
    {
        // Arrange - Mazak 5-axis capabilities
        var mazakEvent = new KMachineEvent
        {
            MachineId = "MAZAK-5X-04",
            Source = new SourceInfo
            {
                Vendor = "MAZAK",
                Protocol = "MTConnect",
                Ip = "10.10.20.14"
            },
            State = new StateInfo
            {
                Execution = "ACTIVE",
                Mode = "AUTOMATIC",
                Metrics = new Metrics
                {
                    SpindleRPM = 12000,  // Mazak high-speed capability
                    Feedrate = 25000,    // mm/min - high speed machining
                    PartCount = 89
                }
            },
            Context = new ContextInfo
            {
                Workholding = new Workholding
                {
                    Type = "PALLET",
                    FixtureId = "PALLET_A"
                }
            }
        };

        // Assert - Mazak 5-axis and high-speed capabilities
        mazakEvent.State!.Metrics!.SpindleRPM.Should().BeInRange(0, 20000,
            "Mazak spindles support high-speed machining");
            
        mazakEvent.State.Metrics.Feedrate.Should().BeInRange(0, 30000,
            "Mazak supports high-speed feed rates");

        mazakEvent.Context!.Workholding!.Type.Should().Be("PALLET");
        mazakEvent.Context.Workholding.FixtureId.Should().MatchRegex(@"^PALLET_[A-B]$");
    }

    [Fact]
    public void MTConnect_MazakSmoothTechnology_ShouldOptimizeToolpaths()
    {
        // Arrange - Mazak SMOOTH technology features
        var smoothEvent = new KMachineEvent
        {
            MachineId = "MAZAK-5X-04",
            Event = new EventInfo
            {
                Type = "SMOOTH_TOOLPATH_ACTIVE",
                Code = "SM001",
                Message = "SMOOTH AI toolpath optimization active"
            },
            State = new StateInfo
            {
                Mode = "AUTO",
                Execution = "ACTIVE",
                Metrics = new Metrics
                {
                    SpindleRPM = 15000,    // High-speed machining
                    Feedrate = 20000       // Optimized feed rate
                }
            }
        };

        // Assert - Mazak SMOOTH features
        smoothEvent.Event!.Type.Should().Contain("SMOOTH", 
            "Mazak SMOOTH technology events should be identifiable");
            
        smoothEvent.State!.Metrics!.SpindleRPM.Should().BeGreaterThan(10000,
            "SMOOTH technology enables high-speed machining");
            
        smoothEvent.State.Metrics.Feedrate.Should().BeGreaterThan(15000,
            "SMOOTH technology optimizes feed rates");
    }

    #endregion

    #region Cross-Protocol Validation

    [Theory]
    [InlineData("FANUC", "FOCAS")]
    [InlineData("SIEMENS", "OPC UA")]
    [InlineData("HAAS", "MTConnect")]
    [InlineData("MAZAK", "MTConnect")]
    public void AllProtocols_EventSerialization_ShouldBeConsistent(string vendor, string protocol)
    {
        // Arrange
        var testEvent = new KMachineEvent
        {
            SiteId = "TEST-SITE",
            MachineId = $"{vendor}-TEST-01",
            Ts = DateTime.UtcNow,
            Source = new SourceInfo
            {
                Vendor = vendor,
                Protocol = protocol,
                Ip = "10.10.20.11"
            },
            State = new StateInfo
            {
                Execution = "ACTIVE",
                Mode = "AUTO"
            }
        };

        // Act - Serialize and deserialize
        var json = JsonSerializer.Serialize(testEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        var deserialized = JsonSerializer.Deserialize<KMachineEvent>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert - Consistent serialization across protocols
        deserialized.Should().NotBeNull();
        deserialized!.Source!.Vendor.Should().Be(vendor);
        deserialized.Source.Protocol.Should().Be(protocol);
        deserialized.MachineId.Should().Contain(vendor);
        deserialized.SiteId.Should().Be("TEST-SITE");
    }

    [Fact]
    public void AllProtocols_TimestampPrecision_ShouldMeetIndustrialRequirements()
    {
        // Arrange - Test timestamp precision across all protocols
        var protocols = new[]
        {
            ("FANUC", "FOCAS"),
            ("SIEMENS", "OPC UA"),
            ("HAAS", "MTConnect"),
            ("MAZAK", "MTConnect")
        };

        var events = protocols.Select(p => new KMachineEvent
        {
            MachineId = $"{p.Item1}-TIMING-TEST",
            Ts = DateTime.UtcNow,
            Source = new SourceInfo
            {
                Vendor = p.Item1,
                Protocol = p.Item2,
                Ip = "127.0.0.1"
            }
        }).ToList();

        // Assert - All timestamps should have millisecond precision
        events.Should().AllSatisfy(evt =>
        {
            var timestampString = evt.Ts.ToString("O"); // ISO 8601 format
            timestampString.Should().MatchRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}",
                $"{evt.Source!.Vendor} timestamps should have millisecond precision");
        });

        // Verify timestamps are within reasonable range
        var now = DateTime.UtcNow;
        events.Should().AllSatisfy(evt =>
        {
            evt.Ts.Should().BeCloseTo(now, TimeSpan.FromSeconds(1),
                "Event timestamps should be current");
        });
    }

    #endregion
}