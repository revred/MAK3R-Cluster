namespace MAK3R.Ingestion.Models;

/// <summary>
/// Document classification result with type and metadata
/// </summary>
public record DocumentClassificationResult
{
    public DocumentType DocumentType { get; init; } = DocumentType.Unknown;
    public string MimeType { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public List<string> DetectedFields { get; init; } = new();
}

/// <summary>
/// Document type enumeration for DigitalTwin2 processing
/// </summary>
public enum DocumentType
{
    Unknown = 0,
    Invoice = 1,
    PurchaseOrder = 2,
    JobCard = 3,
    VendorMaster = 4,
    ProductCatalog = 5,
    BillOfMaterials = 6,
    QualityCertificate = 7,
    DeliveryNote = 8,
    Pdf = 9,
    Csv = 10,
    Excel = 11,
    Image = 12,
    Database = 13
}