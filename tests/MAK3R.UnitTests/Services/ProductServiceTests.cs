using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MAK3R.PWA.Services;
using MAK3R.PWA.Models;

namespace MAK3R.UnitTests.Services;

public class ProductServiceTests
{
    private readonly Mock<ILogger<ProductService>> _loggerMock;
    private readonly ProductService _productService;

    public ProductServiceTests()
    {
        _loggerMock = new Mock<ILogger<ProductService>>();
        _productService = new ProductService(_loggerMock.Object);
    }

    [Fact]
    public async Task GetProductsAsync_WithoutFilter_ShouldReturnAllProducts()
    {
        // Act
        var result = await _productService.GetProductsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(p => !string.IsNullOrEmpty(p.Id)).Should().BeTrue();
        result.All(p => !string.IsNullOrEmpty(p.Name)).Should().BeTrue();
        result.All(p => !string.IsNullOrEmpty(p.Sku)).Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_WithSearchFilter_ShouldReturnMatchingProducts()
    {
        // Arrange
        var filter = new ProductFilter { SearchTerm = "CNC" };

        // Act
        var result = await _productService.GetProductsAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.All(p => 
            p.Name.Contains("CNC", StringComparison.OrdinalIgnoreCase) ||
            p.Sku.Contains("CNC", StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains("CNC", StringComparison.OrdinalIgnoreCase)
        ).Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_WithCategoryFilter_ShouldReturnProductsOfSpecifiedCategory()
    {
        // Arrange
        var filter = new ProductFilter { Category = ProductCategory.Machinery };

        // Act
        var result = await _productService.GetProductsAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.All(p => p.Category == ProductCategory.Machinery).Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_WithActiveFilter_ShouldReturnOnlyActiveProducts()
    {
        // Arrange
        var filter = new ProductFilter { IsActive = true };

        // Act
        var result = await _productService.GetProductsAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.All(p => p.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_WithDigitalTwinFilter_ShouldReturnProductsWithDigitalTwins()
    {
        // Arrange
        var filter = new ProductFilter { HasDigitalTwin = true };

        // Act
        var result = await _productService.GetProductsAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.All(p => p.HasDigitalTwin).Should().BeTrue();
        result.All(p => !string.IsNullOrEmpty(p.DigitalTwinId)).Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_WithPriceRangeFilter_ShouldReturnProductsInRange()
    {
        // Arrange
        var filter = new ProductFilter { MinPrice = 1000, MaxPrice = 10000 };

        // Act
        var result = await _productService.GetProductsAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.All(p => p.Price >= 1000 && p.Price <= 10000).Should().BeTrue();
    }

    [Fact]
    public async Task GetProductsAsync_WithSorting_ShouldReturnSortedProducts()
    {
        // Arrange
        var filter = new ProductFilter { SortBy = "Price", SortDescending = false };

        // Act
        var result = await _productService.GetProductsAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeInAscendingOrder(p => p.Price);
    }

    [Fact]
    public async Task GetProductsAsync_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        var filter = new ProductFilter { Page = 1, PageSize = 2 };

        // Act
        var result = await _productService.GetProductsAsync(filter);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task GetProductByIdAsync_WithValidId_ShouldReturnProduct()
    {
        // Arrange
        var allProducts = await _productService.GetProductsAsync();
        var existingProduct = allProducts.First();

        // Act
        var result = await _productService.GetProductByIdAsync(existingProduct.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(existingProduct.Id);
        result.Name.Should().Be(existingProduct.Name);
        result.Sku.Should().Be(existingProduct.Sku);
    }

    [Fact]
    public async Task GetProductByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = "non-existent-id";

        // Act
        var result = await _productService.GetProductByIdAsync(invalidId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProductBySkuAsync_WithValidSku_ShouldReturnProduct()
    {
        // Arrange
        var allProducts = await _productService.GetProductsAsync();
        var existingProduct = allProducts.First();

        // Act
        var result = await _productService.GetProductBySkuAsync(existingProduct.Sku);

        // Assert
        result.Should().NotBeNull();
        result!.Sku.Should().Be(existingProduct.Sku);
        result.Name.Should().Be(existingProduct.Name);
    }

    [Fact]
    public async Task GetProductBySkuAsync_WithInvalidSku_ShouldReturnNull()
    {
        // Arrange
        var invalidSku = "NON-EXISTENT-SKU";

        // Act
        var result = await _productService.GetProductBySkuAsync(invalidSku);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateProductAsync_WithValidProduct_ShouldCreateAndReturnProduct()
    {
        // Arrange
        var newProduct = new Product
        {
            Name = "Test Product",
            Sku = "TEST-001",
            Description = "Test product description",
            Price = 100.50m,
            Category = ProductCategory.Components,
            IsActive = true
        };

        // Act
        var result = await _productService.CreateProductAsync(newProduct);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
        result.Name.Should().Be(newProduct.Name);
        result.Sku.Should().Be(newProduct.Sku);
        result.Price.Should().Be(newProduct.Price);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Verify product was added to collection
        var retrievedProduct = await _productService.GetProductByIdAsync(result.Id);
        retrievedProduct.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateProductAsync_WithDuplicateSku_ShouldThrowException()
    {
        // Arrange
        var allProducts = await _productService.GetProductsAsync();
        var existingProduct = allProducts.First();
        
        var duplicateProduct = new Product
        {
            Name = "Duplicate Product",
            Sku = existingProduct.Sku, // Same SKU as existing product
            Price = 200.00m
        };

        // Act & Assert
        var act = async () => await _productService.CreateProductAsync(duplicateProduct);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Product with SKU '{existingProduct.Sku}' already exists.");
    }

    [Fact]
    public async Task UpdateProductAsync_WithValidProduct_ShouldUpdateProduct()
    {
        // Arrange
        var allProducts = await _productService.GetProductsAsync();
        var existingProduct = allProducts.First();
        
        existingProduct.Name = "Updated Product Name";
        existingProduct.Price = 999.99m;
        existingProduct.Description = "Updated description";

        // Act
        var result = await _productService.UpdateProductAsync(existingProduct);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Product Name");
        result.Price.Should().Be(999.99m);
        result.Description.Should().Be("Updated description");
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Verify product was updated in collection
        var retrievedProduct = await _productService.GetProductByIdAsync(existingProduct.Id);
        retrievedProduct!.Name.Should().Be("Updated Product Name");
    }

    [Fact]
    public async Task UpdateProductAsync_WithNonExistentProduct_ShouldThrowException()
    {
        // Arrange
        var nonExistentProduct = new Product
        {
            Id = "non-existent-id",
            Name = "Non-existent Product",
            Sku = "NON-001",
            Price = 100.00m
        };

        // Act & Assert
        var act = async () => await _productService.UpdateProductAsync(nonExistentProduct);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Product with ID 'non-existent-id' not found.");
    }

    [Fact]
    public async Task UpdateProductAsync_WithDuplicateSku_ShouldThrowException()
    {
        // Arrange
        var allProducts = await _productService.GetProductsAsync();
        var product1 = allProducts.First();
        var product2 = allProducts.Skip(1).First();
        
        product1.Sku = product2.Sku; // Set to duplicate SKU

        // Act & Assert
        var act = async () => await _productService.UpdateProductAsync(product1);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Another product already uses SKU '{product2.Sku}'.");
    }

    [Fact]
    public async Task DeleteProductAsync_WithValidId_ShouldDeleteProductAndReturnTrue()
    {
        // Arrange
        var newProduct = new Product
        {
            Name = "Product to Delete",
            Sku = "DELETE-001",
            Price = 50.00m
        };
        var createdProduct = await _productService.CreateProductAsync(newProduct);

        // Act
        var result = await _productService.DeleteProductAsync(createdProduct.Id);

        // Assert
        result.Should().BeTrue();

        // Verify product was removed
        var deletedProduct = await _productService.GetProductByIdAsync(createdProduct.Id);
        deletedProduct.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProductAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Arrange
        var invalidId = "non-existent-id";

        // Act
        var result = await _productService.DeleteProductAsync(invalidId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetTagsAsync_ShouldReturnDistinctOrderedTags()
    {
        // Act
        var result = await _productService.GetTagsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeInAscendingOrder();
        result.Should().OnlyHaveUniqueItems();
        result.Should().Contain(tag => !string.IsNullOrWhiteSpace(tag));
    }

    [Fact]
    public async Task GetCategoryStatsAsync_ShouldReturnCategoryCountDictionary()
    {
        // Act
        var result = await _productService.GetCategoryStatsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Keys.Should().AllBeOfType<ProductCategory>();
        result.Values.Should().OnlyContain(v => v >= 0);
    }

    [Theory]
    [InlineData(DigitalTwinStatus.None)]
    [InlineData(DigitalTwinStatus.Skeleton)]
    public async Task CreateDigitalSkeletonAsync_WithValidProductId_ShouldCreateDigitalTwin(DigitalTwinStatus initialStatus)
    {
        // Arrange
        var newProduct = new Product
        {
            Name = "Test Product for Digital Twin",
            Sku = "TWIN-001",
            Price = 100.00m,
            TwinStatus = initialStatus
        };
        var createdProduct = await _productService.CreateProductAsync(newProduct);

        // Act
        var result = await _productService.CreateDigitalSkeletonAsync(createdProduct.Id);

        // Assert
        result.Should().BeTrue();

        // Verify digital twin was created
        var updatedProduct = await _productService.GetProductByIdAsync(createdProduct.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.HasDigitalTwin.Should().BeTrue();
        updatedProduct.DigitalTwinId.Should().NotBeNullOrEmpty();
        updatedProduct.TwinStatus.Should().Be(DigitalTwinStatus.Skeleton);
        updatedProduct.LastSyncAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateDigitalSkeletonAsync_WithInvalidProductId_ShouldReturnFalse()
    {
        // Arrange
        var invalidId = "non-existent-id";

        // Act
        var result = await _productService.CreateDigitalSkeletonAsync(invalidId);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(DigitalTwinStatus.Skeleton, DigitalTwinStatus.Partial)]
    [InlineData(DigitalTwinStatus.Partial, DigitalTwinStatus.Complete)]
    [InlineData(DigitalTwinStatus.Complete, DigitalTwinStatus.Enhanced)]
    public async Task UpgradeToDigitalTwinAsync_WithValidStatus_ShouldUpgradeStatus(
        DigitalTwinStatus initialStatus, DigitalTwinStatus expectedStatus)
    {
        // Arrange
        var newProduct = new Product
        {
            Name = "Test Product for Upgrade",
            Sku = "UPGRADE-001",
            Price = 100.00m,
            DigitalTwinId = "existing-twin-id",
            TwinStatus = initialStatus
        };
        var createdProduct = await _productService.CreateProductAsync(newProduct);

        // Act
        var result = await _productService.UpgradeToDigitalTwinAsync(createdProduct.Id);

        // Assert
        result.Should().BeTrue();

        // Verify upgrade
        var updatedProduct = await _productService.GetProductByIdAsync(createdProduct.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.TwinStatus.Should().Be(expectedStatus);
        updatedProduct.LastSyncAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UpgradeToDigitalTwinAsync_WithoutDigitalTwin_ShouldReturnFalse()
    {
        // Arrange
        var newProduct = new Product
        {
            Name = "Test Product without Twin",
            Sku = "NOTWIN-001",
            Price = 100.00m,
            TwinStatus = DigitalTwinStatus.None
        };
        var createdProduct = await _productService.CreateProductAsync(newProduct);

        // Act
        var result = await _productService.UpgradeToDigitalTwinAsync(createdProduct.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SyncDigitalTwinAsync_WithValidDigitalTwin_ShouldUpdateLastSyncTime()
    {
        // Arrange
        var newProduct = new Product
        {
            Name = "Test Product for Sync",
            Sku = "SYNC-001",
            Price = 100.00m,
            DigitalTwinId = "twin-for-sync",
            TwinStatus = DigitalTwinStatus.Complete
        };
        var createdProduct = await _productService.CreateProductAsync(newProduct);

        // Act
        var result = await _productService.SyncDigitalTwinAsync(createdProduct.Id);

        // Assert
        result.Should().BeTrue();

        // Verify sync
        var updatedProduct = await _productService.GetProductByIdAsync(createdProduct.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.LastSyncAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SyncDigitalTwinAsync_WithoutDigitalTwin_ShouldReturnFalse()
    {
        // Arrange
        var newProduct = new Product
        {
            Name = "Test Product without Twin",
            Sku = "NOSYNC-001",
            Price = 100.00m,
            TwinStatus = DigitalTwinStatus.None
        };
        var createdProduct = await _productService.CreateProductAsync(newProduct);

        // Act
        var result = await _productService.SyncDigitalTwinAsync(createdProduct.Id);

        // Assert
        result.Should().BeFalse();
    }
}