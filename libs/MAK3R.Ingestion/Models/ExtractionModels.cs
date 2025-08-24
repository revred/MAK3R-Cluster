using MAK3R.Core.Models;

namespace MAK3R.Ingestion.Models;

/// <summary>
/// Document extraction result from any extractor
/// </summary>
public record DocumentExtractionResult
{
    public string ExtractionId { get; init; } = string.Empty;
    public DocumentType DocumentType { get; init; }
    public string ExtractorType { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public List<ExtractedFact> Facts { get; init; } = new();
    public List<EvidenceItem> Evidence { get; init; } = new();
    public long ProcessingTimeMs { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Extracted fact with confidence and evidence
/// </summary>
public record ExtractedFact
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Attribute { get; init; } = string.Empty;
    public object Value { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string? EvidenceId { get; init; }
    public string Context { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTime ExtractedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Evidence item linking facts to source content
/// </summary>
public record EvidenceItem
{
    public string Id { get; init; } = string.Empty;
    public EvidenceSourceType SourceType { get; init; }
    public string SourcePath { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Evidence source type enumeration
/// </summary>
public enum EvidenceSourceType
{
    PdfPage = 1,
    CsvRow = 2,
    ExcelCell = 3,
    ImageFile = 4,
    OcrText = 5,
    DatabaseTable = 6,
    DatabaseFile = 7,
    TextFile = 8,
    JsonField = 9,
    XmlNode = 10
}

/// <summary>
/// Document extraction options
/// </summary>
public record DocumentExtractionOptions
{
    public double MinConfidenceThreshold { get; init; } = 0.6;
    public bool StoreIntermediateResults { get; init; } = true;
    public bool EnableParallelProcessing { get; init; } = false;
    public Dictionary<string, object> ExtractorSpecificOptions { get; init; } = new();
}

/// <summary>
/// Pipeline-level extraction result (covers all extractors)
/// </summary>
public record ExtractionResult
{
    public bool IsSuccess { get; init; }
    public List<DocumentExtractionResult> ExtractorResults { get; init; } = new();
    public TimeSpan TotalTime { get; init; }
    public int TotalFactsExtracted { get; init; }
    public int TotalEvidenceItems { get; init; }
    public double AverageConfidence { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Fact mapping result
/// </summary>
public record MappingResult
{
    public bool IsSuccess { get; init; }
    public int FactsMapped { get; init; }
    public int EntitiesCreated { get; init; }
    public int RelationsCreated { get; init; }
    public TimeSpan MappingTime { get; init; }
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public Dictionary<string, object> Statistics { get; init; } = new();
}