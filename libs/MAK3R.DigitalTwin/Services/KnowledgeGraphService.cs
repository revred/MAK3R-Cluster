using MAK3R.Core;
using MAK3R.Data;
using MAK3R.Data.Entities;
using MAK3R.Data.Services;
using MAK3R.Ingestion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MAK3R.DigitalTwin.Services;

/// <summary>
/// High-level Knowledge Graph service implementation for DigitalTwin2
/// </summary>
public class KnowledgeGraphService : IKnowledgeGraphService
{
    private readonly IKnowledgeGraphRepository _repository;
    private readonly IIngestionPipeline _ingestionPipeline;
    private readonly MAK3RDbContext _context;
    private readonly ILogger<KnowledgeGraphService> _logger;

    public KnowledgeGraphService(
        IKnowledgeGraphRepository repository,
        IIngestionPipeline ingestionPipeline,
        MAK3RDbContext context,
        ILogger<KnowledgeGraphService> logger)
    {
        _repository = repository;
        _ingestionPipeline = ingestionPipeline;
        _context = context;
        _logger = logger;
    }

    public async Task<Result<KnowledgeEntity>> CreateEntityAsync(
        string type, 
        Dictionary<string, object> attributes, 
        string dataRoomId, 
        string? evidenceId = null, 
        CancellationToken ct = default)
    {
        try
        {
            var entity = new KnowledgeEntity(UlidGenerator.NewId(), type, dataRoomId);

            foreach (var kvp in attributes)
            {
                var confidence = kvp.Value is Dictionary<string, object> attrDict && attrDict.ContainsKey("confidence")
                    ? Convert.ToDouble(attrDict["confidence"])
                    : 0.8;

                var value = kvp.Value is Dictionary<string, object> dict && dict.ContainsKey("value")
                    ? dict["value"]
                    : kvp.Value;

                entity.SetAttribute(kvp.Key, value, confidence, evidenceId);
            }

            var result = await _repository.CreateEntityAsync(entity, ct);
            if (!result.IsSuccess)
            {
                return Result<KnowledgeEntity>.Failure(result.Error!);
            }

            _logger.LogInformation("Created entity {EntityId} of type {Type} in data room {DataRoomId}", 
                entity.Id, type, dataRoomId);

            return Result<KnowledgeEntity>.Success(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create entity of type {Type} in data room {DataRoomId}", type, dataRoomId);
            return Result<KnowledgeEntity>.Failure($"Entity creation failed: {ex.Message}");
        }
    }

    public async Task<Result<KnowledgeEntity>> GetEntityAsync(string entityId, string dataRoomId, CancellationToken ct = default)
    {
        try
        {
            var result = await _repository.GetEntityAsync(entityId, dataRoomId, ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entity {EntityId} from data room {DataRoomId}", entityId, dataRoomId);
            return Result<KnowledgeEntity>.Failure($"Entity retrieval failed: {ex.Message}");
        }
    }

    public async Task<Result<KnowledgeEntity>> UpdateEntityAsync(
        string entityId, 
        Dictionary<string, object> attributes, 
        string dataRoomId, 
        string? evidenceId = null, 
        CancellationToken ct = default)
    {
        try
        {
            var entityResult = await _repository.GetEntityAsync(entityId, dataRoomId, ct);
            if (!entityResult.IsSuccess)
            {
                return entityResult;
            }

            var entity = entityResult.Value;

            foreach (var kvp in attributes)
            {
                var confidence = kvp.Value is Dictionary<string, object> attrDict && attrDict.ContainsKey("confidence")
                    ? Convert.ToDouble(attrDict["confidence"])
                    : 0.8;

                var value = kvp.Value is Dictionary<string, object> dict && dict.ContainsKey("value")
                    ? dict["value"]
                    : kvp.Value;

                entity.SetAttribute(kvp.Key, value, confidence, evidenceId);
            }

            var updateResult = await _repository.UpdateEntityAsync(entity, ct);
            if (!updateResult.IsSuccess)
            {
                return Result<KnowledgeEntity>.Failure(updateResult.Error!);
            }

            _logger.LogInformation("Updated entity {EntityId} in data room {DataRoomId}", entityId, dataRoomId);

            return Result<KnowledgeEntity>.Success(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update entity {EntityId} in data room {DataRoomId}", entityId, dataRoomId);
            return Result<KnowledgeEntity>.Failure($"Entity update failed: {ex.Message}");
        }
    }

    public async Task<Result<List<KnowledgeEntity>>> FindEntitiesAsync(
        string type, 
        string dataRoomId, 
        Dictionary<string, object>? filters = null, 
        CancellationToken ct = default)
    {
        try
        {
            var result = await _repository.FindEntitiesAsync(type, dataRoomId, filters, ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find entities of type {Type} in data room {DataRoomId}", type, dataRoomId);
            return Result<List<KnowledgeEntity>>.Failure($"Entity search failed: {ex.Message}");
        }
    }

    public async Task<Result<EntityRelation>> CreateRelationAsync(
        string sourceEntityId, 
        string targetEntityId, 
        string relationType, 
        Dictionary<string, object>? properties = null, 
        string? evidenceId = null, 
        CancellationToken ct = default)
    {
        try
        {
            var relation = new EntityRelation
            {
                Id = UlidGenerator.NewId(),
                SourceEntityId = sourceEntityId,
                TargetEntityId = targetEntityId,
                RelationType = relationType,
                Properties = properties ?? new Dictionary<string, object>(),
                EvidenceId = evidenceId,
                Confidence = properties?.ContainsKey("confidence") == true 
                    ? Convert.ToDouble(properties["confidence"]) 
                    : 0.8,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.EntityRelations.Add(relation);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Created relation {RelationType} between {SourceId} and {TargetId}", 
                relationType, sourceEntityId, targetEntityId);

            return Result<EntityRelation>.Success(relation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create relation between {SourceId} and {TargetId}", sourceEntityId, targetEntityId);
            return Result<EntityRelation>.Failure($"Relation creation failed: {ex.Message}");
        }
    }

    public async Task<Result<List<EntityRelation>>> GetEntityRelationsAsync(
        string entityId, 
        string dataRoomId, 
        string? relationType = null, 
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.EntityRelations
                .Include(r => r.SourceEntity)
                .Include(r => r.TargetEntity)
                .Where(r => (r.SourceEntityId == entityId || r.TargetEntityId == entityId) &&
                           (r.SourceEntity.DataRoomId == dataRoomId || r.TargetEntity.DataRoomId == dataRoomId));

            if (!string.IsNullOrEmpty(relationType))
            {
                query = query.Where(r => r.RelationType == relationType);
            }

            var relations = await query.ToListAsync(ct);

            return Result<List<EntityRelation>>.Success(relations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get relations for entity {EntityId} in data room {DataRoomId}", entityId, dataRoomId);
            return Result<List<EntityRelation>>.Failure($"Relation retrieval failed: {ex.Message}");
        }
    }

    public async Task<Result<Evidence>> CreateEvidenceAsync(
        EvidenceSourceType sourceType, 
        string sourcePath, 
        string mimeType, 
        string content, 
        Dictionary<string, object>? metadata = null, 
        CancellationToken ct = default)
    {
        try
        {
            var evidence = new Evidence
            {
                Id = UlidGenerator.NewId(),
                SourceType = sourceType,
                SourcePath = sourcePath,
                MimeType = mimeType,
                Content = content,
                ContentHash = ComputeContentHash(content),
                Metadata = metadata ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Evidence.Add(evidence);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Created evidence {EvidenceId} from {SourcePath}", evidence.Id, sourcePath);

            return Result<Evidence>.Success(evidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create evidence from {SourcePath}", sourcePath);
            return Result<Evidence>.Failure($"Evidence creation failed: {ex.Message}");
        }
    }

    public async Task<Result<List<Evidence>>> GetEntityEvidenceAsync(string entityId, string dataRoomId, CancellationToken ct = default)
    {
        try
        {
            var evidenceIds = await _context.EntityAttributes
                .Where(a => a.EntityId == entityId && !string.IsNullOrEmpty(a.EvidenceId))
                .Select(a => a.EvidenceId!)
                .Distinct()
                .ToListAsync(ct);

            var evidence = await _context.Evidence
                .Where(e => evidenceIds.Contains(e.Id))
                .ToListAsync(ct);

            return Result<List<Evidence>>.Success(evidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get evidence for entity {EntityId} in data room {DataRoomId}", entityId, dataRoomId);
            return Result<List<Evidence>>.Failure($"Evidence retrieval failed: {ex.Message}");
        }
    }

    public async Task<Result<KnowledgeGraphStats>> GetStatsAsync(string dataRoomId, CancellationToken ct = default)
    {
        try
        {
            var totalEntities = await _context.KnowledgeEntities
                .Where(e => e.DataRoomId == dataRoomId)
                .CountAsync(ct);

            var totalRelations = await _context.EntityRelations
                .Include(r => r.SourceEntity)
                .Where(r => r.SourceEntity.DataRoomId == dataRoomId)
                .CountAsync(ct);

            var totalEvidence = await _context.Evidence.CountAsync(ct);

            var entitiesByType = await _context.KnowledgeEntities
                .Where(e => e.DataRoomId == dataRoomId)
                .GroupBy(e => e.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type, x => x.Count, ct);

            var relationsByType = await _context.EntityRelations
                .Include(r => r.SourceEntity)
                .Where(r => r.SourceEntity.DataRoomId == dataRoomId)
                .GroupBy(r => r.RelationType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type, x => x.Count, ct);

            var averageConfidence = await _context.EntityAttributes
                .Include(a => a.Entity)
                .Where(a => a.Entity.DataRoomId == dataRoomId)
                .AverageAsync(a => (double?)a.Confidence, ct) ?? 0.0;

            var evidencePercentage = totalEntities > 0 
                ? (double)await _context.EntityAttributes
                    .Include(a => a.Entity)
                    .Where(a => a.Entity.DataRoomId == dataRoomId && !string.IsNullOrEmpty(a.EvidenceId))
                    .CountAsync(ct) / totalEntities 
                : 0.0;

            var stats = new KnowledgeGraphStats
            {
                TotalEntities = totalEntities,
                TotalRelations = totalRelations,
                TotalEvidence = totalEvidence,
                EntitiesByType = entitiesByType,
                RelationsByType = relationsByType,
                AverageConfidence = averageConfidence,
                EvidencePercentage = evidencePercentage,
                LastUpdated = DateTime.UtcNow,
                DataRoomId = dataRoomId
            };

            return Result<KnowledgeGraphStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get knowledge graph stats for data room {DataRoomId}", dataRoomId);
            return Result<KnowledgeGraphStats>.Failure($"Stats retrieval failed: {ex.Message}");
        }
    }

    public async Task<Result<List<EntityInsight>>> GetEntityInsightsAsync(string entityId, string dataRoomId, CancellationToken ct = default)
    {
        try
        {
            // Placeholder for AI-driven insights generation
            // This would integrate with ML/AI services for pattern recognition
            var insights = new List<EntityInsight>();

            var entity = await _repository.GetEntityAsync(entityId, dataRoomId, ct);
            if (!entity.IsSuccess)
            {
                return Result<List<EntityInsight>>.Success(insights);
            }

            // Example insight: High confidence attributes
            var highConfidenceAttrs = entity.Value.Attributes
                .Where(a => a.Confidence > 0.9)
                .ToList();

            if (highConfidenceAttrs.Count > 0)
            {
                insights.Add(new EntityInsight
                {
                    Id = UlidGenerator.NewId(),
                    EntityId = entityId,
                    InsightType = "high_confidence_data",
                    Title = "High Confidence Attributes",
                    Description = $"Entity has {highConfidenceAttrs.Count} attributes with >90% confidence",
                    Confidence = 0.95,
                    EvidenceIds = highConfidenceAttrs
                        .Where(a => !string.IsNullOrEmpty(a.EvidenceId))
                        .Select(a => a.EvidenceId!)
                        .ToList(),
                    GeneratedAt = DateTime.UtcNow
                });
            }

            return Result<List<EntityInsight>>.Success(insights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get insights for entity {EntityId} in data room {DataRoomId}", entityId, dataRoomId);
            return Result<List<EntityInsight>>.Failure($"Insights generation failed: {ex.Message}");
        }
    }

    public async Task<Result<List<GraphPattern>>> DetectPatternsAsync(string dataRoomId, CancellationToken ct = default)
    {
        try
        {
            // Placeholder for graph pattern detection
            // This would analyze relationships and detect common patterns
            var patterns = new List<GraphPattern>();

            // Example: Detect hub entities (entities with many relationships)
            var hubEntities = await _context.EntityRelations
                .Include(r => r.SourceEntity)
                .Include(r => r.TargetEntity)
                .Where(r => r.SourceEntity.DataRoomId == dataRoomId || r.TargetEntity.DataRoomId == dataRoomId)
                .SelectMany(r => new[] { r.SourceEntityId, r.TargetEntityId })
                .GroupBy(id => id)
                .Where(g => g.Count() > 5)
                .Select(g => new { EntityId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var hub in hubEntities)
            {
                patterns.Add(new GraphPattern
                {
                    Id = UlidGenerator.NewId(),
                    PatternType = "hub_entity",
                    Description = $"Entity {hub.EntityId} acts as a hub with {hub.Count} connections",
                    EntityIds = new List<string> { hub.EntityId },
                    Strength = Math.Min(1.0, hub.Count / 10.0),
                    DetectedAt = DateTime.UtcNow
                });
            }

            return Result<List<GraphPattern>>.Success(patterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect patterns in data room {DataRoomId}", dataRoomId);
            return Result<List<GraphPattern>>.Failure($"Pattern detection failed: {ex.Message}");
        }
    }

    public async Task<Result<List<KnowledgeEntity>>> ExecuteGraphQueryAsync(
        string query, 
        Dictionary<string, object>? parameters, 
        string dataRoomId, 
        CancellationToken ct = default)
    {
        try
        {
            // Placeholder for graph query execution
            // This would implement a graph query language (like Cypher)
            _logger.LogWarning("Graph query execution not yet implemented: {Query}", query);
            
            return Result<List<KnowledgeEntity>>.Success(new List<KnowledgeEntity>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute graph query in data room {DataRoomId}", dataRoomId);
            return Result<List<KnowledgeEntity>>.Failure($"Query execution failed: {ex.Message}");
        }
    }

    public async Task<Result<ColdStartResult>> ExecuteColdStartAsync(
        string dataRoomId, 
        ColdStartOptions? options = null, 
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var coldStartOptions = options ?? new ColdStartOptions();
        var result = new ColdStartResult
        {
            IsSuccess = true,
            ValidationErrors = new List<string>(),
            Warnings = new List<string>()
        };

        try
        {
            _logger.LogInformation("Starting cold start process for data room {DataRoomId}", dataRoomId);

            // This would typically:
            // 1. Process documents from a designated folder
            // 2. Run them through the ingestion pipeline
            // 3. Validate the resulting knowledge graph meets cold start criteria
            
            // For now, return success metrics from current graph state
            var statsResult = await GetStatsAsync(dataRoomId, ct);
            if (statsResult.IsSuccess)
            {
                var stats = statsResult.Value;
                result = result with
                {
                    CreatedEntities = stats.TotalEntities,
                    CreatedRelations = stats.TotalRelations,
                    CreatedEvidence = stats.TotalEvidence,
                    ProcessingTime = DateTime.UtcNow - startTime,
                    OverallConfidence = stats.AverageConfidence,
                    EvidenceCoverage = stats.EvidencePercentage,
                    Metrics = new Dictionary<string, object>
                    {
                        ["entitiesByType"] = stats.EntitiesByType,
                        ["relationsByType"] = stats.RelationsByType,
                        ["coldStartCompliant"] = stats.TotalEntities >= 100 && stats.EvidencePercentage >= 0.9
                    }
                };

                // Validate cold start criteria
                if (stats.TotalEntities < 100)
                {
                    result.ValidationErrors.Add($"Insufficient entities: {stats.TotalEntities} < 100 required");
                }

                if (stats.EvidencePercentage < 0.9)
                {
                    result.ValidationErrors.Add($"Insufficient evidence coverage: {stats.EvidencePercentage:P1} < 90% required");
                }

                result = result with { IsSuccess = result.ValidationErrors.Count == 0 };
            }

            _logger.LogInformation("Cold start completed for data room {DataRoomId}. Success: {IsSuccess}", 
                dataRoomId, result.IsSuccess);

            return Result<ColdStartResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cold start failed for data room {DataRoomId}", dataRoomId);
            
            result = result with
            {
                IsSuccess = false,
                ProcessingTime = DateTime.UtcNow - startTime,
                ValidationErrors = result.ValidationErrors.Concat(new[] { $"Cold start execution failed: {ex.Message}" }).ToList()
            };

            return Result<ColdStartResult>.Success(result);
        }
    }

    private string ComputeContentHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}