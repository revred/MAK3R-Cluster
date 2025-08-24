using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Mak3r.Edge.Models;

namespace Mak3r.Edge.Services;

public class ConfigService : BackgroundService
{
    private readonly EdgeConfig _cfg;
    private readonly ILogger<ConfigService> _log;
    private List<EdgeConnectorConfig>? _machinesConfig;

    public ConfigService(IOptions<EdgeConfig> cfg, ILogger<ConfigService> log) 
    { 
        _cfg = cfg.Value; 
        _log = log; 
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Config loaded: Site={Site} Env={Env}", _cfg.SiteId, _cfg.Env);
        await LoadMachinesConfigAsync();
    }

    public List<EdgeConnectorConfig>? GetMachinesConfig() => _machinesConfig;

    private async Task LoadMachinesConfigAsync()
    {
        try
        {
            var machinesConfigPath = Environment.GetEnvironmentVariable("MACHINES_CONFIG_PATH") ?? "./config/machines.json";
            
            if (File.Exists(machinesConfigPath))
            {
                var configJson = await File.ReadAllTextAsync(machinesConfigPath);
                var wrapper = JsonSerializer.Deserialize<MachinesConfigWrapper>(configJson, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                _machinesConfig = wrapper?.Machines ?? new List<EdgeConnectorConfig>();
                _log.LogInformation("Loaded {Count} machine configurations", _machinesConfig.Count);
            }
            else
            {
                _log.LogWarning("Machines configuration file not found: {Path}", machinesConfigPath);
                _machinesConfig = new List<EdgeConnectorConfig>();
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load machines configuration");
            _machinesConfig = new List<EdgeConnectorConfig>();
        }
    }

    private class MachinesConfigWrapper
    {
        public List<EdgeConnectorConfig> Machines { get; set; } = new();
    }
}
