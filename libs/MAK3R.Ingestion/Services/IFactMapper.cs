using MAK3R.Core;
using MAK3R.Data.Entities;

namespace MAK3R.Ingestion.Services;

/// <summary>
/// DigitalTwin2 Fact Mapper - maps extracted facts to Knowledge Graph entities
/// Domain-specific mappers handle business logic for different document types
/// </summary>
public interface IFactMapper
{
    /// <summary>
    /// Map extracted facts to Knowledge Graph entities and relations
    /// </summary>
    Task<Result<MappingResult>> MapAsync(
        ExtractionResult extraction,
        string dataRoomId,
        string correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Check if this mapper can handle the document type
    /// </summary>
    bool CanMap(string documentType);

    /// <summary>
    /// Get mapper metadata and field mappings
    /// </summary>
    MapperInfo GetInfo();
}

/// <summary>
/// Fact mapping result with entities and relations
/// </summary>
public record MappingResult
{
    public List<KnowledgeEntity> Entities { get; init; } = new();
    public List<EntityRelation> Relations { get; init; } = new();
    public Dictionary<string, List<string>> EntityMappings { get; init; } = new(); // fact_id -> entity_ids
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public Dictionary<string, object> Statistics { get; init; } = new();
}

/// <summary>
/// Mapper metadata and field mappings
/// </summary>
public record MapperInfo
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public List<string> SupportedTypes { get; init; } = new();
    public Dictionary<string, FieldMapping> FieldMappings { get; init; } = new();
    public Dictionary<string, object> Configuration { get; init; } = new();
}

/// <summary>
/// Field mapping configuration
/// </summary>
public record FieldMapping
{
    public string SourceField { get; init; } = string.Empty;
    public string TargetEntityType { get; init; } = string.Empty;
    public string TargetAttribute { get; init; } = string.Empty;
    public string? DataType { get; init; }
    public bool Required { get; init; }
    public List<ValidationRule> ValidationRules { get; init; } = new();
    public Dictionary<string, object> Transform { get; init; } = new();
}

/// <summary>
/// Field validation rule
/// </summary>
public record ValidationRule
{
    public string Type { get; init; } = string.Empty; // regex, range, enum, custom
    public string Pattern { get; init; } = string.Empty;
    public object? MinValue { get; init; }
    public object? MaxValue { get; init; }
    public List<string> AllowedValues { get; init; } = new();
    public string ErrorMessage { get; init; } = string.Empty;
}