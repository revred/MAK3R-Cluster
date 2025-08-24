using MAK3R.Core;
using MAK3R.Ingestion.Models;

namespace MAK3R.Ingestion.Services;

/// <summary>
/// DigitalTwin2 Document Extractor - extracts structured facts from classified documents
/// Pluggable extractors for different document types with evidence tracking
/// </summary>
public interface IDocumentExtractor
{
    /// <summary>
    /// Extract facts from document with evidence coordinates
    /// </summary>
    Task<Result<DocumentExtractionResult>> ExtractAsync(
        Stream documentStream, 
        string fileName, 
        DocumentClassificationResult classification,
        string dataRoomId,
        string correlationId,
        DocumentExtractionOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Check if this extractor can handle the document type
    /// </summary>
    Task<bool> CanExtractAsync(DocumentClassificationResult classification, CancellationToken ct = default);

    /// <summary>
    /// Get extractor metadata and capabilities
    /// </summary>
    ExtractorInfo GetInfo();

    /// <summary>
    /// Extractor type name for identification
    /// </summary>
    string ExtractorType { get; }
}

/// <summary>
/// Extractor metadata and capabilities
/// </summary>
public record ExtractorInfo
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public List<string> SupportedTypes { get; init; } = new();
    public List<string> SupportedMimeTypes { get; init; } = new();
    public Dictionary<string, object> Configuration { get; init; } = new();
}