namespace MAK3R.Shared.DTOs;

public record MachineDto(
    Guid Id,
    string Name,
    string? Make,
    string? Model,
    string? SerialNumber,
    string? OpcUaNode,
    Guid SiteId,
    string SiteName,
    MachineStatus Status,
    Dictionary<string, object>? CurrentMetrics,
    DateTime CreatedUtc,
    DateTime UpdatedUtc
);

public record CreateMachineRequest(
    string Name,
    string? Make,
    string? Model,
    string? SerialNumber,
    string? OpcUaNode,
    Guid SiteId
);

public record UpdateMachineRequest(
    string Name,
    string? Make,
    string? Model,
    string? SerialNumber,
    string? OpcUaNode,
    MachineStatus Status
);

public record MachineMetricDto(
    string MachineId,
    string MetricName,
    object Value,
    string? Unit,
    DateTime Timestamp
);

public enum MachineStatus
{
    Unknown,
    Idle,
    Running,
    Maintenance,
    Error,
    Offline
}