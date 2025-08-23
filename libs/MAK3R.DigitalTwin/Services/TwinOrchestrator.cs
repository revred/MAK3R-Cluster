using MAK3R.Core;
using MAK3R.DigitalTwin.Entities;
using MAK3R.Shared.DTOs;

namespace MAK3R.DigitalTwin.Services;

public class TwinOrchestrator : ITwinOrchestrator
{
    private readonly Dictionary<Guid, Company> _companies = new();

    public Task<Result<OnboardingResult>> CreateDigitalTwinAsync(OnboardingWizardDto wizardData, CancellationToken ct = default)
    {
        try
        {
            var company = CreateCompany(wizardData.Company);
            var siteIds = CreateSites(company, wizardData.Sites);
            var machineIds = CreateMachines(company, wizardData.Machines, siteIds);
            var productIds = CreateProducts(company, wizardData.Products);

            _companies[company.Id] = company;

            var result = new OnboardingResult(
                company.Id,
                siteIds,
                machineIds,
                productIds,
                GenerateWarnings(wizardData),
                new List<string>()
            );

            return Task.FromResult(Result<OnboardingResult>.Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<OnboardingResult>.Failure(ex.Message, ex));
        }
    }

    public Task<Result<Company>> GetCompanyTwinAsync(Guid companyId, CancellationToken ct = default)
    {
        if (_companies.TryGetValue(companyId, out var company))
        {
            return Task.FromResult(Result<Company>.Success(company));
        }

        return Task.FromResult(Result<Company>.Failure(Errors.DigitalTwin.CompanyNotFound));
    }

    public Task<Result<TwinValidationResult>> ValidateTwinAsync(Guid companyId, CancellationToken ct = default)
    {
        if (!_companies.TryGetValue(companyId, out var company))
        {
            return Task.FromResult(Result<TwinValidationResult>.Failure(Errors.DigitalTwin.CompanyNotFound));
        }

        var gaps = AnalyzeTwinGaps(company);
        var confidence = CalculateConfidenceScore(company, gaps);
        var recommendations = GenerateRecommendations(gaps);

        var result = new TwinValidationResult(gaps, confidence, recommendations);
        return Task.FromResult(Result<TwinValidationResult>.Success(result));
    }

    public Task<Result> UpdateTwinFromConnectorAsync(Guid companyId, string connectorId, CancellationToken ct = default)
    {
        if (!_companies.TryGetValue(companyId, out var company))
        {
            return Task.FromResult(Result.Failure(Errors.DigitalTwin.CompanyNotFound));
        }

        // TODO: Implement connector-based twin updates
        return Task.FromResult(Result.Success());
    }

    private Company CreateCompany(CompanyInfo info)
    {
        var company = new Company(info.Name, info.RegistrationId, info.TaxId);
        company.UpdateDetails(info.Name, info.RegistrationId, info.TaxId, info.Industry, info.Website, info.Address);
        return company;
    }

    private Dictionary<string, Guid> CreateSites(Company company, List<SiteInfo> sites)
    {
        var siteIds = new Dictionary<string, Guid>();

        foreach (var siteInfo in sites)
        {
            var site = company.AddSite(siteInfo.Name, siteInfo.Address, siteInfo.City, siteInfo.Country);
            if (!string.IsNullOrEmpty(siteInfo.Description))
            {
                site.UpdateDetails(site.Name, site.Address, site.City, site.Country, siteInfo.Description);
            }
            siteIds[siteInfo.Name] = site.Id;
        }

        return siteIds;
    }

    private Dictionary<string, Guid> CreateMachines(Company company, List<MachineInfo> machines, Dictionary<string, Guid> siteIds)
    {
        var machineIds = new Dictionary<string, Guid>();

        foreach (var machineInfo in machines)
        {
            if (siteIds.TryGetValue(machineInfo.SiteName, out var siteId))
            {
                var site = company.Sites.First(s => s.Id == siteId);
                var machine = site.AddMachine(machineInfo.Name, machineInfo.Make, machineInfo.Model, machineInfo.SerialNumber);
                
                if (!string.IsNullOrEmpty(machineInfo.OpcUaNode))
                {
                    machine.UpdateDetails(machine.Name, machine.Make, machine.Model, machine.SerialNumber, machineInfo.OpcUaNode);
                }
                
                machineIds[$"{machineInfo.SiteName}_{machineInfo.Name}"] = machine.Id;
            }
        }

        return machineIds;
    }

    private Dictionary<string, Guid> CreateProducts(Company company, List<ProductInfo> products)
    {
        var productIds = new Dictionary<string, Guid>();

        foreach (var productInfo in products)
        {
            var product = company.AddProduct(productInfo.Name, productInfo.Sku, productInfo.Price, productInfo.Currency);
            product.UpdateDetails(
                product.Name,
                product.Sku,
                product.Price,
                product.Currency,
                productInfo.Description,
                productInfo.Category,
                null, // imageUrl
                null, // manufacturer
                true // isActive
            );
            productIds[productInfo.Name] = product.Id;
        }

        return productIds;
    }

    private List<string> GenerateWarnings(OnboardingWizardDto wizardData)
    {
        var warnings = new List<string>();

        if (string.IsNullOrEmpty(wizardData.Company.RegistrationId))
            warnings.Add("Company registration ID not provided");

        if (wizardData.Sites.Count == 0)
            warnings.Add("No sites defined - consider adding at least one site");

        if (wizardData.Products.Any(p => !p.Price.HasValue))
            warnings.Add("Some products are missing price information");

        return warnings;
    }

    private List<TwinGap> AnalyzeTwinGaps(Company company)
    {
        var gaps = new List<TwinGap>();

        // Analyze company gaps
        if (string.IsNullOrEmpty(company.RegistrationId))
        {
            gaps.Add(new TwinGap("Company", company.Id.ToString(), "MissingData", "Registration ID not provided", "Medium"));
        }

        // Analyze product gaps
        foreach (var product in company.Products.Where(p => !p.Price.HasValue))
        {
            gaps.Add(new TwinGap("Product", product.Id.ToString(), "MissingData", "Product price not set", "High"));
        }

        // Analyze machine gaps
        foreach (var site in company.Sites)
        {
            foreach (var machine in site.Machines.Where(m => string.IsNullOrEmpty(m.OpcUaNode)))
            {
                gaps.Add(new TwinGap("Machine", machine.Id.ToString(), "MissingConnection", "OPC UA node not configured", "Medium"));
            }
        }

        return gaps;
    }

    private double CalculateConfidenceScore(Company company, List<TwinGap> gaps)
    {
        var totalEntities = 1 + company.Sites.Count + company.Sites.Sum(s => s.Machines.Count) + company.Products.Count;
        var highSeverityGaps = gaps.Count(g => g.Severity == "High");
        var mediumSeverityGaps = gaps.Count(g => g.Severity == "Medium");

        var penalty = (highSeverityGaps * 0.2) + (mediumSeverityGaps * 0.1);
        var confidence = Math.Max(0, 1.0 - (penalty / totalEntities));

        return Math.Round(confidence * 100, 1);
    }

    private List<string> GenerateRecommendations(List<TwinGap> gaps)
    {
        var recommendations = new List<string>();

        if (gaps.Any(g => g.GapType == "MissingData" && g.EntityType == "Product"))
            recommendations.Add("Set pricing for all products to enable accurate cost analysis");

        if (gaps.Any(g => g.GapType == "MissingConnection" && g.EntityType == "Machine"))
            recommendations.Add("Configure OPC UA connections for real-time machine monitoring");

        if (gaps.Any(g => g.EntityType == "Company"))
            recommendations.Add("Complete company registration details for compliance tracking");

        return recommendations;
    }
}