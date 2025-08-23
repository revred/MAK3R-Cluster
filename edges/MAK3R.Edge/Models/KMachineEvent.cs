using MessagePack;

namespace Mak3r.Edge.Models;

[MessagePackObject(true)]
public class KMachineEvent
{
    public string SiteId { get; set; } = "";
    public string MachineId { get; set; } = "";
    public DateTime Ts { get; set; }
    public SourceInfo Source { get; set; } = new();
    public StateInfo State { get; set; } = new();
    public EventInfo Event { get; set; } = new();
    public ContextInfo Context { get; set; } = new();
    public string EventId => $"{SiteId}|{MachineId}|{Ts:O}|{Event?.Type}|{Event?.Code}".GetHashCode().ToString("X"); // simple deterministic id
}

[MessagePackObject(true)] public class SourceInfo { public string Vendor { get; set; } = ""; public string Protocol { get; set; } = ""; public string Ip { get; set; } = ""; }
[MessagePackObject(true)]
public class StateInfo
{
    public string? Power { get; set; }
    public string? Availability { get; set; }
    public string? Mode { get; set; }
    public string? Execution { get; set; }
    public ProgramInfo? Program { get; set; }
    public ToolInfo? Tool { get; set; }
    public Overrides? Overrides { get; set; }
    public Metrics? Metrics { get; set; }
}
[MessagePackObject(true)] public class ProgramInfo { public string? Name { get; set; } public int? Block { get; set; } }
[MessagePackObject(true)] public class ToolInfo { public int? Id { get; set; } public double? Life { get; set; } }
[MessagePackObject(true)] public class Overrides { public double? Feed { get; set; } public double? Spindle { get; set; } public double? Rapid { get; set; } }
[MessagePackObject(true)] public class Metrics { public double? SpindleRPM { get; set; } public double? Feedrate { get; set; } public int? PartCount { get; set; } }
[MessagePackObject(true)] public class EventInfo { public string? Type { get; set; } public string? Severity { get; set; } public string? Code { get; set; } public string? Message { get; set; } }
[MessagePackObject(true)] public class ContextInfo { public JobInfo? Job { get; set; } public OperatorInfo? Operator { get; set; } public Workholding? Workholding { get; set; } public Material? Material { get; set; } }
[MessagePackObject(true)] public class JobInfo { public string? Id { get; set; } public string? Op { get; set; } public string? Barcode { get; set; } }
[MessagePackObject(true)] public class OperatorInfo { public string? Badge { get; set; } }
[MessagePackObject(true)] public class Workholding { public string? Type { get; set; } public string? FixtureId { get; set; } }
[MessagePackObject(true)] public class Material { public string? Lot { get; set; } }
