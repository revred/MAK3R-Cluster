using Mak3r.Edge.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using MessagePack;

namespace Mak3r.Edge.Services;

public class UplinkService : BackgroundService
{
    private readonly QueueService _queue;
    private readonly NetDiagDb _db;
    private readonly ILogger<UplinkService> _log;
    private readonly EdgeConfig _cfg;
    private HubConnection? _hub;
    private string _sessionId = Guid.NewGuid().ToString();

    public UplinkService(QueueService queue, NetDiagDb db, IOptions<EdgeConfig> cfg, ILogger<UplinkService> log)
    {
        _queue = queue; _db = db; _log = log; _cfg = cfg.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureConnected(stoppingToken);
                await PumpBatches(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Uplink loop error");
                _db.InsertNetPhase(_sessionId, "HUB_ERROR", ok:false, errCode: ex.GetType().Name, errDetail: ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task EnsureConnected(CancellationToken ct)
    {
        if (_hub is { State: HubConnectionState.Connected }) return;

        _sessionId = Guid.NewGuid().ToString();
        _db.InsertNetPhase(_sessionId, "HUB_NEGOTIATE", ok:true);

        _hub = new HubConnectionBuilder()
            .WithUrl(_cfg.Uplink.HubUrl)
            .WithAutomaticReconnect()
            .AddMessagePackProtocol()
            .Build();

        var sw = Stopwatch.StartNew();
        await _hub.StartAsync(ct);
        sw.Stop();
        _db.InsertNetPhase(_sessionId, "HUB_CONNECTED", ok:true, latencyMs:(int)sw.ElapsedMilliseconds);
        _log.LogInformation("SignalR connected in {ms} ms", sw.ElapsedMilliseconds);
    }

    private record BatchFrame(string siteId, string batchId, List<KMachineEvent> events);

    private async Task PumpBatches(CancellationToken ct)
    {
        var batch = new List<KMachineEvent>(_cfg.Uplink.Batch.MaxEvents);
        var sw = Stopwatch.StartNew();

        await foreach (var e in _queue.ReadAllAsync(ct))
        {
            batch.Add(e);
            if (ShouldFlush(batch, sw)) { await Send(batch, ct); batch.Clear(); sw.Restart(); }
        }
        if (batch.Count > 0) await Send(batch, ct);
    }

    private bool ShouldFlush(List<KMachineEvent> batch, Stopwatch sw)
    {
        if (batch.Count >= _cfg.Uplink.Batch.MaxEvents) return true;
        if (sw.ElapsedMilliseconds >= _cfg.Uplink.Batch.FlushMs) return true;
        var bytes = MessagePackSerializer.SerializeToJson(batch).Length; // approx
        return bytes >= _cfg.Uplink.Batch.MaxBytes;
    }

    private async Task Send(List<KMachineEvent> batch, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString();
        _db.InsertBatch(id, batch.Count, MessagePackSerializer.SerializeToJson(batch).Length);
        _db.UpdateBatchSent(id);
        var frame = new BatchFrame(_cfg.SiteId, id, batch);

        var payload = MessagePackSerializer.Serialize(frame);
        try
        {
            if (_hub is null) throw new InvalidOperationException("Hub not connected");
            await _hub.InvokeAsync("PublishBatch", frame, ct);
            _db.UpdateBatchAck(id, ok:true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Send batch failed; spooling");
            _db.UpdateBatchAck(id, ok:false, err: ex.Message);
            await SpoolAsync(id, payload);
            // backoff
            await Task.Delay(TimeSpan.FromMilliseconds(_cfg.Uplink.Retry.BaseDelayMs), ct);
            // attempt reconnect on next loop
            _hub = null;
        }
    }

    private async Task SpoolAsync(string batchId, byte[] payload)
    {
        var dir = Path.Combine(_cfg.Storage.Root, "spool");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMddHHmmss}_{batchId}.mpack");
        await File.WriteAllBytesAsync(path, payload);
    }
}
