using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mak3r.Edge.Services;

public class ConfigService : BackgroundService
{
    private readonly EdgeConfig _cfg;
    private readonly ILogger<ConfigService> _log;
    public ConfigService(IOptions<EdgeConfig> cfg, ILogger<ConfigService> log) { _cfg = cfg.Value; _log = log; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Config loaded: Site={Site} Env={Env}", _cfg.SiteId, _cfg.Env);
        return Task.CompletedTask;
    }
}
