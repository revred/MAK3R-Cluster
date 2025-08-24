namespace MAK3R.PWA.Services
{
    public interface IFileIngestionService
    {
        Task<FileAnalysisResult> AnalyzeFileAsync(Stream fileStream, string fileName);
        Task<SchemaInferenceResult> InferSchemaAsync(Stream fileStream, string fileName);
        Task<DataImportResult> ImportDataAsync(Stream fileStream, string fileName, ImportMapping mapping);
        Task<List<string>> GetSupportedFormatsAsync();
        Task<List<ImportTemplate>> GetImportTemplatesAsync();
    }

    public class FileAnalysisResult
    {
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int EstimatedRecordCount { get; set; }
        public List<string> DetectedColumns { get; set; } = new();
        public Dictionary<string, DataType> ColumnTypes { get; set; } = new();
        public List<string> SampleData { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public bool IsValid { get; set; } = true;
    }

    public class SchemaInferenceResult
    {
        public List<SchemaField> Fields { get; set; } = new();
        public List<SuggestedMapping> SuggestedMappings { get; set; } = new();
        public DataEntityType RecommendedEntityType { get; set; }
        public double ConfidenceScore { get; set; }
        public List<string> ValidationWarnings { get; set; } = new();
    }

    public class SchemaField
    {
        public string Name { get; set; } = string.Empty;
        public DataType Type { get; set; }
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
        public object? DefaultValue { get; set; }
        public List<object> SampleValues { get; set; } = new();
        public int NullCount { get; set; }
        public string? Pattern { get; set; }
    }

    public class SuggestedMapping
    {
        public string SourceField { get; set; } = string.Empty;
        public string TargetField { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public List<string> AlternativeTargets { get; set; } = new();
        public string? TransformationHint { get; set; }
    }

    public class ImportMapping
    {
        public DataEntityType EntityType { get; set; }
        public Dictionary<string, string> FieldMappings { get; set; } = new();
        public Dictionary<string, object> DefaultValues { get; set; } = new();
        public List<string> RequiredFields { get; set; } = new();
        public bool SkipFirstRow { get; set; } = true;
        public string? DateFormat { get; set; }
        public string? NumberFormat { get; set; }
    }

    public class DataImportResult
    {
        public int TotalRecords { get; set; }
        public int SuccessfulImports { get; set; }
        public int Errors { get; set; }
        public List<ImportError> ErrorDetails { get; set; } = new();
        public List<object> ImportedData { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
    }

    public class ImportError
    {
        public int RowNumber { get; set; }
        public string Field { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string Severity { get; set; } = "Error";
    }

    public class ImportTemplate
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DataEntityType EntityType { get; set; }
        public ImportMapping Mapping { get; set; } = new();
        public List<string> SampleHeaders { get; set; } = new();
    }

    public enum DataType
    {
        String,
        Integer,
        Decimal,
        Boolean,
        DateTime,
        Email,
        Url,
        Currency,
        Percentage,
        Unknown
    }

    public enum DataEntityType
    {
        Product,
        Customer,
        Order,
        Inventory,
        Machine,
        Sensor,
        Transaction,
        Unknown
    }
}