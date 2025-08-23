using Mak3r.Edge.Models;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mak3r.Edge.Services;

public class QueueService
{
    private readonly Channel<KMachineEvent> _channel;
    private readonly ILogger<QueueService> _log;
    private readonly EdgeConfig _cfg;

    public QueueService(IOptions<EdgeConfig> cfg, ILogger<QueueService> log)
    {
        _cfg = cfg.Value;
        var opts = new BoundedChannelOptions(_cfg.Queue.Capacity)
        {
            FullMode = _cfg.Queue.DropPolicy.Equals("drop-oldest", StringComparison.OrdinalIgnoreCase)
                ? BoundedChannelFullMode.DropOldest : BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<KMachineEvent>(opts);
        _log = log;
    }

    public ValueTask WriteAsync(KMachineEvent e, CancellationToken ct) => _channel.Writer.WriteAsync(e, ct);
    public IAsyncEnumerable<KMachineEvent> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public int ApproxDepth => _channel.Reader.Count;
}
