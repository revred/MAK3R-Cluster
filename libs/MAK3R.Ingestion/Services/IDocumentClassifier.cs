using MAK3R.Core;
using MAK3R.Ingestion.Models;

namespace MAK3R.Ingestion.Services;

/// <summary>
/// DigitalTwin2 Document Classifier - identifies document types for targeted extraction
/// Enables specialized extractors for invoices, POs, job cards, vendor masters, etc.
/// </summary>
public interface IDocumentClassifier
{
    /// <summary>
    /// Classify document type from file content and metadata
    /// </summary>
    Task<Result<DocumentClassificationResult>> ClassifyAsync(Stream documentStream, string fileName, string mimeType, CancellationToken ct = default);

    /// <summary>
    /// Get supported document types and their confidence thresholds
    /// </summary>
    Task<Result<List<SupportedDocumentType>>> GetSupportedTypesAsync(CancellationToken ct = default);
}

/// <summary>
/// Document classification result with confidence scoring
/// </summary>
public record DocumentClassification
{
    public string DocumentType { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public List<string> DetectedFields { get; init; } = new();
    
    /// <summary>
    /// Common document types for manufacturing businesses
    /// </summary>
    public static class Types
    {
        public const string Invoice = "invoice";
        public const string PurchaseOrder = "purchase_order";
        public const string JobCard = "job_card";
        public const string VendorMaster = "vendor_master";
        public const string ProductCatalog = "product_catalog";
        public const string BillOfMaterials = "bill_of_materials";
        public const string QualityCertificate = "quality_certificate";
        public const string DeliveryNote = "delivery_note";
        public const string Unknown = "unknown";
    }
}

/// <summary>
/// Supported document type configuration
/// </summary>
public record SupportedDocumentType
{
    public string Type { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public double MinConfidence { get; init; } = 0.7;
    public List<string> RequiredFields { get; init; } = new();
    public List<string> OptionalFields { get; init; } = new();
    public Dictionary<string, object> ExtractionHints { get; init; } = new();
}