using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using MAK3R.PWA.Services;
using MAK3R.PWA.Models;

namespace MAK3R.UnitTests.Services;

public class FileIngestionServiceTests
{
    private readonly Mock<ILogger<FileIngestionService>> _loggerMock;
    private readonly Mock<IProductService> _productServiceMock;
    private readonly FileIngestionService _fileIngestionService;

    public FileIngestionServiceTests()
    {
        _loggerMock = new Mock<ILogger<FileIngestionService>>();
        _productServiceMock = new Mock<IProductService>();
        _fileIngestionService = new FileIngestionService(_loggerMock.Object, _productServiceMock.Object);
    }

    [Fact]
    public async Task GetSupportedFormatsAsync_ShouldReturnExpectedFormats()
    {
        // Act
        var result = await _fileIngestionService.GetSupportedFormatsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(".csv");
        result.Should().Contain(".json");
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetImportTemplatesAsync_ShouldReturnPredefinedTemplates()
    {
        // Act
        var result = await _fileIngestionService.GetImportTemplatesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        
        var productTemplate = result.FirstOrDefault(t => t.EntityType == DataEntityType.Product);
        productTemplate.Should().NotBeNull();
        productTemplate!.Name.Should().Be("Standard Product Import");
        productTemplate.SampleHeaders.Should().Contain("Name");
        productTemplate.SampleHeaders.Should().Contain("SKU");
        productTemplate.Mapping.RequiredFields.Should().Contain("Name");
        productTemplate.Mapping.RequiredFields.Should().Contain("SKU");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithValidCsvFile_ShouldReturnValidAnalysis()
    {
        // Arrange
        var csvContent = "Name,SKU,Price,Description\nTest Product,TEST-001,100.50,Test description\nAnother Product,TEST-002,200.00,Another description";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileName = "test.csv";

        // Act
        var result = await _fileIngestionService.AnalyzeFileAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(fileName);
        result.FileType.Should().Be(".csv");
        result.IsValid.Should().BeTrue();
        result.EstimatedRecordCount.Should().Be(2);
        result.DetectedColumns.Should().HaveCount(4);
        result.DetectedColumns.Should().Contain("Name");
        result.DetectedColumns.Should().Contain("SKU");
        result.DetectedColumns.Should().Contain("Price");
        result.DetectedColumns.Should().Contain("Description");
        result.ColumnTypes["Name"].Should().Be(DataType.String);
        result.ColumnTypes["Price"].Should().Be(DataType.Decimal);
        result.SampleData.Should().HaveCountGreaterThan(0);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithValidJsonFile_ShouldReturnValidAnalysis()
    {
        // Arrange
        var jsonContent = @"[
            {""Name"": ""Test Product"", ""SKU"": ""TEST-001"", ""Price"": 100.50, ""Description"": ""Test description""},
            {""Name"": ""Another Product"", ""SKU"": ""TEST-002"", ""Price"": 200.00, ""Description"": ""Another description""}
        ]";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
        var fileName = "test.json";

        // Act
        var result = await _fileIngestionService.AnalyzeFileAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(fileName);
        result.FileType.Should().Be(".json");
        result.IsValid.Should().BeTrue();
        result.EstimatedRecordCount.Should().Be(2);
        result.DetectedColumns.Should().HaveCount(4);
        result.DetectedColumns.Should().Contain("Name");
        result.DetectedColumns.Should().Contain("SKU");
        result.DetectedColumns.Should().Contain("Price");
        result.DetectedColumns.Should().Contain("Description");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithUnsupportedFormat_ShouldReturnInvalidResult()
    {
        // Arrange
        var content = "Some content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileName = "test.txt";

        // Act
        var result = await _fileIngestionService.AnalyzeFileAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(fileName);
        result.FileType.Should().Be(".txt");
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Contains("Unsupported file type"));
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithEmptyCsvFile_ShouldReturnInvalidResult()
    {
        // Arrange
        var csvContent = "Name,SKU,Price,Description"; // Header only, no data rows
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileName = "empty.csv";

        // Act
        var result = await _fileIngestionService.AnalyzeFileAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Contains("No data rows found"));
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithInvalidJsonFile_ShouldReturnInvalidResult()
    {
        // Arrange
        var invalidJsonContent = @"{invalid json content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidJsonContent));
        var fileName = "invalid.json";

        // Act
        var result = await _fileIngestionService.AnalyzeFileAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Contains("Invalid JSON format"));
    }

    [Fact]
    public async Task InferSchemaAsync_WithValidCsvFile_ShouldReturnSchemaInference()
    {
        // Arrange
        var csvContent = @"Product_Name,Product_SKU,Unit_Price,Product_Description,Active
Test CNC Machine,CNC-001,45000.00,High precision CNC machine,true
Test Press,PRESS-001,32000.50,Industrial hydraulic press,false";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileName = "products.csv";

        // Act
        var result = await _fileIngestionService.InferSchemaAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
        result.Fields.Should().HaveCount(5);
        result.RecommendedEntityType.Should().Be(DataEntityType.Product);
        result.ConfidenceScore.Should().BeGreaterThan(0);
        
        var nameField = result.Fields.FirstOrDefault(f => f.Name == "Product_Name");
        nameField.Should().NotBeNull();
        nameField!.Type.Should().Be(DataType.String);
        
        var priceField = result.Fields.FirstOrDefault(f => f.Name == "Unit_Price");
        priceField.Should().NotBeNull();
        priceField!.Type.Should().Be(DataType.Decimal);
        
        var activeField = result.Fields.FirstOrDefault(f => f.Name == "Active");
        activeField.Should().NotBeNull();
        activeField!.Type.Should().Be(DataType.Boolean);

        result.SuggestedMappings.Should().HaveCountGreaterThan(0);
        result.SuggestedMappings.Should().Contain(m => m.TargetField == "Name");
        result.SuggestedMappings.Should().Contain(m => m.TargetField == "Sku");
        result.SuggestedMappings.Should().Contain(m => m.TargetField == "Price");
    }

    [Fact]
    public async Task InferSchemaAsync_WithMachineData_ShouldInferMachineEntityType()
    {
        // Arrange
        var csvContent = @"Equipment_ID,Model_Number,Serial_Number,Location,Status
CNC Mill 001,XYZ-3000,SN123456,Factory Floor A,Running
Press 002,ABC-500,SN789012,Factory Floor B,Maintenance";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileName = "machines.csv";

        // Act
        var result = await _fileIngestionService.InferSchemaAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
        result.RecommendedEntityType.Should().Be(DataEntityType.Machine);
        result.Fields.Should().HaveCount(5);
        result.ConfidenceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ImportDataAsync_WithValidCsvProductData_ShouldImportSuccessfully()
    {
        // Arrange
        var csvContent = @"Name,SKU,Price,Description,Active
Test Product 1,TEST-001,100.50,Test description 1,true
Test Product 2,TEST-002,200.00,Test description 2,false";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileName = "products.csv";
        
        var mapping = new ImportMapping
        {
            EntityType = DataEntityType.Product,
            FieldMappings = new Dictionary<string, string>
            {
                { "Name", "Name" },
                { "SKU", "Sku" },
                { "Price", "Price" },
                { "Description", "Description" },
                { "Active", "IsActive" }
            },
            RequiredFields = new List<string> { "Name", "SKU" },
            SkipFirstRow = true
        };

        _productServiceMock.Setup(x => x.CreateProductAsync(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);

        // Act
        var result = await _fileIngestionService.ImportDataAsync(stream, fileName, mapping);

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(2);
        result.SuccessfulImports.Should().Be(2);
        result.Errors.Should().Be(0);
        result.ImportedData.Should().HaveCount(2);
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);

        _productServiceMock.Verify(x => x.CreateProductAsync(It.IsAny<Product>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ImportDataAsync_WithDuplicateSkus_ShouldReportErrors()
    {
        // Arrange
        var csvContent = @"Name,SKU,Price,Description
Valid Product,VALID-001,100.00,Valid product
Duplicate SKU Product,DUPLICATE-SKU,200.00,This will fail";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileName = "products.csv";
        
        var mapping = new ImportMapping
        {
            EntityType = DataEntityType.Product,
            FieldMappings = new Dictionary<string, string>
            {
                { "Name", "Name" },
                { "SKU", "Sku" },
                { "Price", "Price" },
                { "Description", "Description" }
            },
            RequiredFields = new List<string> { "Name", "SKU" },
            SkipFirstRow = true
        };

        _productServiceMock.SetupSequence(x => x.CreateProductAsync(It.IsAny<Product>()))
            .ReturnsAsync(new Product()) // First call succeeds
            .ThrowsAsync(new InvalidOperationException("Product with SKU 'DUPLICATE-SKU' already exists.")); // Second call fails

        // Act
        var result = await _fileIngestionService.ImportDataAsync(stream, fileName, mapping);

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(2);
        result.SuccessfulImports.Should().Be(1);
        result.Errors.Should().Be(1);
        result.ErrorDetails.Should().HaveCount(1);
        result.ErrorDetails.First().Error.Should().Contain("DUPLICATE-SKU");
    }

    [Fact]
    public async Task ImportDataAsync_WithMissingRequiredFields_ShouldReportErrors()
    {
        // Arrange
        var csvContent = @"Name,SKU,Price,Description
Valid Product,VALID-001,100.00,Valid product
,MISSING-NAME,200.00,Missing name should fail
No SKU Product,,150.00,Missing SKU should fail";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileName = "products.csv";
        
        var mapping = new ImportMapping
        {
            EntityType = DataEntityType.Product,
            FieldMappings = new Dictionary<string, string>
            {
                { "Name", "Name" },
                { "SKU", "Sku" },
                { "Price", "Price" },
                { "Description", "Description" }
            },
            RequiredFields = new List<string> { "Name", "SKU" },
            SkipFirstRow = true
        };

        _productServiceMock.Setup(x => x.CreateProductAsync(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);

        // Act
        var result = await _fileIngestionService.ImportDataAsync(stream, fileName, mapping);

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(3);
        result.SuccessfulImports.Should().Be(1);
        result.Errors.Should().Be(2);
        result.ErrorDetails.Should().HaveCount(2);
        result.ErrorDetails.Should().Contain(e => e.Error.Contains("Required field"));
    }

    [Theory]
    [InlineData("123", DataType.Integer)]
    [InlineData("123.45", DataType.Decimal)]
    [InlineData("true", DataType.Boolean)]
    [InlineData("2023-12-25", DataType.DateTime)]
    [InlineData("test@example.com", DataType.Email)]
    [InlineData("https://example.com", DataType.Url)]
    [InlineData("$100.50", DataType.Currency)]
    [InlineData("25%", DataType.Percentage)]
    [InlineData("regular text", DataType.String)]
    public void InferDataType_WithVariousInputs_ShouldReturnCorrectType(string input, DataType expectedType)
    {
        // This test requires access to private method, so we'll test it indirectly through CSV analysis
        // Arrange
        var csvContent = $"TestColumn\n{input}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var fileName = "test.csv";

        // Act
        var result = _fileIngestionService.AnalyzeFileAsync(stream, fileName).Result;

        // Assert
        result.ColumnTypes["TestColumn"].Should().Be(expectedType);
    }

    [Fact]
    public async Task ImportDataAsync_WithUnsupportedFormat_ShouldReturnErrorResult()
    {
        // Arrange
        var content = "Some content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileName = "test.txt";
        var mapping = new ImportMapping();

        // Act
        var result = await _fileIngestionService.ImportDataAsync(stream, fileName, mapping);

        // Assert
        result.Should().NotBeNull();
        result.Errors.Should().Be(1);
        result.SuccessfulImports.Should().Be(0);
        result.ErrorDetails.Should().ContainSingle()
            .Which.Error.Should().Contain("Import not supported for .txt files");
    }

    [Fact]
    public async Task InferSchemaAsync_WithUnsupportedFormat_ShouldReturnWarning()
    {
        // Arrange
        var content = "Some content";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileName = "test.txt";

        // Act
        var result = await _fileIngestionService.InferSchemaAsync(stream, fileName);

        // Assert
        result.Should().NotBeNull();
        result.ValidationWarnings.Should().Contain(w => w.Contains("not supported"));
    }
}