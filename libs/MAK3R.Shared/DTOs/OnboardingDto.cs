namespace MAK3R.Shared.DTOs;

public class OnboardingWizardDto
{
    public CompanyInfo Company { get; set; } = new();
    public List<SiteInfo> Sites { get; set; } = new();
    public List<MachineInfo> Machines { get; set; } = new();
    public List<ProductInfo> Products { get; set; } = new();
    public List<UserInfo> Users { get; set; } = new();
}

public class CompanyInfo
{
    public string Name { get; set; } = "";
    public string? RegistrationId { get; set; }
    public string? TaxId { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
}

public class SiteInfo
{
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Description { get; set; }
}

public class MachineInfo
{
    public string Name { get; set; } = "";
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? OpcUaNode { get; set; }
    public string SiteName { get; set; } = "";
}

public class ProductInfo
{
    public string Name { get; set; } = "";
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class UserInfo
{
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Role { get; set; } = "";
}

public record OnboardingResult(
    Guid CompanyId,
    Dictionary<string, Guid> SiteIds,
    Dictionary<string, Guid> MachineIds,
    Dictionary<string, Guid> ProductIds,
    List<string> Warnings,
    List<string> Errors
);