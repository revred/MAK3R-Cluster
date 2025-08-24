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

    public async Task<List<object>> GetRecentNetPhasesAsync(int limit = 100)
    {
        using var cn = new SqliteConnection(ConnStr); 
        await cn.OpenAsync();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT session_id, ts_utc, phase, latency_ms, ok, error_code, error_detail 
                           FROM net_phase ORDER BY id DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        
        var results = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new {
                sessionId = reader.GetString("session_id"),
                timestamp = reader.GetString("ts_utc"),
                phase = reader.GetString("phase"),
                latencyMs = reader.IsDBNull("latency_ms") ? null : reader.GetInt32("latency_ms"),
                ok = reader.GetInt32("ok") == 1,
                errorCode = reader.IsDBNull("error_code") ? null : reader.GetString("error_code"),
                errorDetail = reader.IsDBNull("error_detail") ? null : reader.GetString("error_detail")
            });
        }
        return results;
    }

    public async Task<List<object>> GetRecentBatchesAsync(int limit = 50)
    {
        using var cn = new SqliteConnection(ConnStr); 
        await cn.OpenAsync();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"SELECT batch_id, enq_ts_utc, send_ts_utc, ack_ts_utc, events_count, bytes, ack_ok, ack_err
                           FROM uplink_batch ORDER BY enq_ts_utc DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        
        var results = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new {
                batchId = reader.GetString("batch_id"),
                enqueuedAt = reader.GetString("enq_ts_utc"),
                sentAt = reader.IsDBNull("send_ts_utc") ? null : reader.GetString("send_ts_utc"),
                ackedAt = reader.IsDBNull("ack_ts_utc") ? null : reader.GetString("ack_ts_utc"),
                eventCount = reader.GetInt32("events_count"),
                bytes = reader.GetInt32("bytes"),
                ackOk = reader.IsDBNull("ack_ok") ? null : reader.GetInt32("ack_ok") == 1,
                ackError = reader.IsDBNull("ack_err") ? null : reader.GetString("ack_err")
            });
        }
        return results;
    }

    public async Task<object> GetNetworkStatsAsync()
    {
        using var cn = new SqliteConnection(ConnStr); 
        await cn.OpenAsync();
        
        // Get recent phase statistics
        using var phaseCmd = cn.CreateCommand();
        phaseCmd.CommandText = @"
            SELECT 
                COUNT(*) as total_phases,
                SUM(CASE WHEN ok = 1 THEN 1 ELSE 0 END) as successful_phases,
                AVG(CASE WHEN latency_ms IS NOT NULL THEN latency_ms END) as avg_latency_ms
            FROM net_phase 
            WHERE datetime(ts_utc) > datetime('now', '-1 hour')";
        
        object? phaseStats = null;
        using var phaseReader = await phaseCmd.ExecuteReaderAsync();
        if (await phaseReader.ReadAsync())
        {
            phaseStats = new {
                totalPhases = phaseReader.GetInt32("total_phases"),
                successfulPhases = phaseReader.GetInt32("successful_phases"),
                avgLatencyMs = phaseReader.IsDBNull("avg_latency_ms") ? null : phaseReader.GetDouble("avg_latency_ms")
            };
        }

        // Get batch statistics
        using var batchCmd = cn.CreateCommand();
        batchCmd.CommandText = @"
            SELECT 
                COUNT(*) as total_batches,
                SUM(CASE WHEN ack_ok = 1 THEN 1 ELSE 0 END) as successful_batches,
                SUM(events_count) as total_events,
                SUM(bytes) as total_bytes
            FROM uplink_batch 
            WHERE datetime(enq_ts_utc) > datetime('now', '-1 hour')";
        
        object? batchStats = null;
        using var batchReader = await batchCmd.ExecuteReaderAsync();
        if (await batchReader.ReadAsync())
        {
            batchStats = new {
                totalBatches = batchReader.GetInt32("total_batches"),
                successfulBatches = batchReader.GetInt32("successful_batches"),
                totalEvents = batchReader.GetInt64("total_events"),
                totalBytes = batchReader.GetInt64("total_bytes")
            };
        }

        return new {
            timestamp = DateTime.UtcNow,
            phases = phaseStats,
            batches = batchStats
        };
    }

    public async Task<List<object>> GetRecentEventsAsync(int limit = 20)
    {
        // This is a simplified implementation - in production would have actual event storage
        return new List<object>
        {
            new { timestamp = DateTime.UtcNow, machineId = "FANUC-TC-01", eventType = "CYCLE_START" },
            new { timestamp = DateTime.UtcNow.AddMinutes(-1), machineId = "HAAS-MILL-03", eventType = "PART_COMPLETED" },
            new { timestamp = DateTime.UtcNow.AddMinutes(-2), machineId = "SIEMENS-TC-02", eventType = "TOOL_CHANGE" }
        };
    }
}
