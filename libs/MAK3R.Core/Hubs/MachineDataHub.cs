using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MAK3R.Core.Models;

namespace MAK3R.Core.Hubs;

public class MachineDataHub : Hub<IMachineDataHubClient>
{
    private readonly ILogger<MachineDataHub> _logger;

    public MachineDataHub(ILogger<MachineDataHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinMachineGroup(string machineId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"machine_{machineId}");
        _logger.LogInformation("Connection {ConnectionId} joined machine group {MachineId}", 
            Context.ConnectionId, machineId);
    }

    public async Task LeaveMachineGroup(string machineId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"machine_{machineId}");
        _logger.LogInformation("Connection {ConnectionId} left machine group {MachineId}", 
            Context.ConnectionId, machineId);
    }

    public async Task JoinSiteGroup(string siteId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"site_{siteId}");
        _logger.LogInformation("Connection {ConnectionId} joined site group {SiteId}", 
            Context.ConnectionId, siteId);
    }

    public async Task LeaveSiteGroup(string siteId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"site_{siteId}");
        _logger.LogInformation("Connection {ConnectionId} left site group {SiteId}", 
            Context.ConnectionId, siteId);
    }

    public async Task JoinDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
        _logger.LogInformation("Connection {ConnectionId} joined dashboard group", Context.ConnectionId);
    }

    public async Task LeaveDashboard()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dashboard");
        _logger.LogInformation("Connection {ConnectionId} left dashboard group", Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IMachineDataHubClient
{
    Task MachineStatusUpdated(MachineStatusUpdate update);
    Task MachineMetricsUpdated(MachineMetricsUpdate update);
    Task AnomalyDetected(AnomalyAlert alert);
    Task ProductionStarted(ProductionEvent productionEvent);
    Task ProductionCompleted(ProductionEvent productionEvent);
    Task DashboardStatsUpdated(DashboardStats stats);
}

public record MachineStatusUpdate(
    string MachineId,
    string MachineName,
    MachineStatus Status,
    DateTime Timestamp,
    Dictionary<string, object>? Metadata = null
);

public record MachineMetricsUpdate(
    string MachineId,
    string MachineName,
    Dictionary<string, double> Metrics,
    DateTime Timestamp
);

public record AnomalyAlert(
    string Id,
    string MachineId,
    string MachineName,
    string Type,
    string Description,
    string Severity,
    DateTime Timestamp,
    Dictionary<string, object>? Context = null
);

public record ProductionEvent(
    string Id,
    string MachineId,
    string MachineName,
    string ProductId,
    string ProductName,
    int Quantity,
    DateTime StartTime,
    DateTime? EndTime = null,
    Dictionary<string, object>? Metadata = null
);

public record DashboardStats(
    int TotalMachines,
    int RunningMachines,
    int IdleMachines,
    int MaintenanceMachines,
    double OverallEfficiency,
    int ProductionOrdersToday,
    int CompletedOrdersToday,
    DateTime LastUpdated
);