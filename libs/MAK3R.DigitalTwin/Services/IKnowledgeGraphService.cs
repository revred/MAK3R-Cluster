using MAK3R.Core;
using MAK3R.Data.Entities;

namespace MAK3R.DigitalTwin.Services;

/// <summary>
/// High-level Knowledge Graph service for DigitalTwin2 operations
/// </summary>
public interface IKnowledgeGraphService
{
    // Entity Operations
    Task<Result<KnowledgeEntity>> CreateEntityAsync(string type, Dictionary<string, object> attributes, string dataRoomId, string? evidenceId = null, CancellationToken ct = default);
    Task<Result<KnowledgeEntity>> GetEntityAsync(string entityId, string dataRoomId, CancellationToken ct = default);
    Task<Result<KnowledgeEntity>> UpdateEntityAsync(string entityId, Dictionary<string, object> attributes, string dataRoomId, string? evidenceId = null, CancellationToken ct = default);
    Task<Result<List<KnowledgeEntity>>> FindEntitiesAsync(string type, string dataRoomId, Dictionary<string, object>? filters = null, CancellationToken ct = default);
    
    // Relationship Operations
    Task<Result<EntityRelation>> CreateRelationAsync(string sourceEntityId, string targetEntityId, string relationType, Dictionary<string, object>? properties = null, string? evidenceId = null, CancellationToken ct = default);
    Task<Result<List<EntityRelation>>> GetEntityRelationsAsync(string entityId, string dataRoomId, string? relationType = null, CancellationToken ct = default);
    
    // Evidence Operations
    Task<Result<Evidence>> CreateEvidenceAsync(EvidenceSourceType sourceType, string sourcePath, string mimeType, string content, Dictionary<string, object>? metadata = null, CancellationToken ct = default);
    Task<Result<List<Evidence>>> GetEntityEvidenceAsync(string entityId, string dataRoomId, CancellationToken ct = default);
    
    // Analytics & Insights
    Task<Result<KnowledgeGraphStats>> GetStatsAsync(string dataRoomId, CancellationToken ct = default);
    Task<Result<List<EntityInsight>>> GetEntityInsightsAsync(string entityId, string dataRoomId, CancellationToken ct = default);
    Task<Result<List<GraphPattern>>> DetectPatternsAsync(string dataRoomId, CancellationToken ct = default);
    
    // Query Operations
    Task<Result<List<KnowledgeEntity>>> ExecuteGraphQueryAsync(string query, Dictionary<string, object>? parameters, string dataRoomId, CancellationToken ct = default);
    
    // Cold Start Operations
    Task<Result<ColdStartResult>> ExecuteColdStartAsync(string dataRoomId, ColdStartOptions? options = null, CancellationToken ct = default);
}

/// <summary>
/// Knowledge Graph statistics for cold start validation and monitoring
/// </summary>
public record KnowledgeGraphStats
{
    public int TotalEntities { get; init; }
    public int TotalRelations { get; init; }
    public int TotalEvidence { get; init; }
    public Dictionary<string, int> EntitiesByType { get; init; } = new();
    public Dictionary<string, int> RelationsByType { get; init; } = new();
    public double AverageConfidence { get; init; }
    public double EvidencePercentage { get; init; }
    public DateTime LastUpdated { get; init; }
    public string DataRoomId { get; init; } = string.Empty;
}

/// <summary>
/// Entity insight for AI-driven analysis
/// </summary>
public record EntityInsight
{
    public string Id { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string InsightType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public List<string> EvidenceIds { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Graph pattern detection for relationship analysis
/// </summary>
public record GraphPattern
{
    public string Id { get; init; } = string.Empty;
    public string PatternType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> EntityIds { get; init; } = new();
    public List<string> RelationTypes { get; init; } = new();
    public double Strength { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public DateTime DetectedAt { get; init; }
}

/// <summary>
/// Cold start execution result with validation metrics
/// </summary>
public record ColdStartResult
{
    public bool IsSuccess { get; init; }
    public int ProcessedDocuments { get; init; }
    public int CreatedEntities { get; init; }
    public int CreatedRelations { get; init; }
    public int CreatedEvidence { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public double OverallConfidence { get; init; }
    public double EvidenceCoverage { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public Dictionary<string, object> Metrics { get; init; } = new();
}

/// <summary>
/// Cold start configuration options
/// </summary>
public record ColdStartOptions
{
    public int MaxDocuments { get; init; } = 2000;
    public double MinConfidenceThreshold { get; init; } = 0.6;
    public bool EnableParallelProcessing { get; init; } = true;
    public int MaxParallelism { get; init; } = 4;
    public bool StoreIntermediateResults { get; init; } = true;
    public List<string> DocumentTypes { get; init; } = new() { "pdf", "csv", "xlsx", "sqlite", "jpeg", "png" };
    public Dictionary<string, object> ExtractorOptions { get; init; } = new();
}