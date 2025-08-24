using System.ComponentModel.DataAnnotations;

namespace MAK3R.PWA.Models
{
    public class Product
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "SKU is required")]
        [StringLength(50, ErrorMessage = "SKU cannot exceed 50 characters")]
        public string Sku { get; set; } = string.Empty;
        
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Price must be non-negative")]
        public decimal Price { get; set; }
        
        public string ImageUrl { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public ProductCategory Category { get; set; } = ProductCategory.Manufacturing;
        
        public List<string> Tags { get; set; } = new();
        
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        // Digital twin connection properties
        public string? DigitalTwinId { get; set; }
        public bool HasDigitalTwin => !string.IsNullOrEmpty(DigitalTwinId);
        public DateTime? LastSyncAt { get; set; }
        public DigitalTwinStatus TwinStatus { get; set; } = DigitalTwinStatus.Skeleton;
    }

    public enum ProductCategory
    {
        Manufacturing,
        Machinery,
        Components,
        Software,
        Services,
        Consumables
    }

    public enum DigitalTwinStatus
    {
        None,           // No digital twin
        Skeleton,       // Basic connected skeleton
        Partial,        // Some features implemented
        Complete,       // Full digital twin
        Enhanced        // AI-enhanced twin
    }

    public class ProductFilter
    {
        public string? SearchTerm { get; set; }
        public ProductCategory? Category { get; set; }
        public bool? IsActive { get; set; }
        public bool? HasDigitalTwin { get; set; }
        public DigitalTwinStatus? TwinStatus { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public List<string>? Tags { get; set; }
        public string SortBy { get; set; } = "Name";
        public bool SortDescending { get; set; } = false;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class ProductImportResult
    {
        public int TotalRecords { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<Product> ImportedProducts { get; set; } = new();
    }
}