namespace MAK3R.Shared.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string? Sku,
    decimal? Price,
    string? Currency,
    string? Description,
    string? Category,
    string? ImageUrl,
    string? Manufacturer,
    bool IsActive,
    Dictionary<string, object>? Attributes,
    DateTime CreatedUtc,
    DateTime UpdatedUtc
);

public record CreateProductRequest(
    string Name,
    string? Sku,
    decimal? Price,
    string? Currency,
    string? Description,
    string? Category,
    string? ImageUrl,
    string? Manufacturer,
    Dictionary<string, object>? Attributes
);

public record UpdateProductRequest(
    string Name,
    string? Sku,
    decimal? Price,
    string? Currency,
    string? Description,
    string? Category,
    string? ImageUrl,
    string? Manufacturer,
    bool IsActive,
    Dictionary<string, object>? Attributes
);