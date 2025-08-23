using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

namespace Mak3r.Edge;

public class NetDiagDb
{
    private readonly EdgeConfig _cfg;
    private readonly ILogger<NetDiagDb> _log;
    private string ConnStr => $"Data Source={_cfg.Storage.Sqlite.Path}";

    public NetDiagDb(IOptions<EdgeConfig> cfg, ILogger<NetDiagDb> log)
    {
        _cfg = cfg.Value;
        _log = log;
        Directory.CreateDirectory(Path.GetDirectoryName(_cfg.Storage.Sqlite.Path)!);
    }

    public void Init()
    {
        using var cn = new SqliteConnection(ConnStr);
        cn.Open();
        var schema = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Persistence", "SqliteSchema.sql"));
        using var cmd = cn.CreateCommand();
        cmd.CommandText = schema;
        cmd.ExecuteNonQuery();
        _log.LogInformation("SQLite initialized at {Path}", _cfg.Storage.Sqlite.Path);
    }

    public void InsertNetPhase(string sessionId, string phase, bool ok, int? latencyMs = null, string? errCode = null, string? errDetail = null)
    {
        using var cn = new SqliteConnection(ConnStr); cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"INSERT INTO net_phase(session_id, ts_utc, phase, latency_ms, ok, error_code, error_detail)
                            VALUES ($sid, datetime('now'), $phase, $lat, $ok, $err, $detail)";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$phase", phase);
        cmd.Parameters.AddWithValue("$lat", (object?)latencyMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ok", ok ? 1 : 0);
        cmd.Parameters.AddWithValue("$err", (object?)errCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$detail", (object?)errDetail ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void InsertQueueSample(int depth)
    {
        using var cn = new SqliteConnection(ConnStr); cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"INSERT INTO queue_sample(ts_utc, depth) VALUES (datetime('now'), $d)";
        cmd.Parameters.AddWithValue("$d", depth);
        cmd.ExecuteNonQuery();
    }

    public void InsertBatch(string batchId, int events, int bytes)
    {
        using var cn = new SqliteConnection(ConnStr); cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"INSERT INTO uplink_batch(batch_id,enq_ts_utc,events_count,bytes) 
                            VALUES ($id, datetime('now'), $n, $b)";
        cmd.Parameters.AddWithValue("$id", batchId);
        cmd.Parameters.AddWithValue("$n", events);
        cmd.Parameters.AddWithValue("$b", bytes);
        cmd.ExecuteNonQuery();
    }

    public void UpdateBatchSent(string batchId)
    {
        using var cn = new SqliteConnection(ConnStr); cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"UPDATE uplink_batch SET send_ts_utc=datetime('now') WHERE batch_id=$id";
        cmd.Parameters.AddWithValue("$id", batchId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateBatchAck(string batchId, bool ok, string? err = null)
    {
        using var cn = new SqliteConnection(ConnStr); cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"UPDATE uplink_batch SET ack_ts_utc=datetime('now'), ack_ok=$ok, ack_err=$err WHERE batch_id=$id";
        cmd.Parameters.AddWithValue("$id", batchId);
        cmd.Parameters.AddWithValue("$ok", ok ? 1 : 0);
        cmd.Parameters.AddWithValue("$err", (object?)err ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}
