using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using MAK3R.PWA.Models;

namespace MAK3R.PWA.Services
{
    public class FileIngestionService : IFileIngestionService
    {
        private readonly ILogger<FileIngestionService> _logger;
        private readonly IProductService _productService;

        public FileIngestionService(ILogger<FileIngestionService> logger, IProductService productService)
        {
            _logger = logger;
            _productService = productService;
        }

        public async Task<FileAnalysisResult> AnalyzeFileAsync(Stream fileStream, string fileName)
        {
            var result = new FileAnalysisResult
            {
                FileName = fileName,
                FileSize = fileStream.Length
            };

            try
            {
                var extension = Path.GetExtension(fileName).ToLower();
                result.FileType = extension;

                switch (extension)
                {
                    case ".csv":
                        await AnalyzeCsvFile(fileStream, result);
                        break;
                    case ".json":
                        await AnalyzeJsonFile(fileStream, result);
                        break;
                    case ".xlsx":
                    case ".xls":
                        result.Issues.Add("Excel files are not yet supported. Please convert to CSV format.");
                        result.IsValid = false;
                        break;
                    default:
                        result.Issues.Add($"Unsupported file type: {extension}");
                        result.IsValid = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Error analyzing file: {ex.Message}");
                result.IsValid = false;
                _logger.LogError(ex, "Error analyzing file {FileName}", fileName);
            }

            return result;
        }

        public async Task<SchemaInferenceResult> InferSchemaAsync(Stream fileStream, string fileName)
        {
            var result = new SchemaInferenceResult();
            
            try
            {
                var extension = Path.GetExtension(fileName).ToLower();
                
                switch (extension)
                {
                    case ".csv":
                        await InferCsvSchema(fileStream, result);
                        break;
                    case ".json":
                        await InferJsonSchema(fileStream, result);
                        break;
                    default:
                        result.ValidationWarnings.Add($"Schema inference not supported for {extension} files");
                        return result;
                }

                // Determine entity type based on field analysis
                result.RecommendedEntityType = DetermineEntityType(result.Fields);
                result.ConfidenceScore = CalculateConfidenceScore(result.Fields, result.RecommendedEntityType);

                // Generate suggested mappings
                result.SuggestedMappings = GenerateSuggestedMappings(result.Fields, result.RecommendedEntityType);
            }
            catch (Exception ex)
            {
                result.ValidationWarnings.Add($"Error inferring schema: {ex.Message}");
                _logger.LogError(ex, "Error inferring schema for file {FileName}", fileName);
            }

            return result;
        }

        public async Task<DataImportResult> ImportDataAsync(Stream fileStream, string fileName, ImportMapping mapping)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new DataImportResult();

            try
            {
                var extension = Path.GetExtension(fileName).ToLower();

                switch (extension)
                {
                    case ".csv":
                        await ImportCsvData(fileStream, mapping, result);
                        break;
                    case ".json":
                        await ImportJsonData(fileStream, mapping, result);
                        break;
                    default:
                        throw new NotSupportedException($"Import not supported for {extension} files");
                }
            }
            catch (Exception ex)
            {
                result.ErrorDetails.Add(new ImportError
                {
                    RowNumber = 0,
                    Field = "General",
                    Value = fileName,
                    Error = ex.Message,
                    Severity = "Critical"
                });
                _logger.LogError(ex, "Error importing data from file {FileName}", fileName);
            }

            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            result.Errors = result.ErrorDetails.Count;
            result.SuccessfulImports = Math.Max(0, result.TotalRecords - result.Errors);

            return result;
        }

        public async Task<List<string>> GetSupportedFormatsAsync()
        {
            await Task.CompletedTask;
            return new List<string> { ".csv", ".json" };
        }

        public async Task<List<ImportTemplate>> GetImportTemplatesAsync()
        {
            await Task.CompletedTask;
            
            return new List<ImportTemplate>
            {
                new ImportTemplate
                {
                    Id = "product-standard",
                    Name = "Standard Product Import",
                    Description = "Import products with basic information (Name, SKU, Price, Description)",
                    EntityType = DataEntityType.Product,
                    SampleHeaders = new List<string> { "Name", "SKU", "Price", "Description", "Active", "Category" },
                    Mapping = new ImportMapping
                    {
                        EntityType = DataEntityType.Product,
                        FieldMappings = new Dictionary<string, string>
                        {
                            { "Name", "Name" },
                            { "SKU", "Sku" },
                            { "Price", "Price" },
                            { "Description", "Description" },
                            { "Active", "IsActive" },
                            { "Category", "Category" }
                        },
                        RequiredFields = new List<string> { "Name", "SKU" }
                    }
                },
                new ImportTemplate
                {
                    Id = "machine-data",
                    Name = "Machine Data Import",
                    Description = "Import manufacturing machine information and specifications",
                    EntityType = DataEntityType.Machine,
                    SampleHeaders = new List<string> { "Machine_Name", "Model", "Serial_Number", "Location", "Status", "Last_Maintenance" },
                    Mapping = new ImportMapping
                    {
                        EntityType = DataEntityType.Machine,
                        RequiredFields = new List<string> { "Machine_Name", "Model" }
                    }
                },
                new ImportTemplate
                {
                    Id = "inventory-basic",
                    Name = "Basic Inventory Import",
                    Description = "Import inventory levels and stock information",
                    EntityType = DataEntityType.Inventory,
                    SampleHeaders = new List<string> { "Product_SKU", "Quantity", "Location", "Min_Stock", "Max_Stock", "Last_Updated" },
                    Mapping = new ImportMapping
                    {
                        EntityType = DataEntityType.Inventory,
                        RequiredFields = new List<string> { "Product_SKU", "Quantity" }
                    }
                }
            };
        }

        private async Task AnalyzeCsvFile(Stream fileStream, FileAnalysisResult result)
        {
            fileStream.Position = 0;
            using var reader = new StreamReader(fileStream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            // Read header
            await csv.ReadAsync();
            csv.ReadHeader();
            result.DetectedColumns = csv.HeaderRecord?.ToList() ?? new List<string>();

            // Sample a few rows for analysis
            var rowCount = 0;
            var sampleRows = new List<string[]>();
            
            while (await csv.ReadAsync() && rowCount < 5)
            {
                var row = new string[result.DetectedColumns.Count];
                for (int i = 0; i < result.DetectedColumns.Count; i++)
                {
                    row[i] = csv.GetField(i) ?? "";
                }
                sampleRows.Add(row);
                rowCount++;
            }

            // Count total rows (approximate)
            while (await csv.ReadAsync())
            {
                rowCount++;
            }

            result.EstimatedRecordCount = rowCount;

            // Analyze column types
            for (int i = 0; i < result.DetectedColumns.Count; i++)
            {
                var columnName = result.DetectedColumns[i];
                var sampleValues = sampleRows.Select(row => i < row.Length ? row[i] : "").ToList();
                result.ColumnTypes[columnName] = InferDataType(sampleValues);
            }

            // Generate sample data preview
            result.SampleData = sampleRows.Take(3).Select(row => string.Join(", ", row)).ToList();

            // Validate
            if (result.DetectedColumns.Count == 0)
            {
                result.Issues.Add("No columns detected in CSV file");
                result.IsValid = false;
            }

            if (result.EstimatedRecordCount == 0)
            {
                result.Issues.Add("No data rows found in CSV file");
                result.IsValid = false;
            }
        }

        private async Task AnalyzeJsonFile(Stream fileStream, FileAnalysisResult result)
        {
            fileStream.Position = 0;
            using var reader = new StreamReader(fileStream);
            var jsonContent = await reader.ReadToEndAsync();

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonContent);
                var root = jsonDoc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    result.EstimatedRecordCount = root.GetArrayLength();
                    
                    if (result.EstimatedRecordCount > 0)
                    {
                        var firstElement = root[0];
                        if (firstElement.ValueKind == JsonValueKind.Object)
                        {
                            result.DetectedColumns = firstElement.EnumerateObject().Select(p => p.Name).ToList();
                            
                            // Analyze types from first few objects
                            var sampleCount = Math.Min(5, result.EstimatedRecordCount);
                            for (int i = 0; i < result.DetectedColumns.Count; i++)
                            {
                                var columnName = result.DetectedColumns[i];
                                var sampleValues = new List<string>();
                                
                                for (int j = 0; j < sampleCount; j++)
                                {
                                    if (root[j].TryGetProperty(columnName, out var prop))
                                    {
                                        sampleValues.Add(prop.ToString());
                                    }
                                }
                                
                                result.ColumnTypes[columnName] = InferDataType(sampleValues);
                            }
                        }
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    result.EstimatedRecordCount = 1;
                    result.DetectedColumns = root.EnumerateObject().Select(p => p.Name).ToList();
                }
            }
            catch (JsonException ex)
            {
                result.Issues.Add($"Invalid JSON format: {ex.Message}");
                result.IsValid = false;
            }
        }

        private async Task InferCsvSchema(Stream fileStream, SchemaInferenceResult result)
        {
            fileStream.Position = 0;
            using var reader = new StreamReader(fileStream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord?.ToList() ?? new List<string>();

            var allRows = new List<string[]>();
            while (await csv.ReadAsync())
            {
                var row = new string[headers.Count];
                for (int i = 0; i < headers.Count; i++)
                {
                    row[i] = csv.GetField(i) ?? "";
                }
                allRows.Add(row);
                
                // Limit analysis to first 1000 rows for performance
                if (allRows.Count >= 1000) break;
            }

            // Analyze each field
            for (int i = 0; i < headers.Count; i++)
            {
                var fieldName = headers[i];
                var values = allRows.Select(row => i < row.Length ? row[i] : "").ToList();
                var nonEmptyValues = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

                var nullCount = values.Count - nonEmptyValues.Count;
                var field = new SchemaField
                {
                    Name = fieldName,
                    Type = InferDataType(values),
                    NullCount = nullCount,
                    SampleValues = nonEmptyValues.Take(5).Cast<object>().ToList(),
                    IsUnique = nonEmptyValues.Count == nonEmptyValues.Distinct().Count(),
                    IsRequired = nullCount == 0
                };

                // Detect patterns
                if (field.Type == DataType.String && nonEmptyValues.Any())
                {
                    field.Pattern = DetectPattern(nonEmptyValues);
                }

                result.Fields.Add(field);
            }
        }

        private async Task InferJsonSchema(Stream fileStream, SchemaInferenceResult result)
        {
            // Similar implementation for JSON schema inference
            await Task.CompletedTask;
            result.ValidationWarnings.Add("JSON schema inference not yet implemented");
        }

        private async Task ImportCsvData(Stream fileStream, ImportMapping mapping, DataImportResult result)
        {
            fileStream.Position = 0;
            using var reader = new StreamReader(fileStream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            if (mapping.SkipFirstRow)
            {
                await csv.ReadAsync();
                csv.ReadHeader();
            }

            var rowNumber = mapping.SkipFirstRow ? 2 : 1;

            while (await csv.ReadAsync())
            {
                result.TotalRecords++;
                
                try
                {
                    if (mapping.EntityType == DataEntityType.Product)
                    {
                        var product = await CreateProductFromRow(csv, mapping, rowNumber);
                        if (product != null)
                        {
                            result.ImportedData.Add(product);
                        }
                    }
                    // Add other entity types as needed
                }
                catch (Exception ex)
                {
                    result.ErrorDetails.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        Field = "General",
                        Value = "",
                        Error = ex.Message
                    });
                }

                rowNumber++;
            }
        }

        private async Task ImportJsonData(Stream fileStream, ImportMapping mapping, DataImportResult result)
        {
            // JSON import implementation
            await Task.CompletedTask;
            result.ErrorDetails.Add(new ImportError
            {
                Error = "JSON import not yet implemented",
                Severity = "Warning"
            });
        }

        private async Task<Product?> CreateProductFromRow(CsvReader csv, ImportMapping mapping, int rowNumber)
        {
            try
            {
                var product = new Product();

                foreach (var fieldMap in mapping.FieldMappings)
                {
                    var sourceField = fieldMap.Key;
                    var targetField = fieldMap.Value;
                    var value = csv.GetField(sourceField);

                    switch (targetField.ToLower())
                    {
                        case "name":
                            product.Name = value ?? "";
                            break;
                        case "sku":
                            product.Sku = value ?? "";
                            break;
                        case "description":
                            product.Description = value ?? "";
                            break;
                        case "price":
                            if (decimal.TryParse(value, out var price))
                                product.Price = price;
                            break;
                        case "isactive":
                            if (bool.TryParse(value, out var isActive))
                                product.IsActive = isActive;
                            break;
                        case "category":
                            if (Enum.TryParse<ProductCategory>(value, out var category))
                                product.Category = category;
                            break;
                    }
                }

                // Validate required fields
                foreach (var requiredField in mapping.RequiredFields)
                {
                    if (mapping.FieldMappings.ContainsKey(requiredField))
                    {
                        var targetField = mapping.FieldMappings[requiredField];
                        var value = GetProductFieldValue(product, targetField);
                        
                        if (string.IsNullOrWhiteSpace(value?.ToString()))
                        {
                            throw new InvalidOperationException($"Required field '{requiredField}' is empty");
                        }
                    }
                }

                return await _productService.CreateProductAsync(product);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error creating product: {ex.Message}");
            }
        }

        private object? GetProductFieldValue(Product product, string fieldName)
        {
            return fieldName.ToLower() switch
            {
                "name" => product.Name,
                "sku" => product.Sku,
                "description" => product.Description,
                "price" => product.Price,
                "isactive" => product.IsActive,
                "category" => product.Category,
                _ => null
            };
        }

        private DataType InferDataType(List<string> values)
        {
            var nonEmptyValues = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            
            if (!nonEmptyValues.Any())
                return DataType.String;

            // Check for boolean
            if (nonEmptyValues.All(v => bool.TryParse(v, out _)))
                return DataType.Boolean;

            // Check for integer
            if (nonEmptyValues.All(v => int.TryParse(v, out _)))
                return DataType.Integer;

            // Check for decimal
            if (nonEmptyValues.All(v => decimal.TryParse(v, out _)))
                return DataType.Decimal;

            // Check for datetime
            if (nonEmptyValues.All(v => DateTime.TryParse(v, out _)))
                return DataType.DateTime;

            // Check for email
            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (nonEmptyValues.All(v => emailRegex.IsMatch(v)))
                return DataType.Email;

            // Check for URL
            if (nonEmptyValues.All(v => Uri.TryCreate(v, UriKind.Absolute, out _)))
                return DataType.Url;

            // Check for currency (starts with currency symbol)
            if (nonEmptyValues.All(v => v.StartsWith("$") || v.StartsWith("€") || v.StartsWith("£")))
                return DataType.Currency;

            // Check for percentage
            if (nonEmptyValues.All(v => v.EndsWith("%") && decimal.TryParse(v.TrimEnd('%'), out _)))
                return DataType.Percentage;

            return DataType.String;
        }

        private DataEntityType DetermineEntityType(List<SchemaField> fields)
        {
            var fieldNames = fields.Select(f => f.Name.ToLower()).ToList();

            // Product indicators
            var productIndicators = new[] { "name", "sku", "price", "product", "item", "description" };
            if (productIndicators.Any(indicator => fieldNames.Any(name => name.Contains(indicator))))
                return DataEntityType.Product;

            // Machine indicators
            var machineIndicators = new[] { "machine", "equipment", "device", "serial", "model" };
            if (machineIndicators.Any(indicator => fieldNames.Any(name => name.Contains(indicator))))
                return DataEntityType.Machine;

            // Inventory indicators
            var inventoryIndicators = new[] { "quantity", "stock", "inventory", "warehouse", "location" };
            if (inventoryIndicators.Any(indicator => fieldNames.Any(name => name.Contains(indicator))))
                return DataEntityType.Inventory;

            return DataEntityType.Unknown;
        }

        private double CalculateConfidenceScore(List<SchemaField> fields, DataEntityType entityType)
        {
            // Simple confidence scoring based on field matching
            var fieldNames = fields.Select(f => f.Name.ToLower()).ToList();
            var matchCount = 0;
            var totalFields = fields.Count;

            var expectedFields = entityType switch
            {
                DataEntityType.Product => new[] { "name", "sku", "price", "description" },
                DataEntityType.Machine => new[] { "name", "model", "serial", "location" },
                DataEntityType.Inventory => new[] { "sku", "quantity", "location" },
                _ => new string[0]
            };

            foreach (var expected in expectedFields)
            {
                if (fieldNames.Any(name => name.Contains(expected)))
                    matchCount++;
            }

            return totalFields > 0 ? (double)matchCount / totalFields : 0.0;
        }

        private List<SuggestedMapping> GenerateSuggestedMappings(List<SchemaField> fields, DataEntityType entityType)
        {
            var mappings = new List<SuggestedMapping>();

            if (entityType == DataEntityType.Product)
            {
                var productMappings = new Dictionary<string, string[]>
                {
                    { "Name", new[] { "name", "title", "product_name", "item_name" } },
                    { "Sku", new[] { "sku", "code", "item_code", "product_code", "part_number" } },
                    { "Price", new[] { "price", "cost", "amount", "value", "unit_price" } },
                    { "Description", new[] { "description", "details", "notes", "summary" } },
                    { "Category", new[] { "category", "type", "group", "classification" } }
                };

                foreach (var field in fields)
                {
                    var fieldNameLower = field.Name.ToLower();
                    
                    foreach (var mapping in productMappings)
                    {
                        var targetField = mapping.Key;
                        var sourcePatterns = mapping.Value;
                        
                        var confidence = sourcePatterns.Max(pattern => 
                            fieldNameLower.Contains(pattern) ? 1.0 : 
                            CalculateSimilarity(fieldNameLower, pattern));

                        if (confidence > 0.6)
                        {
                            mappings.Add(new SuggestedMapping
                            {
                                SourceField = field.Name,
                                TargetField = targetField,
                                ConfidenceScore = confidence
                            });
                        }
                    }
                }
            }

            return mappings.OrderByDescending(m => m.ConfidenceScore).ToList();
        }

        private double CalculateSimilarity(string source, string target)
        {
            // Simple Levenshtein distance-based similarity
            var distance = LevenshteinDistance(source, target);
            var maxLength = Math.Max(source.Length, target.Length);
            return maxLength == 0 ? 1.0 : 1.0 - (double)distance / maxLength;
        }

        private int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
            if (string.IsNullOrEmpty(target)) return source.Length;

            var matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= target.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source.Length, target.Length];
        }

        private string? DetectPattern(List<string> values)
        {
            // Detect common patterns like SKU formats, phone numbers, etc.
            if (values.All(v => Regex.IsMatch(v, @"^[A-Z]{2,3}-\d{3,6}$")))
                return "SKU Format (ABC-123)";
                
            if (values.All(v => Regex.IsMatch(v, @"^\d{3}-\d{3}-\d{4}$")))
                return "Phone Number (XXX-XXX-XXXX)";
                
            return null;
        }
    }
}