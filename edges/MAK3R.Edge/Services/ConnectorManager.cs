using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mak3r.Edge.Services;

public class ConnectorManager : BackgroundService
{
    private readonly EdgeConfig _cfg;
    private readonly ILogger<ConnectorManager> _log;

    public ConnectorManager(IOptions<EdgeConfig> cfg, ILogger<ConnectorManager> log)
    {
        _cfg = cfg.Value; _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ConnectorManager ready (real connectors registered separately)");
        return Task.CompletedTask;
    }
}
