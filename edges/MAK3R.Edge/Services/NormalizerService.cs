using Mak3r.Edge.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mak3r.Edge.Services;

public class NormalizerService : BackgroundService
{
    private readonly QueueService _queue;
    private readonly ILogger<NormalizerService> _log;

    public NormalizerService(QueueService queue, ILogger<NormalizerService> log)
    {
        _queue = queue; _log = log;
    }

    // In this starter, Virtual events are already KMachineEvent; a real implementation would map raw vendor payloads here.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("NormalizerService active");
        return Task.CompletedTask;
    }

    public Task EnqueueAsync(KMachineEvent e, CancellationToken ct) => _queue.WriteAsync(e, ct).AsTask();
}
