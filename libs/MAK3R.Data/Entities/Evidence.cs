using MAK3R.Core;

namespace MAK3R.Data.Entities;

/// <summary>
/// DigitalTwin2 Evidence - links facts to source documents with coordinates
/// Every piece of extracted data maintains full traceability to original source
/// </summary>
public class Evidence
{
    public string Id { get; private set; }
    public string SourceType { get; private set; }
    public string SourceId { get; private set; }
    public string SourcePath { get; private set; }
    public int? PageNumber { get; private set; }
    public string? BoundingBox { get; private set; }
    public string? TextSpan { get; private set; }
    public double ExtractionConfidence { get; private set; }
    public string ExtractionMethod { get; private set; }
    public string DataRoomId { get; private set; }
    public string CorrelationId { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    protected Evidence() 
    { 
        Metadata = new Dictionary<string, object>();
    }

    public Evidence(
        string sourceType, 
        string sourceId, 
        string sourcePath, 
        double extractionConfidence, 
        string extractionMethod,
        string dataRoomId,
        string correlationId) : this()
    {
        Guard.NotNullOrWhiteSpace(sourceType);
        Guard.NotNullOrWhiteSpace(sourceId);
        Guard.NotNullOrWhiteSpace(sourcePath);
        Guard.NotNull(extractionConfidence);
        Guard.NotNullOrWhiteSpace(extractionMethod);
        Guard.NotNullOrWhiteSpace(dataRoomId);
        Guard.NotNullOrWhiteSpace(correlationId);

        Id = UlidGenerator.NewId();
        SourceType = sourceType;
        SourceId = sourceId;
        SourcePath = sourcePath;
        ExtractionConfidence = extractionConfidence;
        ExtractionMethod = extractionMethod;
        DataRoomId = dataRoomId;
        CorrelationId = correlationId;
        CreatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Add document coordinates for PDFs
    /// </summary>
    public void SetDocumentCoordinates(int pageNumber, string boundingBox, string? textSpan = null)
    {
        Guard.GreaterThan(pageNumber, 0);
        Guard.NotNullOrWhiteSpace(boundingBox);

        PageNumber = pageNumber;
        BoundingBox = boundingBox;
        TextSpan = textSpan;
    }

    /// <summary>
    /// Add extraction metadata
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        Guard.NotNullOrWhiteSpace(key);
        Metadata[key] = value;
    }

    /// <summary>
    /// Get typed metadata value
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (!Metadata.TryGetValue(key, out var value) || value == null)
            return default;

        if (value is T directValue)
            return directValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}