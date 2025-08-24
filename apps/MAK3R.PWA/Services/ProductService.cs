using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using MAK3R.PWA.Models;

namespace MAK3R.PWA.Services
{
    public class ProductService : IProductService
    {
        private readonly List<Product> _products = new();
        private readonly ILogger<ProductService> _logger;

        public ProductService(ILogger<ProductService> logger)
        {
            _logger = logger;
            InitializeSampleData();
        }

        public async Task<List<Product>> GetProductsAsync(ProductFilter? filter = null)
        {
            await Task.Delay(10); // Simulate async operation
            
            var query = _products.AsQueryable();

            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    var searchLower = filter.SearchTerm.ToLower();
                    query = query.Where(p => 
                        p.Name.ToLower().Contains(searchLower) ||
                        p.Sku.ToLower().Contains(searchLower) ||
                        p.Description.ToLower().Contains(searchLower));
                }

                if (filter.Category.HasValue)
                    query = query.Where(p => p.Category == filter.Category.Value);

                if (filter.IsActive.HasValue)
                    query = query.Where(p => p.IsActive == filter.IsActive.Value);

                if (filter.HasDigitalTwin.HasValue)
                    query = query.Where(p => p.HasDigitalTwin == filter.HasDigitalTwin.Value);

                if (filter.TwinStatus.HasValue)
                    query = query.Where(p => p.TwinStatus == filter.TwinStatus.Value);

                if (filter.MinPrice.HasValue)
                    query = query.Where(p => p.Price >= filter.MinPrice.Value);

                if (filter.MaxPrice.HasValue)
                    query = query.Where(p => p.Price <= filter.MaxPrice.Value);

                if (filter.Tags?.Any() == true)
                    query = query.Where(p => filter.Tags.Any(tag => p.Tags.Contains(tag)));

                // Sorting
                query = filter.SortBy?.ToLower() switch
                {
                    "name" => filter.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                    "price" => filter.SortDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                    "createdat" => filter.SortDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
                    "sku" => filter.SortDescending ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
                    _ => query.OrderBy(p => p.Name)
                };

                // Pagination
                if (filter.PageSize > 0)
                {
                    query = query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize);
                }
            }

            return query.ToList();
        }

        public async Task<Product?> GetProductByIdAsync(string id)
        {
            await Task.Delay(10);
            return _products.FirstOrDefault(p => p.Id == id);
        }

        public async Task<Product?> GetProductBySkuAsync(string sku)
        {
            await Task.Delay(10);
            return _products.FirstOrDefault(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            await Task.Delay(10);
            
            // Validate unique SKU
            if (_products.Any(p => p.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Product with SKU '{product.Sku}' already exists.");
            }

            product.Id = Guid.NewGuid().ToString();
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            
            _products.Add(product);
            
            _logger.LogInformation("Created product {ProductName} with SKU {Sku}", product.Name, product.Sku);
            
            return product;
        }

        public async Task<Product> UpdateProductAsync(Product product)
        {
            await Task.Delay(10);
            
            var existing = _products.FirstOrDefault(p => p.Id == product.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Product with ID '{product.Id}' not found.");
            }

            // Check for SKU conflicts with other products
            var conflictingProduct = _products.FirstOrDefault(p => 
                p.Id != product.Id && 
                p.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase));
                
            if (conflictingProduct != null)
            {
                throw new InvalidOperationException($"Another product already uses SKU '{product.Sku}'.");
            }

            // Update properties
            existing.Name = product.Name;
            existing.Sku = product.Sku;
            existing.Description = product.Description;
            existing.Price = product.Price;
            existing.ImageUrl = product.ImageUrl;
            existing.IsActive = product.IsActive;
            existing.Category = product.Category;
            existing.Tags = product.Tags;
            existing.Metadata = product.Metadata;
            existing.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Updated product {ProductName} with SKU {Sku}", existing.Name, existing.Sku);
            
            return existing;
        }

        public async Task<bool> DeleteProductAsync(string id)
        {
            await Task.Delay(10);
            
            var product = _products.FirstOrDefault(p => p.Id == id);
            if (product == null)
                return false;

            _products.Remove(product);
            
            _logger.LogInformation("Deleted product {ProductName} with SKU {Sku}", product.Name, product.Sku);
            
            return true;
        }

        public async Task<ProductImportResult> ImportProductsAsync(Stream fileStream, string fileName)
        {
            await Task.Delay(100); // Simulate processing time
            
            var result = new ProductImportResult();
            
            try
            {
                var extension = Path.GetExtension(fileName).ToLower();
                
                if (extension == ".csv")
                {
                    using var reader = new StreamReader(fileStream);
                    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                    
                    var records = csv.GetRecords<ProductImportDto>();
                    
                    foreach (var record in records)
                    {
                        result.TotalRecords++;
                        
                        try
                        {
                            // Check if product already exists
                            if (_products.Any(p => p.Sku.Equals(record.Sku, StringComparison.OrdinalIgnoreCase)))
                            {
                                result.Errors.Add($"Row {result.TotalRecords}: Product with SKU '{record.Sku}' already exists");
                                result.ErrorCount++;
                                continue;
                            }
                            
                            var product = new Product
                            {
                                Name = record.Name ?? $"Product {result.TotalRecords}",
                                Sku = record.Sku ?? $"SKU-{Guid.NewGuid().ToString()[..8]}",
                                Description = record.Description ?? "",
                                Price = record.Price ?? 0,
                                IsActive = record.IsActive ?? true,
                                Category = Enum.TryParse<ProductCategory>(record.Category, out var cat) ? cat : ProductCategory.Manufacturing,
                                Tags = string.IsNullOrEmpty(record.Tags) ? new List<string>() : record.Tags.Split(',').Select(t => t.Trim()).ToList()
                            };
                            
                            _products.Add(product);
                            result.ImportedProducts.Add(product);
                            result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Row {result.TotalRecords}: {ex.Message}");
                            result.ErrorCount++;
                        }
                    }
                }
                else if (extension == ".json")
                {
                    using var reader = new StreamReader(fileStream);
                    var json = await reader.ReadToEndAsync();
                    var products = JsonSerializer.Deserialize<List<Product>>(json);
                    
                    if (products != null)
                    {
                        foreach (var product in products)
                        {
                            result.TotalRecords++;
                            
                            try
                            {
                                if (_products.Any(p => p.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase)))
                                {
                                    result.Errors.Add($"Product {result.TotalRecords}: SKU '{product.Sku}' already exists");
                                    result.ErrorCount++;
                                    continue;
                                }
                                
                                product.Id = Guid.NewGuid().ToString();
                                product.CreatedAt = DateTime.UtcNow;
                                product.UpdatedAt = DateTime.UtcNow;
                                
                                _products.Add(product);
                                result.ImportedProducts.Add(product);
                                result.SuccessCount++;
                            }
                            catch (Exception ex)
                            {
                                result.Errors.Add($"Product {result.TotalRecords}: {ex.Message}");
                                result.ErrorCount++;
                            }
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException($"File format '{extension}' is not supported. Use .csv or .json files.");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Import failed: {ex.Message}");
                result.ErrorCount++;
            }
            
            _logger.LogInformation("Imported {SuccessCount} products, {ErrorCount} errors", result.SuccessCount, result.ErrorCount);
            
            return result;
        }

        public async Task<byte[]> ExportProductsAsync(string format = "csv")
        {
            await Task.Delay(50); // Simulate processing time
            
            if (format.ToLower() == "csv")
            {
                using var stream = new MemoryStream();
                using var writer = new StreamWriter(stream);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                
                var exportData = _products.Select(p => new ProductExportDto
                {
                    Name = p.Name,
                    Sku = p.Sku,
                    Description = p.Description,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    Category = p.Category.ToString(),
                    Tags = string.Join(", ", p.Tags),
                    CreatedAt = p.CreatedAt,
                    HasDigitalTwin = p.HasDigitalTwin,
                    TwinStatus = p.TwinStatus.ToString()
                });
                
                csv.WriteRecords(exportData);
                writer.Flush();
                return stream.ToArray();
            }
            else if (format.ToLower() == "json")
            {
                var json = JsonSerializer.Serialize(_products, new JsonSerializerOptions { WriteIndented = true });
                return Encoding.UTF8.GetBytes(json);
            }
            
            throw new NotSupportedException($"Export format '{format}' is not supported.");
        }

        public async Task<List<string>> GetTagsAsync()
        {
            await Task.Delay(10);
            return _products.SelectMany(p => p.Tags).Distinct().OrderBy(t => t).ToList();
        }

        public async Task<Dictionary<ProductCategory, int>> GetCategoryStatsAsync()
        {
            await Task.Delay(10);
            return _products
                .GroupBy(p => p.Category)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<bool> CreateDigitalSkeletonAsync(string productId)
        {
            await Task.Delay(100); // Simulate skeleton creation time
            
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product == null)
                return false;
                
            product.DigitalTwinId = $"twin-{Guid.NewGuid().ToString()[..8]}";
            product.TwinStatus = DigitalTwinStatus.Skeleton;
            product.LastSyncAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Created digital skeleton for product {ProductName}", product.Name);
            
            return true;
        }

        public async Task<bool> UpgradeToDigitalTwinAsync(string productId)
        {
            await Task.Delay(200); // Simulate upgrade time
            
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product == null || !product.HasDigitalTwin)
                return false;
                
            product.TwinStatus = product.TwinStatus switch
            {
                DigitalTwinStatus.Skeleton => DigitalTwinStatus.Partial,
                DigitalTwinStatus.Partial => DigitalTwinStatus.Complete,
                DigitalTwinStatus.Complete => DigitalTwinStatus.Enhanced,
                _ => product.TwinStatus
            };
            
            product.LastSyncAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Upgraded digital twin for product {ProductName} to {Status}", 
                product.Name, product.TwinStatus);
            
            return true;
        }

        public async Task<bool> SyncDigitalTwinAsync(string productId)
        {
            await Task.Delay(50); // Simulate sync time
            
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product == null || !product.HasDigitalTwin)
                return false;
                
            product.LastSyncAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Synced digital twin for product {ProductName}", product.Name);
            
            return true;
        }

        private void InitializeSampleData()
        {
            _products.AddRange(new[]
            {
                new Product
                {
                    Name = "CNC Milling Machine MX-2000",
                    Sku = "CNC-MX2000",
                    Price = 45000,
                    Description = "High precision 3-axis CNC milling machine with automatic tool changer and digital control system",
                    IsActive = true,
                    Category = ProductCategory.Machinery,
                    Tags = new List<string> { "CNC", "Milling", "Precision", "Manufacturing" },
                    DigitalTwinId = "twin-cnc001",
                    TwinStatus = DigitalTwinStatus.Complete,
                    LastSyncAt = DateTime.UtcNow.AddMinutes(-5)
                },
                new Product
                {
                    Name = "Hydraulic Press HP-500",
                    Sku = "PRESS-HP500",
                    Price = 32000,
                    Description = "Heavy duty 500-ton hydraulic press for metal forming and stamping operations",
                    IsActive = true,
                    Category = ProductCategory.Machinery,
                    Tags = new List<string> { "Hydraulic", "Press", "Metal Forming" },
                    DigitalTwinId = "twin-press001",
                    TwinStatus = DigitalTwinStatus.Partial,
                    LastSyncAt = DateTime.UtcNow.AddMinutes(-15)
                },
                new Product
                {
                    Name = "IoT Quality Sensor Kit",
                    Sku = "QSK-IOT-001",
                    Price = 1200,
                    Description = "Comprehensive IoT sensor package for real-time quality monitoring including temperature, vibration, and pressure sensors",
                    IsActive = false,
                    Category = ProductCategory.Components,
                    Tags = new List<string> { "IoT", "Sensors", "Quality", "Monitoring" },
                    TwinStatus = DigitalTwinStatus.None
                },
                new Product
                {
                    Name = "PLC Automation Controller AC-Pro",
                    Sku = "PLC-ACPRO",
                    Price = 8500,
                    Description = "Advanced programmable logic controller for manufacturing automation with Ethernet connectivity",
                    IsActive = true,
                    Category = ProductCategory.Components,
                    Tags = new List<string> { "PLC", "Automation", "Control", "Ethernet" },
                    DigitalTwinId = "twin-plc001",
                    TwinStatus = DigitalTwinStatus.Skeleton,
                    LastSyncAt = DateTime.UtcNow.AddHours(-2)
                },
                new Product
                {
                    Name = "MAK3R Analytics Software",
                    Sku = "SOFT-MAS-001",
                    Price = 15000,
                    Description = "Comprehensive analytics software for manufacturing data visualization and predictive maintenance",
                    IsActive = true,
                    Category = ProductCategory.Software,
                    Tags = new List<string> { "Software", "Analytics", "Predictive", "Visualization" },
                    DigitalTwinId = "twin-soft001",
                    TwinStatus = DigitalTwinStatus.Enhanced,
                    LastSyncAt = DateTime.UtcNow.AddMinutes(-1)
                }
            });
        }

        // DTO classes for import/export
        public class ProductImportDto
        {
            public string? Name { get; set; }
            public string? Sku { get; set; }
            public string? Description { get; set; }
            public decimal? Price { get; set; }
            public bool? IsActive { get; set; }
            public string? Category { get; set; }
            public string? Tags { get; set; }
        }

        public class ProductExportDto
        {
            public string Name { get; set; } = "";
            public string Sku { get; set; } = "";
            public string Description { get; set; } = "";
            public decimal Price { get; set; }
            public bool IsActive { get; set; }
            public string Category { get; set; } = "";
            public string Tags { get; set; } = "";
            public DateTime CreatedAt { get; set; }
            public bool HasDigitalTwin { get; set; }
            public string TwinStatus { get; set; } = "";
        }
    }
}