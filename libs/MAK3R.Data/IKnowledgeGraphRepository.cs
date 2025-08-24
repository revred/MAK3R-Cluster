using MAK3R.Core;
using MAK3R.Data.Entities;

namespace MAK3R.Data;

/// <summary>
/// DigitalTwin2 Knowledge Graph Repository - EAV operations with evidence tracking
/// All operations are data room isolated and maintain full audit trails
/// </summary>
public interface IKnowledgeGraphRepository
{
    // Entity Operations
    Task<Result<KnowledgeEntity>> CreateEntityAsync(string type, string dataRoomId, CancellationToken ct = default);
    Task<Result<KnowledgeEntity>> GetEntityAsync(string entityId, CancellationToken ct = default);
    Task<Result<List<KnowledgeEntity>>> GetEntitiesByTypeAsync(string type, string dataRoomId, CancellationToken ct = default);
    Task<Result<KnowledgeEntity>> UpdateEntityAsync(KnowledgeEntity entity, CancellationToken ct = default);
    Task<Result> DeleteEntityAsync(string entityId, CancellationToken ct = default);

    // Attribute Operations
    Task<Result<EntityAttribute>> SetAttributeAsync(string entityId, string name, object value, double confidence, string? evidenceId = null, CancellationToken ct = default);
    Task<Result<List<EntityAttribute>>> GetAttributesAsync(string entityId, CancellationToken ct = default);
    Task<Result<T?>> GetAttributeValueAsync<T>(string entityId, string attributeName, CancellationToken ct = default);

    // Relation Operations
    Task<Result<EntityRelation>> CreateRelationAsync(string sourceEntityId, string targetEntityId, string relationshipType, double confidence = 1.0, string? evidenceId = null, CancellationToken ct = default);
    Task<Result<List<EntityRelation>>> GetRelationsAsync(string entityId, string? relationshipType = null, CancellationToken ct = default);
    Task<Result<List<KnowledgeEntity>>> GetRelatedEntitiesAsync(string entityId, string relationshipType, CancellationToken ct = default);

    // Evidence Operations
    Task<Result<Evidence>> CreateEvidenceAsync(string sourceType, string sourceId, string sourcePath, double extractionConfidence, string extractionMethod, string dataRoomId, string correlationId, CancellationToken ct = default);
    Task<Result<Evidence>> GetEvidenceAsync(string evidenceId, CancellationToken ct = default);
    Task<Result<List<Evidence>>> GetEvidenceBySourceAsync(string sourceType, string sourceId, CancellationToken ct = default);

    // Event Ledger Operations
    Task<Result<EventLedger>> AppendEventAsync(string eventType, string sourceId, string sourceType, string dataRoomId, string correlationId, DateTime eventTimestamp, Dictionary<string, object> eventData, CancellationToken ct = default);
    Task<Result<List<EventLedger>>> GetEventsAsync(string? dataRoomId = null, string? eventType = null, DateTime? fromTimestamp = null, DateTime? toTimestamp = null, int limit = 100, CancellationToken ct = default);
    Task<Result<long>> GetNextSequenceNumberAsync(CancellationToken ct = default);

    // Query Operations
    Task<Result<List<KnowledgeEntity>>> QueryEntitiesAsync(string dataRoomId, Dictionary<string, object>? attributeFilters = null, CancellationToken ct = default);
    Task<Result<Dictionary<string, object>>> GetEntityGraphAsync(string entityId, int depth = 2, CancellationToken ct = default);
}