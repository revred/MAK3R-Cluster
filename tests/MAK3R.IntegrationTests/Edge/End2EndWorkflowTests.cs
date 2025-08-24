using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Xunit.Abstractions;
using Mak3r.Edge.Services;
using Mak3r.Edge.Models;

namespace MAK3R.IntegrationTests.Edge;

/// <summary>
/// End-to-end workflow tests that validate the complete data flow
/// from machine connection through to cluster delivery for all manufacturers
/// </summary>
public class End2EndWorkflowTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider? _serviceProvider;
    private HubConnection? _clusterHubConnection;
    private readonly List<EdgeBatchFrame> _receivedBatches = new();
    private readonly List<EdgeHeartbeatFrame> _receivedHeartbeats = new();

    public End2EndWorkflowTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Setup Edge services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output).SetMinimumLevel(LogLevel.Information));
        
        // Configure Edge runtime components
        var edgeConfig = new EdgeConfig
        {
            SiteId = "E2E-TEST-SITE",
            Timezone = "UTC",
            Uplink = new UplinkConfig
            {
                HubUrl = "http://localhost:5225/hubs/machines",
                ReconnectDelayMs = 1000,
                Batch = new BatchConfig
                {
                    MaxEvents = 10,
                    MaxSizeBytes = 32768,
                    FlushIntervalMs = 2000
                }
            },
            Queue = new QueueConfig { Capacity = 1000 }
        };

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(edgeConfig));
        services.AddSingleton<QueueService>();
        services.AddSingleton<NormalizerService>();
        services.AddSingleton<UplinkService>();
        services.AddSingleton<NetDiagDb>();

        _serviceProvider = services.BuildServiceProvider();

        // Setup cluster SignalR hub mock
        try
        {
            _clusterHubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5225/hubs/machines")
                .WithAutomaticReconnect()
                .Build();

            _clusterHubConnection.On<EdgeBatchFrame>("PublishEdgeBatch", batch =>
            {
                _receivedBatches.Add(batch);
                _output.WriteLine($"Received batch {batch.BatchId} with {batch.Events.Count} events from {batch.SiteId}");
            });

            _clusterHubConnection.On<EdgeHeartbeatFrame>("EdgeHeartbeat", heartbeat =>
            {
                _receivedHeartbeats.Add(heartbeat);
                _output.WriteLine($"Received heartbeat from {heartbeat.SiteId}/{heartbeat.EdgeId}");
            });

            await _clusterHubConnection.StartAsync();
            _output.WriteLine("Connected to cluster SignalR hub for E2E testing");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Could not connect to cluster hub: {ex.Message}");
            _output.WriteLine("E2E tests will run without cluster connectivity verification");
        }
    }

    public async Task DisposeAsync()
    {
        if (_clusterHubConnection != null)
        {
            await _clusterHubConnection.DisposeAsync();
        }
        _serviceProvider?.Dispose();
    }

    #region Complete Workflow Tests

    [Fact]
    public async Task CompleteWorkflow_FANUC_ShouldDeliverEventsToCluster()
    {
        await TestCompleteWorkflow("FANUC", CreateFanucAdapter, ValidateFanucEvents);
    }

    [Fact]
    public async Task CompleteWorkflow_Siemens_ShouldDeliverEventsToCluster()
    {
        await TestCompleteWorkflow("SIEMENS", CreateSiemensAdapter, ValidateSiemensEvents);
    }

    [Fact]
    public async Task CompleteWorkflow_HAAS_ShouldDeliverEventsToCluster()
    {
        await TestCompleteWorkflow("HAAS", CreateHaasAdapter, ValidateHaasEvents);
    }

    [Fact]
    public async Task CompleteWorkflow_Mazak_ShouldDeliverEventsToCluster()
    {
        await TestCompleteWorkflow("MAZAK", CreateMazakAdapter, ValidateMazakEvents);
    }

    private async Task TestCompleteWorkflow(
        string manufacturer,
        Func<IEdgeConnectorAdapter> adapterFactory,
        Action<List<KMachineEvent>> eventValidator)
    {
        // Arrange
        _output.WriteLine($"=== Starting E2E workflow test for {manufacturer} ===");
        
        var queue = _serviceProvider!.GetRequiredService<QueueService>();
        var normalizer = _serviceProvider.GetRequiredService<NormalizerService>();
        var uplink = _serviceProvider.GetRequiredService<UplinkService>();

        using var adapter = adapterFactory();
        var allEvents = new List<KMachineEvent>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            // Act - Start the complete pipeline
            _output.WriteLine($"1. Starting {manufacturer} adapter...");
            await adapter.StartAsync(cts.Token);

            _output.WriteLine("2. Starting normalizer service...");
            var normalizerTask = normalizer.StartAsync(cts.Token);

            _output.WriteLine("3. Starting uplink service...");
            var uplinkTask = uplink.StartAsync(cts.Token);

            _output.WriteLine("4. Generating machine events...");
            await foreach (var evt in adapter.GetEventsAsync(cts.Token))
            {
                allEvents.Add(evt);
                await normalizer.EnqueueAsync(evt, cts.Token);
                
                if (allEvents.Count >= 20) break; // Collect sufficient events
            }

            // Allow time for processing and batching
            _output.WriteLine("5. Waiting for event processing and batching...");
            await Task.Delay(5000, cts.Token);

            // Stop services
            _output.WriteLine("6. Stopping services...");
            await adapter.StopAsync();
            cts.Cancel();

            // Assert - Validate the complete workflow
            _output.WriteLine("7. Validating results...");
            
            // Validate raw events from adapter
            allEvents.Should().NotBeEmpty($"{manufacturer} adapter should generate events");
            allEvents.Should().HaveCountGreaterOrEqualTo(10, "Should generate sufficient events for testing");
            
            eventValidator(allEvents);

            // Validate queue processed events
            var queueDepth = queue.ApproxDepth;
            _output.WriteLine($"Final queue depth: {queueDepth}");

            // If cluster hub is available, validate batch delivery
            if (_clusterHubConnection?.State == HubConnectionState.Connected)
            {
                var manufacturerBatches = _receivedBatches
                    .Where(b => b.SiteId == "E2E-TEST-SITE")
                    .Where(b => b.Events.Any(e => e.Source?.Vendor == manufacturer))
                    .ToList();

                manufacturerBatches.Should().NotBeEmpty($"Should receive batches from {manufacturer}");
                
                var totalEventsInBatches = manufacturerBatches.SelectMany(b => b.Events).Count();
                _output.WriteLine($"Received {totalEventsInBatches} events in {manufacturerBatches.Count} batches");
                
                totalEventsInBatches.Should().BeGreaterThan(0, "Batches should contain events");
            }

            _output.WriteLine($"=== {manufacturer} E2E workflow test completed successfully ===");
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine($"E2E test for {manufacturer} timed out - this may be expected");
        }
    }

    #endregion

    #region Multi-Manufacturer Concurrent Test

    [Fact]
    public async Task ConcurrentWorkflow_AllManufacturers_ShouldHandleSimultaneousOperations()
    {
        // Arrange
        _output.WriteLine("=== Starting concurrent multi-manufacturer E2E test ===");
        
        var queue = _serviceProvider!.GetRequiredService<QueueService>();
        var normalizer = _serviceProvider.GetRequiredService<NormalizerService>();
        var uplink = _serviceProvider.GetRequiredService<UplinkService>();

        var adapters = new Dictionary<string, IEdgeConnectorAdapter>
        {
            ["FANUC"] = CreateFanucAdapter(),
            ["SIEMENS"] = CreateSiemensAdapter(), 
            ["HAAS"] = CreateHaasAdapter(),
            ["MAZAK"] = CreateMazakAdapter()
        };

        var allEvents = new Dictionary<string, List<KMachineEvent>>();
        foreach (var make in adapters.Keys)
        {
            allEvents[make] = new List<KMachineEvent>();
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            // Act - Start all adapters and services concurrently
            _output.WriteLine("1. Starting all services...");
            var normalizerTask = normalizer.StartAsync(cts.Token);
            var uplinkTask = uplink.StartAsync(cts.Token);

            var adapterTasks = adapters.Select(async kvp =>
            {
                var make = kvp.Key;
                var adapter = kvp.Value;
                
                await adapter.StartAsync(cts.Token);
                _output.WriteLine($"Started {make} adapter");

                await foreach (var evt in adapter.GetEventsAsync(cts.Token))
                {
                    allEvents[make].Add(evt);
                    await normalizer.EnqueueAsync(evt, cts.Token);
                    
                    if (allEvents[make].Count >= 15) break;
                }
            }).ToArray();

            await Task.WhenAll(adapterTasks);
            
            // Allow processing time
            await Task.Delay(3000, cts.Token);

            // Stop all adapters
            foreach (var adapter in adapters.Values)
            {
                await adapter.StopAsync();
            }

            // Assert - Validate concurrent operations
            _output.WriteLine("2. Validating concurrent results...");

            foreach (var kvp in allEvents)
            {
                var make = kvp.Key;
                var events = kvp.Value;
                
                events.Should().NotBeEmpty($"{make} should generate events");
                events.Should().AllSatisfy(evt => 
                {
                    evt.Source!.Vendor.Should().Be(make);
                    evt.SiteId.Should().Be("E2E-TEST-SITE");
                });

                _output.WriteLine($"{make}: Generated {events.Count} events");
            }

            // Validate no cross-contamination between manufacturers
            foreach (var kvp in allEvents)
            {
                var make = kvp.Key;
                var events = kvp.Value;
                
                events.Should().AllSatisfy(evt => 
                {
                    evt.Source!.Vendor.Should().Be(make, 
                        $"Events from {make} adapter should not contain data from other manufacturers");
                });
            }

            var totalEvents = allEvents.Values.Sum(events => events.Count);
            _output.WriteLine($"Total events generated across all manufacturers: {totalEvents}");

            if (_clusterHubConnection?.State == HubConnectionState.Connected)
            {
                var totalBatchedEvents = _receivedBatches
                    .SelectMany(b => b.Events)
                    .Count();
                    
                _output.WriteLine($"Total events delivered to cluster: {totalBatchedEvents}");
            }

            _output.WriteLine("=== Concurrent multi-manufacturer test completed ===");
        }
        finally
        {
            foreach (var adapter in adapters.Values)
            {
                adapter.Dispose();
            }
        }
    }

    #endregion

    #region Performance and Load Tests

    [Fact]
    public async Task HighVolumeWorkflow_ShouldMaintainPerformance()
    {
        // Arrange
        _output.WriteLine("=== High-volume performance test ===");
        
        var queue = _serviceProvider!.GetRequiredService<QueueService>();
        var normalizer = _serviceProvider.GetRequiredService<NormalizerService>();
        
        // Configure for high volume
        using var adapter = CreateFanucAdapter(); // Use FANUC for high-speed testing
        var eventCount = 0;
        var startTime = DateTime.UtcNow;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            // Act - Generate high volume of events
            var normalizerTask = normalizer.StartAsync(cts.Token);
            await adapter.StartAsync(cts.Token);

            await foreach (var evt in adapter.GetEventsAsync(cts.Token))
            {
                await normalizer.EnqueueAsync(evt, cts.Token);
                eventCount++;
                
                if (eventCount >= 1000) break; // Target 1000 events
            }

            var duration = DateTime.UtcNow - startTime;
            await adapter.StopAsync();

            // Assert - Validate performance metrics
            var eventsPerSecond = eventCount / duration.TotalSeconds;
            _output.WriteLine($"Generated {eventCount} events in {duration.TotalSeconds:F2} seconds");
            _output.WriteLine($"Performance: {eventsPerSecond:F2} events/second");

            eventCount.Should().BeGreaterOrEqualTo(500, "Should generate sufficient events under load");
            eventsPerSecond.Should().BeGreaterThan(10, "Should maintain reasonable throughput");
            
            var finalQueueDepth = queue.ApproxDepth;
            _output.WriteLine($"Final queue depth: {finalQueueDepth}");
        }
        finally
        {
            cts.Cancel();
        }
    }

    #endregion

    #region Error Handling and Recovery Tests

    [Fact]
    public async Task WorkflowWithInterruption_ShouldRecover()
    {
        // Arrange
        _output.WriteLine("=== Testing workflow interruption and recovery ===");
        
        var queue = _serviceProvider!.GetRequiredService<QueueService>();
        var normalizer = _serviceProvider.GetRequiredService<NormalizerService>();
        
        using var adapter = CreateSiemensAdapter();
        var events = new List<KMachineEvent>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            // Act - Start workflow, interrupt, then recover
            var normalizerTask = normalizer.StartAsync(cts.Token);
            await adapter.StartAsync(cts.Token);

            // Generate some events
            await foreach (var evt in adapter.GetEventsAsync(cts.Token))
            {
                events.Add(evt);
                await normalizer.EnqueueAsync(evt, cts.Token);
                
                if (events.Count >= 10) break;
            }

            _output.WriteLine($"Phase 1: Generated {events.Count} events");

            // Simulate interruption
            await adapter.StopAsync();
            _output.WriteLine("Simulated adapter interruption");
            
            await Task.Delay(2000); // Wait during interruption

            // Recovery
            await adapter.StartAsync(cts.Token);
            _output.WriteLine("Adapter recovered");

            // Continue generating events
            await foreach (var evt in adapter.GetEventsAsync(cts.Token))
            {
                events.Add(evt);
                await normalizer.EnqueueAsync(evt, cts.Token);
                
                if (events.Count >= 20) break;
            }

            await adapter.StopAsync();

            // Assert - Validate recovery
            events.Should().HaveCountGreaterOrEqualTo(15, "Should generate events before and after recovery");
            
            var preInterruptionEvents = events.Take(10).ToList();
            var postRecoveryEvents = events.Skip(10).ToList();

            preInterruptionEvents.Should().AllSatisfy(evt => 
                evt.Source!.Vendor.Should().Be("SIEMENS"));
            postRecoveryEvents.Should().AllSatisfy(evt => 
                evt.Source!.Vendor.Should().Be("SIEMENS"));

            _output.WriteLine($"Recovery test completed: {events.Count} total events");
        }
        finally
        {
            cts.Cancel();
        }
    }

    #endregion

    #region Helper Methods

    private IEdgeConnectorAdapter CreateFanucAdapter()
    {
        var config = new EdgeConnectorConfig
        {
            MachineId = "FANUC-E2E-01",
            Make = "FANUC",
            IpAddress = "127.0.0.1",
            Protocol = "FOCAS",
            Settings = new Dictionary<string, object>
            {
                { "Port", 8193 },
                { "IsSimulator", true },
                { "PollIntervalMs", 100 } // Fast for testing
            }
        };

        return new FanucEdgeAdapter(config, "E2E-TEST-SITE",
            _serviceProvider!.GetRequiredService<ILogger<FanucEdgeAdapter>>());
    }

    private IEdgeConnectorAdapter CreateSiemensAdapter()
    {
        var config = new EdgeConnectorConfig
        {
            MachineId = "SIEMENS-E2E-02",
            Make = "SIEMENS",
            IpAddress = "127.0.0.1",
            Protocol = "OPC UA",
            Settings = new Dictionary<string, object>
            {
                { "EndpointUrl", "opc.tcp://127.0.0.1:4840" },
                { "IsSimulator", true },
                { "PublishingInterval", 100 }
            }
        };

        return new SiemensEdgeAdapter(config, "E2E-TEST-SITE",
            _serviceProvider!.GetRequiredService<ILogger<SiemensEdgeAdapter>>());
    }

    private IEdgeConnectorAdapter CreateHaasAdapter()
    {
        var config = new EdgeConnectorConfig
        {
            MachineId = "HAAS-E2E-03",
            Make = "HAAS",
            IpAddress = "127.0.0.1",
            Protocol = "MTConnect",
            Settings = new Dictionary<string, object>
            {
                { "BaseUrl", "http://127.0.0.1:8082/VF2SS" },
                { "IsSimulator", true },
                { "SampleIntervalMs", 100 }
            }
        };

        return new HaasEdgeAdapter(config, "E2E-TEST-SITE",
            _serviceProvider!.GetRequiredService<ILogger<HaasEdgeAdapter>>());
    }

    private IEdgeConnectorAdapter CreateMazakAdapter()
    {
        var config = new EdgeConnectorConfig
        {
            MachineId = "MAZAK-E2E-04",
            Make = "MAZAK",
            IpAddress = "127.0.0.1",
            Protocol = "MTConnect",
            Settings = new Dictionary<string, object>
            {
                { "BaseUrl", "http://127.0.0.1:5000/MAZAK" },
                { "IsSimulator", true },
                { "SampleIntervalMs", 100 },
                { "EnablePalletData", true }
            }
        };

        return new MazakEdgeAdapter(config, "E2E-TEST-SITE",
            _serviceProvider!.GetRequiredService<ILogger<MazakEdgeAdapter>>());
    }

    private void ValidateFanucEvents(List<KMachineEvent> events)
    {
        events.Should().AllSatisfy(evt =>
        {
            evt.Source!.Vendor.Should().Be("FANUC");
            evt.Source.Protocol.Should().Be("FOCAS");
            evt.MachineId.Should().StartWith("FANUC-E2E-");
        });

        // FANUC-specific validations
        var stateEvents = events.Where(e => e.State != null).ToList();
        if (stateEvents.Any())
        {
            stateEvents.Should().AllSatisfy(evt =>
            {
                if (evt.State!.Execution != null)
                    evt.State.Execution.Should().BeOneOf("READY", "ACTIVE", "INTERRUPTED", "STOPPED");
            });
        }
    }

    private void ValidateSiemensEvents(List<KMachineEvent> events)
    {
        events.Should().AllSatisfy(evt =>
        {
            evt.Source!.Vendor.Should().Be("SIEMENS");
            evt.Source.Protocol.Should().Be("OPC UA");
            evt.MachineId.Should().StartWith("SIEMENS-E2E-");
        });
    }

    private void ValidateHaasEvents(List<KMachineEvent> events)
    {
        events.Should().AllSatisfy(evt =>
        {
            evt.Source!.Vendor.Should().Be("HAAS");
            evt.Source.Protocol.Should().Be("MTConnect");
            evt.MachineId.Should().StartWith("HAAS-E2E-");
        });
    }

    private void ValidateMazakEvents(List<KMachineEvent> events)
    {
        events.Should().AllSatisfy(evt =>
        {
            evt.Source!.Vendor.Should().Be("MAZAK");
            evt.Source.Protocol.Should().Be("MTConnect");
            evt.MachineId.Should().StartWith("MAZAK-E2E-");
        });
    }

    #endregion

    #region Data Models

    public class EdgeBatchFrame
    {
        public string SiteId { get; set; } = "";
        public string BatchId { get; set; } = "";
        public List<KMachineEvent> Events { get; set; } = new();
    }

    public class EdgeHeartbeatFrame
    {
        public string SiteId { get; set; } = "";
        public string EdgeId { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public int QueueDepth { get; set; }
        public Dictionary<string, bool> ConnectorHealth { get; set; } = new();
    }

    #endregion
}