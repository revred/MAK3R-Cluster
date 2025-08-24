using MAK3R.Core;
using MAK3R.Data;
using MAK3R.Ingestion.Services;
using Microsoft.Extensions.Logging;

namespace MAK3R.Ingestion;

/// <summary>
/// DigitalTwin2 Ingestion Pipeline - orchestrates the complete file→facts→storage flow
/// Coordinates classification, extraction, mapping and storage with full audit trails
/// </summary>
public class IngestionPipeline : IIngestionPipeline
{
    private readonly IDocumentClassifier _classifier;
    private readonly IEnumerable<IDocumentExtractor> _extractors;
    private readonly IEnumerable<IFactMapper> _mappers;
    private readonly IKnowledgeGraphRepository _repository;
    private readonly ILogger<IngestionPipeline> _logger;

    private readonly PipelineStatus _status;

    public IngestionPipeline(
        IDocumentClassifier classifier,
        IEnumerable<IDocumentExtractor> extractors,
        IEnumerable<IFactMapper> mappers,
        IKnowledgeGraphRepository repository,
        ILogger<IngestionPipeline> logger)
    {
        _classifier = Guard.NotNull(classifier);
        _extractors = Guard.NotNull(extractors);
        _mappers = Guard.NotNull(mappers);
        _repository = Guard.NotNull(repository);
        _logger = Guard.NotNull(logger);

        _status = new PipelineStatus
        {
            IsHealthy = true,
            DocumentsProcessedToday = 0,
            AverageProcessingTime = 0.0,
            SuccessRate = 1.0,
            ActiveExtractors = _extractors.Select(e => e.GetInfo().Name).ToList(),
            ActiveMappers = _mappers.Select(m => m.GetInfo().Name).ToList(),
            LastProcessed = DateTime.UtcNow
        };
    }

    public async Task<Result<PipelineResult>> ProcessDocumentAsync(
        Stream documentStream,
        string fileName,
        string mimeType,
        string dataRoomId,
        string correlationId,
        PipelineOptions? options = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var processingId = UlidGenerator.NewId();
        options ??= new PipelineOptions();

        _logger.LogInformation("Starting document processing: {FileName} ({ProcessingId})", fileName, processingId);

        try
        {
            var result = new PipelineResult
            {
                DocumentId = UlidGenerator.NewId(),
                ProcessingId = processingId,
                CompletedStage = PipelineStage.Started
            };

            // Stage 1: Classification
            if (!options.SkipClassification)
            {
                _logger.LogDebug("Classifying document: {FileName}", fileName);
                
                var classificationResult = await _classifier.ClassifyAsync(documentStream, fileName, mimeType, ct);
                if (!classificationResult.IsSuccess)
                {
                    return Result<PipelineResult>.Failure($"Classification failed: {classificationResult.Error}", classificationResult.Exception);
                }

                result = result with 
                { 
                    Classification = classificationResult.Value,
                    CompletedStage = PipelineStage.Classification
                };

                _logger.LogInformation("Document classified as: {DocumentType} (confidence: {Confidence:P2})", 
                    result.Classification?.DocumentType, result.Classification?.Confidence);
            }
            else if (!string.IsNullOrEmpty(options.ForceDocumentType))
            {
                result = result with 
                { 
                    Classification = new DocumentClassification 
                    { 
                        DocumentType = options.ForceDocumentType, 
                        Confidence = 1.0 
                    }
                };
            }

            if (result.Classification == null)
            {
                return Result<PipelineResult>.Failure("No document classification available");
            }

            // Stage 2: Extraction
            var extractor = _extractors.FirstOrDefault(e => e.CanExtract(result.Classification.DocumentType));
            if (extractor == null)
            {
                return Result<PipelineResult>.Failure($"No extractor found for document type: {result.Classification.DocumentType}");
            }

            _logger.LogDebug("Extracting facts using: {ExtractorName}", extractor.GetInfo().Name);
            
            var extractionResult = await extractor.ExtractAsync(
                documentStream, fileName, result.Classification, dataRoomId, correlationId, ct);
            
            if (!extractionResult.IsSuccess)
            {
                return Result<PipelineResult>.Failure($"Extraction failed: {extractionResult.Error}", extractionResult.Exception);
            }

            result = result with 
            { 
                Extraction = extractionResult.Value,
                CompletedStage = PipelineStage.Extraction
            };

            _logger.LogInformation("Extracted {FactCount} facts from document", result.Extraction.Facts.Count);

            // Stage 3: Mapping
            var mapper = _mappers.FirstOrDefault(m => m.CanMap(result.Classification.DocumentType));
            if (mapper == null)
            {
                return Result<PipelineResult>.Failure($"No mapper found for document type: {result.Classification.DocumentType}");
            }

            _logger.LogDebug("Mapping facts using: {MapperName}", mapper.GetInfo().Name);
            
            var mappingResult = await mapper.MapAsync(result.Extraction, dataRoomId, correlationId, ct);
            if (!mappingResult.IsSuccess)
            {
                return Result<PipelineResult>.Failure($"Mapping failed: {mappingResult.Error}", mappingResult.Exception);
            }

            result = result with 
            { 
                Mapping = mappingResult.Value,
                CompletedStage = PipelineStage.Mapping
            };

            _logger.LogInformation("Mapped to {EntityCount} entities and {RelationCount} relations", 
                result.Mapping.Entities.Count, result.Mapping.Relations.Count);

            // Stage 4: Storage
            var storageResult = await StoreKnowledgeGraphAsync(result.Mapping, result.Extraction, ct);
            if (!storageResult.IsSuccess)
            {
                return Result<PipelineResult>.Failure($"Storage failed: {storageResult.Error}", storageResult.Exception);
            }

            result = result with 
            { 
                EntitiesCreated = result.Mapping.Entities.Count,
                RelationsCreated = result.Mapping.Relations.Count,
                EvidenceRecordsCreated = result.Extraction.Evidence.Count,
                CompletedStage = PipelineStage.Storage
            };

            // Final stage: Complete
            var processingTime = DateTime.UtcNow - startTime;
            result = result with 
            { 
                ProcessingTime = processingTime,
                OverallConfidence = result.Extraction?.OverallConfidence ?? 0.0,
                CompletedStage = PipelineStage.Completed,
                Statistics = new Dictionary<string, object>
                {
                    ["ProcessingTimeMs"] = processingTime.TotalMilliseconds,
                    ["ExtractorUsed"] = extractor.GetInfo().Name,
                    ["MapperUsed"] = mapper.GetInfo().Name,
                    ["DocumentSize"] = documentStream.Length
                }
            };

            _logger.LogInformation("Document processing completed: {FileName} in {ProcessingTime:c}", fileName, processingTime);

            return Result<PipelineResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline processing failed for document: {FileName}", fileName);
            
            var errorResult = new PipelineResult
            {
                DocumentId = UlidGenerator.NewId(),
                ProcessingId = processingId,
                CompletedStage = PipelineStage.Failed,
                ProcessingTime = DateTime.UtcNow - startTime,
                Errors = [ex.Message]
            };

            return Result<PipelineResult>.Success(errorResult);
        }
    }

    public async Task<Result<BatchPipelineResult>> ProcessBatchAsync(
        IEnumerable<DocumentInput> documents,
        string dataRoomId,
        string correlationId,
        PipelineOptions? options = null,
        CancellationToken ct = default)
    {
        var batchId = UlidGenerator.NewId();
        var startTime = DateTime.UtcNow;
        var documentList = documents.ToList();
        var results = new List<PipelineResult>();

        _logger.LogInformation("Starting batch processing: {DocumentCount} documents ({BatchId})", documentList.Count, batchId);

        try
        {
            if (options?.EnableParallelProcessing == true)
            {
                var semaphore = new SemaphoreSlim(options.MaxParallelism);
                var tasks = documentList.Select(async doc =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        return await ProcessDocumentAsync(
                            doc.Stream, doc.FileName, doc.MimeType, dataRoomId, correlationId, options, ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                var batchResults = await Task.WhenAll(tasks);
                results.AddRange(batchResults.Where(r => r.IsSuccess).Select(r => r.Value!));
            }
            else
            {
                foreach (var doc in documentList)
                {
                    var result = await ProcessDocumentAsync(
                        doc.Stream, doc.FileName, doc.MimeType, dataRoomId, correlationId, options, ct);
                    
                    if (result.IsSuccess)
                    {
                        results.Add(result.Value!);
                    }
                }
            }

            var batchResult = new BatchPipelineResult
            {
                BatchId = batchId,
                TotalDocuments = documentList.Count,
                SuccessfulDocuments = results.Count(r => r.CompletedStage == PipelineStage.Completed),
                FailedDocuments = results.Count(r => r.CompletedStage == PipelineStage.Failed),
                TotalProcessingTime = DateTime.UtcNow - startTime,
                Results = results,
                BatchStatistics = new Dictionary<string, object>
                {
                    ["AverageProcessingTime"] = results.Count > 0 ? results.Average(r => r.ProcessingTime.TotalMilliseconds) : 0,
                    ["TotalEntitiesCreated"] = results.Sum(r => r.EntitiesCreated),
                    ["TotalRelationsCreated"] = results.Sum(r => r.RelationsCreated),
                    ["AverageConfidence"] = results.Count > 0 ? results.Average(r => r.OverallConfidence) : 0
                }
            };

            _logger.LogInformation("Batch processing completed: {SuccessfulDocuments}/{TotalDocuments} successful", 
                batchResult.SuccessfulDocuments, batchResult.TotalDocuments);

            return Result<BatchPipelineResult>.Success(batchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch processing failed: {BatchId}", batchId);
            return Result<BatchPipelineResult>.Failure($"Batch processing failed: {ex.Message}", ex);
        }
    }

    public async Task<Result<PipelineStatus>> GetStatusAsync(CancellationToken ct = default)
    {
        // TODO: Implement real status tracking with metrics storage
        return Result<PipelineStatus>.Success(_status);
    }

    private async Task<Result> StoreKnowledgeGraphAsync(MappingResult mapping, ExtractionResult extraction, CancellationToken ct)
    {
        try
        {
            // Store evidence first
            foreach (var evidence in extraction.Evidence)
            {
                var evidenceResult = await _repository.CreateEvidenceAsync(
                    evidence.SourceType, evidence.SourceId, evidence.SourcePath,
                    evidence.ExtractionConfidence, evidence.ExtractionMethod,
                    evidence.DataRoomId, evidence.CorrelationId, ct);

                if (!evidenceResult.IsSuccess)
                {
                    return Result.Failure($"Failed to store evidence: {evidenceResult.Error}");
                }
            }

            // Store entities
            foreach (var entity in mapping.Entities)
            {
                var entityResult = await _repository.UpdateEntityAsync(entity, ct);
                if (!entityResult.IsSuccess)
                {
                    return Result.Failure($"Failed to store entity: {entityResult.Error}");
                }
            }

            // Store relations
            foreach (var relation in mapping.Relations)
            {
                var relationResult = await _repository.CreateRelationAsync(
                    relation.SourceEntityId, relation.TargetEntityId, 
                    relation.RelationshipType, relation.Confidence, relation.EvidenceId, ct);

                if (!relationResult.IsSuccess)
                {
                    return Result.Failure($"Failed to store relation: {relationResult.Error}");
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Knowledge graph storage failed: {ex.Message}", ex);
        }
    }
}