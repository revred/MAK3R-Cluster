using MAK3R.DigitalTwin.Entities;
using MAK3R.Shared.DTOs;

namespace MAK3R.Data;

public static class SeedData
{
    public static OnboardingWizardDto GetContosoGearsData()
    {
        return new OnboardingWizardDto
        {
            Company = new CompanyInfo
            {
                Name = "Contoso Gears Pvt Ltd",
                RegistrationId = "CIN-U12345MH2010PTC123456",
                TaxId = "GST-27ABCDE1234F1Z5",
                Industry = "Manufacturing",
                Website = "https://contosogears.com",
                Address = "Plot 42, Industrial Estate, Phase II, Mumbai, Maharashtra 400001"
            },
            Sites = new List<SiteInfo>
            {
                new SiteInfo
                {
                    Name = "Mumbai Manufacturing Hub",
                    Address = "Plot 42, Industrial Estate, Phase II, Mumbai, Maharashtra 400001",
                    City = "Mumbai",
                    Country = "India",
                    Description = "Primary manufacturing facility with CNC machines and assembly lines"
                },
                new SiteInfo
                {
                    Name = "Pune R&D Center",
                    Address = "Tech Park, Hinjewadi Phase 1, Pune, Maharashtra 411057",
                    City = "Pune",
                    Country = "India",
                    Description = "Research and development center for new gear technologies"
                }
            },
            Machines = new List<MachineInfo>
            {
                new MachineInfo
                {
                    Name = "CNC Mill #1",
                    Make = "Haas",
                    Model = "VF-2SS",
                    SerialNumber = "HAS2023001",
                    OpcUaNode = "ns=2;i=1001",
                    SiteName = "Mumbai Manufacturing Hub"
                },
                new MachineInfo
                {
                    Name = "CNC Lathe #1",
                    Make = "DMG Mori",
                    Model = "CTX Beta 800",
                    SerialNumber = "DMG2023001",
                    OpcUaNode = "ns=2;i=1002",
                    SiteName = "Mumbai Manufacturing Hub"
                },
                new MachineInfo
                {
                    Name = "Assembly Line #1",
                    Make = "Bosch Rexroth",
                    Model = "TS 2plus",
                    SerialNumber = "BR2023001",
                    OpcUaNode = "ns=2;i=1003",
                    SiteName = "Mumbai Manufacturing Hub"
                },
                new MachineInfo
                {
                    Name = "Testing Rig",
                    Make = "Custom",
                    Model = "TR-2000",
                    SerialNumber = "CT2023001",
                    OpcUaNode = null,
                    SiteName = "Pune R&D Center"
                }
            },
            Products = new List<ProductInfo>
            {
                new ProductInfo
                {
                    Name = "Precision Spur Gear 20T",
                    Sku = "PSG-20T-M2",
                    Price = 1250.00m,
                    Currency = "INR",
                    Description = "High precision spur gear, 20 teeth, Module 2",
                    Category = "Spur Gears"
                },
                new ProductInfo
                {
                    Name = "Helical Gear 30T",
                    Sku = "HG-30T-M1.5",
                    Price = 1850.00m,
                    Currency = "INR",
                    Description = "Helical gear with 30 teeth, Module 1.5",
                    Category = "Helical Gears"
                },
                new ProductInfo
                {
                    Name = "Planetary Gear Set",
                    Sku = "PGS-4:1",
                    Price = 12500.00m,
                    Currency = "INR",
                    Description = "Complete planetary gear set with 4:1 ratio",
                    Category = "Planetary Gears"
                },
                new ProductInfo
                {
                    Name = "Worm Gear 40T",
                    Sku = "WG-40T-M3",
                    Price = 2250.00m,
                    Currency = "INR",
                    Description = "Worm gear, 40 teeth, Module 3",
                    Category = "Worm Gears"
                },
                new ProductInfo
                {
                    Name = "Bevel Gear Pair",
                    Sku = "BGP-25T-90DEG",
                    Price = 5500.00m,
                    Currency = "INR",
                    Description = "90-degree bevel gear pair, 25 teeth",
                    Category = "Bevel Gears"
                },
                new ProductInfo
                {
                    Name = "Custom Gear Assembly",
                    Sku = "CGA-CUSTOM",
                    Price = null,
                    Currency = "INR",
                    Description = "Made-to-order custom gear assemblies",
                    Category = "Custom Solutions"
                },
                new ProductInfo
                {
                    Name = "Gear Oil SAE 90",
                    Sku = "GO-SAE90-5L",
                    Price = 850.00m,
                    Currency = "INR",
                    Description = "High-quality gear oil, SAE 90, 5-liter container",
                    Category = "Lubricants"
                },
                new ProductInfo
                {
                    Name = "Precision Bearing Set",
                    Sku = "PBS-6205-2RS",
                    Price = 450.00m,
                    Currency = "INR",
                    Description = "Ball bearing set 6205-2RS for gear assemblies",
                    Category = "Components"
                }
            },
            Users = new List<UserInfo>
            {
                new UserInfo
                {
                    Email = "admin@contosogears.com",
                    FirstName = "Rajesh",
                    LastName = "Kumar",
                    Role = "Administrator"
                },
                new UserInfo
                {
                    Email = "production@contosogears.com",
                    FirstName = "Priya",
                    LastName = "Sharma",
                    Role = "Production Manager"
                },
                new UserInfo
                {
                    Email = "engineer@contosogears.com",
                    FirstName = "Arjun",
                    LastName = "Patel",
                    Role = "Design Engineer"
                }
            }
        };
    }

    public static async Task SeedDatabaseAsync(MAK3RDbContext context)
    {
        // Check if data already exists
        if (context.Companies.Any())
            return;

        var seedData = GetContosoGearsData();

        // Create the company
        var company = new Company(
            seedData.Company.Name,
            seedData.Company.RegistrationId,
            seedData.Company.TaxId
        );
        
        company.UpdateDetails(
            seedData.Company.Name,
            seedData.Company.RegistrationId,
            seedData.Company.TaxId,
            seedData.Company.Industry,
            seedData.Company.Website,
            seedData.Company.Address
        );

        // Create sites
        foreach (var siteInfo in seedData.Sites)
        {
            var site = company.AddSite(siteInfo.Name, siteInfo.Address, siteInfo.City, siteInfo.Country);
            if (!string.IsNullOrEmpty(siteInfo.Description))
            {
                site.UpdateDetails(site.Name, site.Address, site.City, site.Country, siteInfo.Description);
            }
        }

        // Create machines
        foreach (var machineInfo in seedData.Machines)
        {
            var site = company.Sites.FirstOrDefault(s => s.Name == machineInfo.SiteName);
            if (site != null)
            {
                var machine = site.AddMachine(machineInfo.Name, machineInfo.Make, machineInfo.Model, machineInfo.SerialNumber);
                if (!string.IsNullOrEmpty(machineInfo.OpcUaNode))
                {
                    machine.UpdateDetails(machine.Name, machine.Make, machine.Model, machine.SerialNumber, machineInfo.OpcUaNode);
                }

                // Add some sample metrics
                machine.UpdateStatus(MachineStatus.Running);
                machine.UpdateMetric("Temperature", 45.5, "°C");
                machine.UpdateMetric("RPM", 1500, "rpm");
                machine.UpdateMetric("Vibration", 0.8, "mm/s");
            }
        }

        // Create products
        foreach (var productInfo in seedData.Products)
        {
            var product = company.AddProduct(productInfo.Name, productInfo.Sku, productInfo.Price, productInfo.Currency);
            product.UpdateDetails(
                product.Name,
                product.Sku,
                product.Price,
                product.Currency,
                productInfo.Description,
                productInfo.Category,
                null, // imageUrl - will be populated later
                "Contoso Gears Pvt Ltd", // manufacturer
                true // isActive
            );

            // Add some sample attributes
            product.SetAttribute("Material", "Steel");
            product.SetAttribute("Tolerance", "±0.01mm");
            product.SetAttribute("HeatTreatment", "Case Hardened");
        }

        // Add the company to context and save
        context.Companies.Add(company);
        await context.SaveChangesAsync();
    }
}