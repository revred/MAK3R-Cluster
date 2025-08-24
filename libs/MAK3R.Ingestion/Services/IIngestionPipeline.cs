using MAK3R.Core;
using MAK3R.Ingestion.Models;

namespace MAK3R.Ingestion.Services;

/// <summary>
/// DigitalTwin2 Ingestion Pipeline - orchestrates document processing flow
/// Coordinates classification → extraction → mapping → storage with audit trails
/// </summary>
public interface IIngestionPipeline
{
    /// <summary>
    /// Process document through complete ingestion pipeline
    /// </summary>
    Task<Result<PipelineResult>> ProcessDocumentAsync(
        Stream documentStream,
        string fileName,
        string mimeType,
        string dataRoomId,
        string correlationId,
        PipelineOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Process batch of documents
    /// </summary>
    Task<Result<BatchPipelineResult>> ProcessBatchAsync(
        IEnumerable<DocumentInput> documents,
        string dataRoomId,
        string correlationId,
        PipelineOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get pipeline status and statistics
    /// </summary>
    Task<Result<PipelineStatus>> GetStatusAsync(CancellationToken ct = default);
}

/// <summary>
/// Document input for batch processing
/// </summary>
public record DocumentInput
{
    public string Id { get; init; } = string.Empty;
    public Stream Stream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Pipeline processing options
/// </summary>
public record PipelineOptions
{
    public bool SkipClassification { get; init; } = false;
    public string? ForceDocumentType { get; init; }
    public double MinConfidenceThreshold { get; init; } = 0.6;
    public bool StoreIntermediateResults { get; init; } = true;
    public bool EnableParallelProcessing { get; init; } = true;
    public int MaxParallelism { get; init; } = 4;
    public Dictionary<string, object> ExtractorSettings { get; init; } = new();
    public Dictionary<string, object> MapperSettings { get; init; } = new();
}

/// <summary>
/// Pipeline processing result
/// </summary>
public record PipelineResult
{
    public string DocumentId { get; init; } = string.Empty;
    public string ProcessingId { get; init; } = string.Empty;
    public PipelineStage CompletedStage { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    
    // Stage Results
    public DocumentClassificationResult? Classification { get; init; }
    public ExtractionResult? Extraction { get; init; }
    public MappingResult? Mapping { get; init; }
    
    // Storage Results
    public int EntitiesCreated { get; init; }
    public int RelationsCreated { get; init; }
    public int EvidenceRecordsCreated { get; init; }
    
    // Quality Metrics
    public double OverallConfidence { get; init; }
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public Dictionary<string, object> Statistics { get; init; } = new();
}

/// <summary>
/// Batch processing result
/// </summary>
public record BatchPipelineResult
{
    public string BatchId { get; init; } = string.Empty;
    public int TotalDocuments { get; init; }
    public int SuccessfulDocuments { get; init; }
    public int FailedDocuments { get; init; }
    public TimeSpan TotalProcessingTime { get; init; }
    public List<PipelineResult> Results { get; init; } = new();
    public Dictionary<string, object> BatchStatistics { get; init; } = new();
}

/// <summary>
/// Pipeline status and health metrics
/// </summary>
public record PipelineStatus
{
    public bool IsHealthy { get; init; }
    public int DocumentsProcessedToday { get; init; }
    public double AverageProcessingTime { get; init; }
    public double SuccessRate { get; init; }
    public Dictionary<string, int> DocumentTypeDistribution { get; init; } = new();
    public List<string> ActiveExtractors { get; init; } = new();
    public List<string> ActiveMappers { get; init; } = new();
    public DateTime LastProcessed { get; init; }
}

/// <summary>
/// Pipeline processing stages
/// </summary>
public enum PipelineStage
{
    Started,
    Classification,
    Extraction,
    Mapping,
    Storage,
    Completed,
    Failed
}