using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mak3r.Edge.Services;

public class HealthService : BackgroundService
{
    private readonly ILogger<HealthService> _log;
    public HealthService(ILogger<HealthService> log) { _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _log.LogDebug("Heartbeat {ts}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
