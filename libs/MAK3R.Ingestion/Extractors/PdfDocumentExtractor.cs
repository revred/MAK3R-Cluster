using MAK3R.Core;
using MAK3R.Data.Entities;
using MAK3R.Ingestion.Services;

namespace MAK3R.Ingestion.Extractors;

/// <summary>
/// DigitalTwin2 PDF Document Extractor - extracts text and structured data from PDFs
/// Uses PDF parsing libraries with coordinate-based evidence tracking
/// </summary>
public class PdfDocumentExtractor : IDocumentExtractor
{
    private readonly ExtractorInfo _info;

    public PdfDocumentExtractor()
    {
        _info = new ExtractorInfo
        {
            Name = "PDF Document Extractor",
            Version = "1.0.0",
            SupportedTypes = [
                DocumentClassification.Types.Invoice,
                DocumentClassification.Types.PurchaseOrder,
                DocumentClassification.Types.JobCard,
                DocumentClassification.Types.VendorMaster,
                DocumentClassification.Types.QualityCertificate,
                DocumentClassification.Types.DeliveryNote
            ],
            SupportedMimeTypes = ["application/pdf"],
            Configuration = new Dictionary<string, object>
            {
                ["MinConfidence"] = 0.6,
                ["ExtractTables"] = true,
                ["ExtractImages"] = false,
                ["PreserveFormatting"] = true
            }
        };
    }

    public async Task<Result<ExtractionResult>> ExtractAsync(
        Stream documentStream,
        string fileName,
        DocumentClassification classification,
        string dataRoomId,
        string correlationId,
        CancellationToken ct = default)
    {
        try
        {
            Guard.NotNull(documentStream);
            Guard.NotNullOrWhiteSpace(fileName);
            Guard.NotNull(classification);
            Guard.NotNullOrWhiteSpace(dataRoomId);
            Guard.NotNullOrWhiteSpace(correlationId);

            var documentId = UlidGenerator.NewId();
            var facts = new List<ExtractedFact>();
            var evidence = new List<Evidence>();
            var warnings = new List<string>();

            // TODO: Integrate with PDF parsing library (e.g., PdfPig, iTextSharp)
            // For now, simulate extraction based on document type
            
            switch (classification.DocumentType)
            {
                case DocumentClassification.Types.Invoice:
                    await ExtractInvoiceFactsAsync(documentStream, fileName, documentId, dataRoomId, correlationId, facts, evidence, ct);
                    break;
                    
                case DocumentClassification.Types.PurchaseOrder:
                    await ExtractPurchaseOrderFactsAsync(documentStream, fileName, documentId, dataRoomId, correlationId, facts, evidence, ct);
                    break;
                    
                default:
                    await ExtractGenericFactsAsync(documentStream, fileName, documentId, dataRoomId, correlationId, facts, evidence, ct);
                    warnings.Add($"Generic extraction used for document type: {classification.DocumentType}");
                    break;
            }

            var result = new ExtractionResult
            {
                DocumentId = documentId,
                DocumentType = classification.DocumentType,
                Facts = facts,
                Evidence = evidence,
                OverallConfidence = CalculateOverallConfidence(facts),
                Metadata = new Dictionary<string, object>
                {
                    ["FileName"] = fileName,
                    ["ProcessingTime"] = DateTime.UtcNow,
                    ["ExtractorVersion"] = _info.Version,
                    ["PageCount"] = 1, // TODO: Get actual page count
                    ["FileSize"] = documentStream.Length
                },
                Warnings = warnings
            };

            return Result<ExtractionResult>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<ExtractionResult>.Failure($"PDF extraction failed: {ex.Message}", ex);
        }
    }

    public bool CanExtract(string documentType) => _info.SupportedTypes.Contains(documentType);

    public ExtractorInfo GetInfo() => _info;

    private async Task ExtractInvoiceFactsAsync(
        Stream stream, string fileName, string documentId, string dataRoomId, string correlationId,
        List<ExtractedFact> facts, List<Evidence> evidence, CancellationToken ct)
    {
        // Simulate invoice data extraction
        var invoiceEvidence = new Evidence("PDF", documentId, fileName, 0.9, "PDF_TEXT_EXTRACTION", dataRoomId, correlationId);
        invoiceEvidence.SetDocumentCoordinates(1, "10,10,200,30", "Invoice #INV-2024-001");
        evidence.Add(invoiceEvidence);

        facts.Add(new ExtractedFact
        {
            FactId = UlidGenerator.NewId(),
            EntityType = ExtractedFact.EntityTypes.Invoice,
            AttributeName = "InvoiceNumber",
            Value = "INV-2024-001",
            Confidence = 0.95,
            EvidenceId = invoiceEvidence.Id
        });

        facts.Add(new ExtractedFact
        {
            FactId = UlidGenerator.NewId(),
            EntityType = ExtractedFact.EntityTypes.Invoice,
            AttributeName = "Amount",
            Value = 1250.00m,
            Confidence = 0.88,
            EvidenceId = invoiceEvidence.Id
        });

        await Task.CompletedTask;
    }

    private async Task ExtractPurchaseOrderFactsAsync(
        Stream stream, string fileName, string documentId, string dataRoomId, string correlationId,
        List<ExtractedFact> facts, List<Evidence> evidence, CancellationToken ct)
    {
        // Simulate purchase order data extraction
        var poEvidence = new Evidence("PDF", documentId, fileName, 0.85, "PDF_TEXT_EXTRACTION", dataRoomId, correlationId);
        poEvidence.SetDocumentCoordinates(1, "15,50,180,70", "PO #PO-2024-045");
        evidence.Add(poEvidence);

        facts.Add(new ExtractedFact
        {
            FactId = UlidGenerator.NewId(),
            EntityType = ExtractedFact.EntityTypes.PurchaseOrder,
            AttributeName = "PONumber",
            Value = "PO-2024-045",
            Confidence = 0.92,
            EvidenceId = poEvidence.Id
        });

        await Task.CompletedTask;
    }

    private async Task ExtractGenericFactsAsync(
        Stream stream, string fileName, string documentId, string dataRoomId, string correlationId,
        List<ExtractedFact> facts, List<Evidence> evidence, CancellationToken ct)
    {
        // Generic text extraction
        var genericEvidence = new Evidence("PDF", documentId, fileName, 0.7, "PDF_GENERIC_EXTRACTION", dataRoomId, correlationId);
        evidence.Add(genericEvidence);

        facts.Add(new ExtractedFact
        {
            FactId = UlidGenerator.NewId(),
            EntityType = "document",
            AttributeName = "FileName",
            Value = fileName,
            Confidence = 1.0,
            EvidenceId = genericEvidence.Id
        });

        await Task.CompletedTask;
    }

    private static double CalculateOverallConfidence(List<ExtractedFact> facts)
    {
        if (facts.Count == 0) return 0.0;
        return facts.Average(f => f.Confidence);
    }
}