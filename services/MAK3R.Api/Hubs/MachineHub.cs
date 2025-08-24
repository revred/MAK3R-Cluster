using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MAK3R.Api.Hubs;

public class MachineHub : Hub
{
    private readonly ILogger<MachineHub> _logger;

    public MachineHub(ILogger<MachineHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Edge client connected: {ConnectionId}", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "EdgeClients");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Edge client disconnected: {ConnectionId}", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "EdgeClients");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Receives batches of KMachineEvents from Edge nodes
    /// </summary>
    public async Task PublishEdgeBatch(EdgeBatchFrame frame)
    {
        try
        {
            _logger.LogInformation("Received edge batch from {SiteId}: {EventCount} events", 
                frame.SiteId, frame.Events.Count);

            // Process each event in the batch
            foreach (var machineEvent in frame.Events)
            {
                await ProcessMachineEvent(machineEvent);
            }

            // Send acknowledgment back to edge
            await Clients.Caller.SendAsync("BatchAck", frame.BatchId, true);
            
            // Broadcast to connected web clients for real-time updates
            await Clients.Group("WebClients").SendAsync("MachineEvents", frame.Events);
            
            _logger.LogDebug("Processed and acknowledged batch {BatchId}", frame.BatchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing edge batch {BatchId}", frame.BatchId);
            await Clients.Caller.SendAsync("BatchAck", frame.BatchId, false, ex.Message);
        }
    }

    /// <summary>
    /// Receives heartbeats from Edge nodes
    /// </summary>
    public async Task EdgeHeartbeat(EdgeHeartbeatFrame heartbeat)
    {
        _logger.LogDebug("Edge heartbeat from {SiteId}/{EdgeId}: Queue depth {QueueDepth}", 
            heartbeat.SiteId, heartbeat.EdgeId, heartbeat.QueueDepth);

        // Store edge health metrics (could be persisted to database)
        // For now, just broadcast to monitoring dashboard
        await Clients.Group("WebClients").SendAsync("EdgeHeartbeat", heartbeat);
    }

    /// <summary>
    /// Web clients can join to receive machine events
    /// </summary>
    public async Task JoinWebClients()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "WebClients");
        _logger.LogDebug("Web client joined: {ConnectionId}", Context.ConnectionId);
    }

    /// <summary>
    /// Send sampling rate change to specific Edge
    /// </summary>
    public async Task SetSampling(string siteId, string machineId, int intervalMs)
    {
        _logger.LogInformation("Setting sampling rate for {MachineId}: {IntervalMs}ms", machineId, intervalMs);
        await Clients.Group("EdgeClients").SendAsync("SetSampling", machineId, intervalMs);
    }

    /// <summary>
    /// Execute MCP tool on specific machine via Edge
    /// </summary>
    public async Task RunMcp(string siteId, string machineId, string tool, object args)
    {
        _logger.LogInformation("Running MCP tool {Tool} on {MachineId}", tool, machineId);
        await Clients.Group("EdgeClients").SendAsync("RunMcp", machineId, tool, args);
    }

    private async Task ProcessMachineEvent(KMachineEvent machineEvent)
    {
        // Here we would typically:
        // 1. Store to time-series database
        // 2. Run anomaly detection rules
        // 3. Update machine status cache
        // 4. Trigger workflow automations

        _logger.LogDebug("Processing event from {MachineId}: {EventType}", 
            machineEvent.MachineId, machineEvent.Event?.Type);

        // For now, just log significant events
        if (machineEvent.Event?.Type is "ALARM" or "CYCLE_START" or "CYCLE_STOP" or "PART_COMPLETED")
        {
            _logger.LogInformation("Machine event: {MachineId} - {EventType} at {Timestamp}", 
                machineEvent.MachineId, machineEvent.Event.Type, machineEvent.Ts);
        }

        await Task.CompletedTask;
    }
}

// Data transfer objects matching Edge models
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

// KMachineEvent model (should match Edge model)
public class KMachineEvent
{
    public string SiteId { get; set; } = "";
    public string MachineId { get; set; } = "";
    public DateTime Ts { get; set; }
    public SourceInfo? Source { get; set; }
    public StateInfo? State { get; set; }
    public EventInfo? Event { get; set; }
    public ContextInfo? Context { get; set; }
    public string EventId => $"{SiteId}|{MachineId}|{Ts:O}|{Event?.Type}|{Event?.Code}".GetHashCode().ToString("X");
}

public class SourceInfo { public string Vendor { get; set; } = ""; public string Protocol { get; set; } = ""; public string Ip { get; set; } = ""; }
public class StateInfo
{
    public string? Power { get; set; }
    public string? Availability { get; set; }
    public string? Mode { get; set; }
    public string? Execution { get; set; }
    public ProgramInfo? Program { get; set; }
    public ToolInfo? Tool { get; set; }
    public Overrides? Overrides { get; set; }
    public Metrics? Metrics { get; set; }
}
public class ProgramInfo { public string? Name { get; set; } public int? Block { get; set; } }
public class ToolInfo { public int? Id { get; set; } public double? Life { get; set; } }
public class Overrides { public double? Feed { get; set; } public double? Spindle { get; set; } public double? Rapid { get; set; } }
public class Metrics { public double? SpindleRPM { get; set; } public double? Feedrate { get; set; } public int? PartCount { get; set; } }
public class EventInfo { public string? Type { get; set; } public string? Severity { get; set; } public string? Code { get; set; } public string? Message { get; set; } }
public class ContextInfo { public JobInfo? Job { get; set; } public OperatorInfo? Operator { get; set; } public Workholding? Workholding { get; set; } public Material? Material { get; set; } }
public class JobInfo { public string? Id { get; set; } public string? Op { get; set; } public string? Barcode { get; set; } }
public class OperatorInfo { public string? Badge { get; set; } }
public class Workholding { public string? Type { get; set; } public string? FixtureId { get; set; } }
public class Material { public string? Lot { get; set; } }