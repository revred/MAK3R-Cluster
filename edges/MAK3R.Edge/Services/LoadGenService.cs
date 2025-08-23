using Mak3r.Edge.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mak3r.Edge.Services;

public class LoadGenService : BackgroundService
{
    private readonly EdgeConfig _cfg;
    private readonly NormalizerService _normalizer;
    private readonly ILogger<LoadGenService> _log;
    private readonly Random _rng = new();

    public LoadGenService(IOptions<EdgeConfig> cfg, NormalizerService norm, ILogger<LoadGenService> log)
    { _cfg = cfg.Value; _normalizer = norm; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.LoadGen.Enabled) { _log.LogInformation("LoadGen disabled"); return; }
        _log.LogInformation("LoadGen enabled: {n} machines @ {r} Hz", _cfg.LoadGen.Machines, _cfg.LoadGen.RatePerMachineHz);

        var tasks = Enumerable.Range(1, _cfg.LoadGen.Machines).Select(i => RunVmAsync(i, stoppingToken)).ToArray();
        await Task.WhenAll(tasks);
    }

    private async Task RunVmAsync(int idx, CancellationToken ct)
    {
        var machId = $"VIRT-{idx:0000}";
        var rateHz = _cfg.LoadGen.RatePerMachineHz;
        var baseDelay = TimeSpan.FromMilliseconds(1000.0 / Math.Max(0.1, rateHz));

        var lastPart = 0;
        var execution = "READY";

        var start = DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            // Burst logic
            var delay = baseDelay;
            if (_cfg.LoadGen.Burst.Enabled && (DateTime.UtcNow - start).TotalSeconds % _cfg.LoadGen.Burst.EverySec < 1)
                delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds / Math.Max(1, _cfg.LoadGen.Burst.FactorX));

            // Jitter
            var jit = _cfg.LoadGen.JitterPct;
            var factor = 1.0 + (( _rng.NextDouble() * 2 - 1) * jit/100.0);
            delay = TimeSpan.FromMilliseconds(Math.Max(10, delay.TotalMilliseconds * factor));

            // State machine: READY -> ACTIVE -> PART_COMPLETED -> READY
            if (execution == "READY") execution = "ACTIVE"
            ; else if (execution == "ACTIVE" && _rng.NextDouble() < 0.15) { execution = "FEED_HOLD"; }
            else if (execution == "FEED_HOLD" && _rng.NextDouble() < 0.30) { execution = "ACTIVE"; }
            else if (execution == "ACTIVE" && _rng.NextDouble() < 0.10) { execution = "READY"; lastPart++; }

            var e = new KMachineEvent
            {
                SiteId = _cfg.SiteId,
                MachineId = machId,
                Ts = DateTime.UtcNow,
                Source = new SourceInfo{ Vendor = "VIRTUAL", Protocol = "INPROC", Ip = "127.0.0.1" },
                State = new StateInfo{
                    Execution = execution,
                    Program = new ProgramInfo{ Name = "O1234", Block = _rng.Next(10,9999) },
                    Tool = new ToolInfo{ Id = _rng.Next(1,16), Life = _rng.NextDouble()*100.0 },
                    Metrics = new Metrics{ SpindleRPM = 1000 + _rng.Next(5000), Feedrate = 500 + _rng.Next(5000), PartCount = lastPart }
                },
                Event = new EventInfo{ Type = execution == "READY" ? "PART_COMPLETED" : "STATE", Severity = "INFO", Code = "VIRT", Message = "sim" }
            };

            await _normalizer.EnqueueAsync(e, ct);
            await Task.Delay(delay, ct);
        }
    }
}
