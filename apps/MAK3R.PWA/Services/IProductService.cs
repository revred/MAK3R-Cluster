using MAK3R.PWA.Models;

namespace MAK3R.PWA.Services
{
    public interface IProductService
    {
        Task<List<Product>> GetProductsAsync(ProductFilter? filter = null);
        Task<Product?> GetProductByIdAsync(string id);
        Task<Product?> GetProductBySkuAsync(string sku);
        Task<Product> CreateProductAsync(Product product);
        Task<Product> UpdateProductAsync(Product product);
        Task<bool> DeleteProductAsync(string id);
        Task<ProductImportResult> ImportProductsAsync(Stream fileStream, string fileName);
        Task<byte[]> ExportProductsAsync(string format = "csv");
        Task<List<string>> GetTagsAsync();
        Task<Dictionary<ProductCategory, int>> GetCategoryStatsAsync();
        
        // Digital twin methods
        Task<bool> CreateDigitalSkeletonAsync(string productId);
        Task<bool> UpgradeToDigitalTwinAsync(string productId);
        Task<bool> SyncDigitalTwinAsync(string productId);
    }
}