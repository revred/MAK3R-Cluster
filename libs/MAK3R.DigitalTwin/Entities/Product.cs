using MAK3R.Core;

namespace MAK3R.DigitalTwin.Entities;

public class Product : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid CompanyId { get; private set; }
    public string? Sku { get; private set; }
    public decimal? Price { get; private set; }
    public string? Currency { get; private set; }
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? Manufacturer { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Dictionary<string, object> Attributes { get; private set; } = new();

    private Product() : base() { }

    public Product(string name, Guid companyId, string? sku = null, decimal? price = null, string? currency = null) : base()
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        CompanyId = Guard.NotEmpty(companyId);
        Sku = sku;
        Price = price;
        Currency = currency;
    }

    public void UpdateDetails(
        string name, 
        string? sku, 
        decimal? price, 
        string? currency, 
        string? description, 
        string? category, 
        string? imageUrl, 
        string? manufacturer,
        bool isActive)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        Sku = sku;
        Price = price;
        Currency = currency;
        Description = description;
        Category = category;
        ImageUrl = imageUrl;
        Manufacturer = manufacturer;
        IsActive = isActive;
        UpdateVersion();
    }

    public void SetAttribute(string key, object value)
    {
        Guard.NotNullOrWhiteSpace(key);
        Guard.NotNull(value);

        Attributes[key] = value;
        UpdateVersion();
    }

    public void RemoveAttribute(string key)
    {
        if (Attributes.Remove(key))
        {
            UpdateVersion();
        }
    }

    public void UpdateAttributes(Dictionary<string, object> newAttributes)
    {
        Attributes.Clear();
        foreach (var attr in newAttributes)
        {
            Attributes[attr.Key] = attr.Value;
        }
        UpdateVersion();
    }

    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            UpdateVersion();
        }
    }

    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            UpdateVersion();
        }
    }
}