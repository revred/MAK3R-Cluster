using MAK3R.Core;
using MAK3R.Ingestion.Models;
using MAK3R.Ingestion.Services;

namespace MAK3R.Ingestion.Classifiers;

/// <summary>
/// DigitalTwin2 Basic Document Classifier - simple rule-based document type detection
/// Uses filename patterns and content heuristics for classification
/// </summary>
public class BasicDocumentClassifier : IDocumentClassifier
{
    private readonly List<SupportedDocumentType> _supportedTypes;

    public BasicDocumentClassifier()
    {
        _supportedTypes = [
            new SupportedDocumentType
            {
                Type = DocumentClassification.Types.Invoice,
                DisplayName = "Invoice",
                MinConfidence = 0.7,
                RequiredFields = ["InvoiceNumber", "Amount", "Date"],
                OptionalFields = ["VendorName", "CustomerName", "LineItems"],
                ExtractionHints = new Dictionary<string, object>
                {
                    ["KeywordPatterns"] = new[] { "invoice", "bill", "amount due", "total", "inv#", "invoice#" },
                    ["NumberPatterns"] = new[] { @"inv-\d+", @"invoice\s*#?\s*\d+", @"\d{4}-\d{3,4}" }
                }
            },
            
            new SupportedDocumentType
            {
                Type = DocumentClassification.Types.PurchaseOrder,
                DisplayName = "Purchase Order",
                MinConfidence = 0.7,
                RequiredFields = ["PONumber", "Date"],
                OptionalFields = ["VendorName", "Items", "DeliveryDate"],
                ExtractionHints = new Dictionary<string, object>
                {
                    ["KeywordPatterns"] = new[] { "purchase order", "po#", "po number", "order date" },
                    ["NumberPatterns"] = new[] { @"po-\d+", @"purchase\s*order\s*#?\s*\d+", @"\d{4}-\d{3,4}" }
                }
            },
            
            new SupportedDocumentType
            {
                Type = DocumentClassification.Types.JobCard,
                DisplayName = "Job Card",
                MinConfidence = 0.7,
                RequiredFields = ["JobNumber", "Date"],
                OptionalFields = ["Machine", "Operator", "StartTime", "EndTime"],
                ExtractionHints = new Dictionary<string, object>
                {
                    ["KeywordPatterns"] = new[] { "job card", "work order", "job#", "job number", "machine" },
                    ["NumberPatterns"] = new[] { @"job-\d+", @"wo-\d+", @"job\s*#?\s*\d+" }
                }
            },
            
            new SupportedDocumentType
            {
                Type = DocumentClassification.Types.VendorMaster,
                DisplayName = "Vendor Master",
                MinConfidence = 0.7,
                RequiredFields = ["VendorName", "VendorCode"],
                OptionalFields = ["Address", "Contact", "TaxId"],
                ExtractionHints = new Dictionary<string, object>
                {
                    ["KeywordPatterns"] = new[] { "vendor", "supplier", "vendor code", "vendor master", "supplier details" }
                }
            }
        ];
    }

    public async Task<Result<DocumentClassificationResult>> ClassifyAsync(
        Stream documentStream,
        string fileName,
        string mimeType,
        CancellationToken ct = default)
    {
        try
        {
            Guard.NotNull(documentStream);
            Guard.NotNullOrWhiteSpace(fileName);
            Guard.NotNullOrWhiteSpace(mimeType);

            // First, classify by MIME type for structural formats
            var docType = ClassifyByMimeType(mimeType);
            
            if (docType != DocumentType.Unknown)
            {
                // Direct MIME type classification
                return Result<DocumentClassificationResult>.Success(new DocumentClassificationResult
                {
                    DocumentType = docType,
                    MimeType = mimeType,
                    Confidence = 0.95, // High confidence for MIME type matches
                    Metadata = new Dictionary<string, object>
                    {
                        ["FileName"] = fileName,
                        ["MimeType"] = mimeType,
                        ["FileSize"] = documentStream.Length,
                        ["ClassificationMethod"] = "mime_type"
                    }
                });
            }

            // For generic types (like PDFs), use content classification
            var classification = new DocumentClassificationResult
            {
                DocumentType = DocumentType.Unknown,
                MimeType = mimeType,
                Confidence = 0.0,
                Metadata = new Dictionary<string, object>
                {
                    ["FileName"] = fileName,
                    ["MimeType"] = mimeType,
                    ["FileSize"] = documentStream.Length,
                    ["ClassificationMethod"] = "content_analysis"
                }
            };

            // Classification based on filename patterns
            var filenameConfidence = ClassifyByFilename(fileName);
            
            // Classification based on content (if possible to read)
            var contentConfidence = await ClassifyByContentAsync(documentStream, mimeType, ct);
            
            // Combine scores with weighted average
            var combinedResults = CombineClassificationResults(filenameConfidence, contentConfidence);
            
            if (combinedResults.Any())
            {
                var best = combinedResults.OrderByDescending(r => r.confidence).First();
                var supportedType = _supportedTypes.First(t => t.Type == best.type);
                
                if (best.confidence >= supportedType.MinConfidence)
                {
                    // Map to DocumentType enum
                    var docType = best.type switch
                    {
                        DocumentClassification.Types.Invoice => DocumentType.Invoice,
                        DocumentClassification.Types.PurchaseOrder => DocumentType.PurchaseOrder,
                        DocumentClassification.Types.JobCard => DocumentType.JobCard,
                        DocumentClassification.Types.VendorMaster => DocumentType.VendorMaster,
                        _ => DocumentType.Unknown
                    };

                    classification = classification with
                    {
                        DocumentType = docType,
                        Confidence = best.confidence,
                        DetectedFields = GetExpectedFields(best.type)
                    };
                }
            }

            return Result<DocumentClassificationResult>.Success(classification);
        }
        catch (Exception ex)
        {
            return Result<DocumentClassificationResult>.Failure($"Classification failed: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<SupportedDocumentType>>> GetSupportedTypesAsync(CancellationToken ct = default)
    {
        return Result<List<SupportedDocumentType>>.Success(_supportedTypes);
    }

    private Dictionary<string, double> ClassifyByFilename(string fileName)
    {
        var results = new Dictionary<string, double>();
        var lowerFileName = fileName.ToLowerInvariant();

        // Invoice patterns
        if (ContainsAny(lowerFileName, ["invoice", "inv", "bill"]))
        {
            results[DocumentClassification.Types.Invoice] = 0.7;
        }

        // Purchase Order patterns  
        if (ContainsAny(lowerFileName, ["purchase", "po", "order"]))
        {
            results[DocumentClassification.Types.PurchaseOrder] = 0.7;
        }

        // Job Card patterns
        if (ContainsAny(lowerFileName, ["job", "work", "card", "wo"]))
        {
            results[DocumentClassification.Types.JobCard] = 0.7;
        }

        // Vendor patterns
        if (ContainsAny(lowerFileName, ["vendor", "supplier", "master"]))
        {
            results[DocumentClassification.Types.VendorMaster] = 0.7;
        }

        return results;
    }

    private async Task<Dictionary<string, double>> ClassifyByContentAsync(Stream stream, string mimeType, CancellationToken ct)
    {
        var results = new Dictionary<string, double>();

        try
        {
            // For PDFs, we'd use a PDF library to extract text
            // For text files, we can read directly
            // This is a simplified implementation
            
            if (mimeType == "application/pdf")
            {
                // TODO: Integrate PDF text extraction library
                // For now, assume moderate confidence based on being a PDF
                results[DocumentClassification.Types.Invoice] = 0.3;
                results[DocumentClassification.Types.PurchaseOrder] = 0.3;
                results[DocumentClassification.Types.JobCard] = 0.3;
            }
            else if (mimeType.StartsWith("text/"))
            {
                // For text files, we can sample content
                using var reader = new StreamReader(stream, leaveOpen: true);
                var buffer = new char[1000];
                var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                var content = new string(buffer, 0, charsRead).ToLowerInvariant();
                
                stream.Seek(0, SeekOrigin.Begin); // Reset stream position
                
                results = ClassifyTextContent(content);
            }
        }
        catch
        {
            // If content reading fails, return empty results
        }

        return results;
    }

    private Dictionary<string, double> ClassifyTextContent(string content)
    {
        var results = new Dictionary<string, double>();

        foreach (var docType in _supportedTypes)
        {
            if (docType.ExtractionHints.TryGetValue("KeywordPatterns", out var keywordsObj) && 
                keywordsObj is string[] keywords)
            {
                var matches = keywords.Count(keyword => content.Contains(keyword));
                var confidence = Math.Min(0.9, (double)matches / keywords.Length);
                
                if (confidence > 0.2) // Minimum threshold
                {
                    results[docType.Type] = confidence;
                }
            }
        }

        return results;
    }

    private List<(string type, double confidence)> CombineClassificationResults(
        Dictionary<string, double> filenameResults, 
        Dictionary<string, double> contentResults)
    {
        var combined = new Dictionary<string, double>();

        // Weight: 40% filename, 60% content
        foreach (var type in _supportedTypes.Select(t => t.Type))
        {
            var filenameScore = filenameResults.GetValueOrDefault(type, 0.0);
            var contentScore = contentResults.GetValueOrDefault(type, 0.0);
            
            if (filenameScore > 0 || contentScore > 0)
            {
                combined[type] = (filenameScore * 0.4) + (contentScore * 0.6);
            }
        }

        return combined.Select(kvp => (kvp.Key, kvp.Value)).ToList();
    }

    private List<string> GetExpectedFields(string documentType)
    {
        var docType = _supportedTypes.FirstOrDefault(t => t.Type == documentType);
        return docType?.RequiredFields ?? [];
    }

    private DocumentType ClassifyByMimeType(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" or "image/png" => DocumentType.Image,
            "application/x-sqlite3" or "application/vnd.sqlite3" => DocumentType.Database,
            "text/csv" => DocumentType.Csv,
            "application/vnd.ms-excel" or 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => DocumentType.Excel,
            "application/pdf" => DocumentType.Pdf,
            _ => DocumentType.Unknown
        };
    }

    private static bool ContainsAny(string text, string[] patterns)
    {
        return patterns.Any(pattern => text.Contains(pattern));
    }
}