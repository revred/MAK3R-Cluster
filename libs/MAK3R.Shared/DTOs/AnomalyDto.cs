namespace MAK3R.Shared.DTOs;

public record AnomalyDto(
    Guid Id,
    string EntityType,
    string EntityId,
    AnomalySeverity Severity,
    string Message,
    string? RuleId,
    AnomalyStatus Status,
    string? AssignedTo,
    string? Resolution,
    DateTime DetectedUtc,
    DateTime? ResolvedUtc
);

public record CreateAnomalyRequest(
    string EntityType,
    string EntityId,
    AnomalySeverity Severity,
    string Message,
    string? RuleId
);

public record UpdateAnomalyRequest(
    AnomalyStatus Status,
    string? AssignedTo,
    string? Resolution
);

public record AnomalyRuleDto(
    string Id,
    string Name,
    string Description,
    string EntityType,
    string Condition,
    AnomalySeverity Severity,
    string? Action,
    bool IsEnabled,
    DateTime CreatedUtc,
    DateTime UpdatedUtc
);

public enum AnomalySeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum AnomalyStatus
{
    Open,
    InProgress,
    Resolved,
    Dismissed
}