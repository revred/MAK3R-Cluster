using MAK3R.Core.Models;
using MAK3R.Ingestion.Models;
using MAK3R.Ingestion.Services;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace MAK3R.Ingestion.Extractors;

/// <summary>
/// Document extractor for image files (JPEG, PNG) with OCR capabilities
/// </summary>
public class ImageDocumentExtractor : IDocumentExtractor
{
    private readonly IOcrService? _ocrService;
    private static readonly string[] SupportedImageTypes = { "image/jpeg", "image/png", "image/jpg" };

    public string ExtractorType => "ImageExtractor";

    public ImageDocumentExtractor(IOcrService? ocrService = null)
    {
        _ocrService = ocrService;
    }

    public Task<bool> CanExtractAsync(DocumentClassificationResult classification, CancellationToken ct = default)
    {
        return Task.FromResult(classification.DocumentType == DocumentType.Image &&
                              SupportedImageTypes.Contains(classification.MimeType.ToLowerInvariant()));
    }

    public async Task<Result<DocumentExtractionResult>> ExtractAsync(
        Stream documentStream,
        string fileName,
        DocumentClassificationResult classification,
        string dataRoomId,
        string correlationId,
        DocumentExtractionOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            var extractionId = UlidGenerator.NewId();
            var extractionOptions = options ?? new DocumentExtractionOptions();
            
            var extractedFacts = new List<ExtractedFact>();
            var evidenceItems = new List<EvidenceItem>();
            
            // Create evidence for the image file
            var imageEvidenceId = UlidGenerator.NewId();
            documentStream.Position = 0;
            var imageBytes = new byte[documentStream.Length];
            await documentStream.ReadAsync(imageBytes, 0, imageBytes.Length, ct);
            
            var imageEvidence = new EvidenceItem
            {
                Id = imageEvidenceId,
                SourceType = EvidenceSourceType.ImageFile,
                SourcePath = fileName,
                MimeType = classification.MimeType,
                Content = Convert.ToBase64String(imageBytes),
                ContentHash = ComputeContentHash(imageBytes),
                Metadata = new Dictionary<string, object>
                {
                    ["originalFileName"] = fileName,
                    ["fileSize"] = imageBytes.Length,
                    ["extractedAt"] = DateTime.UtcNow.ToString("O")
                }
            };

            // Extract image metadata
            var imageMetadata = await ExtractImageMetadataAsync(imageBytes, classification.MimeType);
            if (imageMetadata != null)
            {
                imageEvidence.Metadata = imageEvidence.Metadata
                    .Concat(imageMetadata)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                // Create facts from image metadata
                await ExtractMetadataFactsAsync(imageMetadata, imageEvidenceId, extractedFacts, extractionOptions.MinConfidenceThreshold);
            }

            evidenceItems.Add(imageEvidence);

            // Perform OCR if service is available
            string? ocrText = null;
            if (_ocrService != null)
            {
                documentStream.Position = 0;
                var ocrResult = await _ocrService.ExtractTextAsync(documentStream, classification.MimeType, ct);
                if (ocrResult.IsSuccess && !string.IsNullOrWhiteSpace(ocrResult.Value))
                {
                    ocrText = ocrResult.Value;
                    
                    // Create OCR evidence
                    var ocrEvidenceId = UlidGenerator.NewId();
                    var ocrEvidence = new EvidenceItem
                    {
                        Id = ocrEvidenceId,
                        SourceType = EvidenceSourceType.OcrText,
                        SourcePath = $"{fileName}::ocr",
                        MimeType = "text/plain",
                        Content = ocrText,
                        ContentHash = ComputeStringHash(ocrText),
                        Metadata = new Dictionary<string, object>
                        {
                            ["sourceImage"] = fileName,
                            ["ocrEngine"] = _ocrService.GetType().Name,
                            ["extractedAt"] = DateTime.UtcNow.ToString("O"),
                            ["characterCount"] = ocrText.Length
                        }
                    };
                    evidenceItems.Add(ocrEvidence);

                    // Extract facts from OCR text
                    await ExtractTextFactsAsync(ocrText, ocrEvidenceId, extractedFacts, extractionOptions.MinConfidenceThreshold);
                }
            }

            // Detect image content patterns
            await DetectImagePatternsAsync(imageBytes, classification.MimeType, imageEvidenceId, extractedFacts, extractionOptions.MinConfidenceThreshold);

            var result = new DocumentExtractionResult
            {
                ExtractionId = extractionId,
                DocumentType = DocumentType.Image,
                ExtractorType = ExtractorType,
                IsSuccess = true,
                Facts = extractedFacts,
                Evidence = evidenceItems,
                ProcessingTimeMs = 0, // Would need proper timing
                Metadata = new Dictionary<string, object>
                {
                    ["imageFormat"] = classification.MimeType,
                    ["hasOcr"] = ocrText != null,
                    ["ocrTextLength"] = ocrText?.Length ?? 0,
                    ["factsExtracted"] = extractedFacts.Count,
                    ["extractionMethod"] = _ocrService != null ? "OCR+Metadata" : "Metadata"
                }
            };

            return Result<DocumentExtractionResult>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<DocumentExtractionResult>.Failure($"Image extraction failed: {ex.Message}");
        }
    }

    private async Task<Dictionary<string, object>?> ExtractImageMetadataAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            using var image = Image.FromStream(stream);

            var metadata = new Dictionary<string, object>
            {
                ["width"] = image.Width,
                ["height"] = image.Height,
                ["pixelFormat"] = image.PixelFormat.ToString(),
                ["horizontalResolution"] = image.HorizontalResolution,
                ["verticalResolution"] = image.VerticalResolution
            };

            // Extract EXIF data if available
            if (image.PropertyItems.Length > 0)
            {
                var exifData = new Dictionary<string, object>();
                
                foreach (var prop in image.PropertyItems)
                {
                    try
                    {
                        var value = ExtractPropertyValue(prop);
                        if (value != null)
                        {
                            exifData[$"exif_{prop.Id:X4}"] = value;
                        }
                    }
                    catch
                    {
                        // Skip problematic EXIF properties
                    }
                }

                if (exifData.Count > 0)
                {
                    metadata["exifData"] = exifData;
                }
            }

            return metadata;
        }
        catch
        {
            return null;
        }
    }

    private object? ExtractPropertyValue(PropertyItem prop)
    {
        try
        {
            return prop.Type switch
            {
                1 => prop.Value[0], // BYTE
                2 => System.Text.Encoding.ASCII.GetString(prop.Value).TrimEnd('\0'), // ASCII
                3 => BitConverter.ToUInt16(prop.Value, 0), // SHORT
                4 => BitConverter.ToUInt32(prop.Value, 0), // LONG
                5 => BitConverter.ToUInt32(prop.Value, 0) / (double)BitConverter.ToUInt32(prop.Value, 4), // RATIONAL
                7 => prop.Value, // UNDEFINED (as byte array)
                9 => BitConverter.ToInt32(prop.Value, 0), // SLONG
                10 => BitConverter.ToInt32(prop.Value, 0) / (double)BitConverter.ToInt32(prop.Value, 4), // SRATIONAL
                _ => Convert.ToBase64String(prop.Value) // Default to base64 for unknown types
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task ExtractMetadataFactsAsync(
        Dictionary<string, object> metadata,
        string evidenceId,
        List<ExtractedFact> facts,
        double minConfidence)
    {
        // Extract dimension facts
        if (metadata.ContainsKey("width") && metadata.ContainsKey("height"))
        {
            var dimensionFact = new ExtractedFact
            {
                Id = UlidGenerator.NewId(),
                Type = "image_property",
                Attribute = "dimensions",
                Value = $"{metadata["width"]}x{metadata["height"]}",
                Confidence = 1.0,
                EvidenceId = evidenceId,
                Context = "Image metadata",
                Metadata = new Dictionary<string, object>
                {
                    ["width"] = metadata["width"],
                    ["height"] = metadata["height"],
                    ["aspectRatio"] = Convert.ToDouble(metadata["width"]) / Convert.ToDouble(metadata["height"])
                }
            };
            facts.Add(dimensionFact);
        }

        // Extract resolution facts
        if (metadata.ContainsKey("horizontalResolution") && metadata.ContainsKey("verticalResolution"))
        {
            var resolutionFact = new ExtractedFact
            {
                Id = UlidGenerator.NewId(),
                Type = "image_property",
                Attribute = "resolution",
                Value = $"{metadata["horizontalResolution"]}x{metadata["verticalResolution"]} DPI",
                Confidence = 1.0,
                EvidenceId = evidenceId,
                Context = "Image metadata",
                Metadata = new Dictionary<string, object>
                {
                    ["horizontalResolution"] = metadata["horizontalResolution"],
                    ["verticalResolution"] = metadata["verticalResolution"]
                }
            };
            facts.Add(resolutionFact);
        }

        // Extract EXIF facts
        if (metadata.ContainsKey("exifData") && metadata["exifData"] is Dictionary<string, object> exifData)
        {
            foreach (var kvp in exifData)
            {
                var exifFact = new ExtractedFact
                {
                    Id = UlidGenerator.NewId(),
                    Type = "image_exif",
                    Attribute = kvp.Key,
                    Value = kvp.Value?.ToString() ?? "",
                    Confidence = 0.9,
                    EvidenceId = evidenceId,
                    Context = "EXIF metadata",
                    Metadata = new Dictionary<string, object>
                    {
                        ["exifProperty"] = kvp.Key,
                        ["dataType"] = kvp.Value?.GetType().Name ?? "null"
                    }
                };
                facts.Add(exifFact);
            }
        }

        await Task.CompletedTask;
    }

    private async Task ExtractTextFactsAsync(
        string ocrText,
        string evidenceId,
        List<ExtractedFact> facts,
        double minConfidence)
    {
        // Extract basic text statistics
        var textStats = new ExtractedFact
        {
            Id = UlidGenerator.NewId(),
            Type = "text_analysis",
            Attribute = "ocr_statistics",
            Value = $"{ocrText.Length} characters, {ocrText.Split().Length} words",
            Confidence = 0.8,
            EvidenceId = evidenceId,
            Context = "OCR text analysis",
            Metadata = new Dictionary<string, object>
            {
                ["characterCount"] = ocrText.Length,
                ["wordCount"] = ocrText.Split().Length,
                ["lineCount"] = ocrText.Split('\n').Length
            }
        };
        facts.Add(textStats);

        // Extract email addresses
        var emailPattern = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
        var emails = emailPattern.Matches(ocrText);
        foreach (Match email in emails)
        {
            var emailFact = new ExtractedFact
            {
                Id = UlidGenerator.NewId(),
                Type = "contact_info",
                Attribute = "email_address",
                Value = email.Value,
                Confidence = 0.9,
                EvidenceId = evidenceId,
                Context = "OCR text extraction",
                Metadata = new Dictionary<string, object>
                {
                    ["extractionMethod"] = "regex_pattern",
                    ["position"] = email.Index
                }
            };
            facts.Add(emailFact);
        }

        // Extract phone numbers
        var phonePattern = new Regex(@"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b");
        var phones = phonePattern.Matches(ocrText);
        foreach (Match phone in phones)
        {
            var phoneFact = new ExtractedFact
            {
                Id = UlidGenerator.NewId(),
                Type = "contact_info",
                Attribute = "phone_number",
                Value = phone.Value,
                Confidence = 0.8,
                EvidenceId = evidenceId,
                Context = "OCR text extraction",
                Metadata = new Dictionary<string, object>
                {
                    ["extractionMethod"] = "regex_pattern",
                    ["position"] = phone.Index
                }
            };
            facts.Add(phoneFact);
        }

        // Extract dates
        var datePattern = new Regex(@"\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b");
        var dates = datePattern.Matches(ocrText);
        foreach (Match date in dates)
        {
            var dateFact = new ExtractedFact
            {
                Id = UlidGenerator.NewId(),
                Type = "temporal_info",
                Attribute = "date",
                Value = date.Value,
                Confidence = 0.7,
                EvidenceId = evidenceId,
                Context = "OCR text extraction",
                Metadata = new Dictionary<string, object>
                {
                    ["extractionMethod"] = "regex_pattern",
                    ["position"] = date.Index
                }
            };
            facts.Add(dateFact);
        }

        await Task.CompletedTask;
    }

    private async Task DetectImagePatternsAsync(
        byte[] imageBytes,
        string mimeType,
        string evidenceId,
        List<ExtractedFact> facts,
        double minConfidence)
    {
        try
        {
            // Detect if image appears to be a document scan
            var isDocumentScan = await DetectDocumentScanAsync(imageBytes);
            if (isDocumentScan)
            {
                var scanFact = new ExtractedFact
                {
                    Id = UlidGenerator.NewId(),
                    Type = "image_classification",
                    Attribute = "document_scan",
                    Value = "true",
                    Confidence = 0.8,
                    EvidenceId = evidenceId,
                    Context = "Image pattern analysis",
                    Metadata = new Dictionary<string, object>
                    {
                        ["detectionMethod"] = "aspect_ratio_analysis",
                        ["classification"] = "document_scan"
                    }
                };
                facts.Add(scanFact);
            }

            // Detect dominant colors (simplified)
            var dominantColor = DetectDominantColor(imageBytes);
            if (!string.IsNullOrEmpty(dominantColor))
            {
                var colorFact = new ExtractedFact
                {
                    Id = UlidGenerator.NewId(),
                    Type = "image_property",
                    Attribute = "dominant_color",
                    Value = dominantColor,
                    Confidence = 0.7,
                    EvidenceId = evidenceId,
                    Context = "Image color analysis",
                    Metadata = new Dictionary<string, object>
                    {
                        ["colorAnalysisMethod"] = "simplified_dominant_color"
                    }
                };
                facts.Add(colorFact);
            }
        }
        catch
        {
            // Skip pattern detection if it fails
        }

        await Task.CompletedTask;
    }

    private async Task<bool> DetectDocumentScanAsync(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            using var image = Image.FromStream(stream);

            // Simple heuristic: aspect ratio close to standard document ratios
            var aspectRatio = (double)image.Width / image.Height;
            
            // Common document ratios: A4 (1.414), Letter (1.294), Legal (1.647)
            var documentRatios = new[] { 1.414, 1.294, 1.647, 0.707, 0.773, 0.607 }; // Include inverse ratios
            
            var tolerance = 0.1;
            var isDocumentRatio = documentRatios.Any(ratio => Math.Abs(aspectRatio - ratio) < tolerance);
            
            return await Task.FromResult(isDocumentRatio && Math.Min(image.Width, image.Height) > 600);
        }
        catch
        {
            return false;
        }
    }

    private string DetectDominantColor(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            using var image = new Bitmap(stream);
            
            // Sample center pixel as a very simple dominant color detection
            var centerX = image.Width / 2;
            var centerY = image.Height / 2;
            var centerPixel = image.GetPixel(centerX, centerY);
            
            // Convert to simple color names
            if (centerPixel.GetBrightness() > 0.8)
                return "white";
            if (centerPixel.GetBrightness() < 0.2)
                return "black";
            if (centerPixel.GetSaturation() < 0.3)
                return "gray";
            
            var hue = centerPixel.GetHue();
            return hue switch
            {
                >= 0 and < 60 => "red",
                >= 60 and < 120 => "yellow",
                >= 120 and < 180 => "green",
                >= 180 and < 240 => "cyan",
                >= 240 and < 300 => "blue",
                _ => "magenta"
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ComputeContentHash(byte[] content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(content);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string ComputeStringHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Interface for OCR services
/// </summary>
public interface IOcrService
{
    Task<Result<string>> ExtractTextAsync(Stream imageStream, string mimeType, CancellationToken ct = default);
}

/// <summary>
/// Simple OCR service implementation (placeholder for Tesseract or cloud OCR services)
/// </summary>
public class SimpleOcrService : IOcrService
{
    public async Task<Result<string>> ExtractTextAsync(Stream imageStream, string mimeType, CancellationToken ct = default)
    {
        try
        {
            // This would integrate with Tesseract.NET, Azure Computer Vision, AWS Textract, etc.
            // For now, return a placeholder implementation
            await Task.Delay(100, ct); // Simulate processing time
            
            return Result<string>.Success("OCR placeholder - integrate with actual OCR service");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"OCR failed: {ex.Message}");
        }
    }
}