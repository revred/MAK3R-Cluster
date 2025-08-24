using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MAK3R.Core.Hubs;
using MAK3R.Core.Models;

namespace MAK3R.Core.Services;

public class MachineDataBroadcastService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MachineDataBroadcastService> _logger;
    private readonly Random _random = new();

    public MachineDataBroadcastService(
        IServiceProvider serviceProvider,
        ILogger<MachineDataBroadcastService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Machine Data Broadcast Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MachineDataHub, IMachineDataHubClient>>();

                await BroadcastMachineUpdates(hubContext);
                await BroadcastDashboardStats(hubContext);
                await BroadcastRandomAnomalies(hubContext);

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in machine data broadcast service");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Machine Data Broadcast Service stopped");
    }

    private async Task BroadcastMachineUpdates(IHubContext<MachineDataHub, IMachineDataHubClient> hubContext)
    {
        var machines = GetSimulatedMachines();

        foreach (var machine in machines)
        {
            // Broadcast status updates
            var statusUpdate = new MachineStatusUpdate(
                machine.Id,
                machine.Name,
                GetRandomStatus(),
                DateTime.UtcNow
            );

            await hubContext.Clients.Group($"machine_{machine.Id}")
                .MachineStatusUpdated(statusUpdate);

            await hubContext.Clients.Group("dashboard")
                .MachineStatusUpdated(statusUpdate);

            // Broadcast metrics updates
            var metricsUpdate = new MachineMetricsUpdate(
                machine.Id,
                machine.Name,
                GenerateRandomMetrics(machine.Type),
                DateTime.UtcNow
            );

            await hubContext.Clients.Group($"machine_{machine.Id}")
                .MachineMetricsUpdated(metricsUpdate);

            await hubContext.Clients.Group("dashboard")
                .MachineMetricsUpdated(metricsUpdate);
        }
    }

    private async Task BroadcastDashboardStats(IHubContext<MachineDataHub, IMachineDataHubClient> hubContext)
    {
        var stats = new DashboardStats(
            TotalMachines: 15,
            RunningMachines: _random.Next(8, 12),
            IdleMachines: _random.Next(2, 4),
            MaintenanceMachines: _random.Next(1, 3),
            OverallEfficiency: Math.Round(75 + _random.NextDouble() * 20, 1),
            ProductionOrdersToday: _random.Next(45, 65),
            CompletedOrdersToday: _random.Next(35, 55),
            LastUpdated: DateTime.UtcNow
        );

        await hubContext.Clients.Group("dashboard")
            .DashboardStatsUpdated(stats);
    }

    private async Task BroadcastRandomAnomalies(IHubContext<MachineDataHub, IMachineDataHubClient> hubContext)
    {
        if (_random.NextDouble() > 0.95) // 5% chance per cycle
        {
            var machines = GetSimulatedMachines();
            var machine = machines[_random.Next(machines.Length)];
            var anomalyTypes = new[] { "Temperature", "Vibration", "Performance", "Quality" };
            var severities = new[] { "Low", "Medium", "High" };

            var alert = new AnomalyAlert(
                Guid.NewGuid().ToString(),
                machine.Id,
                machine.Name,
                anomalyTypes[_random.Next(anomalyTypes.Length)],
                GenerateAnomalyDescription(),
                severities[_random.Next(severities.Length)],
                DateTime.UtcNow,
                new Dictionary<string, object>
                {
                    ["threshold"] = _random.Next(50, 200),
                    ["actualValue"] = _random.Next(200, 300),
                    ["duration"] = TimeSpan.FromMinutes(_random.Next(1, 30))
                }
            );

            await hubContext.Clients.Group($"machine_{machine.Id}")
                .AnomalyDetected(alert);

            await hubContext.Clients.Group("dashboard")
                .AnomalyDetected(alert);

            _logger.LogInformation("Broadcasted anomaly alert for machine {MachineId}: {Type}", 
                machine.Id, alert.Type);
        }
    }

    private static SimulatedMachine[] GetSimulatedMachines() => new[]
    {
        new SimulatedMachine("cnc-001", "CNC-001", "CNC"),
        new SimulatedMachine("press-002", "Press-002", "Press"),
        new SimulatedMachine("mill-003", "Mill-003", "Mill"),
        new SimulatedMachine("lathe-004", "Lathe-004", "Lathe"),
        new SimulatedMachine("grinder-005", "Grinder-005", "Grinder")
    };

    private MachineStatus GetRandomStatus()
    {
        var statuses = new[] { MachineStatus.Running, MachineStatus.Idle, MachineStatus.Maintenance };
        var weights = new[] { 0.7, 0.25, 0.05 }; // 70% running, 25% idle, 5% maintenance

        var random = _random.NextDouble();
        var cumulative = 0.0;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (random <= cumulative)
                return statuses[i];
        }

        return MachineStatus.Running;
    }

    private Dictionary<string, double> GenerateRandomMetrics(string machineType)
    {
        return machineType.ToLower() switch
        {
            "cnc" => new Dictionary<string, double>
            {
                ["rpm"] = Math.Round(1800 + _random.NextDouble() * 400, 0),
                ["temperature"] = Math.Round(65 + _random.NextDouble() * 20, 1),
                ["vibration"] = Math.Round(_random.NextDouble() * 10, 2),
                ["efficiency"] = Math.Round(80 + _random.NextDouble() * 15, 1)
            },
            "press" => new Dictionary<string, double>
            {
                ["pressure"] = Math.Round(150 + _random.NextDouble() * 100, 0),
                ["temperature"] = Math.Round(60 + _random.NextDouble() * 25, 1),
                ["cycleTime"] = Math.Round(8 + _random.NextDouble() * 4, 1),
                ["efficiency"] = Math.Round(75 + _random.NextDouble() * 20, 1)
            },
            "mill" => new Dictionary<string, double>
            {
                ["rpm"] = Math.Round(800 + _random.NextDouble() * 600, 0),
                ["temperature"] = Math.Round(70 + _random.NextDouble() * 15, 1),
                ["feedRate"] = Math.Round(150 + _random.NextDouble() * 100, 0),
                ["efficiency"] = Math.Round(85 + _random.NextDouble() * 12, 1)
            },
            _ => new Dictionary<string, double>
            {
                ["temperature"] = Math.Round(65 + _random.NextDouble() * 20, 1),
                ["efficiency"] = Math.Round(80 + _random.NextDouble() * 15, 1)
            }
        };
    }

    private string GenerateAnomalyDescription()
    {
        var descriptions = new[]
        {
            "Temperature exceeding normal operating range",
            "Unusual vibration pattern detected",
            "Performance degradation observed",
            "Quality parameters outside specification",
            "Unexpected power consumption spike",
            "Coolant level critically low",
            "Tool wear exceeding recommended limits"
        };

        return descriptions[_random.Next(descriptions.Length)];
    }

    private record SimulatedMachine(string Id, string Name, string Type);
}