using MAK3R.Core.Models;
using MAK3R.Ingestion.Models;
using MAK3R.Ingestion.Services;
using System.Diagnostics;
using System.Text.Json;

namespace MAK3R.Ingestion.Extractors;

public class SqliteDocumentExtractor : IDocumentExtractor
{
    public string ExtractorType => "SQLiteExtractor";
    
    public Task<bool> CanExtractAsync(DocumentClassificationResult classification, CancellationToken ct = default)
    {
        return Task.FromResult(classification.DocumentType == DocumentType.Database &&
                              classification.MimeType == "application/x-sqlite3");
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
            
            // Save stream to temporary file for DB2XL processing
            var tempDbPath = Path.GetTempFileName();
            tempDbPath = Path.ChangeExtension(tempDbPath, ".sqlite");
            
            try
            {
                using (var fileStream = File.Create(tempDbPath))
                {
                    documentStream.Position = 0;
                    await documentStream.CopyToAsync(fileStream, ct);
                }

                // Use DB2XL to extract structured data from SQLite database
                var db2xlResult = await ExtractDatabaseStructureAsync(tempDbPath, correlationId, ct);
                if (!db2xlResult.IsSuccess)
                {
                    return Result<DocumentExtractionResult>.Failure($"DB2XL extraction failed: {db2xlResult.Error}");
                }

                var extractedFacts = new List<ExtractedFact>();
                var evidenceItems = new List<EvidenceItem>();
                
                // Create evidence for the database file
                var dbEvidenceId = UlidGenerator.NewId();
                var dbEvidence = new EvidenceItem
                {
                    Id = dbEvidenceId,
                    SourceType = EvidenceSourceType.DatabaseFile,
                    SourcePath = fileName,
                    MimeType = "application/x-sqlite3",
                    Content = $"SQLite Database: {fileName}",
                    ContentHash = ComputeContentHash(documentStream),
                    Metadata = new Dictionary<string, object>
                    {
                        ["originalFileName"] = fileName,
                        ["tableCount"] = db2xlResult.Value.Tables.Count,
                        ["totalRows"] = db2xlResult.Value.Tables.Sum(t => t.EstimatedRows),
                        ["databaseSize"] = new FileInfo(tempDbPath).Length,
                        ["extractedAt"] = DateTime.UtcNow.ToString("O")
                    }
                };
                evidenceItems.Add(dbEvidence);

                // Process each table as structured data
                foreach (var table in db2xlResult.Value.Tables)
                {
                    var tableEvidenceId = UlidGenerator.NewId();
                    var tableEvidence = new EvidenceItem
                    {
                        Id = tableEvidenceId,
                        SourceType = EvidenceSourceType.DatabaseTable,
                        SourcePath = $"{fileName}::{table.Name}",
                        MimeType = "application/json",
                        Content = JsonSerializer.Serialize(table.SampleData, new JsonSerializerOptions { WriteIndented = true }),
                        ContentHash = ComputeStringHash(JsonSerializer.Serialize(table.SampleData)),
                        Metadata = new Dictionary<string, object>
                        {
                            ["tableName"] = table.Name,
                            ["columnCount"] = table.Columns.Count,
                            ["estimatedRows"] = table.EstimatedRows,
                            ["primaryKeys"] = table.PrimaryKeys.ToArray(),
                            ["tableType"] = table.Type,
                            ["sampleRowCount"] = table.SampleData.Count
                        }
                    };
                    evidenceItems.Add(tableEvidence);

                    // Extract facts from table schema and sample data
                    await ExtractTableFactsAsync(table, tableEvidenceId, extractedFacts, extractionOptions.MinConfidenceThreshold);
                    
                    // Extract relationships and patterns
                    await ExtractDataPatternsAsync(table, tableEvidenceId, extractedFacts, extractionOptions.MinConfidenceThreshold);
                }

                var result = new DocumentExtractionResult
                {
                    ExtractionId = extractionId,
                    DocumentType = DocumentType.Database,
                    ExtractorType = ExtractorType,
                    IsSuccess = true,
                    Facts = extractedFacts,
                    Evidence = evidenceItems,
                    ProcessingTimeMs = 0, // Would need proper timing
                    Metadata = new Dictionary<string, object>
                    {
                        ["db2xlVersion"] = "1.0",
                        ["extractionMethod"] = "DB2XL-MCP",
                        ["tablesProcessed"] = db2xlResult.Value.Tables.Count,
                        ["totalFactsExtracted"] = extractedFacts.Count,
                        ["hasEncryptedContent"] = await DetectEncryptedContentAsync(tempDbPath, ct)
                    }
                };

                return Result<DocumentExtractionResult>.Success(result);
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempDbPath))
                {
                    File.Delete(tempDbPath);
                }
            }
        }
        catch (Exception ex)
        {
            return Result<DocumentExtractionResult>.Failure($"SQLite extraction failed: {ex.Message}");
        }
    }

    private async Task<Result<DatabasePreview>> ExtractDatabaseStructureAsync(string dbPath, string correlationId, CancellationToken ct)
    {
        try
        {
            // Use DB2XL executable to get database structure
            var db2xlPath = FindDb2XlExecutable();
            if (db2xlPath == null)
            {
                return Result<DatabasePreview>.Failure("DB2XL executable not found");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = db2xlPath,
                Arguments = $"mcp preview \"{dbPath}\" --max-rows 10 --include-sample-data --format json",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                return Result<DatabasePreview>.Failure($"DB2XL process failed: {error}");
            }

            var previewResult = JsonSerializer.Deserialize<DatabasePreview>(output);
            if (previewResult == null)
            {
                return Result<DatabasePreview>.Failure("Failed to parse DB2XL output");
            }

            return Result<DatabasePreview>.Success(previewResult);
        }
        catch (Exception ex)
        {
            return Result<DatabasePreview>.Failure($"DB2XL execution failed: {ex.Message}");
        }
    }

    private string? FindDb2XlExecutable()
    {
        // Look for DB2XL in common locations
        var possiblePaths = new[]
        {
            @"C:\code\DB2XL\DB2XL.Console\bin\Debug\net9.0\DB2XL.Console.exe",
            @"C:\code\DB2XL\DB2XL.Console\bin\Release\net9.0\DB2XL.Console.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "db2xl.exe"),
            "db2xl.exe" // Try PATH
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private async Task ExtractTableFactsAsync(
        TablePreview table, 
        string evidenceId, 
        List<ExtractedFact> facts, 
        double minConfidence)
    {
        // Extract schema facts
        var schemaFact = new ExtractedFact
        {
            Id = UlidGenerator.NewId(),
            Type = "database_schema",
            Attribute = "table_definition",
            Value = table.Name,
            Confidence = 1.0,
            EvidenceId = evidenceId,
            Context = $"Table: {table.Name}",
            Metadata = new Dictionary<string, object>
            {
                ["columnCount"] = table.Columns.Count,
                ["estimatedRows"] = table.EstimatedRows,
                ["tableType"] = table.Type,
                ["hasPrimaryKey"] = table.PrimaryKeys.Count > 0
            }
        };
        facts.Add(schemaFact);

        // Extract column definitions
        foreach (var column in table.Columns)
        {
            var columnFact = new ExtractedFact
            {
                Id = UlidGenerator.NewId(),
                Type = "database_column",
                Attribute = "column_definition",
                Value = $"{column.Column.Name} ({column.Column.Type})",
                Confidence = 1.0,
                EvidenceId = evidenceId,
                Context = $"Table: {table.Name}, Column: {column.Column.Name}",
                Metadata = new Dictionary<string, object>
                {
                    ["columnName"] = column.Column.Name,
                    ["dataType"] = column.Column.Type,
                    ["isNullable"] = column.Column.AllowNull,
                    ["isPrimaryKey"] = column.Column.IsPrimaryKey,
                    ["defaultValue"] = column.Column.DefaultValue ?? ""
                }
            };
            facts.Add(columnFact);
        }

        // Extract data patterns from sample data
        if (table.SampleData.Count > 0)
        {
            await ExtractSampleDataFactsAsync(table, evidenceId, facts, minConfidence);
        }
    }

    private async Task ExtractSampleDataFactsAsync(
        TablePreview table,
        string evidenceId,
        List<ExtractedFact> facts,
        double minConfidence)
    {
        var sampleRow = table.SampleData.FirstOrDefault();
        if (sampleRow == null) return;

        // Detect patterns in the sample data
        foreach (var kvp in sampleRow)
        {
            var columnName = kvp.Key;
            var value = kvp.Value;

            if (value == null) continue;

            var valueStr = value.ToString();
            if (string.IsNullOrEmpty(valueStr)) continue;

            // Detect potential entity types based on data patterns
            var entityType = DetectEntityType(columnName, valueStr);
            if (!string.IsNullOrEmpty(entityType))
            {
                var entityFact = new ExtractedFact
                {
                    Id = UlidGenerator.NewId(),
                    Type = "database_entity",
                    Attribute = entityType,
                    Value = valueStr,
                    Confidence = 0.8, // Pattern-based detection has moderate confidence
                    EvidenceId = evidenceId,
                    Context = $"Table: {table.Name}, Column: {columnName}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["detectionMethod"] = "pattern_matching",
                        ["sampleValue"] = valueStr,
                        ["columnName"] = columnName,
                        ["tableName"] = table.Name
                    }
                };
                facts.Add(entityFact);
            }
        }

        await Task.CompletedTask;
    }

    private async Task ExtractDataPatternsAsync(
        TablePreview table,
        string evidenceId,
        List<ExtractedFact> facts,
        double minConfidence)
    {
        // Analyze data patterns from the table
        if (table.DataPatterns != null)
        {
            // Extract timestamp patterns
            foreach (var timestampCol in table.DataPatterns.TimestampColumns)
            {
                var timestampFact = new ExtractedFact
                {
                    Id = UlidGenerator.NewId(),
                    Type = "data_pattern",
                    Attribute = "timestamp_column",
                    Value = timestampCol,
                    Confidence = 0.9,
                    EvidenceId = evidenceId,
                    Context = $"Table: {table.Name}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["patternType"] = "timestamp",
                        ["columnName"] = timestampCol,
                        ["tableName"] = table.Name
                    }
                };
                facts.Add(timestampFact);
            }

            // Extract PII patterns (with privacy awareness)
            foreach (var piiCol in table.DataPatterns.PotentialPiiColumns)
            {
                var piiFact = new ExtractedFact
                {
                    Id = UlidGenerator.NewId(),
                    Type = "privacy_concern",
                    Attribute = "potential_pii",
                    Value = "[REDACTED]", // Don't store actual PII values
                    Confidence = 0.7,
                    EvidenceId = evidenceId,
                    Context = $"Table: {table.Name}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["patternType"] = "pii_detection",
                        ["columnName"] = piiCol,
                        ["tableName"] = table.Name,
                        ["privacyNote"] = "Actual values redacted for privacy"
                    }
                };
                facts.Add(piiFact);
            }
        }

        await Task.CompletedTask;
    }

    private string DetectEntityType(string columnName, string value)
    {
        var lowerColumnName = columnName.ToLowerInvariant();
        var lowerValue = value.ToLowerInvariant();

        // Email detection
        if (lowerColumnName.Contains("email") || lowerValue.Contains("@"))
        {
            return "email_address";
        }

        // Phone detection
        if (lowerColumnName.Contains("phone") || lowerColumnName.Contains("tel"))
        {
            return "phone_number";
        }

        // Address detection
        if (lowerColumnName.Contains("address") || lowerColumnName.Contains("street"))
        {
            return "address";
        }

        // Name detection
        if (lowerColumnName.Contains("name") || lowerColumnName.Contains("firstname") || lowerColumnName.Contains("lastname"))
        {
            return "person_name";
        }

        // Company detection
        if (lowerColumnName.Contains("company") || lowerColumnName.Contains("organization") || lowerColumnName.Contains("corp"))
        {
            return "company_name";
        }

        // ID detection
        if (lowerColumnName.Contains("id") && (lowerValue.Length == 36 || lowerValue.Length == 26)) // GUID or ULID length
        {
            return "identifier";
        }

        return string.Empty;
    }

    private async Task<bool> DetectEncryptedContentAsync(string dbPath, CancellationToken ct)
    {
        try
        {
            // Use DB2XL's built-in encrypted content detection
            var db2xlPath = FindDb2XlExecutable();
            if (db2xlPath == null) return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = db2xlPath,
                Arguments = $"analyze \"{dbPath}\" --check-encrypted --format json",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0 && output.Contains("\"hasEncrypted\":true"))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private string ComputeContentHash(Stream stream)
    {
        stream.Position = 0;
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string ComputeStringHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

// Support models for DB2XL integration
public record DatabasePreview
{
    public bool IsSuccess { get; init; }
    public DatabaseSummary Summary { get; init; } = new();
    public List<TablePreview> Tables { get; init; } = new();
    public List<string> Errors { get; init; } = new();
}

public record DatabaseSummary
{
    public string FilePath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public int TableCount { get; init; }
    public int ViewCount { get; init; }
    public long TotalEstimatedRows { get; init; }
    public DateTime LastModified { get; init; }
}

public record TablePreview
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "table";
    public List<ColumnPreview> Columns { get; init; } = new();
    public List<string> PrimaryKeys { get; init; } = new();
    public long EstimatedRows { get; init; }
    public List<Dictionary<string, object?>> SampleData { get; init; } = new();
    public TableDataPatterns? DataPatterns { get; init; }
}

public record ColumnPreview
{
    public MAK3R.Core.Models.ColumnInfo Column { get; init; } = new() { Name = "", Type = "" };
}

public record TableDataPatterns
{
    public List<string> TimestampColumns { get; init; } = new();
    public List<string> PotentialPiiColumns { get; init; } = new();
    public List<string> JsonColumns { get; init; } = new();
    public List<string> IdColumns { get; init; } = new();
}