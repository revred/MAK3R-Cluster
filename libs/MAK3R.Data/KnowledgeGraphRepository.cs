using Microsoft.EntityFrameworkCore;
using MAK3R.Core;
using MAK3R.Data;
using MAK3R.Data.Entities;

namespace MAK3R.Data;

/// <summary>
/// DigitalTwin2 Knowledge Graph Repository implementation
/// High-performance EAV operations with data room isolation and evidence tracking
/// </summary>
public class KnowledgeGraphRepository : IKnowledgeGraphRepository
{
    private readonly MAK3RDbContext _context;

    public KnowledgeGraphRepository(MAK3RDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Result<KnowledgeEntity>> CreateEntityAsync(string type, string dataRoomId, CancellationToken ct = default)
    {
        try
        {
            var entity = new KnowledgeEntity(type, dataRoomId);
            _context.KnowledgeEntities.Add(entity);
            await _context.SaveChangesAsync(ct);
            
            return Result<KnowledgeEntity>.Success(entity);
        }
        catch (Exception ex)
        {
            return Result<KnowledgeEntity>.Failure($"Failed to create entity: {ex.Message}", ex);
        }
    }

    public async Task<Result<KnowledgeEntity>> GetEntityAsync(string entityId, CancellationToken ct = default)
    {
        try
        {
            var entity = await _context.KnowledgeEntities
                .Include(e => e.Attributes)
                .Include(e => e.Relations)
                .FirstOrDefaultAsync(e => e.Id == entityId, ct);

            if (entity == null)
                return Result<KnowledgeEntity>.Failure("Entity not found");

            return Result<KnowledgeEntity>.Success(entity);
        }
        catch (Exception ex)
        {
            return Result<KnowledgeEntity>.Failure($"Failed to get entity: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<KnowledgeEntity>>> GetEntitiesByTypeAsync(string type, string dataRoomId, CancellationToken ct = default)
    {
        try
        {
            var entities = await _context.KnowledgeEntities
                .Where(e => e.Type == type && e.DataRoomId == dataRoomId)
                .Include(e => e.Attributes)
                .Include(e => e.Relations)
                .ToListAsync(ct);

            return Result<List<KnowledgeEntity>>.Success(entities);
        }
        catch (Exception ex)
        {
            return Result<List<KnowledgeEntity>>.Failure($"Failed to get entities: {ex.Message}", ex);
        }
    }

    public async Task<Result<KnowledgeEntity>> UpdateEntityAsync(KnowledgeEntity entity, CancellationToken ct = default)
    {
        try
        {
            _context.KnowledgeEntities.Update(entity);
            await _context.SaveChangesAsync(ct);
            
            return Result<KnowledgeEntity>.Success(entity);
        }
        catch (Exception ex)
        {
            return Result<KnowledgeEntity>.Failure($"Failed to update entity: {ex.Message}", ex);
        }
    }

    public async Task<Result> DeleteEntityAsync(string entityId, CancellationToken ct = default)
    {
        try
        {
            var entity = await _context.KnowledgeEntities
                .FirstOrDefaultAsync(e => e.Id == entityId, ct);

            if (entity == null)
                return Result.Failure("Entity not found");

            _context.KnowledgeEntities.Remove(entity);
            await _context.SaveChangesAsync(ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete entity: {ex.Message}", ex);
        }
    }

    public async Task<Result<EntityAttribute>> SetAttributeAsync(string entityId, string name, object value, double confidence, string? evidenceId = null, CancellationToken ct = default)
    {
        try
        {
            var entity = await _context.KnowledgeEntities
                .Include(e => e.Attributes)
                .FirstOrDefaultAsync(e => e.Id == entityId, ct);

            if (entity == null)
                return Result<EntityAttribute>.Failure("Entity not found");

            var attribute = entity.SetAttribute(name, value, confidence, evidenceId);
            await _context.SaveChangesAsync(ct);

            return Result<EntityAttribute>.Success(attribute);
        }
        catch (Exception ex)
        {
            return Result<EntityAttribute>.Failure($"Failed to set attribute: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<EntityAttribute>>> GetAttributesAsync(string entityId, CancellationToken ct = default)
    {
        try
        {
            var attributes = await _context.EntityAttributes
                .Where(a => a.EntityId == entityId)
                .ToListAsync(ct);

            return Result<List<EntityAttribute>>.Success(attributes);
        }
        catch (Exception ex)
        {
            return Result<List<EntityAttribute>>.Failure($"Failed to get attributes: {ex.Message}", ex);
        }
    }

    public async Task<Result<T?>> GetAttributeValueAsync<T>(string entityId, string attributeName, CancellationToken ct = default)
    {
        try
        {
            var entity = await _context.KnowledgeEntities
                .Include(e => e.Attributes)
                .FirstOrDefaultAsync(e => e.Id == entityId, ct);

            if (entity == null)
                return Result<T?>.Failure("Entity not found");

            var value = entity.GetAttributeValue<T>(attributeName);
            return Result<T?>.Success(value);
        }
        catch (Exception ex)
        {
            return Result<T?>.Failure($"Failed to get attribute value: {ex.Message}", ex);
        }
    }

    public async Task<Result<EntityRelation>> CreateRelationAsync(string sourceEntityId, string targetEntityId, string relationshipType, double confidence = 1.0, string? evidenceId = null, CancellationToken ct = default)
    {
        try
        {
            var entity = await _context.KnowledgeEntities
                .Include(e => e.Relations)
                .FirstOrDefaultAsync(e => e.Id == sourceEntityId, ct);

            if (entity == null)
                return Result<EntityRelation>.Failure("Source entity not found");

            // Verify target entity exists
            var targetExists = await _context.KnowledgeEntities
                .AnyAsync(e => e.Id == targetEntityId, ct);

            if (!targetExists)
                return Result<EntityRelation>.Failure("Target entity not found");

            var relation = entity.AddRelation(relationshipType, targetEntityId, confidence, evidenceId);
            await _context.SaveChangesAsync(ct);

            return Result<EntityRelation>.Success(relation);
        }
        catch (Exception ex)
        {
            return Result<EntityRelation>.Failure($"Failed to create relation: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<EntityRelation>>> GetRelationsAsync(string entityId, string? relationshipType = null, CancellationToken ct = default)
    {
        try
        {
            var query = _context.EntityRelations
                .Where(r => r.SourceEntityId == entityId);

            if (!string.IsNullOrEmpty(relationshipType))
                query = query.Where(r => r.RelationshipType == relationshipType);

            var relations = await query.ToListAsync(ct);
            return Result<List<EntityRelation>>.Success(relations);
        }
        catch (Exception ex)
        {
            return Result<List<EntityRelation>>.Failure($"Failed to get relations: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<KnowledgeEntity>>> GetRelatedEntitiesAsync(string entityId, string relationshipType, CancellationToken ct = default)
    {
        try
        {
            var relatedEntityIds = await _context.EntityRelations
                .Where(r => r.SourceEntityId == entityId && r.RelationshipType == relationshipType)
                .Select(r => r.TargetEntityId)
                .ToListAsync(ct);

            var entities = await _context.KnowledgeEntities
                .Where(e => relatedEntityIds.Contains(e.Id))
                .Include(e => e.Attributes)
                .ToListAsync(ct);

            return Result<List<KnowledgeEntity>>.Success(entities);
        }
        catch (Exception ex)
        {
            return Result<List<KnowledgeEntity>>.Failure($"Failed to get related entities: {ex.Message}", ex);
        }
    }

    public async Task<Result<Evidence>> CreateEvidenceAsync(string sourceType, string sourceId, string sourcePath, double extractionConfidence, string extractionMethod, string dataRoomId, string correlationId, CancellationToken ct = default)
    {
        try
        {
            var evidence = new Evidence(sourceType, sourceId, sourcePath, extractionConfidence, extractionMethod, dataRoomId, correlationId);
            _context.Evidence.Add(evidence);
            await _context.SaveChangesAsync(ct);

            return Result<Evidence>.Success(evidence);
        }
        catch (Exception ex)
        {
            return Result<Evidence>.Failure($"Failed to create evidence: {ex.Message}", ex);
        }
    }

    public async Task<Result<Evidence>> GetEvidenceAsync(string evidenceId, CancellationToken ct = default)
    {
        try
        {
            var evidence = await _context.Evidence
                .FirstOrDefaultAsync(e => e.Id == evidenceId, ct);

            if (evidence == null)
                return Result<Evidence>.Failure("Evidence not found");

            return Result<Evidence>.Success(evidence);
        }
        catch (Exception ex)
        {
            return Result<Evidence>.Failure($"Failed to get evidence: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<Evidence>>> GetEvidenceBySourceAsync(string sourceType, string sourceId, CancellationToken ct = default)
    {
        try
        {
            var evidence = await _context.Evidence
                .Where(e => e.SourceType == sourceType && e.SourceId == sourceId)
                .ToListAsync(ct);

            return Result<List<Evidence>>.Success(evidence);
        }
        catch (Exception ex)
        {
            return Result<List<Evidence>>.Failure($"Failed to get evidence: {ex.Message}", ex);
        }
    }

    public async Task<Result<EventLedger>> AppendEventAsync(string eventType, string sourceId, string sourceType, string dataRoomId, string correlationId, DateTime eventTimestamp, Dictionary<string, object> eventData, CancellationToken ct = default)
    {
        try
        {
            var sequenceResult = await GetNextSequenceNumberAsync(ct);
            if (!sequenceResult.IsSuccess)
                return Result<EventLedger>.Failure(sequenceResult.Error!, sequenceResult.Exception);

            var eventLedger = new EventLedger(eventType, sourceId, sourceType, dataRoomId, correlationId, eventTimestamp, sequenceResult.Value);
            eventLedger.SetEventData(eventData);

            _context.EventLedger.Add(eventLedger);
            await _context.SaveChangesAsync(ct);

            return Result<EventLedger>.Success(eventLedger);
        }
        catch (Exception ex)
        {
            return Result<EventLedger>.Failure($"Failed to append event: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<EventLedger>>> GetEventsAsync(string? dataRoomId = null, string? eventType = null, DateTime? fromTimestamp = null, DateTime? toTimestamp = null, int limit = 100, CancellationToken ct = default)
    {
        try
        {
            var query = _context.EventLedger.AsQueryable();

            if (!string.IsNullOrEmpty(dataRoomId))
                query = query.Where(e => e.DataRoomId == dataRoomId);

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(e => e.EventType == eventType);

            if (fromTimestamp.HasValue)
                query = query.Where(e => e.EventTimestamp >= fromTimestamp.Value);

            if (toTimestamp.HasValue)
                query = query.Where(e => e.EventTimestamp <= toTimestamp.Value);

            var events = await query
                .OrderBy(e => e.EventTimestamp)
                .Take(limit)
                .ToListAsync(ct);

            return Result<List<EventLedger>>.Success(events);
        }
        catch (Exception ex)
        {
            return Result<List<EventLedger>>.Failure($"Failed to get events: {ex.Message}", ex);
        }
    }

    public async Task<Result<long>> GetNextSequenceNumberAsync(CancellationToken ct = default)
    {
        try
        {
            var lastSequence = await _context.EventLedger
                .MaxAsync(e => (long?)e.SequenceNumber, ct) ?? 0;

            return Result<long>.Success(lastSequence + 1);
        }
        catch (Exception ex)
        {
            return Result<long>.Failure($"Failed to get sequence number: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<KnowledgeEntity>>> QueryEntitiesAsync(string dataRoomId, Dictionary<string, object>? attributeFilters = null, CancellationToken ct = default)
    {
        try
        {
            var query = _context.KnowledgeEntities
                .Where(e => e.DataRoomId == dataRoomId)
                .Include(e => e.Attributes)
                .Include(e => e.Relations);

            // TODO: Implement attribute filtering (requires JSON queries or separate filter logic)
            var entities = await query.ToListAsync(ct);

            return Result<List<KnowledgeEntity>>.Success(entities);
        }
        catch (Exception ex)
        {
            return Result<List<KnowledgeEntity>>.Failure($"Failed to query entities: {ex.Message}", ex);
        }
    }

    public async Task<Result<Dictionary<string, object>>> GetEntityGraphAsync(string entityId, int depth = 2, CancellationToken ct = default)
    {
        try
        {
            var graph = new Dictionary<string, object>();
            var visited = new HashSet<string>();

            await BuildEntityGraph(entityId, depth, graph, visited, ct);

            return Result<Dictionary<string, object>>.Success(graph);
        }
        catch (Exception ex)
        {
            return Result<Dictionary<string, object>>.Failure($"Failed to get entity graph: {ex.Message}", ex);
        }
    }

    private async Task BuildEntityGraph(string entityId, int remainingDepth, Dictionary<string, object> graph, HashSet<string> visited, CancellationToken ct)
    {
        if (remainingDepth <= 0 || visited.Contains(entityId))
            return;

        visited.Add(entityId);

        var entityResult = await GetEntityAsync(entityId, ct);
        if (!entityResult.IsSuccess)
            return;

        var entity = entityResult.Value!;
        var entityData = new Dictionary<string, object>
        {
            ["id"] = entity.Id,
            ["type"] = entity.Type,
            ["attributes"] = entity.Attributes.ToDictionary(a => a.Name, a => a.Value),
            ["relations"] = new Dictionary<string, List<string>>()
        };

        graph[entityId] = entityData;

        var relationsDict = (Dictionary<string, List<string>>)entityData["relations"];
        
        foreach (var relation in entity.Relations)
        {
            if (!relationsDict.ContainsKey(relation.RelationshipType))
                relationsDict[relation.RelationshipType] = new List<string>();

            relationsDict[relation.RelationshipType].Add(relation.TargetEntityId);

            // Recursively build graph for related entities
            await BuildEntityGraph(relation.TargetEntityId, remainingDepth - 1, graph, visited, ct);
        }
    }
}