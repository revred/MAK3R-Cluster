using MAK3R.Core;
using MAK3R.Data.Entities;
using MAK3R.Ingestion.Services;

namespace MAK3R.Ingestion.Mappers;

/// <summary>
/// DigitalTwin2 Invoice Fact Mapper - maps invoice facts to Knowledge Graph entities
/// Creates Invoice, Company, LineItem entities with proper relationships
/// </summary>
public class InvoiceFactMapper : IFactMapper
{
    private readonly MapperInfo _info;

    public InvoiceFactMapper()
    {
        _info = new MapperInfo
        {
            Name = "Invoice Fact Mapper",
            Version = "1.0.0",
            SupportedTypes = [DocumentClassification.Types.Invoice],
            FieldMappings = new Dictionary<string, FieldMapping>
            {
                ["InvoiceNumber"] = new FieldMapping
                {
                    SourceField = "InvoiceNumber",
                    TargetEntityType = ExtractedFact.EntityTypes.Invoice,
                    TargetAttribute = "number",
                    DataType = "string",
                    Required = true,
                    ValidationRules = [
                        new ValidationRule
                        {
                            Type = "regex",
                            Pattern = @"^[A-Z]{2,3}-\d{4}-\d{3,4}$",
                            ErrorMessage = "Invoice number must follow format: XXX-YYYY-NNN"
                        }
                    ]
                },
                ["Amount"] = new FieldMapping
                {
                    SourceField = "Amount",
                    TargetEntityType = ExtractedFact.EntityTypes.Invoice,
                    TargetAttribute = "total_amount",
                    DataType = "decimal",
                    Required = true,
                    ValidationRules = [
                        new ValidationRule
                        {
                            Type = "range",
                            MinValue = 0.01m,
                            MaxValue = 1000000m,
                            ErrorMessage = "Invoice amount must be between $0.01 and $1,000,000"
                        }
                    ]
                },
                ["VendorName"] = new FieldMapping
                {
                    SourceField = "VendorName",
                    TargetEntityType = ExtractedFact.EntityTypes.Vendor,
                    TargetAttribute = "name",
                    DataType = "string",
                    Required = true
                }
            }
        };
    }

    public async Task<Result<MappingResult>> MapAsync(
        ExtractionResult extraction,
        string dataRoomId,
        string correlationId,
        CancellationToken ct = default)
    {
        try
        {
            Guard.NotNull(extraction);
            Guard.NotNullOrWhiteSpace(dataRoomId);
            Guard.NotNullOrWhiteSpace(correlationId);

            var entities = new List<KnowledgeEntity>();
            var relations = new List<EntityRelation>();
            var entityMappings = new Dictionary<string, List<string>>();
            var warnings = new List<string>();

            // Create invoice entity
            var invoiceEntity = new KnowledgeEntity(ExtractedFact.EntityTypes.Invoice, dataRoomId);
            entities.Add(invoiceEntity);

            // Group facts by entity type for processing
            var factsByEntity = extraction.Facts.GroupBy(f => f.EntityType).ToList();

            foreach (var entityGroup in factsByEntity)
            {
                var entityType = entityGroup.Key;
                var entityFacts = entityGroup.ToList();

                KnowledgeEntity targetEntity;
                if (entityType == ExtractedFact.EntityTypes.Invoice)
                {
                    targetEntity = invoiceEntity;
                }
                else
                {
                    // Create new entity for vendors, line items, etc.
                    targetEntity = new KnowledgeEntity(entityType, dataRoomId);
                    entities.Add(targetEntity);

                    // Create relation to invoice
                    var relation = new EntityRelation(
                        targetEntity.Id, 
                        invoiceEntity.Id, 
                        $"{entityType}_to_invoice", 
                        0.9
                    );
                    relations.Add(relation);
                }

                // Map facts to entity attributes
                foreach (var fact in entityFacts)
                {
                    try
                    {
                        var validationResult = await ValidateFactAsync(fact);
                        if (!validationResult.IsSuccess)
                        {
                            warnings.Add($"Validation failed for {fact.AttributeName}: {validationResult.Error}");
                            continue;
                        }

                        var evidenceId = fact.EvidenceId;
                        var transformedValue = await TransformValueAsync(fact);
                        
                        targetEntity.SetAttribute(fact.AttributeName, transformedValue.Value, transformedValue.Confidence, evidenceId);
                        
                        // Track entity mapping
                        if (!entityMappings.ContainsKey(fact.FactId))
                            entityMappings[fact.FactId] = new List<string>();
                        entityMappings[fact.FactId].Add(targetEntity.Id);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to map fact {fact.FactId}: {ex.Message}");
                    }
                }
            }

            var result = new MappingResult
            {
                Entities = entities,
                Relations = relations,
                EntityMappings = entityMappings,
                Warnings = warnings,
                Statistics = new Dictionary<string, object>
                {
                    ["EntitiesCreated"] = entities.Count,
                    ["RelationsCreated"] = relations.Count,
                    ["FactsMapped"] = entityMappings.Count,
                    ["MappingConfidence"] = CalculateMappingConfidence(extraction.Facts)
                }
            };

            return Result<MappingResult>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<MappingResult>.Failure($"Invoice mapping failed: {ex.Message}", ex);
        }
    }

    public bool CanMap(string documentType) => _info.SupportedTypes.Contains(documentType);

    public MapperInfo GetInfo() => _info;

    private async Task<Result> ValidateFactAsync(ExtractedFact fact)
    {
        if (!_info.FieldMappings.TryGetValue(fact.AttributeName, out var mapping))
        {
            return Result.Success(); // No validation rules defined
        }

        foreach (var rule in mapping.ValidationRules)
        {
            var isValid = rule.Type switch
            {
                "regex" => ValidateRegex(fact.Value.ToString() ?? "", rule.Pattern),
                "range" => ValidateRange(fact.Value, rule.MinValue, rule.MaxValue),
                "enum" => ValidateEnum(fact.Value.ToString() ?? "", rule.AllowedValues),
                _ => true
            };

            if (!isValid)
            {
                return Result.Failure(rule.ErrorMessage);
            }
        }

        return Result.Success();
    }

    private async Task<(object Value, double Confidence)> TransformValueAsync(ExtractedFact fact)
    {
        // Apply any transformations based on field mapping configuration
        if (_info.FieldMappings.TryGetValue(fact.AttributeName, out var mapping))
        {
            // Apply data type conversion
            var convertedValue = ConvertValue(fact.Value, mapping.DataType);
            return (convertedValue, fact.Confidence);
        }

        return (fact.Value, fact.Confidence);
    }

    private static bool ValidateRegex(string value, string pattern)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
        }
        catch
        {
            return false;
        }
    }

    private static bool ValidateRange(object value, object? minValue, object? maxValue)
    {
        if (value is decimal decimalValue)
        {
            var min = minValue as decimal? ?? decimal.MinValue;
            var max = maxValue as decimal? ?? decimal.MaxValue;
            return decimalValue >= min && decimalValue <= max;
        }

        return true; // Skip validation if not numeric
    }

    private static bool ValidateEnum(string value, List<string> allowedValues)
    {
        return allowedValues.Count == 0 || allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static object ConvertValue(object value, string? dataType)
    {
        return dataType switch
        {
            "decimal" when value is string str => decimal.TryParse(str, out var d) ? d : value,
            "int" when value is string str => int.TryParse(str, out var i) ? i : value,
            "datetime" when value is string str => DateTime.TryParse(str, out var dt) ? dt : value,
            _ => value
        };
    }

    private static double CalculateMappingConfidence(List<ExtractedFact> facts)
    {
        if (facts.Count == 0) return 0.0;
        return facts.Average(f => f.Confidence);
    }
}