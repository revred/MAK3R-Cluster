using System.Text;
using System.Text.Json;

namespace MAK3R.UnitTests.TestHelpers;

public static class JsonTestDataHelper
{
    public static MemoryStream CreateValidProductJson()
    {
        var products = new[]
        {
            new
            {
                Name = "Test CNC Machine",
                SKU = "CNC-TEST-001",
                Price = 45000.00m,
                Description = "High precision test CNC machine",
                Active = true,
                Category = "Machinery"
            },
            new
            {
                Name = "Test Hydraulic Press",
                SKU = "PRESS-TEST-001",
                Price = 32000.50m,
                Description = "Industrial test hydraulic press",
                Active = true,
                Category = "Machinery"
            },
            new
            {
                Name = "Test Sensor Kit",
                SKU = "SENSOR-TEST-001",
                Price = 1200.00m,
                Description = "IoT sensor kit for testing",
                Active = false,
                Category = "Components"
            }
        };

        var json = JsonSerializer.Serialize(products, new JsonSerializerOptions { WriteIndented = true });
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public static MemoryStream CreateValidMachineJson()
    {
        var machines = new[]
        {
            new
            {
                Machine_Name = "CNC Mill 001",
                Model = "XYZ-3000",
                Serial_Number = "SN123456",
                Location = "Factory Floor A",
                Status = "Running",
                Last_Maintenance = "2023-12-01"
            },
            new
            {
                Machine_Name = "Hydraulic Press 002",
                Model = "ABC-500",
                Serial_Number = "SN789012",
                Location = "Factory Floor B",
                Status = "Maintenance",
                Last_Maintenance = "2023-11-15"
            }
        };

        var json = JsonSerializer.Serialize(machines, new JsonSerializerOptions { WriteIndented = true });
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public static MemoryStream CreateSingleObjectJson()
    {
        var product = new
        {
            Name = "Single Test Product",
            SKU = "SINGLE-001",
            Price = 100.00m,
            Description = "Single product object test",
            Active = true
        };

        var json = JsonSerializer.Serialize(product, new JsonSerializerOptions { WriteIndented = true });
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public static MemoryStream CreateNestedJson()
    {
        var data = new[]
        {
            new
            {
                Product = new
                {
                    Name = "Nested Product",
                    SKU = "NESTED-001"
                },
                Details = new
                {
                    Price = 100.00m,
                    Description = "Product with nested structure"
                }
            }
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public static MemoryStream CreateJsonWithMixedTypes()
    {
        var data = new[]
        {
            new Dictionary<string, object>
            {
                { "Name", "Mixed Type Product" },
                { "SKU", "MIXED-001" },
                { "Price", 100.50 },
                { "InStock", true },
                { "Tags", new[] { "tag1", "tag2" } },
                { "Metadata", new { Color = "Red", Weight = 5.5 } }
            }
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public static MemoryStream CreateInvalidJson()
    {
        var invalidJson = @"{
            ""Name"": ""Invalid JSON"",
            ""SKU"": ""INVALID-001"",
            ""Price"": 100.00,
            ""Description"": ""This JSON is missing a closing brace""
        ";

        return new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));
    }

    public static MemoryStream CreateEmptyArrayJson()
    {
        var json = "[]";
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public static MemoryStream CreateJsonWithNullValues()
    {
        var products = new object[]
        {
            new
            {
                Name = "Product with Nulls",
                SKU = "NULL-001",
                Price = (decimal?)null,
                Description = (string?)null,
                Active = true
            },
            new
            {
                Name = (string?)null,
                SKU = "NULL-002",
                Price = 200.00m,
                Description = "Another product with null name",
                Active = (bool?)null
            }
        };

        var json = JsonSerializer.Serialize(products, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        });
        
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public static MemoryStream CreateLargeJson(int recordCount = 1000)
    {
        var products = new List<object>();
        
        for (int i = 1; i <= recordCount; i++)
        {
            products.Add(new
            {
                Name = $"Generated Product {i:D4}",
                SKU = $"GEN-{i:D6}",
                Price = i * 10.5m,
                Description = $"Auto-generated test product {i}",
                Active = i % 2 == 0,
                Category = i % 3 == 0 ? "Machinery" : "Components"
            });
        }

        var json = JsonSerializer.Serialize(products, new JsonSerializerOptions { WriteIndented = false });
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    public static MemoryStream CreateJsonWithSpecialCharacters()
    {
        var products = new[]
        {
            new
            {
                Name = "Product with \"quotes\"",
                SKU = "QUOTE-001",
                Price = 100.00m,
                Description = "Description with \"quotes\" and special chars: éñü"
            },
            new
            {
                Name = "Product with unicode: 中文",
                SKU = "UNICODE-001",
                Price = 200.00m,
                Description = "Unicode description: 中文 한글 العربية"
            },
            new
            {
                Name = "Product with\nnewline",
                SKU = "NEWLINE-001",
                Price = 300.00m,
                Description = "Description with\nmultiple\nlines"
            }
        };

        var json = JsonSerializer.Serialize(products, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }
}