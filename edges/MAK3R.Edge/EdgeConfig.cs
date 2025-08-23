namespace Mak3r.Edge;

public class EdgeConfig
{
    public string SiteId { get; set; } = "SITE";
    public string Timezone { get; set; } = "UTC";
    public string Env { get; set; } = "dev";
    public UplinkConfig Uplink { get; set; } = new();
    public QueueCfg Queue { get; set; } = new();
    public StorageCfg Storage { get; set; } = new();
    public AdminApiCfg AdminApi { get; set; } = new();
    public LoadGenCfg LoadGen { get; set; } = new();
}

public class UplinkConfig
{
    public string HubUrl { get; set; } = "";
    public BatchCfg Batch { get; set; } = new();
    public RetryCfg Retry { get; set; } = new();
}
public class BatchCfg { public int MaxEvents { get; set; } = 200; public int MaxBytes { get; set; } = 256_000; public int FlushMs { get; set; } = 100; }
public class RetryCfg { public int BaseDelayMs { get; set; } = 500; public int MaxDelayMs { get; set; } = 30_000; public bool Jitter { get; set; } = true; }
public class QueueCfg { public int Capacity { get; set; } = 20_000; public string DropPolicy { get; set; } = "block"; }
public class StorageCfg { public string Root { get; set; } = "/var/lib/mak3r"; public SqliteCfg Sqlite { get; set; } = new(); }
public class SqliteCfg { public string Path { get; set; } = "/var/lib/mak3r/edge_netdiag.db"; public int RetentionDays { get; set; } = 90; }
public class AdminApiCfg { public string Listen { get; set; } = "http://0.0.0.0:5080"; }
public class LoadGenCfg
{
    public bool Enabled { get; set; } = false;
    public int Machines { get; set; } = 1000;
    public double RatePerMachineHz { get; set; } = 0.5;
    public int JitterPct { get; set; } = 15;
    public BurstCfg Burst { get; set; } = new();
    public FlapWanCfg FlapWan { get; set; } = new();
}
public class BurstCfg { public bool Enabled { get; set; } = true; public int EverySec { get; set; } = 30; public int FactorX { get; set; } = 3; }
public class FlapWanCfg { public bool Enabled { get; set; } = true; public int DownMs { get; set; } = 15000; public int EverySec { get; set; } = 300; }
