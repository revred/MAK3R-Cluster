using MAK3R.Core;

namespace MAK3R.DigitalTwin.Entities;

public class Company : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? RegistrationId { get; private set; }
    public string? TaxId { get; private set; }
    public string? Industry { get; private set; }
    public string? Website { get; private set; }
    public string? Address { get; private set; }

    public List<Site> Sites { get; private set; } = new();
    public List<Product> Products { get; private set; } = new();

    private Company() : base() { }

    public Company(string name, string? registrationId = null, string? taxId = null) : base()
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        RegistrationId = registrationId;
        TaxId = taxId;
    }

    public void UpdateDetails(string name, string? registrationId, string? taxId, string? industry, string? website, string? address)
    {
        Name = Guard.NotNullOrWhiteSpace(name);
        RegistrationId = registrationId;
        TaxId = taxId;
        Industry = industry;
        Website = website;
        Address = address;
        UpdateVersion();
    }

    public Site AddSite(string name, string? address = null, string? city = null, string? country = null)
    {
        var site = new Site(name, Id, address, city, country);
        Sites.Add(site);
        UpdateVersion();
        return site;
    }

    public Product AddProduct(string name, string? sku = null, decimal? price = null, string? currency = null)
    {
        var product = new Product(name, Id, sku, price, currency);
        Products.Add(product);
        UpdateVersion();
        return product;
    }

    public void RemoveSite(Guid siteId)
    {
        var site = Sites.FirstOrDefault(s => s.Id == siteId);
        if (site != null)
        {
            Sites.Remove(site);
            UpdateVersion();
        }
    }

    public void RemoveProduct(Guid productId)
    {
        var product = Products.FirstOrDefault(p => p.Id == productId);
        if (product != null)
        {
            Products.Remove(product);
            UpdateVersion();
        }
    }
}