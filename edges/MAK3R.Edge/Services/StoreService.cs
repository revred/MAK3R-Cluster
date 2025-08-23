using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mak3r.Edge.Services;

public class StoreService : BackgroundService
{
    private readonly NetDiagDb _db;
    private readonly QueueService _queue;
    private readonly ILogger<StoreService> _log;

    public StoreService(NetDiagDb db, QueueService queue, ILogger<StoreService> log)
    {
        _db = db; _queue = queue; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _db.InsertQueueSample(_queue.ApproxDepth);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
